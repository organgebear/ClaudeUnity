using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class EditorControlHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "EnterPlayMode":
                    EditorApplication.isPlaying = true;
                    return CommandResult.Ok("{\"playMode\":true}");

                case "ExitPlayMode":
                    EditorApplication.isPlaying = false;
                    return CommandResult.Ok("{\"playMode\":false}");

                case "RefreshAssets":
                    DeferredRefresh.Request();
                    return CommandResult.Ok("{\"refreshed\":\"deferred, will refresh after conversation ends\"}");

                case "CompileScripts":
                    DeferredRefresh.RequestCompile();
                    return CommandResult.Ok("{\"compiling\":\"deferred, will compile after conversation ends\"}");

                case "SetSelection":
                    return SetSelection(p);

                case "GetSelection":
                    return GetSelection();

                case "ExecuteMenuItem":
                    return ExecuteMenuItem(p);

                case "GetEditorInfo":
                    return GetEditorInfo();

                default:
                    return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult SetSelection(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");
            Selection.activeGameObject = go;
            return CommandResult.Ok($"{{\"selected\":\"{target}\"}}");
        }

        private CommandResult GetSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return CommandResult.Ok("{\"selection\":null}");
            return CommandResult.Ok($"{{\"selection\":\"{selected.name}\",\"instanceId\":{selected.GetInstanceID()}}}");
        }

        private CommandResult ExecuteMenuItem(JsonObject p)
        {
            var menuPath = p.GetString("menuPath");
            var result = EditorApplication.ExecuteMenuItem(menuPath);
            return result
                ? CommandResult.Ok($"{{\"executed\":\"{menuPath}\"}}")
                : CommandResult.Fail($"Menu item '{menuPath}' not found or failed");
        }

        private CommandResult GetEditorInfo()
        {
            var info = new Dictionary<string, object>
            {
                ["unityVersion"] = Application.unityVersion,
                ["platform"] = Application.platform.ToString(),
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["isCompiling"] = EditorApplication.isCompiling
            };
            return CommandResult.Ok(info);
        }
    }
}
