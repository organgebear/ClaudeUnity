using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClaudeUnity
{
    public enum CommandStatus
    {
        Pending,
        Executing,
        Success,
        Failed
    }

    [Serializable]
    public class CommandExecution
    {
        public string CommandId;
        public string CommandType;
        public string ParametersJson;
        public CommandStatus Status;
        public string ResultJson;
        public string ErrorMessage;
        public string TargetName;
        public DateTime StartedAt;
        public DateTime CompletedAt;
    }

    [Serializable]
    public class ChatMessage
    {
        public string Role; // "user" or "assistant"
        public string TextContent;
        public List<CommandExecution> Commands = new List<CommandExecution>();
        public DateTime Timestamp;

        // For API message reconstruction (not serialized)
        [NonSerialized] public List<ContentBlock> _assistantBlocks;
        [NonSerialized] public List<ContentBlock> _toolResults;

        public ChatMessage(string role, string text)
        {
            Role = role;
            TextContent = text;
            Timestamp = DateTime.Now;
        }
    }

    [Serializable]
    public class ConversationSession
    {
        public string Id;
        public List<ChatMessage> Messages = new List<ChatMessage>();
        public DateTime CreatedAt;

        public ConversationSession()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            CreatedAt = DateTime.Now;
        }

        public void AddUserMessage(string text)
        {
            Messages.Add(new ChatMessage("user", text));
        }

        public ChatMessage AddAssistantMessage(string text)
        {
            var msg = new ChatMessage("assistant", text);
            Messages.Add(msg);
            return msg;
        }

        public void Clear()
        {
            Messages.Clear();
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            CreatedAt = DateTime.Now;
        }
    }
}
