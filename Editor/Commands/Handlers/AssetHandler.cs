using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class AssetHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "FindAssets": return FindAssets(p);
                case "CreateAsset": return CreateAsset(p);
                case "ManageAsset": return ManageAsset(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult FindAssets(JsonObject p)
        {
            var filter = p.GetString("searchFilter") ?? "";
            var limit = p.GetInt("limit", 20);
            var foldersArr = p.GetArray("searchInFolders");
            string[] folders = null;
            if (foldersArr != null)
            {
                folders = new string[foldersArr.Count];
                for (int i = 0; i < foldersArr.Count; i++)
                    folders[i] = foldersArr[i]?.ToString() ?? "";
            }

            var guids = folders != null
                ? AssetDatabase.FindAssets(filter, folders)
                : AssetDatabase.FindAssets(filter);

            var results = new List<string>();
            var count = Mathf.Min(guids.Length, limit);
            for (int i = 0; i < count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                results.Add($"{{\"path\":\"{path}\",\"guid\":\"{guids[i]}\"}}");
            }

            return CommandResult.Ok($"{{\"total\":{guids.Length},\"results\":[{string.Join(",", results)}]}}");
        }

        private CommandResult CreateAsset(JsonObject p)
        {
            var assetType = p.GetString("assetType");
            var name = p.GetString("name");
            var folder = p.GetString("folder");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            Object asset = null;
            string ext = ".asset";

            switch (assetType)
            {
                case "Material":
                    asset = new Material(Shader.Find("Standard"));
                    ext = ".mat";
                    break;
                case "AnimationClip":
                    asset = new AnimationClip();
                    ext = ".anim";
                    break;
                default:
                    return CommandResult.Fail($"Unsupported asset type: {assetType}");
            }

            var path = $"{folder}/{name}{ext}";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return CommandResult.Ok($"{{\"path\":\"{path}\",\"type\":\"{assetType}\"}}");
        }

        private CommandResult ManageAsset(JsonObject p)
        {
            var action = p.GetString("action");
            var path = p.GetString("path");

            switch (action)
            {
                case "delete":
                    AssetDatabase.DeleteAsset(path);
                    return CommandResult.Ok($"{{\"deleted\":\"{path}\"}}");

                case "getinfo":
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null) return CommandResult.Fail($"Asset not found: {path}");
                    return CommandResult.Ok($"{{\"path\":\"{path}\",\"type\":\"{asset.GetType().Name}\",\"name\":\"{asset.name}\"}}");

                case "import":
                case "reimport":
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    return CommandResult.Ok($"{{\"reimported\":\"{path}\"}}");

                default:
                    return CommandResult.Fail($"Unknown action: {action}");
            }
        }
    }
}
