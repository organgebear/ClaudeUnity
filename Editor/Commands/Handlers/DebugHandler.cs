using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class DebugHandler : ICommandHandler
    {
        private static readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private static bool _registered;

        private struct LogEntry
        {
            public string Message;
            public string Type;
            public DateTime Time;
        }

        public DebugHandler()
        {
            if (!_registered)
            {
                Application.logMessageReceived += OnLogReceived;
                _registered = true;
            }
        }

        private static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            _logBuffer.Add(new LogEntry
            {
                Message = message,
                Type = type.ToString(),
                Time = DateTime.Now
            });
            if (_logBuffer.Count > 200) _logBuffer.RemoveAt(0);
        }

        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "DebugLog": return DebugLog(p);
                case "GetLogs": return GetLogs(p);
                case "PauseEditor":
                    EditorApplication.isPaused = true;
                    return CommandResult.Ok("{\"paused\":true}");
                case "ResumeEditor":
                    EditorApplication.isPaused = false;
                    return CommandResult.Ok("{\"paused\":false}");
                case "ClearConsole":
                    ClearConsole();
                    return CommandResult.Ok("{\"cleared\":true}");
                default:
                    return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult DebugLog(JsonObject p)
        {
            var message = p.GetString("message") ?? "";
            var logType = p.GetString("logType") ?? "Log";

            switch (logType)
            {
                case "Warning": Debug.LogWarning($"[ClaudeUnity] {message}"); break;
                case "Error": Debug.LogError($"[ClaudeUnity] {message}"); break;
                default: Debug.Log($"[ClaudeUnity] {message}"); break;
            }

            return CommandResult.Ok($"{{\"logged\":\"{logType}\"}}");
        }

        private CommandResult GetLogs(JsonObject p)
        {
            var count = p.GetInt("count", 10);
            var filterType = p.GetString("logType");

            var results = new List<string>();
            for (int i = _logBuffer.Count - 1; i >= 0 && results.Count < count; i--)
            {
                var entry = _logBuffer[i];
                if (filterType != null && entry.Type != filterType) continue;
                var escaped = entry.Message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
                results.Add($"{{\"message\":\"{escaped}\",\"type\":\"{entry.Type}\",\"time\":\"{entry.Time:HH:mm:ss}\"}}");
            }

            return CommandResult.Ok($"{{\"logs\":[{string.Join(",", results)}]}}");
        }

        private static void ClearConsole()
        {
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntries != null)
            {
                var clear = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                clear?.Invoke(null, null);
            }
            _logBuffer.Clear();
        }
    }
}
