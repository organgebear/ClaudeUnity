using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class CommandExecutor
    {
        private readonly Dictionary<string, ICommandHandler> _handlers = new Dictionary<string, ICommandHandler>();

        public CommandExecutor()
        {
            var go = new GameObjectHandler();
            Register("CreateGameObject", go);
            Register("DeleteGameObject", go);
            Register("SetTransform", go);
            Register("SetParent", go);
            Register("SetActive", go);
            Register("GetGameObjectInfo", go);

            var comp = new ComponentHandler();
            Register("AddComponent", comp);
            Register("RemoveComponent", comp);
            Register("GetComponents", comp);
            Register("SetComponentProperty", comp);

            var scene = new SceneHandler();
            Register("Scene", scene);
            Register("GetSceneInfo", scene);
            Register("GetSceneHierarchy", scene);

            var prefab = new PrefabHandler();
            Register("CreatePrefab", prefab);
            Register("InstantiatePrefab", prefab);
            Register("ApplyPrefab", prefab);
            Register("RevertPrefab", prefab);
            Register("UnpackPrefab", prefab);

            var asset = new AssetHandler();
            Register("FindAssets", asset);
            Register("CreateAsset", asset);
            Register("ManageAsset", asset);

            var mat = new MaterialHandler();
            Register("CreateMaterial", mat);
            Register("SetMaterial", mat);
            Register("SetMaterialProperty", mat);
            Register("FindShader", mat);
            Register("ListShaders", mat);
            Register("GetMaterialProperties", mat);
            Register("SetMaterialShader", mat);
            Register("GetShaderProperties", mat);

            var light = new LightHandler();
            Register("Light", light);

            var anim = new AnimatorHandler();
            Register("Animator", anim);

            var ui = new UIHandler();
            Register("UI", ui);

            var editor = new EditorControlHandler();
            Register("EnterPlayMode", editor);
            Register("ExitPlayMode", editor);
            Register("RefreshAssets", editor);
            Register("CompileScripts", editor);
            Register("SetSelection", editor);
            Register("GetSelection", editor);
            Register("ExecuteMenuItem", editor);
            Register("GetEditorInfo", editor);

            var debug = new DebugHandler();
            Register("DebugLog", debug);
            Register("GetLogs", debug);
            Register("PauseEditor", debug);
            Register("ResumeEditor", debug);
            Register("ClearConsole", debug);

            var project = new ProjectHandler();
            Register("GetProjectStructure", project);
            Register("MoveFile", project);
            Register("RenameFile", project);
            Register("DuplicateFile", project);

            var validation = new ValidationHandler();
            Register("ValidateScene", validation);
            Register("ValidateAssets", validation);
            Register("FindMissingScripts", validation);
            Register("CleanupEmptyFolders", validation);
            Register("OptimizeTextures", validation);

            var script = new ScriptHandler();
            Register("CreateScript", script);
            Register("ReadScript", script);
            Register("EditScript", script);

            var fs = new FileSystemHandler();
            Register("ReadFile", fs);
            Register("WriteFile", fs);
            Register("ListFiles", fs);
            Register("GetCompileErrors", fs);
            Register("DeleteFile", fs);
        }

        private void Register(string type, ICommandHandler handler)
        {
            _handlers[type] = handler;
        }

        public CommandResult Execute(string commandType, JsonObject parameters)
        {
            if (!_handlers.TryGetValue(commandType, out var handler))
                return CommandResult.Fail($"Unknown command: {commandType}");

            try
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName($"ClaudeUnity: {commandType}");
                var result = handler.Execute(commandType, parameters);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClaudeUnity] Command {commandType} failed: {ex.Message}\n{ex.StackTrace}");
                return CommandResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Parses a UnitySkills JSON result string into a CommandResult.
        /// </summary>
        public CommandResult ParseUnitySkillsResult(string resultJson, string commandName)
        {
            try
            {
                var json = SimpleJsonParser.Parse(resultJson);
                // UnitySkills returns error responses with "error" field
                var error = json.GetString("error");
                if (!string.IsNullOrEmpty(error))
                    return CommandResult.Fail(error);

                // Check for structured error
                var errorObj = json.GetObject("error");
                if (errorObj != null)
                {
                    var msg = errorObj.GetString("message") ?? errorObj.GetString("code") ?? "Unknown error";
                    return CommandResult.Fail(msg);
                }

                return CommandResult.Ok(resultJson);
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"UnitySkills parse error: {ex.Message}");
            }
        }
    }
}
