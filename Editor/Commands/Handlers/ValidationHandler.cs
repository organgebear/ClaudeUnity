using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class ValidationHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "ValidateScene": return ValidateScene(p);
                case "ValidateAssets": return ValidateAssets(p);
                case "FindMissingScripts": return FindMissingScripts(p);
                case "CleanupEmptyFolders": return CleanupEmptyFolders(p);
                case "OptimizeTextures": return OptimizeTextures(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult ValidateScene(JsonObject p)
        {
            var issues = new List<string>();
            var allObjects = Object.FindObjectsOfType<GameObject>();

            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c == null)
                        issues.Add($"{{\"type\":\"missing_script\",\"object\":\"{go.name}\"}}");
                }
            }

            return CommandResult.Ok($"{{\"issueCount\":{issues.Count},\"issues\":[{string.Join(",", issues)}]}}");
        }

        private CommandResult ValidateAssets(JsonObject p)
        {
            var path = p.GetString("path") ?? "Assets";
            var issues = new List<string>();

            var guids = AssetDatabase.FindAssets("", new[] { path });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null && !Directory.Exists(assetPath))
                    issues.Add($"{{\"type\":\"broken_reference\",\"path\":\"{assetPath}\"}}");
            }

            return CommandResult.Ok($"{{\"issueCount\":{issues.Count},\"issues\":[{string.Join(",", issues)}]}}");
        }

        private CommandResult FindMissingScripts(JsonObject p)
        {
            var results = new List<string>();
            var allObjects = Object.FindObjectsOfType<GameObject>();

            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                        results.Add($"{{\"object\":\"{go.name}\",\"componentIndex\":{i}}}");
                }
            }

            return CommandResult.Ok($"{{\"count\":{results.Count},\"missing\":[{string.Join(",", results)}]}}");
        }

        private CommandResult CleanupEmptyFolders(JsonObject p)
        {
            var dryRun = p.GetBool("dryRun", true);
            var emptyFolders = new List<string>();
            FindEmptyFolders("Assets", emptyFolders);

            if (!dryRun)
            {
                foreach (var folder in emptyFolders)
                {
                    AssetDatabase.DeleteAsset(folder);
                }
                AssetDatabase.Refresh();
            }

            var items = new List<string>();
            foreach (var f in emptyFolders) items.Add($"\"{f}\"");

            return CommandResult.Ok($"{{\"dryRun\":{(dryRun ? "true" : "false")},\"count\":{emptyFolders.Count},\"folders\":[{string.Join(",", items)}]}}");
        }

        private void FindEmptyFolders(string path, List<string> results)
        {
            if (!Directory.Exists(path)) return;

            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                FindEmptyFolders(dir, results);
            }

            var files = Directory.GetFiles(path);
            var subDirs = Directory.GetDirectories(path);
            // Empty if no files (except .meta) and no subdirs
            bool hasFiles = false;
            foreach (var f in files)
                if (!f.EndsWith(".meta")) { hasFiles = true; break; }

            if (!hasFiles && subDirs.Length == 0 && path != "Assets")
                results.Add(path.Replace("\\", "/"));
        }

        private CommandResult OptimizeTextures(JsonObject p)
        {
            var maxSize = p.GetInt("maxSize", 2048);
            var searchPath = p.GetString("path") ?? "Assets";
            var optimized = new List<string>();

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { searchPath });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                if (importer.maxTextureSize > maxSize)
                {
                    importer.maxTextureSize = maxSize;
                    importer.SaveAndReimport();
                    optimized.Add($"\"{assetPath}\"");
                }
            }

            return CommandResult.Ok($"{{\"optimized\":{optimized.Count},\"textures\":[{string.Join(",", optimized)}]}}");
        }
    }
}
