using System;
using System.Collections.Generic;

namespace ClaudeUnity
{
    // ── Request Types ──

    [Serializable]
    public class ApiRequest
    {
        public string model;
        public int max_tokens;
        public float temperature;
        public string system;
        public bool stream;
        public List<ApiMessage> messages;
        public List<ToolDefinition> tools;
    }

    [Serializable]
    public class ApiMessage
    {
        public string role;
        public object content; // string or List<ContentBlock>

        public static ApiMessage User(string text) =>
            new ApiMessage { role = "user", content = text };

        public static ApiMessage Assistant(List<ContentBlock> blocks) =>
            new ApiMessage { role = "assistant", content = blocks };

        public static ApiMessage UserToolResults(List<ContentBlock> results) =>
            new ApiMessage { role = "user", content = results };
    }

    [Serializable]
    public class ContentBlock
    {
        public string type; // "text", "tool_use", "tool_result"

        // text block
        public string text;

        // tool_use block
        public string id;
        public string name;
        public Dictionary<string, object> input;

        // tool_result block
        public string tool_use_id;
        public string content;
        public bool is_error;

        public static ContentBlock Text(string text) =>
            new ContentBlock { type = "text", text = text };

        public static ContentBlock ToolUse(string id, string name, Dictionary<string, object> input) =>
            new ContentBlock { type = "tool_use", id = id, name = name, input = input };

        public static ContentBlock ToolResult(string toolUseId, string content, bool isError = false) =>
            new ContentBlock { type = "tool_result", tool_use_id = toolUseId, content = content, is_error = isError };
    }

    // ── Tool Definitions ──

    [Serializable]
    public class ToolDefinition
    {
        public string name;
        public string description;
        public Dictionary<string, object> input_schema;
    }

    // ── Response Types ──

    [Serializable]
    public class ApiResponse
    {
        public string id;
        public string type;
        public string role;
        public string model;
        public string stop_reason;
        public List<ResponseContentBlock> content;
        public ApiUsage usage;
    }

    [Serializable]
    public class ResponseContentBlock
    {
        public string type; // "text" or "tool_use"
        public string text;
        public string id;
        public string name;
        public string input_raw; // raw JSON string for tool input
    }

    [Serializable]
    public class ApiUsage
    {
        public int input_tokens;
        public int output_tokens;
    }

    // ── Stream Event Types ──

    public enum StreamEventType
    {
        MessageStart,
        ContentBlockStart,
        ContentBlockDelta,
        ContentBlockStop,
        MessageDelta,
        MessageStop,
        Ping,
        Error
    }

    public class StreamEvent
    {
        public StreamEventType Type;
        public int? Index;

        // For content_block_start
        public string BlockType; // "text" or "tool_use"
        public string ToolUseId;
        public string ToolName;

        // For content_block_delta
        public string TextDelta;
        public string InputJsonDelta;

        // For message_delta
        public string StopReason;

        // For error
        public string ErrorMessage;
    }
}
