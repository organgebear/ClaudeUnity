using System;
using System.Collections.Generic;
using System.Text;

namespace ClaudeUnity
{
    public class StreamParser
    {
        private string _currentBlockType;
        private string _currentToolId;
        private string _currentToolName;
        private readonly StringBuilder _toolInputAccumulator = new StringBuilder();

        public StreamEvent ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            if (!line.StartsWith("data: ")) return null;

            var json = line.Substring(6).Trim();
            if (json == "[DONE]") return new StreamEvent { Type = StreamEventType.MessageStop };

            try
            {
                var data = SimpleJsonParser.Parse(json);
                var eventType = data.GetString("type");

                switch (eventType)
                {
                    case "message_start":
                        return new StreamEvent { Type = StreamEventType.MessageStart };

                    case "content_block_start":
                    {
                        var index = data.GetInt("index");
                        var cb = data.GetObject("content_block");
                        var blockType = cb?.GetString("type") ?? "text";
                        _currentBlockType = blockType;

                        var evt = new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockStart,
                            Index = index,
                            BlockType = blockType
                        };

                        if (blockType == "tool_use")
                        {
                            _currentToolId = cb.GetString("id");
                            _currentToolName = cb.GetString("name");
                            _toolInputAccumulator.Clear();
                            evt.ToolUseId = _currentToolId;
                            evt.ToolName = _currentToolName;
                        }
                        return evt;
                    }

                    case "content_block_delta":
                    {
                        var index = data.GetInt("index");
                        var delta = data.GetObject("delta");
                        var deltaType = delta?.GetString("type") ?? "";

                        var evt = new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockDelta,
                            Index = index
                        };

                        if (deltaType == "text_delta")
                        {
                            evt.TextDelta = delta.GetString("text");
                        }
                        else if (deltaType == "input_json_delta")
                        {
                            var partial = delta.GetString("partial_json");
                            _toolInputAccumulator.Append(partial);
                            evt.InputJsonDelta = partial;
                        }
                        return evt;
                    }

                    case "content_block_stop":
                    {
                        var evt = new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockStop,
                            Index = data.GetInt("index")
                        };

                        if (_currentBlockType == "tool_use")
                        {
                            evt.ToolUseId = _currentToolId;
                            evt.ToolName = _currentToolName;
                            evt.InputJsonDelta = _toolInputAccumulator.ToString();
                            _toolInputAccumulator.Clear();
                        }
                        _currentBlockType = null;
                        return evt;
                    }

                    case "message_delta":
                    {
                        var delta = data.GetObject("delta");
                        return new StreamEvent
                        {
                            Type = StreamEventType.MessageDelta,
                            StopReason = delta?.GetString("stop_reason")
                        };
                    }

                    case "message_stop":
                        return new StreamEvent { Type = StreamEventType.MessageStop };

                    case "ping":
                        return new StreamEvent { Type = StreamEventType.Ping };

                    case "error":
                    {
                        var error = data.GetObject("error");
                        return new StreamEvent
                        {
                            Type = StreamEventType.Error,
                            ErrorMessage = error?.GetString("message") ?? "Unknown error"
                        };
                    }

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                return new StreamEvent
                {
                    Type = StreamEventType.Error,
                    ErrorMessage = $"Parse error: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// Parses OpenAI-compatible SSE stream (DeepSeek, OpenAI, etc.) into StreamEvent objects.
    /// </summary>
    public class OpenAIStreamParser
    {
        private bool _started;
        private int _blockIndex;
        private string _currentBlockType; // "text" or "tool_use"
        private string _currentToolId;
        private string _currentToolName;
        private readonly StringBuilder _toolArgs = new StringBuilder();
        private int _lastToolIndex = -1;
        private bool _messageDeltaEmitted;

        public List<StreamEvent> ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            if (!line.StartsWith("data: ")) return null;

            var json = line.Substring(6).Trim();
            if (json == "[DONE]")
            {
                // Emit stop for last block if was tool_use
                var events = new List<StreamEvent>();
                if (_currentBlockType == "tool_use" && _toolArgs.Length > 0)
                {
                    events.Add(new StreamEvent
                    {
                        Type = StreamEventType.ContentBlockStop,
                        Index = _blockIndex,
                        ToolUseId = _currentToolId,
                        ToolName = _currentToolName,
                        InputJsonDelta = _toolArgs.ToString()
                    });
                }
                events.Add(new StreamEvent { Type = StreamEventType.MessageStop });
                Reset();
                return events;
            }

            try
            {
                var root = SimpleJsonParser.Parse(json);
                var choices = root.GetArray("choices");
                if (choices == null || choices.Count == 0) return null;

                var firstChoice = choices[0] as Dictionary<string, object>;
                if (firstChoice == null) return null;
                var choice = new JsonObject(firstChoice);

                var delta = choice.GetObject("delta");
                var finishReason = choice.GetString("finish_reason");
                var events = new List<StreamEvent>();

                // Handle finish_reason
                if (!string.IsNullOrEmpty(finishReason) && !_messageDeltaEmitted)
                {
                    _messageDeltaEmitted = true;

                    // Close any open content block
                    if (_currentBlockType == "tool_use" && _toolArgs.Length > 0)
                    {
                        events.Add(new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockStop,
                            Index = _blockIndex,
                            ToolUseId = _currentToolId,
                            ToolName = _currentToolName,
                            InputJsonDelta = _toolArgs.ToString()
                        });
                    }
                    else if (_currentBlockType == "text")
                    {
                        events.Add(new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockStop,
                            Index = _blockIndex
                        });
                    }

                    events.Add(new StreamEvent
                    {
                        Type = StreamEventType.MessageDelta,
                        StopReason = finishReason
                    });
                    return events.Count > 0 ? events : null;
                }

                if (delta == null) return null;

                // Handle tool_calls
                var toolCalls = delta.GetArray("tool_calls");
                if (toolCalls != null && toolCalls.Count > 0)
                {
                    var tc = toolCalls[0] as Dictionary<string, object>;
                    if (tc != null)
                    {
                        var tcObj = new JsonObject(tc);
                        var tcIndex = tcObj.GetInt("index");

                        // New tool call
                        if (tcIndex != _lastToolIndex && _lastToolIndex >= 0)
                        {
                            // Close previous block
                            if (_currentBlockType == "tool_use" && _toolArgs.Length > 0)
                            {
                                events.Add(new StreamEvent
                                {
                                    Type = StreamEventType.ContentBlockStop,
                                    Index = _blockIndex,
                                    ToolUseId = _currentToolId,
                                    ToolName = _currentToolName,
                                    InputJsonDelta = _toolArgs.ToString()
                                });
                            }
                            _blockIndex++;
                            _toolArgs.Clear();
                        }

                        var tcId = tcObj.GetString("id");
                        var tcType = tcObj.GetString("type"); // "function"
                        var function = tcObj.GetObject("function");

                        if (!string.IsNullOrEmpty(tcId))
                        {
                            // Start of a new tool call
                            if (_currentBlockType != "tool_use" || tcIndex != _lastToolIndex)
                            {
                                // Close text block if open
                                if (_currentBlockType == "text")
                                {
                                    events.Add(new StreamEvent
                                    {
                                        Type = StreamEventType.ContentBlockStop,
                                        Index = _blockIndex
                                    });
                                    _blockIndex++;
                                }

                                _currentBlockType = "tool_use";
                                _currentToolId = tcId;
                                _currentToolName = function?.GetString("name") ?? "";
                                _toolArgs.Clear();
                                _lastToolIndex = tcIndex;

                                events.Add(new StreamEvent
                                {
                                    Type = StreamEventType.ContentBlockStart,
                                    Index = _blockIndex,
                                    BlockType = "tool_use",
                                    ToolUseId = _currentToolId,
                                    ToolName = _currentToolName
                                });
                                _started = true;
                            }
                        }

                        if (function != null)
                        {
                            var args = function.GetString("arguments");
                            if (!string.IsNullOrEmpty(args))
                            {
                                _toolArgs.Append(args);
                                events.Add(new StreamEvent
                                {
                                    Type = StreamEventType.ContentBlockDelta,
                                    Index = _blockIndex,
                                    InputJsonDelta = args
                                });
                            }
                        }
                    }
                    return events.Count > 0 ? events : null;
                }

                // Handle text content
                var content = delta.GetString("content");
                if (!string.IsNullOrEmpty(content))
                {
                    if (!_started)
                    {
                        _started = true;
                        _currentBlockType = "text";
                        events.Add(new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockStart,
                            Index = 0,
                            BlockType = "text"
                        });
                    }
                    else if (_currentBlockType == "tool_use")
                    {
                        // Close tool block, start text block
                        if (_toolArgs.Length > 0)
                        {
                            events.Add(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockStop,
                                Index = _blockIndex,
                                ToolUseId = _currentToolId,
                                ToolName = _currentToolName,
                                InputJsonDelta = _toolArgs.ToString()
                            });
                        }
                        _blockIndex++;
                        _currentBlockType = "text";
                        events.Add(new StreamEvent
                        {
                            Type = StreamEventType.ContentBlockStart,
                            Index = _blockIndex,
                            BlockType = "text"
                        });
                    }

                    events.Add(new StreamEvent
                    {
                        Type = StreamEventType.ContentBlockDelta,
                        Index = _blockIndex,
                        TextDelta = content
                    });
                }

                return events.Count > 0 ? events : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Reset()
        {
            _started = false;
            _blockIndex = 0;
            _currentBlockType = null;
            _currentToolId = null;
            _currentToolName = null;
            _toolArgs.Clear();
            _lastToolIndex = -1;
            _messageDeltaEmitted = false;
        }
    }
}
