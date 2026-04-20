using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ClaudeUnity
{
    public class AnimatorHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            var action = p.GetString("action");
            switch (action)
            {
                case "createcontroller": return CreateController(p);
                case "addparameter": return AddParameter(p);
                case "setparameter": return SetParameter(p);
                case "play": return Play(p);
                default: return CommandResult.Fail($"Unknown animator action: {action}");
            }
        }

        private CommandResult CreateController(JsonObject p)
        {
            var name = p.GetString("name") ?? "NewController";
            var folder = p.GetString("folder") ?? "Assets";

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{folder}/{name}.controller");
            return CommandResult.Ok($"{{\"name\":\"{name}\",\"path\":\"{folder}/{name}.controller\"}}");
        }

        private CommandResult AddParameter(JsonObject p)
        {
            var controllerPath = p.GetString("controller");
            var paramName = p.GetString("paramName");
            var paramType = p.GetString("paramType") ?? "float";

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) return CommandResult.Fail($"Controller not found: {controllerPath}");

            AnimatorControllerParameterType type = AnimatorControllerParameterType.Float;
            switch (paramType.ToLower())
            {
                case "float": type = AnimatorControllerParameterType.Float; break;
                case "int": type = AnimatorControllerParameterType.Int; break;
                case "bool": type = AnimatorControllerParameterType.Bool; break;
                case "trigger": type = AnimatorControllerParameterType.Trigger; break;
            }

            controller.AddParameter(paramName, type);
            EditorUtility.SetDirty(controller);
            return CommandResult.Ok($"{{\"controller\":\"{controllerPath}\",\"parameter\":\"{paramName}\",\"type\":\"{paramType}\"}}");
        }

        private CommandResult SetParameter(JsonObject p)
        {
            var target = p.GetString("target");
            var paramName = p.GetString("paramName");
            var value = p.GetString("value");

            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var animator = go.GetComponent<Animator>();
            if (animator == null) return CommandResult.Fail($"No Animator on '{target}'");

            // Try different types
            if (bool.TryParse(value, out var bVal)) animator.SetBool(paramName, bVal);
            else if (int.TryParse(value, out var iVal)) animator.SetInteger(paramName, iVal);
            else if (float.TryParse(value, out var fVal)) animator.SetFloat(paramName, fVal);

            return CommandResult.Ok($"{{\"target\":\"{target}\",\"parameter\":\"{paramName}\",\"value\":\"{value}\"}}");
        }

        private CommandResult Play(JsonObject p)
        {
            var target = p.GetString("target");
            var state = p.GetString("state");
            var layer = p.GetInt("layer", 0);

            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var animator = go.GetComponent<Animator>();
            if (animator == null) return CommandResult.Fail($"No Animator on '{target}'");

            animator.Play(state, layer);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"state\":\"{state}\"}}");
        }
    }
}
