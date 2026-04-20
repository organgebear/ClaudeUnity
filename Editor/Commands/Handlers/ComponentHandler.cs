using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class ComponentHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "AddComponent": return AddComponent(p);
                case "RemoveComponent": return RemoveComponent(p);
                case "GetComponents": return GetComponents(p);
                case "SetComponentProperty": return SetComponentProperty(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult AddComponent(JsonObject p)
        {
            var target = p.GetString("target");
            var typeName = p.GetString("componentType");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var type = FindComponentType(typeName);
            if (type == null) return CommandResult.Fail($"Component type '{typeName}' not found");

            var comp = Undo.AddComponent(go, type);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"component\":\"{type.Name}\"}}");
        }

        private CommandResult RemoveComponent(JsonObject p)
        {
            var target = p.GetString("target");
            var typeName = p.GetString("componentType");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var type = FindComponentType(typeName);
            if (type == null) return CommandResult.Fail($"Component type '{typeName}' not found");

            var comp = go.GetComponent(type);
            if (comp == null) return CommandResult.Fail($"Component '{typeName}' not found on '{target}'");

            Undo.DestroyObjectImmediate(comp);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"removed\":\"{typeName}\"}}");
        }

        private CommandResult GetComponents(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var components = go.GetComponents<Component>();
            var list = new List<string>();
            foreach (var c in components)
                if (c != null) list.Add(c.GetType().Name);

            return CommandResult.Ok($"{{\"target\":\"{target}\",\"components\":[\"{string.Join("\",\"", list)}\"]}}");
        }

        private CommandResult SetComponentProperty(JsonObject p)
        {
            var target = p.GetString("target");
            var typeName = p.GetString("componentType");
            var propName = p.GetString("propertyName");
            var valueStr = p.GetString("value");

            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var type = FindComponentType(typeName);
            if (type == null) return CommandResult.Fail($"Component type '{typeName}' not found");

            var comp = go.GetComponent(type);
            if (comp == null) return CommandResult.Fail($"Component '{typeName}' not found on '{target}'");

            Undo.RecordObject(comp, $"Set {typeName}.{propName}");

            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                var val = ConvertValue(prop.PropertyType, valueStr);
                prop.SetValue(comp, val);
                return CommandResult.Ok($"{{\"target\":\"{target}\",\"component\":\"{typeName}\",\"property\":\"{propName}\",\"set\":true}}");
            }

            var field = type.GetField(propName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var val = ConvertValue(field.FieldType, valueStr);
                field.SetValue(comp, val);
                return CommandResult.Ok($"{{\"target\":\"{target}\",\"component\":\"{typeName}\",\"property\":\"{propName}\",\"set\":true}}");
            }

            return CommandResult.Fail($"Property '{propName}' not found on '{typeName}'");
        }

        private static Type FindComponentType(string name)
        {
            // Try common Unity types first
            var type = Type.GetType($"UnityEngine.{name}, UnityEngine.CoreModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{name}, UnityEngine.PhysicsModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{name}, UnityEngine.AudioModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{name}, UnityEngine.AnimationModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.UI.{name}, UnityEngine.UI");
            if (type != null) return type;

            // Search all assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(name) ?? asm.GetType($"UnityEngine.{name}");
                if (type != null && typeof(Component).IsAssignableFrom(type)) return type;
            }
            return null;
        }

        private static object ConvertValue(Type targetType, string value)
        {
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(Vector2)) return ParseVector2(value);
            if (targetType == typeof(Vector3)) return ParseVector3(value);
            if (targetType == typeof(Vector4)) return ParseVector4(value);
            if (targetType == typeof(Quaternion)) { var v = ParseVector4(value); return new Quaternion(v.x, v.y, v.z, v.w); }
            if (targetType == typeof(Color)) return ParseColor(value);
            if (targetType == typeof(Color32)) { var c = ParseColor(value); return (Color32)c; }
            if (targetType == typeof(Rect)) { var f = ParseFloatArray(value); return f.Length >= 4 ? new Rect(f[0], f[1], f[2], f[3]) : Rect.zero; }
            if (targetType.IsEnum) return Enum.Parse(targetType, value, true);
            return value;
        }

        private static float[] ParseFloatArray(string value)
        {
            value = value.Trim();
            if (value.StartsWith("[")) value = value.Substring(1);
            if (value.EndsWith("]")) value = value.Substring(0, value.Length - 1);
            var parts = value.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                float.TryParse(parts[i].Trim(), out result[i]);
            return result;
        }

        private static Vector2 ParseVector2(string value)
        {
            var f = ParseFloatArray(value);
            return f.Length >= 2 ? new Vector2(f[0], f[1]) : Vector2.zero;
        }

        private static Vector3 ParseVector3(string value)
        {
            var f = ParseFloatArray(value);
            return f.Length >= 3 ? new Vector3(f[0], f[1], f[2]) : Vector3.zero;
        }

        private static Vector4 ParseVector4(string value)
        {
            var f = ParseFloatArray(value);
            return f.Length >= 4 ? new Vector4(f[0], f[1], f[2], f[3]) : Vector4.zero;
        }

        private static Color ParseColor(string value)
        {
            value = value.Trim();
            if (value.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(value, out var c);
                return c;
            }
            var f = ParseFloatArray(value);
            if (f.Length >= 4) return new Color(f[0], f[1], f[2], f[3]);
            if (f.Length >= 3) return new Color(f[0], f[1], f[2]);
            return Color.white;
        }
    }
}
