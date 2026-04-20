using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ClaudeUnity
{
    public class UIHandler : ICommandHandler
    {
        public CommandResult Execute(string commandType, JsonObject p)
        {
            var action = p.GetString("action") ?? "create";
            return action == "create" ? CreateUI(p) : SetUI(p);
        }

        private CommandResult CreateUI(JsonObject p)
        {
            var uiType = p.GetString("uiType") ?? "text";
            var name = p.GetString("name") ?? uiType;
            var parentName = p.GetString("parent");

            Transform parent = null;
            if (!string.IsNullOrEmpty(parentName))
            {
                var parentGo = GameObject.Find(parentName);
                if (parentGo != null) parent = parentGo.transform;
            }

            // Ensure canvas exists for UI elements
            if (uiType != "canvas" && parent == null)
            {
                var canvas = Object.FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    var canvasGo = new GameObject("Canvas");
                    canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGo.AddComponent<CanvasScaler>();
                    canvasGo.AddComponent<GraphicRaycaster>();
                    Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
                }
                parent = canvas.transform;
            }

            GameObject go;
            switch (uiType)
            {
                case "canvas":
                    go = new GameObject(name);
                    var c = go.AddComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    go.AddComponent<CanvasScaler>();
                    go.AddComponent<GraphicRaycaster>();
                    break;

                case "panel":
                    go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    var img = go.AddComponent<Image>();
                    img.color = new Color(1, 1, 1, 0.4f);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    break;

                case "button":
                    go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<Image>();
                    go.AddComponent<Button>();
                    var btnText = new GameObject("Text");
                    btnText.transform.SetParent(go.transform, false);
                    var t = btnText.AddComponent<Text>();
                    t.text = p.GetString("text") ?? "Button";
                    t.alignment = TextAnchor.MiddleCenter;
                    t.color = Color.black;
                    var trt = btnText.GetComponent<RectTransform>();
                    trt.anchorMin = Vector2.zero;
                    trt.anchorMax = Vector2.one;
                    trt.sizeDelta = Vector2.zero;
                    break;

                case "text":
                    go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    var txt = go.AddComponent<Text>();
                    txt.text = p.GetString("text") ?? "Text";
                    txt.color = Color.white;
                    var fs = p.GetInt("fontSize", 14);
                    if (fs > 0) txt.fontSize = fs;
                    break;

                case "image":
                    go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<Image>();
                    break;

                case "inputfield":
                    go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<Image>();
                    var input = go.AddComponent<InputField>();
                    var inputText = new GameObject("Text");
                    inputText.transform.SetParent(go.transform, false);
                    var it = inputText.AddComponent<Text>();
                    it.supportRichText = false;
                    it.color = Color.black;
                    input.textComponent = it;
                    break;

                default:
                    go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    break;
            }

            if (uiType != "canvas" && uiType != "panel" && go.transform.parent == null && parent != null)
                go.transform.SetParent(parent, false);

            ApplyColor(go, p);
            Undo.RegisterCreatedObjectUndo(go, $"Create UI {uiType}");
            return CommandResult.Ok($"{{\"name\":\"{name}\",\"uiType\":\"{uiType}\"}}");
        }

        private CommandResult SetUI(JsonObject p)
        {
            var name = p.GetString("name");
            var go = GameObject.Find(name);
            if (go == null) return CommandResult.Fail($"UI element '{name}' not found");

            Undo.RecordObject(go, $"Set UI {name}");

            var text = p.GetString("text");
            if (text != null)
            {
                var txt = go.GetComponentInChildren<Text>();
                if (txt != null) { Undo.RecordObject(txt, "Set Text"); txt.text = text; }
            }

            var fontSize = p.GetInt("fontSize", 0);
            if (fontSize > 0)
            {
                var txt = go.GetComponentInChildren<Text>();
                if (txt != null) { Undo.RecordObject(txt, "Set FontSize"); txt.fontSize = fontSize; }
            }

            ApplyColor(go, p);
            return CommandResult.Ok($"{{\"name\":\"{name}\",\"set\":true}}");
        }

        private void ApplyColor(GameObject go, JsonObject p)
        {
            var colorHex = p.GetString("color");
            if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var color))
            {
                var graphic = go.GetComponent<Graphic>();
                if (graphic != null) { Undo.RecordObject(graphic, "Set Color"); graphic.color = color; }
            }
        }
    }
}
