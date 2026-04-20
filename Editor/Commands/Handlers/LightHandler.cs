using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class LightHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            var action = p.GetString("action") ?? "create";
            return action == "create" ? CreateLight(p) : SetLight(p);
        }

        private CommandResult CreateLight(JsonObject p)
        {
            var name = p.GetString("name") ?? "New Light";
            var lightType = p.GetString("lightType") ?? "Point";

            var go = new GameObject(name);
            var light = go.AddComponent<Light>();

            if (System.Enum.TryParse<LightType>(lightType, true, out var lt))
                light.type = lt;

            ApplyProperties(light, p);
            Undo.RegisterCreatedObjectUndo(go, $"Create Light {name}");
            return CommandResult.Ok($"{{\"name\":\"{name}\",\"lightType\":\"{lightType}\"}}");
        }

        private CommandResult SetLight(JsonObject p)
        {
            var name = p.GetString("name");
            var go = GameObject.Find(name);
            if (go == null) return CommandResult.Fail($"GameObject '{name}' not found");

            var light = go.GetComponent<Light>();
            if (light == null) return CommandResult.Fail($"No Light component on '{name}'");

            Undo.RecordObject(light, $"Set Light {name}");
            ApplyProperties(light, p);
            return CommandResult.Ok($"{{\"name\":\"{name}\",\"set\":true}}");
        }

        private void ApplyProperties(Light light, JsonObject p)
        {
            var colorHex = p.GetString("color");
            if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var color))
                light.color = color;

            var intensity = p.GetDouble("intensity", -1);
            if (intensity >= 0) light.intensity = (float)intensity;

            var range = p.GetDouble("range", -1);
            if (range >= 0) light.range = (float)range;

            var spotAngle = p.GetDouble("spotAngle", -1);
            if (spotAngle >= 0) light.spotAngle = (float)spotAngle;

            var shadows = p.GetString("shadows");
            if (!string.IsNullOrEmpty(shadows))
            {
                switch (shadows.ToLower())
                {
                    case "none": light.shadows = LightShadows.None; break;
                    case "hard": light.shadows = LightShadows.Hard; break;
                    case "soft": light.shadows = LightShadows.Soft; break;
                }
            }
        }
    }
}
