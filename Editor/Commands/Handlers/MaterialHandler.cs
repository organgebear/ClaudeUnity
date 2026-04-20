using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class MaterialHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "CreateMaterial": return CreateMaterial(p);
                case "SetMaterial": return SetMaterial(p);
                case "SetMaterialProperty": return SetMaterialProperty(p);
                case "FindShader": return FindShader(p);
                case "ListShaders": return ListShaders(p);
                case "GetMaterialProperties": return GetMaterialProperties(p);
                case "SetMaterialShader": return SetMaterialShader(p);
                case "GetShaderProperties": return GetShaderProperties(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult CreateMaterial(JsonObject p)
        {
            var name = p.GetString("name") ?? "NewMaterial";
            var folder = p.GetString("folder") ?? "Assets/Materials";
            var shaderName = p.GetString("shader") ?? "Standard";
            var colorHex = p.GetString("color");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var shader = Shader.Find(shaderName);
            if (shader == null) return CommandResult.Fail($"Shader '{shaderName}' not found");

            var mat = new Material(shader);
            if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var color))
                mat.color = color;

            var path = $"{folder}/{name}.mat";
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return CommandResult.Ok($"{{\"name\":\"{name}\",\"path\":\"{path}\",\"shader\":\"{shaderName}\"}}");
        }

        private CommandResult SetMaterial(JsonObject p)
        {
            var target = p.GetString("target");
            var matPath = p.GetString("material");
            var index = p.GetInt("index", 0);

            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return CommandResult.Fail($"No Renderer on '{target}'");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return CommandResult.Fail($"Material not found: {matPath}");

            Undo.RecordObject(renderer, $"SetMaterial {target}");
            var mats = renderer.sharedMaterials;
            if (index < mats.Length)
            {
                mats[index] = mat;
                renderer.sharedMaterials = mats;
            }
            else
            {
                renderer.sharedMaterial = mat;
            }

            return CommandResult.Ok($"{{\"target\":\"{target}\",\"material\":\"{matPath}\"}}");
        }

        private CommandResult SetMaterialProperty(JsonObject p)
        {
            var matPath = p.GetString("materialPath");
            var propName = p.GetString("propertyName");
            var propType = p.GetString("propertyType");
            var value = p.GetString("value");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return CommandResult.Fail($"Material not found: {matPath}");

            Undo.RecordObject(mat, $"Set {propName}");

            switch (propType)
            {
                case "color":
                    if (ColorUtility.TryParseHtmlString(value, out var color))
                        mat.SetColor(propName, color);
                    break;
                case "float":
                    if (float.TryParse(value, out var f))
                        mat.SetFloat(propName, f);
                    break;
                case "vector":
                    // Expect [x,y,z,w]
                    break;
                case "texture":
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>(value);
                    if (tex != null) mat.SetTexture(propName, tex);
                    break;
            }

            EditorUtility.SetDirty(mat);
            return CommandResult.Ok($"{{\"material\":\"{matPath}\",\"property\":\"{propName}\",\"set\":true}}");
        }

        private CommandResult FindShader(JsonObject p)
        {
            var keyword = p.GetString("keyword")?.ToLower() ?? "";
            var results = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Shader");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader != null && shader.name.ToLower().Contains(keyword))
                    results.Add($"\"{shader.name}\"");
            }
            // Also check built-in
            string[] builtIn = { "Standard", "Unlit/Color", "Unlit/Texture", "Sprites/Default", "UI/Default", "Mobile/Diffuse" };
            foreach (var s in builtIn)
                if (s.ToLower().Contains(keyword) && !results.Contains($"\"{s}\""))
                    results.Add($"\"{s}\"");

            return CommandResult.Ok($"{{\"shaders\":[{string.Join(",", results)}]}}");
        }

        private CommandResult ListShaders(JsonObject p)
        {
            return FindShader(new JsonObject(new Dictionary<string, object> { ["keyword"] = "" }));
        }

        private CommandResult GetMaterialProperties(JsonObject p)
        {
            var matPath = p.GetString("materialPath");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return CommandResult.Fail($"Material not found: {matPath}");

            var shader = mat.shader;
            var props = new List<string>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                var pName = shader.GetPropertyName(i);
                var pType = shader.GetPropertyType(i);
                props.Add($"{{\"name\":\"{pName}\",\"type\":\"{pType}\"}}");
            }

            return CommandResult.Ok($"{{\"material\":\"{matPath}\",\"shader\":\"{shader.name}\",\"properties\":[{string.Join(",", props)}]}}");
        }

        private CommandResult SetMaterialShader(JsonObject p)
        {
            var matPath = p.GetString("materialPath");
            var shaderName = p.GetString("shader");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return CommandResult.Fail($"Material not found: {matPath}");

            var shader = Shader.Find(shaderName);
            if (shader == null) return CommandResult.Fail($"Shader '{shaderName}' not found");

            Undo.RecordObject(mat, "Change Shader");
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return CommandResult.Ok($"{{\"material\":\"{matPath}\",\"shader\":\"{shaderName}\"}}");
        }

        private CommandResult GetShaderProperties(JsonObject p)
        {
            var shaderName = p.GetString("shader");
            var shader = Shader.Find(shaderName);
            if (shader == null) return CommandResult.Fail($"Shader '{shaderName}' not found");

            var props = new List<string>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                var pName = shader.GetPropertyName(i);
                var pType = shader.GetPropertyType(i);
                props.Add($"{{\"name\":\"{pName}\",\"type\":\"{pType}\"}}");
            }

            return CommandResult.Ok($"{{\"shader\":\"{shaderName}\",\"properties\":[{string.Join(",", props)}]}}");
        }
    }
}
