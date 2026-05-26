using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClaudeUnity
{
    public class ClaudeUnitySettingsProvider : SettingsProvider
    {
        private ClaudeUnitySettings _settings;
        private string _apiKeyDisplay = "";

        public ClaudeUnitySettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new ClaudeUnitySettingsProvider("Project/Claude Unity", SettingsScope.Project)
            {
                keywords = new HashSet<string> { "claude", "ai", "unity", "api", "proxy", "relay" }
            };
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = ClaudeUnitySettings.Instance;
            _apiKeyDisplay = _settings.HasApiKey ? "••••••••••••" : "";
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Claude Unity Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // API Key
            EditorGUILayout.LabelField("API Key", EditorStyles.miniLabel);
            var newKey = EditorGUILayout.PasswordField(_apiKeyDisplay);
            if (newKey != _apiKeyDisplay && newKey != "••••••••••••")
            {
                _settings.ApiKey = newKey;
                _apiKeyDisplay = string.IsNullOrEmpty(newKey) ? "" : "••••••••••••";
            }

            EditorGUILayout.Space(5);

            // Base URL
            EditorGUILayout.LabelField("Base URLs", EditorStyles.boldLabel);
            var newUrl = EditorGUILayout.TextField("Anthropic Base URL", _settings.BaseUrl);
            if (newUrl != _settings.BaseUrl) _settings.BaseUrl = newUrl;
            var newOpenAIUrl = EditorGUILayout.TextField("OpenAI Base URL", _settings.BaseUrlOpenAI);
            if (newOpenAIUrl != _settings.BaseUrlOpenAI) _settings.BaseUrlOpenAI = newOpenAIUrl;

            // Model
            var newModel = EditorGUILayout.TextField("Model", _settings.Model);
            if (newModel != _settings.Model) _settings.Model = newModel;

            // Max Tokens
            var newTokens = EditorGUILayout.IntSlider("Max Tokens", _settings.MaxTokens, 1024, 32768);
            if (newTokens != _settings.MaxTokens) _settings.MaxTokens = newTokens;

            // Temperature
            var newTemp = EditorGUILayout.Slider("Temperature", _settings.Temperature, 0f, 1f);
            if (Mathf.Abs(newTemp - _settings.Temperature) > 0.001f) _settings.Temperature = newTemp;

            // Stream
            var newStream = EditorGUILayout.Toggle("Stream Responses", _settings.StreamResponses);
            if (newStream != _settings.StreamResponses) _settings.StreamResponses = newStream;

            // API Provider
            var providers = new[] { "Anthropic", "OpenAI" };
            var currentProvider = _settings.Provider == ApiProvider.OpenAI ? 1 : 0;
            var newProvider = EditorGUILayout.Popup("API Provider", currentProvider, providers);
            if (newProvider != currentProvider) _settings.Provider = newProvider == 1 ? ApiProvider.OpenAI : ApiProvider.Anthropic;

            EditorGUILayout.Space(5);

            // Skills Path
            var newSkills = EditorGUILayout.TextField("Skills Path", _settings.SkillsPath);
            if (newSkills != _settings.SkillsPath) _settings.SkillsPath = newSkills;

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Claude Unity Window"))
                ClaudeUnityWindow.ShowWindow();
        }
    }
}
