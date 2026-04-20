using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class PrefabHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "CreatePrefab": return CreatePrefab(p);
                case "InstantiatePrefab": return InstantiatePrefab(p);
                case "ApplyPrefab": return ApplyPrefab(p);
                case "RevertPrefab": return RevertPrefab(p);
                case "UnpackPrefab": return UnpackPrefab(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult CreatePrefab(JsonObject p)
        {
            var target = p.GetString("target");
            var path = p.GetString("path");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            return prefab != null
                ? CommandResult.Ok($"{{\"target\":\"{target}\",\"path\":\"{path}\"}}")
                : CommandResult.Fail("Failed to create prefab");
        }

        private CommandResult InstantiatePrefab(JsonObject p)
        {
            var path = p.GetString("path");
            var name = p.GetString("name");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return CommandResult.Fail($"Prefab not found at '{path}'");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (!string.IsNullOrEmpty(name)) instance.name = name;

            var pos = p.GetArray("position");
            if (pos != null && pos.Count >= 3)
                instance.transform.position = new Vector3(ToFloat(pos[0]), ToFloat(pos[1]), ToFloat(pos[2]));

            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {path}");
            return CommandResult.Ok($"{{\"name\":\"{instance.name}\",\"path\":\"{path}\"}}");
        }

        private CommandResult ApplyPrefab(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(prefabPath)) return CommandResult.Fail($"'{target}' is not a prefab instance");

            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"applied\":true}}");
        }

        private CommandResult RevertPrefab(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"reverted\":true}}");
        }

        private CommandResult UnpackPrefab(JsonObject p)
        {
            var target = p.GetString("target");
            var mode = p.GetString("mode") ?? "OutermostRoot";
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var unpackMode = mode == "Completely"
                ? PrefabUnpackMode.Completely
                : PrefabUnpackMode.OutermostRoot;

            PrefabUtility.UnpackPrefabInstance(go, unpackMode, InteractionMode.UserAction);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"mode\":\"{mode}\"}}");
        }

        private static float ToFloat(object obj)
        {
            if (obj is double d) return (float)d;
            if (obj is long l) return l;
            return 0f;
        }
    }
}
