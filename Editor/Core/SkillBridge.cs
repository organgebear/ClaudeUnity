using System.Collections.Generic;
using UnityEngine;
using UnitySkills;

namespace ClaudeUnity
{
    /// <summary>
    /// Bridge between ClaudeUnity's tool system and UnitySkills' REST skill system.
    /// Routes tool calls from Claude API responses to UnitySkills' SkillRouter for execution.
    /// </summary>
    public static class SkillBridge
    {
        private static bool _initialized;
        private static List<ToolDefinition> _cachedToolDefs;

        /// <summary>
        /// Whether UnitySkills is available and initialized.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                try { return SkillRouter.SkillCount > 0; }
                catch { return false; }
            }
        }

        /// <summary>
        /// Generates Claude API ToolDefinitions from UnitySkills skill metadata.
        /// </summary>
        public static List<ToolDefinition> GetToolDefinitions()
        {
            if (_cachedToolDefs != null) return _cachedToolDefs;

            try
            {
                var schemaJson = SkillRouter.GetSchema();
                var schema = SimpleJsonParser.Parse(schemaJson);
                var skills = schema.GetArray("skills");
                if (skills == null) return new List<ToolDefinition>();

                _cachedToolDefs = new List<ToolDefinition>();
                foreach (var skill in skills)
                {
                    if (!(skill is Dictionary<string, object> skillDict)) continue;
                    var s = new JsonObject(skillDict);
                    var name = s.GetString("name");
                    var desc = s.GetString("description") ?? "";
                    var parameters = ConvertToInputSchema(s.GetRaw("parameters"));

                    _cachedToolDefs.Add(new ToolDefinition
                    {
                        name = name,
                        description = desc,
                        input_schema = parameters
                    });
                }

                Debug.Log($"[ClaudeUnity] Generated {_cachedToolDefs.Count} tool definitions from UnitySkills");
                return _cachedToolDefs;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ClaudeUnity] Failed to generate tool definitions: {ex.Message}");
                return new List<ToolDefinition>();
            }
        }

        /// <summary>
        /// Executes a UnitySkills skill by name with JSON arguments.
        /// </summary>
        public static string Execute(string skillName, string argsJson)
        {
            try
            {
                var result = SkillRouter.Execute(skillName, argsJson);
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ClaudeUnity] SkillBridge execute '{skillName}' failed: {ex.Message}");
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// Dry-run: validates parameters without executing.
        /// </summary>
        public static string DryRun(string skillName, string argsJson)
        {
            try
            {
                return SkillRouter.DryRun(skillName, argsJson);
            }
            catch (System.Exception ex)
            {
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// Invalidates cached tool definitions (call after domain reload or skill rebuild).
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedToolDefs = null;
        }

        private static Dictionary<string, object> ConvertToInputSchema(object parameters)
        {
            if (parameters == null)
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new List<object>()
                };
            }

            // UnitySkills schema is already close to JSON Schema format
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>(),
                ["required"] = new List<object>()
            };

            if (parameters is Dictionary<string, object> paramDict)
            {
                var properties = new Dictionary<string, object>();
                var required = new List<object>();

                foreach (var kv in paramDict)
                {
                    var paramObj = kv.Value as Dictionary<string, object>;
                    if (paramObj == null) continue;

                    var propDef = new Dictionary<string, object>();
                    var paramType = paramObj.ContainsKey("type") ? paramObj["type"] as string : "string";
                    var paramDesc = paramObj.ContainsKey("description") ? paramObj["description"] as string : "";
                    var isRequired = paramObj.ContainsKey("required") && (bool)paramObj["required"];

                    propDef["type"] = ConvertType(paramType);
                    if (!string.IsNullOrEmpty(paramDesc))
                        propDef["description"] = paramDesc;

                    properties[kv.Key] = propDef;

                    if (isRequired)
                        required.Add(kv.Key);
                }

                schema["properties"] = properties;
                schema["required"] = required;
            }

            return schema;
        }

        private static string ConvertType(string unitySkillsType)
        {
            if (string.IsNullOrEmpty(unitySkillsType)) return "string";
            // UnitySkills uses JToken types; map to JSON Schema types
            switch (unitySkillsType.ToLowerInvariant())
            {
                case "string": return "string";
                case "integer":
                case "int":
                case "long": return "integer";
                case "float":
                case "double":
                case "number": return "number";
                case "bool":
                case "boolean": return "boolean";
                case "object":
                case "jobject": return "object";
                case "array":
                case "jarray": return "array";
                default: return "string";
            }
        }
    }
}
