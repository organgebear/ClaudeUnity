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
}
