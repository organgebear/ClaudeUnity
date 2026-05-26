using UnityEngine;
using UnityEditor;
using System;

namespace ClaudeUnity
{
    public enum ApiProvider
    {
        Anthropic,
        OpenAI
    }

    public class ClaudeUnitySettings : ScriptableObject
    {
        private const string ASSET_PATH = "Assets/Plugins/ClaudeUnity/Editor/Resources/ClaudeUnitySettings.asset";
        private const string PREFS_KEY_API = "ClaudeUnity_ApiKey";

        [SerializeField] private string model = "claude-sonnet-4-5-20250929";
        [SerializeField] private int maxTokens = 8192;
        [SerializeField] private float temperature = 0f;
        [SerializeField] private bool streamResponses = true;
        [SerializeField] private string skillsPath = "";
        [SerializeField] private string customSystemPromptSuffix = "";
        [SerializeField] private string baseUrl = "https://yunyi.rdzhvip.com/claude";
        [SerializeField] private string baseUrlOpenAI = "";
        [SerializeField] private bool useProxyApi = true;
        [SerializeField] private string customModelName = "";
        [SerializeField] private ApiProvider apiProvider = ApiProvider.OpenAI;

        public string Model { get => model; set { model = value; Save(); } }
        public int MaxTokens { get => maxTokens; set { maxTokens = value; Save(); } }
        public float Temperature { get => temperature; set { temperature = value; Save(); } }
        public bool StreamResponses { get => streamResponses; set { streamResponses = value; Save(); } }
        public string SkillsPath { get => skillsPath; set { skillsPath = value; Save(); } }
        public string CustomSystemPromptSuffix { get => customSystemPromptSuffix; set { customSystemPromptSuffix = value; Save(); } }
        public string BaseUrl { get => baseUrl; set { baseUrl = value; Save(); } }
        public string BaseUrlOpenAI { get => baseUrlOpenAI; set { baseUrlOpenAI = value; Save(); } }
        public bool UseProxyApi { get => useProxyApi; set { useProxyApi = value; Save(); } }
        public string CustomModelName { get => customModelName; set { customModelName = value; Save(); } }
        public ApiProvider Provider { get => apiProvider; set { apiProvider = value; Save(); } }

        /// <summary>
        /// Returns the effective model name. If using proxy API with a custom model name, use that instead.
        /// </summary>
        public string EffectiveModel => string.IsNullOrEmpty(model) ? "claude-sonnet-4-20250514" : model;

        /// <summary>
        /// Builds the full API endpoint URL, handling various base URL formats.
        /// Supports: "https://api.anthropic.com", "https://proxy.com/v1", "https://proxy.com/v1/messages", etc.
        /// </summary>
        public string GetMessagesEndpoint()
        {
            var effectiveUrl = apiProvider == ApiProvider.OpenAI && !string.IsNullOrEmpty(baseUrlOpenAI)
                ? baseUrlOpenAI
                : baseUrl;
            var url = effectiveUrl.TrimEnd('/');

            if (apiProvider == ApiProvider.OpenAI)
            {
                if (url.EndsWith("/v1/chat/completions", System.StringComparison.OrdinalIgnoreCase))
                    return url;
                if (url.EndsWith("/v1", System.StringComparison.OrdinalIgnoreCase))
                    return url + "/chat/completions";
                return url + "/v1/chat/completions";
            }

            if (url.EndsWith("/v1/messages", System.StringComparison.OrdinalIgnoreCase))
                return url;
            if (url.EndsWith("/v1", System.StringComparison.OrdinalIgnoreCase))
                return url + "/messages";
            return url + "/v1/messages";
        }

        public string[] AvailableModels => new[]
        {
            // Official API models
            "claude-sonnet-4-20250514",
            "claude-opus-4-20250514",
            "claude-haiku-4-20250514",
            // Common proxy/relay variants
            "claude-sonnet-4-5-20250929",
            "claude-opus-4-6-20250514",
            "claude-haiku-4-6-20250514",
            // Legacy names
            "claude-3-5-sonnet-20241022",
            "claude-3-opus-20240229",
            "claude-3-haiku-20240307"
        };

        public string ApiKey
        {
            get => DecryptKey(EditorPrefs.GetString(PREFS_KEY_API, ""));
            set => EditorPrefs.SetString(PREFS_KEY_API, EncryptKey(value));
        }

        public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

        private static ClaudeUnitySettings _instance;
        public static ClaudeUnitySettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<ClaudeUnitySettings>(ASSET_PATH);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<ClaudeUnitySettings>();
                        _instance.TryImportFromClaudeCliConfig();
                        var dir = System.IO.Path.GetDirectoryName(ASSET_PATH);
                        if (!System.IO.Directory.Exists(dir))
                            System.IO.Directory.CreateDirectory(dir);
                        AssetDatabase.CreateAsset(_instance, ASSET_PATH);
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }

        private void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>
        /// Reads ~/.claude/settings.json and auto-populates API settings from the CLI config.
        /// Called once when the settings asset is first created.
        /// </summary>
        private void TryImportFromClaudeCliConfig()
        {
            try
            {
                var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                var configPath = System.IO.Path.Combine(home, ".claude", "settings.json");
                if (!System.IO.File.Exists(configPath)) return;

                var json = System.IO.File.ReadAllText(configPath);
                var config = SimpleJsonParser.Parse(json);
                var env = config.GetObject("env");
                if (env == null) return;

                var configBaseUrl = env.GetString("ANTHROPIC_BASE_URL");
                var configAuthToken = env.GetString("ANTHROPIC_AUTH_TOKEN");

                if (!string.IsNullOrEmpty(configBaseUrl))
                {
                    baseUrl = configBaseUrl;
                    useProxyApi = !configBaseUrl.Contains("api.anthropic.com");
                }

                if (!string.IsNullOrEmpty(configAuthToken))
                {
                    ApiKey = configAuthToken;
                }

                Debug.Log($"[ClaudeUnity] Auto-imported settings from Claude CLI config: {configPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClaudeUnity] Could not read Claude CLI config: {ex.Message}");
            }
        }

        private static string EncryptKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var salt = SystemInfo.deviceUniqueIdentifier;
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            var saltBytes = System.Text.Encoding.UTF8.GetBytes(salt);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= saltBytes[i % saltBytes.Length];
            return Convert.ToBase64String(bytes);
        }

        private static string DecryptKey(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            try
            {
                var bytes = Convert.FromBase64String(encrypted);
                var salt = SystemInfo.deviceUniqueIdentifier;
                var saltBytes = System.Text.Encoding.UTF8.GetBytes(salt);
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] ^= saltBytes[i % saltBytes.Length];
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch { return ""; }
        }
    }
}
