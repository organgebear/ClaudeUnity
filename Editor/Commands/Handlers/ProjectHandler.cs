using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class ProjectHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "GetProjectStructure": return GetProjectStructure(p);
                case "MoveFile": return MoveFile(p);
                case "RenameFile": return RenameFile(p);
                case "DuplicateFile": return DuplicateFile(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult GetProjectStructure(JsonObject p)
        {
            var path = p.GetString("path") ?? "Assets";
            var depth = p.GetInt("depth", 3);

            var items = new List<string>();
            BuildStructure(path, 0, depth, items);
            return CommandResult.Ok($"{{\"path\":\"{path}\",\"structure\":[{string.Join(",", items)}]}}");
        }

        private void BuildStructure(string path, int currentDepth, int maxDepth, List<string> items)
        {
            if (currentDepth > maxDepth || !Directory.Exists(path)) return;

            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith(".")) continue;
                var relativePath = dir.Replace("\\", "/");
                items.Add($"{{\"name\":\"{name}\",\"path\":\"{relativePath}\",\"type\":\"folder\",\"depth\":{currentDepth}}}");
                BuildStructure(dir, currentDepth + 1, maxDepth, items);
            }

            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(".") || name.EndsWith(".meta")) continue;
                var relativePath = file.Replace("\\", "/");
                items.Add($"{{\"name\":\"{name}\",\"path\":\"{relativePath}\",\"type\":\"file\",\"depth\":{currentDepth}}}");
            }
        }

        private CommandResult MoveFile(JsonObject p)
        {
            var source = p.GetString("sourcePath");
            var dest = p.GetString("destPath");
            var error = AssetDatabase.MoveAsset(source, dest);
            return string.IsNullOrEmpty(error)
                ? CommandResult.Ok($"{{\"from\":\"{source}\",\"to\":\"{dest}\"}}")
                : CommandResult.Fail(error);
        }

        private CommandResult RenameFile(JsonObject p)
        {
            var path = p.GetString("path");
            var newName = p.GetString("newName");
            var error = AssetDatabase.RenameAsset(path, newName);
            return string.IsNullOrEmpty(error)
                ? CommandResult.Ok($"{{\"path\":\"{path}\",\"newName\":\"{newName}\"}}")
                : CommandResult.Fail(error);
        }

        private CommandResult DuplicateFile(JsonObject p)
        {
            var path = p.GetString("path");
            var dir = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var newPath = $"{dir}/{name}_copy{ext}";

            AssetDatabase.CopyAsset(path, newPath);
            return CommandResult.Ok($"{{\"original\":\"{path}\",\"copy\":\"{newPath}\"}}");
        }
    }
}
