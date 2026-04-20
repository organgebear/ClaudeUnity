using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class GameObjectHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            switch (commandType)
            {
                case "CreateGameObject": return CreateGameObject(p);
                case "DeleteGameObject": return DeleteGameObject(p);
                case "SetTransform": return SetTransform(p);
                case "SetParent": return SetParent(p);
                case "SetActive": return SetActive(p);
                case "GetGameObjectInfo": return GetGameObjectInfo(p);
                default: return CommandResult.Fail($"Unknown: {commandType}");
            }
        }

        private CommandResult CreateGameObject(JsonObject p)
        {
            var name = p.GetString("name") ?? "GameObject";
            var primitiveType = p.GetString("primitiveType") ?? "";

            GameObject go;
            if (!string.IsNullOrEmpty(primitiveType))
            {
                if (Enum.TryParse<PrimitiveType>(primitiveType, true, out var pt))
                {
                    go = GameObject.CreatePrimitive(pt);
                    go.name = name;
                }
                else
                    return CommandResult.Fail($"Unknown primitive type: {primitiveType}");
            }
            else
            {
                go = new GameObject(name);
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return CommandResult.Ok($"{{\"name\":\"{go.name}\",\"instanceId\":{go.GetInstanceID()}}}");
        }

        private CommandResult DeleteGameObject(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");
            Undo.DestroyObjectImmediate(go);
            return CommandResult.Ok($"{{\"deleted\":\"{target}\"}}");
        }

        private CommandResult SetTransform(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            Undo.RecordObject(go.transform, $"SetTransform {target}");

            var pos = p.GetArray("position");
            if (pos != null && pos.Count >= 3)
                go.transform.position = new Vector3(ToFloat(pos[0]), ToFloat(pos[1]), ToFloat(pos[2]));

            var rot = p.GetArray("rotation");
            if (rot != null && rot.Count >= 3)
                go.transform.eulerAngles = new Vector3(ToFloat(rot[0]), ToFloat(rot[1]), ToFloat(rot[2]));

            var scale = p.GetArray("scale");
            if (scale != null && scale.Count >= 3)
                go.transform.localScale = new Vector3(ToFloat(scale[0]), ToFloat(scale[1]), ToFloat(scale[2]));

            return CommandResult.Ok($"{{\"target\":\"{target}\",\"position\":[{go.transform.position.x},{go.transform.position.y},{go.transform.position.z}]}}");
        }

        private CommandResult SetParent(JsonObject p)
        {
            var target = p.GetString("target");
            var parentName = p.GetString("parent");
            var worldStays = p.GetBool("worldPositionStays", true);

            var child = GameObject.Find(target);
            if (child == null) return CommandResult.Fail($"GameObject '{target}' not found");

            Undo.SetTransformParent(child.transform,
                string.IsNullOrEmpty(parentName) ? null : GameObject.Find(parentName)?.transform,
                worldStays, $"SetParent {target}");

            return CommandResult.Ok($"{{\"target\":\"{target}\",\"parent\":\"{parentName ?? "root"}\"}}");
        }

        private CommandResult SetActive(JsonObject p)
        {
            var target = p.GetString("target");
            var active = p.GetBool("active", true);
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            Undo.RecordObject(go, $"SetActive {target}");
            go.SetActive(active);
            return CommandResult.Ok($"{{\"target\":\"{target}\",\"active\":{(active ? "true" : "false")}}}");
        }

        private CommandResult GetGameObjectInfo(JsonObject p)
        {
            var target = p.GetString("target");
            var go = GameObject.Find(target);
            if (go == null) return CommandResult.Fail($"GameObject '{target}' not found");

            var t = go.transform;
            var components = go.GetComponents<Component>();
            var compList = new List<string>();
            foreach (var c in components)
                if (c != null) compList.Add(c.GetType().Name);

            return CommandResult.Ok($"{{\"name\":\"{go.name}\",\"active\":{(go.activeSelf ? "true" : "false")}," +
                $"\"position\":[{t.position.x},{t.position.y},{t.position.z}]," +
                $"\"rotation\":[{t.eulerAngles.x},{t.eulerAngles.y},{t.eulerAngles.z}]," +
                $"\"scale\":[{t.localScale.x},{t.localScale.y},{t.localScale.z}]," +
                $"\"components\":[\"{string.Join("\",\"", compList)}\"]," +
                $"\"childCount\":{t.childCount}}}");
        }

        private static float ToFloat(object obj)
        {
            if (obj is double d) return (float)d;
            if (obj is long l) return l;
            if (obj is int i) return i;
            if (obj is float f) return f;
            return 0f;
        }
    }
}
