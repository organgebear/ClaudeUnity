using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class FileSystemHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "ReadFile": return ReadFile(p);
                case "WriteFile": return WriteFile(p);
                case "ListFiles": return ListFiles(p);
                case "GetCompileErrors": return GetCompileErrors();
                case "DeleteFile": return DeleteFile(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult ReadFile(JsonObject p)
        {
            var path = p.GetString("path");
            if (string.IsNullOrEmpty(path))
                return CommandResult.Fail("path is required");

            var fullPath = path;
            if (!File.Exists(fullPath))
                fullPath = Path.Combine(Application.dataPath, "..", path);
            if (!File.Exists(fullPath))
                return CommandResult.Fail($"File not found: {path}");

            try
            {
                var content = File.ReadAllText(fullPath);
                if (content.Length > 50000)
                    content = content.Substring(0, 50000) + "\n... [truncated]";
                return CommandResult.Ok($"{{\"path\":\"{Esc(path)}\",\"content\":\"{Esc(content)}\"}}");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"Read failed: {ex.Message}");
            }
        }
        private CommandResult WriteFile(JsonObject p)
        {
            var path = p.GetString("path");
            var content = p.GetString("content");
            if (string.IsNullOrEmpty(path))
                return CommandResult.Fail("path is required");
            if (content == null)
                return CommandResult.Fail("content is required");

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, content);
                if (path.StartsWith("Assets"))
                    DeferredRefresh.Request();
                return CommandResult.Ok($"{{\"path\":\"{Esc(path)}\",\"written\":true}}");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"Write failed: {ex.Message}");
            }
        }

        private CommandResult ListFiles(JsonObject p)
        {
            var path = p.GetString("path") ?? "Assets";
            var pattern = p.GetString("pattern") ?? "*";
            var recursive = p.GetBool("recursive", false);

            var fullPath = path;
            if (!Directory.Exists(fullPath))
                fullPath = Path.Combine(Application.dataPath, "..", path);
            if (!Directory.Exists(fullPath))
                return CommandResult.Fail($"Directory not found: {path}");

            try
            {
                var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var entries = new List<string>();

                foreach (var dir in Directory.GetDirectories(fullPath, "*", SearchOption.TopDirectoryOnly))
                    entries.Add($"{{\"name\":\"{Esc(Path.GetFileName(dir))}\",\"type\":\"dir\"}}");

                foreach (var file in Directory.GetFiles(fullPath, pattern, option))
                {
                    var name = Path.GetFileName(file);
                    if (name.EndsWith(".meta")) continue;
                    var rel = file.Replace("\\", "/");
                    var root = Path.Combine(Application.dataPath, "..").Replace("\\", "/");
                    if (rel.StartsWith(root)) rel = rel.Substring(root.Length + 1);
                    entries.Add($"{{\"name\":\"{Esc(name)}\",\"path\":\"{Esc(rel)}\",\"type\":\"file\"}}");
                    if (entries.Count > 200) break;
                }
                return CommandResult.Ok($"{{\"path\":\"{Esc(path)}\",\"entries\":[{string.Join(",", entries)}]}}");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"List failed: {ex.Message}");
            }
        }
        private CommandResult GetCompileErrors()
        {
            var errors = new List<string>();
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType != null)
                {
                    var getCount = logEntriesType.GetMethod("GetCount",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    var startGetting = logEntriesType.GetMethod("StartGettingEntries",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    var getEntry = logEntriesType.GetMethod("GetEntryInternal",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    var endGetting = logEntriesType.GetMethod("EndGettingEntries",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                    if (getCount != null && startGetting != null && endGetting != null)
                    {
                        int count = (int)getCount.Invoke(null, null);
                        startGetting.Invoke(null, null);
                        var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");

                        for (int i = 0; i < count && errors.Count < 50; i++)
                        {
                            if (logEntryType == null || getEntry == null) continue;
                            var entry = Activator.CreateInstance(logEntryType);
                            getEntry.Invoke(null, new object[] { i, entry });

                            var modeField = logEntryType.GetField("mode",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var msgField = logEntryType.GetField("message",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var fileField = logEntryType.GetField("file",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var lineField = logEntryType.GetField("line",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                            if (modeField == null || msgField == null) continue;
                            int mode = (int)modeField.GetValue(entry);
                            if ((mode & 1) == 0 && (mode & 4096) == 0) continue; // Not error

                            var msg = (string)msgField.GetValue(entry) ?? "";
                            var file = fileField != null ? (string)fileField.GetValue(entry) ?? "" : "";
                            var line = lineField != null ? (int)lineField.GetValue(entry) : 0;
                            if (msg.Length > 500) msg = msg.Substring(0, 500);
                            errors.Add($"{{\"message\":\"{Esc(msg)}\",\"file\":\"{Esc(file)}\",\"line\":{line}}}");
                        }
                        endGetting.Invoke(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClaudeUnity] GetCompileErrors: {ex.Message}");
            }

            var isCompiling = EditorApplication.isCompiling;
            return CommandResult.Ok($"{{\"isCompiling\":{(isCompiling ? "true" : "false")},\"errorCount\":{errors.Count},\"errors\":[{string.Join(",", errors)}]}}");
        }

        private CommandResult DeleteFile(JsonObject p)
        {
            var path = p.GetString("path");
            if (string.IsNullOrEmpty(path))
                return CommandResult.Fail("path is required");
            if (path.StartsWith("Assets"))
            {
                if (!AssetDatabase.DeleteAsset(path))
                    return CommandResult.Fail($"Failed to delete: {path}");
                return CommandResult.Ok($"{{\"deleted\":\"{Esc(path)}\"}}");
            }
            if (File.Exists(path))
            {
                File.Delete(path);
                return CommandResult.Ok($"{{\"deleted\":\"{Esc(path)}\"}}");
            }
            return CommandResult.Fail($"File not found: {path}");
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
