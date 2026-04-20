using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ClaudeUnity
{
    public class ClaudeApiClient
    {
        private const string API_VERSION = "2023-06-01";
        private static readonly HttpClient _http = new HttpClient();
        private CancellationTokenSource _cts;

        public async Task SendMessageStreamAsync(
            ClaudeUnitySettings settings,
            List<ApiMessage> messages,
            string systemPrompt,
            List<ToolDefinition> tools,
            Action<StreamEvent> onEvent,
            Action<string> onError,
            CancellationToken externalCt = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _cts.Token;

            try
            {
                var url = settings.GetMessagesEndpoint();
                var requestBody = BuildRequestJson(settings, messages, systemPrompt, tools, true);

                Debug.Log($"[ClaudeUnity] Stream request to: {url}");

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-api-key", settings.ApiKey);
                request.Headers.Add("anthropic-version", API_VERSION);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    var statusCode = (int)response.StatusCode;
                    Debug.LogWarning($"[ClaudeUnity] Stream API returned {statusCode}: {errorBody}");

                    // If streaming returns 404, try non-streaming fallback
                    if (statusCode == 404)
                    {
                        Debug.Log("[ClaudeUnity] Streaming returned 404, falling back to non-streaming...");
                        await FallbackNonStreamAsync(settings, messages, systemPrompt, tools, onEvent, onError, ct);
                        return;
                    }

                    onError?.Invoke($"API Error {statusCode}: {errorBody}");
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                var parser = new StreamParser();
                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    var evt = parser.ParseLine(line);
                    if (evt != null)
                        onEvent?.Invoke(evt);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Fallback for proxy APIs that don't support SSE streaming.
        /// Sends a non-streaming request and simulates stream events from the response.
        /// </summary>
        private async Task FallbackNonStreamAsync(
            ClaudeUnitySettings settings,
            List<ApiMessage> messages,
            string systemPrompt,
            List<ToolDefinition> tools,
            Action<StreamEvent> onEvent,
            Action<string> onError,
            CancellationToken ct)
        {
            try
            {
                var url = settings.GetMessagesEndpoint();
                var requestBody = BuildRequestJson(settings, messages, systemPrompt, tools, false);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-api-key", settings.ApiKey);
                request.Headers.Add("anthropic-version", API_VERSION);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, ct);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    onError?.Invoke($"API Error {(int)response.StatusCode}: {responseBody}");
                    return;
                }

                // Parse the non-streaming response and emit simulated stream events
                EmitEventsFromResponse(responseBody, onEvent);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Converts a non-streaming API response JSON into StreamEvent callbacks,
        /// so the UI code works identically for both streaming and non-streaming.
        /// </summary>
        private void EmitEventsFromResponse(string responseJson, Action<StreamEvent> onEvent)
        {
            // Parse the response to extract content blocks and stop_reason
            var json = SimpleJsonParser.Parse(responseJson);
            var contentArray = json.GetArray("content");
            var stopReason = json.GetString("stop_reason") ?? "end_turn";

            if (contentArray != null)
            {
                int index = 0;
                foreach (var item in contentArray)
                {
                    if (item is Dictionary<string, object> block)
                    {
                        var blockObj = new JsonObject(block);
                        var blockType = blockObj.GetString("type");

                        if (blockType == "text")
                        {
                            // Emit content_block_start + delta + stop
                            onEvent?.Invoke(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockStart,
                                Index = index,
                                BlockType = "text"
                            });
                            onEvent?.Invoke(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockDelta,
                                Index = index,
                                TextDelta = blockObj.GetString("text") ?? ""
                            });
                            onEvent?.Invoke(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockStop,
                                Index = index
                            });
                        }
                        else if (blockType == "tool_use")
                        {
                            var toolId = blockObj.GetString("id");
                            var toolName = blockObj.GetString("name");
                            // Serialize the input dict back to JSON
                            var inputDict = blockObj.GetRaw("input");
                            var inputJson = inputDict != null ? MiniJson.Serialize(inputDict) : "{}";

                            onEvent?.Invoke(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockStart,
                                Index = index,
                                BlockType = "tool_use",
                                ToolUseId = toolId,
                                ToolName = toolName
                            });
                            onEvent?.Invoke(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockDelta,
                                Index = index,
                                InputJsonDelta = inputJson ?? "{}"
                            });
                            onEvent?.Invoke(new StreamEvent
                            {
                                Type = StreamEventType.ContentBlockStop,
                                Index = index
                            });
                        }
                        index++;
                    }
                }
            }

            // Emit message_delta with stop_reason
            onEvent?.Invoke(new StreamEvent
            {
                Type = StreamEventType.MessageDelta,
                StopReason = stopReason
            });
        }

        public async Task<string> SendMessageAsync(
            ClaudeUnitySettings settings,
            List<ApiMessage> messages,
            string systemPrompt,
            List<ToolDefinition> tools,
            CancellationToken ct = default)
        {
            var url = settings.GetMessagesEndpoint();
            var requestBody = BuildRequestJson(settings, messages, systemPrompt, tools, false);

            Debug.Log($"[ClaudeUnity] Request to: {url}");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Headers.Add("anthropic-version", API_VERSION);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {body}");

            return body;
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        private string BuildRequestJson(
            ClaudeUnitySettings settings,
            List<ApiMessage> messages,
            string systemPrompt,
            List<ToolDefinition> tools,
            bool stream)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"model\":\"{0}\",", Escape(settings.EffectiveModel));
            sb.AppendFormat("\"max_tokens\":{0},", settings.MaxTokens);
            sb.AppendFormat("\"temperature\":{0},", settings.Temperature.ToString("F1"));
            if (stream) sb.Append("\"stream\":true,");
            sb.AppendFormat("\"system\":[{{\"type\":\"text\",\"text\":\"{0}\"}}],", Escape(systemPrompt));

            // Tools
            if (tools != null && tools.Count > 0)
            {
                sb.Append("\"tools\":[");
                for (int i = 0; i < tools.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(SerializeTool(tools[i]));
                }
                sb.Append("],");
            }

            // Messages
            sb.Append("\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(SerializeMessage(messages[i]));
            }
            sb.Append("]}");

            return sb.ToString();
        }

        private string SerializeMessage(ApiMessage msg)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{{\"role\":\"{0}\",\"content\":", msg.role);

            if (msg.content is string text)
            {
                sb.AppendFormat("\"{0}\"", Escape(text));
            }
            else if (msg.content is List<ContentBlock> blocks)
            {
                sb.Append("[");
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(SerializeContentBlock(blocks[i]));
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeContentBlock(ContentBlock block)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"type\":\"{0}\"", block.type);

            switch (block.type)
            {
                case "text":
                    sb.AppendFormat(",\"text\":\"{0}\"", Escape(block.text));
                    break;
                case "tool_use":
                    sb.AppendFormat(",\"id\":\"{0}\"", Escape(block.id));
                    sb.AppendFormat(",\"name\":\"{0}\"", Escape(block.name));
                    sb.AppendFormat(",\"input\":{0}", SerializeDict(block.input));
                    break;
                case "tool_result":
                    sb.AppendFormat(",\"tool_use_id\":\"{0}\"", Escape(block.tool_use_id));
                    sb.AppendFormat(",\"content\":\"{0}\"", Escape(block.content ?? ""));
                    if (block.is_error) sb.Append(",\"is_error\":true");
                    break;
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeTool(ToolDefinition tool)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"name\":\"{0}\"", Escape(tool.name));
            sb.AppendFormat(",\"description\":\"{0}\"", Escape(tool.description));
            sb.AppendFormat(",\"input_schema\":{0}", SerializeDict(tool.input_schema));
            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeDict(Dictionary<string, object> dict)
        {
            if (dict == null) return "{}";
            return MiniJson.Serialize(dict);
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// Minimal JSON serializer for dictionaries. Unity's JsonUtility doesn't handle Dictionary.
    /// </summary>
    public static class MiniJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";
            if (obj is string s) return "\"" + EscapeStr(s) + "\"";
            if (obj is bool b) return b ? "true" : "false";
            if (obj is int || obj is long || obj is float || obj is double)
                return Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);
            if (obj is Dictionary<string, object> dict)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(",");
                    sb.AppendFormat("\"{0}\":{1}", EscapeStr(kv.Key), Serialize(kv.Value));
                    first = false;
                }
                sb.Append("}");
                return sb.ToString();
            }
            if (obj is List<object> list)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Serialize(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            if (obj is string[] sarr)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < sarr.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.AppendFormat("\"{0}\"", EscapeStr(sarr[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            if (obj is object[] oarr)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < oarr.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Serialize(oarr[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            return "\"" + EscapeStr(obj.ToString()) + "\"";
        }

        private static string EscapeStr(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") ?? "";
    }
}
