using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClaudeUnity
{
    public class SceneHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "Scene": return ManageScene(p);
                case "GetSceneInfo": return GetSceneInfo(p);
                case "GetSceneHierarchy": return GetSceneHierarchy(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult ManageScene(JsonObject p)
        {
            var action = p.GetString("action");
            switch (action)
            {
                case "new":
                    var name = p.GetString("name") ?? "New Scene";
                    var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    return CommandResult.Ok($"{{\"action\":\"new\",\"scene\":\"{newScene.name}\"}}");

                case "save":
                    EditorSceneManager.SaveOpenScenes();
                    return CommandResult.Ok("{\"action\":\"save\",\"saved\":true}");

                case "load":
                    var path = p.GetString("path");
                    if (string.IsNullOrEmpty(path)) return CommandResult.Fail("Path required for load");
                    EditorSceneManager.OpenScene(path);
                    return CommandResult.Ok($"{{\"action\":\"load\",\"path\":\"{path}\"}}");

                case "saveas":
                    var savePath = p.GetString("path");
                    if (string.IsNullOrEmpty(savePath)) return CommandResult.Fail("Path required for saveas");
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), savePath);
                    return CommandResult.Ok($"{{\"action\":\"saveas\",\"path\":\"{savePath}\"}}");

                default:
                    return CommandResult.Fail($"Unknown scene action: {action}");
            }
        }

        private CommandResult GetSceneInfo(JsonObject p)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            return CommandResult.Ok($"{{\"name\":\"{scene.name}\",\"path\":\"{scene.path}\",\"isDirty\":{(scene.isDirty ? "true" : "false")},\"rootObjectCount\":{rootObjects.Length}}}");
        }

        private CommandResult GetSceneHierarchy(JsonObject p)
        {
            var includeComponents = p.GetBool("includeComponents", false);
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var items = new List<string>();

            foreach (var root in roots)
                BuildHierarchy(root, 0, includeComponents, items);

            return CommandResult.Ok($"{{\"scene\":\"{scene.name}\",\"hierarchy\":[{string.Join(",", items)}]}}");
        }

        private void BuildHierarchy(GameObject go, int depth, bool includeComponents, List<string> items)
        {
            var indent = new string(' ', depth * 2);
            var entry = $"{{\"name\":\"{go.name}\",\"depth\":{depth},\"active\":{(go.activeSelf ? "true" : "false")}";

            if (includeComponents)
            {
                var comps = go.GetComponents<Component>();
                var compNames = new List<string>();
                foreach (var c in comps)
                    if (c != null) compNames.Add($"\"{c.GetType().Name}\"");
                entry += $",\"components\":[{string.Join(",", compNames)}]";
            }

            entry += "}";
            items.Add(entry);

            for (int i = 0; i < go.transform.childCount; i++)
                BuildHierarchy(go.transform.GetChild(i).gameObject, depth + 1, includeComponents, items);
        }
    }
}
