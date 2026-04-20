using System.IO;
using UnityEditor;

namespace ClaudeUnity
{
    public class ScriptHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "CreateScript": return CreateScript(p);
                case "ReadScript": return ReadScript(p);
                case "EditScript": return EditScript(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult CreateScript(JsonObject p)
        {
            var scriptName = p.GetString("scriptName");
            var folder = p.GetString("folder") ?? "Assets/Scripts";
            var code = p.GetString("code");

            if (string.IsNullOrEmpty(scriptName))
                return CommandResult.Fail("scriptName is required");
            if (string.IsNullOrEmpty(code))
                return CommandResult.Fail("code is required");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Strip .cs if user included it
            if (scriptName.EndsWith(".cs"))
                scriptName = scriptName.Substring(0, scriptName.Length - 3);

            var path = $"{folder}/{scriptName}.cs";
            File.WriteAllText(path, code);
            DeferredRefresh.Request();

            return CommandResult.Ok($"{{\"path\":\"{Escape(path)}\"}}");
        }

        private CommandResult ReadScript(JsonObject p)
        {
            var path = p.GetString("path");
            if (string.IsNullOrEmpty(path))
                return CommandResult.Fail("path is required");
            if (!File.Exists(path))
                return CommandResult.Fail($"File not found: {path}");

            var content = File.ReadAllText(path);
            return CommandResult.Ok($"{{\"path\":\"{Escape(path)}\",\"content\":\"{Escape(content)}\"}}");
        }

        private CommandResult EditScript(JsonObject p)
        {
            var path = p.GetString("path");
            var code = p.GetString("code");

            if (string.IsNullOrEmpty(path))
                return CommandResult.Fail("path is required");
            if (string.IsNullOrEmpty(code))
                return CommandResult.Fail("code is required");
            if (!File.Exists(path))
                return CommandResult.Fail($"File not found: {path}");

            File.WriteAllText(path, code);
            DeferredRefresh.Request();

            return CommandResult.Ok($"{{\"path\":\"{Escape(path)}\"}}");
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
