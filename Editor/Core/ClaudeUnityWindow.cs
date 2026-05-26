using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClaudeUnity
{
    public class ClaudeUnityWindow : EditorWindow
    {
        private static string StripEmoji(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (char.IsSurrogate(c)) continue;
                if (c == '️' || c == '︎') continue;
                if (c >= '☀' && c <= '➿') continue;
                if (c >= '─' && c <= '╿') continue;
                if (c >= '▀' && c <= '▟') continue;
                if (c == '‍') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private ClaudeApiClient _apiClient;
        private CommandExecutor _commandExecutor;
        private ConversationSession _session;
        private CancellationTokenSource _cts;
        private bool _isProcessing;

        // UI elements
        private VisualElement _chatContainer;
        private ScrollView _chatScroll;
        private TextField _messageInput;
        private Button _sendBtn;
        private Button _cancelBtn;
        private Label _statusLabel;
        private VisualElement _settingsPanel;
        private VisualElement _welcomeElement;

        // Settings UI
        private TextField _apiKeyInput;
        private TextField _baseUrlInput;
        private TextField _baseUrlOpenAIInput;
        private TextField _modelInput;
        private SliderInt _maxTokensSlider;
        private Label _maxTokensLabel;
        private TextField _skillsPathInput;
        private Label _testResultLabel;
        private DropdownField _apiProviderDropdown;
        // Streaming state
        private StringBuilder _streamingText;
        private Label _streamingLabel;
        private VisualElement _currentAssistantBubble;
        private ChatMessage _currentAssistantMsg;
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        // @ file picker state
        private VisualElement _filePickerPopup;
        private ListView _filePickerList;
        private List<string> _filePickerResults = new List<string>();
        private int _atSignIndex = -1;
        private float _savedScrollPos = -1;

        [MenuItem("Window/AI/Claude Unity")]
        public static void ShowWindow()
        {
            var window = GetWindow<ClaudeUnityWindow>();
            window.titleContent = new GUIContent("Claude Unity");
            window.minSize = new Vector2(400, 500);
        }

        private const string SESSION_KEY = "ClaudeUnity_Session";

        private void OnEnable()
        {
            _apiClient = new ClaudeApiClient();
            _commandExecutor = new CommandExecutor();
            _session = LoadSession();
            _isProcessing = false; // Always reset after domain reload
            DeferredRefresh.Reset();
            EditorApplication.update += ProcessMainThreadQueue;
        }

        private void OnDisable()
        {
            CancelCurrentRequest();
            SaveSession();
            EditorApplication.update -= ProcessMainThreadQueue;
        }

        private void ProcessMainThreadQueue()
        {
            lock (_mainThreadQueue)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    try { _mainThreadQueue.Dequeue()?.Invoke(); }
                    catch (Exception ex) { Debug.LogError($"[ClaudeUnity] UI update error: {ex.Message}"); }
                }
            }
        }

        private void EnqueueMainThread(Action action)
        {
            lock (_mainThreadQueue) { _mainThreadQueue.Enqueue(action); }
        }

        public void CreateGUI()
        {
            var uxmlGuids = AssetDatabase.FindAssets("ClaudeUnityWindow t:VisualTreeAsset");
            if (uxmlGuids.Length == 0) { Debug.LogError("[ClaudeUnity] UXML not found! Make sure the plugin is in Assets/Plugins/ClaudeUnity/"); return; }
            var ussGuids = AssetDatabase.FindAssets("ClaudeUnityWindow t:StyleSheet");
            if (ussGuids.Length == 0) { Debug.LogError("[ClaudeUnity] USS not found!"); return; }
            var uxmlPath = AssetDatabase.GUIDToAssetPath(uxmlGuids[0]);
            var ussPath = AssetDatabase.GUIDToAssetPath(ussGuids[0]);

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);

            // Ensure rootVisualElement fills the entire window
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.flexShrink = 1;
            rootVisualElement.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

            uxml.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(uss);

            // Bind UI elements
            _chatContainer = rootVisualElement.Q<VisualElement>("chat-container");
            _chatScroll = rootVisualElement.Q<ScrollView>("chat-scroll");
            _messageInput = rootVisualElement.Q<TextField>("message-input");
            _sendBtn = rootVisualElement.Q<Button>("send-btn");
            _cancelBtn = rootVisualElement.Q<Button>("cancel-btn");
            _statusLabel = rootVisualElement.Q<Label>("status-label");
            _settingsPanel = rootVisualElement.Q<VisualElement>("settings-panel");
            _welcomeElement = rootVisualElement.Q<VisualElement>("welcome");

            // Settings UI
            _apiKeyInput = rootVisualElement.Q<TextField>("api-key-input");
            _baseUrlInput = rootVisualElement.Q<TextField>("base-url-input");
            _baseUrlOpenAIInput = rootVisualElement.Q<TextField>("base-url-openai-input");
            _modelInput = rootVisualElement.Q<TextField>("model-input");
            _maxTokensSlider = rootVisualElement.Q<SliderInt>("max-tokens-slider");
            _maxTokensLabel = rootVisualElement.Q<Label>("max-tokens-label");
            _skillsPathInput = rootVisualElement.Q<TextField>("skills-path-input");
            _testResultLabel = rootVisualElement.Q<Label>("test-result-label");
            _apiProviderDropdown = rootVisualElement.Q<DropdownField>("api-provider-dropdown");

            SetupEventHandlers();
            SetupFilePicker();
            SetupDragAndDrop();
            LoadSettings();
            RebuildChat();

            // Preserve scroll position when window regains focus
            rootVisualElement.RegisterCallback<FocusInEvent>(e =>
            {
                if (_chatScroll != null && _savedScrollPos >= 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (_chatScroll != null)
                            _chatScroll.verticalScroller.value = _savedScrollPos;
                    };
                }
            });
            _chatScroll.verticalScroller.valueChanged += v => _savedScrollPos = v;
        }
        private void SetupEventHandlers()
        {
            _sendBtn.clicked += OnSendClicked;
            _messageInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return && !e.shiftKey)
                {
                    e.PreventDefault();
                    e.StopPropagation();
                    // Use delayCall to avoid TextField internal processing conflicts
                    EditorApplication.delayCall += OnSendClicked;
                }
                else if (e.keyCode == KeyCode.Escape && _filePickerPopup != null)
                {
                    HideFilePicker();
                }
            }, TrickleDown.TrickleDown);

            _messageInput.RegisterValueChangedCallback(e =>
            {
                var val = e.newValue ?? "";
                var prev = e.previousValue ?? "";
                // Detect @ typed (new char added and it's @)
                if (val.Length > prev.Length && val.Length > 0)
                {
                    var lastChar = val[val.Length - 1];
                    if (lastChar == '@')
                    {
                        _atSignIndex = val.Length - 1;
                        ShowFilePicker("");
                        return;
                    }
                }
                // If picker is open, update search
                if (_filePickerPopup != null && _filePickerPopup.style.display == DisplayStyle.Flex && _atSignIndex >= 0)
                {
                    if (val.Length > _atSignIndex)
                    {
                        var query = val.Substring(_atSignIndex + 1);
                        UpdateFilePickerResults(query);
                    }
                    else
                    {
                        HideFilePicker();
                    }
                }
            });

            rootVisualElement.Q<Button>("settings-btn").clicked += () =>
                _settingsPanel.style.display = DisplayStyle.Flex;
            if (_cancelBtn != null)
                _cancelBtn.clicked += () =>
                {
                    CancelCurrentRequest();
                    _cancelBtn.style.display = DisplayStyle.None;
                };
            rootVisualElement.Q<Button>("settings-close-btn").clicked += () =>
            {
                SaveSettings();
                _settingsPanel.style.display = DisplayStyle.None;
            };
            rootVisualElement.Q<Button>("new-chat-btn").clicked += () =>
            {
                _session.Clear();
                SaveSession();
                RebuildChat();
            };

            // Settings bindings
            _maxTokensSlider?.RegisterValueChangedCallback(e =>
            {
                if (_maxTokensLabel != null) _maxTokensLabel.text = e.newValue.ToString();
            });

            var testBtn = rootVisualElement.Q<Button>("test-connection-btn");
            if (testBtn != null) testBtn.clicked += TestConnection;

            // Model input
            if (_modelInput != null)
            {
                _modelInput.value = ClaudeUnitySettings.Instance.Model;
            }

            // API Provider dropdown
            if (_apiProviderDropdown != null)
            {
                _apiProviderDropdown.choices = new List<string> { "Anthropic", "OpenAI" };
                _apiProviderDropdown.value = ClaudeUnitySettings.Instance.Provider == ApiProvider.OpenAI ? "OpenAI" : "Anthropic";
            }

        }

        private void LoadSettings()
        {
            var s = ClaudeUnitySettings.Instance;
            if (_apiKeyInput != null) _apiKeyInput.value = s.HasApiKey ? "••••••••" : "";
            if (_baseUrlInput != null) _baseUrlInput.value = s.BaseUrl;
            if (_baseUrlOpenAIInput != null) _baseUrlOpenAIInput.value = s.BaseUrlOpenAI;
            if (_modelInput != null) _modelInput.value = s.Model;
            if (_maxTokensSlider != null) _maxTokensSlider.value = s.MaxTokens;
            if (_maxTokensLabel != null) _maxTokensLabel.text = s.MaxTokens.ToString();
            if (_skillsPathInput != null) _skillsPathInput.value = s.SkillsPath;
            if (_apiProviderDropdown != null) _apiProviderDropdown.value = s.Provider == ApiProvider.OpenAI ? "OpenAI" : "Anthropic";
        }

        private void SaveSettings()
        {
            var s = ClaudeUnitySettings.Instance;
            var keyVal = _apiKeyInput?.value ?? "";
            if (!string.IsNullOrEmpty(keyVal) && keyVal != "••••••••")
                s.ApiKey = keyVal;
            if (_baseUrlInput != null) s.BaseUrl = _baseUrlInput.value;
            if (_baseUrlOpenAIInput != null) s.BaseUrlOpenAI = _baseUrlOpenAIInput.value;
            if (_modelInput != null) s.Model = _modelInput.value;
            if (_maxTokensSlider != null) s.MaxTokens = _maxTokensSlider.value;
            if (_skillsPathInput != null) s.SkillsPath = _skillsPathInput.value;
            if (_apiProviderDropdown != null) s.Provider = _apiProviderDropdown.value == "OpenAI" ? ApiProvider.OpenAI : ApiProvider.Anthropic;
        }

        private async void TestConnection()
        {
            if (_testResultLabel == null) return;
            _testResultLabel.text = "Testing...";
            _testResultLabel.RemoveFromClassList("test-result--success");
            _testResultLabel.RemoveFromClassList("test-result--error");

            SaveSettings();
            var settings = ClaudeUnitySettings.Instance;

            if (!settings.HasApiKey)
            {
                _testResultLabel.text = "Please enter an API key first";
                _testResultLabel.AddToClassList("test-result--error");
                Debug.LogError("[ClaudeUnity] Test connection: No API key");
                return;
            }

            try
            {
                var messages = new List<ApiMessage> { ApiMessage.User("Say hello in 5 words or less.") };
                var response = await _apiClient.SendMessageAsync(settings, messages, "You are a test.", null);
                var isOpenAI = settings.Provider == ApiProvider.OpenAI;

                if (isOpenAI)
                {
                    // Parse OpenAI format: {"choices":[{"message":{"content":"..."}}]}
                    var json = SimpleJsonParser.Parse(response);
                    var choices = json.GetArray("choices");
                    if (choices != null && choices.Count > 0)
                    {
                        var firstChoice = choices[0] as Dictionary<string, object>;
                        if (firstChoice != null)
                        {
                            var choiceObj = new JsonObject(firstChoice);
                            var message = choiceObj.GetObject("message");
                            var text = message?.GetString("content");
                            if (!string.IsNullOrEmpty(text))
                            {
                                _testResultLabel.text = $"OK: {text.Trim()}";
                                _testResultLabel.AddToClassList("test-result--success");
                                return;
                            }
                        }
                    }
                }
                else
                {
                    // Parse Anthropic format: {"content":[{"type":"text","text":"..."}]}
                    var json = SimpleJsonParser.Parse(response);
                    var content = json.GetArray("content");
                    if (content != null && content.Count > 0)
                    {
                        var firstBlock = content[0] as Dictionary<string, object>;
                        if (firstBlock != null)
                        {
                            var blockObj = new JsonObject(firstBlock);
                            var text = blockObj.GetString("text");
                            if (!string.IsNullOrEmpty(text))
                            {
                                _testResultLabel.text = $"OK: {text.Trim()}";
                                _testResultLabel.AddToClassList("test-result--success");
                                return;
                            }
                        }
                    }
                }

                _testResultLabel.text = $"Unexpected response: {response.Substring(0, Math.Min(response.Length, 150))}";
                _testResultLabel.AddToClassList("test-result--error");
                Debug.LogError($"[ClaudeUnity] Test connection: Unexpected response format. Full response: {response}");
            }
            catch (Exception ex)
            {
                _testResultLabel.text = $"Failed: {ex.Message}";
                _testResultLabel.AddToClassList("test-result--error");
                Debug.LogError($"[ClaudeUnity] Test connection failed: {ex}");
            }
        }

        // ── Chat UI Building ──

        private void RebuildChat()
        {
            _chatContainer.Clear();
            if (_session.Messages.Count == 0)
            {
                AddWelcomeMessage();
                return;
            }
            foreach (var msg in _session.Messages)
                AddMessageToUI(msg);
            ScrollToBottom();
        }

        private void AddWelcomeMessage()
        {
            var welcome = new VisualElement();
            welcome.AddToClassList("welcome-container");
            welcome.name = "welcome";
            var title = new Label("Claude Unity");
            title.AddToClassList("welcome-title");
            var subtitle = new Label("AI-powered Unity Editor assistant");
            subtitle.AddToClassList("welcome-subtitle");
            var hint = new Label("Try: \"Create a red cube with physics\"");
            hint.AddToClassList("welcome-hint");
            welcome.Add(title);
            welcome.Add(subtitle);
            welcome.Add(hint);
            _chatContainer.Add(welcome);
            _welcomeElement = welcome;
        }

        private VisualElement AddMessageToUI(ChatMessage msg)
        {
            var row = new VisualElement();
            row.AddToClassList("message-row");
            row.AddToClassList(msg.Role == "user" ? "message-row--user" : "message-row--assistant");

            var bubble = new VisualElement();
            bubble.AddToClassList("message-bubble");

            var role = new Label(msg.Role == "user" ? "YOU" : "CLAUDE");
            role.AddToClassList("message-role");
            bubble.Add(role);

            var text = new Label(StripEmoji(msg.TextContent ?? ""));
            text.AddToClassList("message-text");
            bubble.Add(text);

            // Command badges
            if (msg.Commands.Count > 0)
            {
                var cmdsContainer = new VisualElement();
                cmdsContainer.AddToClassList("commands-container");
                foreach (var cmd in msg.Commands)
                    cmdsContainer.Add(CreateCommandBadge(cmd));
                bubble.Add(cmdsContainer);
            }

            var time = new Label(msg.Timestamp.ToString("HH:mm"));
            time.AddToClassList("message-time");
            bubble.Add(time);

            row.Add(bubble);
            _chatContainer.Add(row);
            return row;
        }

        private VisualElement CreateCommandBadge(CommandExecution cmd)
        {
            var badge = new VisualElement();
            badge.AddToClassList("command-badge");
            badge.AddToClassList($"command-badge--{cmd.Status.ToString().ToLower()}");

            var dot = new VisualElement();
            dot.AddToClassList("command-status-dot");
            dot.AddToClassList($"command-status-dot--{cmd.Status.ToString().ToLower()}");
            badge.Add(dot);

            var typeLabel = new Label(cmd.CommandType);
            typeLabel.AddToClassList("command-type-label");
            badge.Add(typeLabel);

            var targetLabel = new Label(cmd.TargetName ?? "");
            targetLabel.AddToClassList("command-target-label");
            badge.Add(targetLabel);

            string statusText;
            switch (cmd.Status)
            {
                case CommandStatus.Pending: statusText = "Pending..."; break;
                case CommandStatus.Executing: statusText = "Executing..."; break;
                case CommandStatus.Success: statusText = "Done"; break;
                case CommandStatus.Failed: statusText = "Failed"; break;
                default: statusText = ""; break;
            }

            var statusLabel = new Label(statusText);
            statusLabel.AddToClassList("command-status-text");
            if (cmd.Status == CommandStatus.Success)
                statusLabel.AddToClassList("command-status-text--success");
            else if (cmd.Status == CommandStatus.Failed)
                statusLabel.AddToClassList("command-status-text--failed");
            badge.Add(statusLabel);

            if (cmd.Status == CommandStatus.Failed && !string.IsNullOrEmpty(cmd.ErrorMessage))
            {
                var errorLabel = new Label(cmd.ErrorMessage);
                errorLabel.AddToClassList("command-error-text");
                badge.Add(errorLabel);
            }

            return badge;
        }

        // ── @ File Picker ──

        private void SetupFilePicker()
        {
            _filePickerPopup = new VisualElement();
            _filePickerPopup.name = "file-picker-popup";
            _filePickerPopup.style.position = Position.Absolute;
            _filePickerPopup.style.bottom = 60;
            _filePickerPopup.style.left = 12;
            _filePickerPopup.style.right = 60;
            _filePickerPopup.style.maxHeight = 200;
            _filePickerPopup.style.backgroundColor = new Color(0.1f, 0.1f, 0.18f, 0.98f);
            _filePickerPopup.style.borderTopLeftRadius = 8;
            _filePickerPopup.style.borderTopRightRadius = 8;
            _filePickerPopup.style.borderBottomLeftRadius = 8;
            _filePickerPopup.style.borderBottomRightRadius = 8;
            _filePickerPopup.style.borderTopWidth = 1;
            _filePickerPopup.style.borderBottomWidth = 1;
            _filePickerPopup.style.borderLeftWidth = 1;
            _filePickerPopup.style.borderRightWidth = 1;
            _filePickerPopup.style.borderTopColor = new Color(1, 1, 1, 0.1f);
            _filePickerPopup.style.borderBottomColor = new Color(1, 1, 1, 0.1f);
            _filePickerPopup.style.borderLeftColor = new Color(1, 1, 1, 0.1f);
            _filePickerPopup.style.borderRightColor = new Color(1, 1, 1, 0.1f);
            _filePickerPopup.style.display = DisplayStyle.None;

            var header = new Label("Select file (@)");
            header.style.fontSize = 10;
            header.style.color = new Color(0.6f, 0.6f, 0.7f);
            header.style.paddingLeft = 10;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 4;
            _filePickerPopup.Add(header);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = 170;

            _filePickerList = new ListView();
            _filePickerList.makeItem = () =>
            {
                var label = new Label();
                label.style.fontSize = 12;
                label.style.color = new Color(0.9f, 0.9f, 0.94f);
                label.style.paddingLeft = 10;
                label.style.paddingRight = 10;
                label.style.paddingTop = 4;
                label.style.paddingBottom = 4;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.RegisterCallback<MouseEnterEvent>(e => label.style.backgroundColor = new Color(0.2f, 0.2f, 0.35f));
                label.RegisterCallback<MouseLeaveEvent>(e => label.style.backgroundColor = Color.clear);
                return label;
            };
            _filePickerList.bindItem = (element, index) =>
            {
                if (index < _filePickerResults.Count)
                    (element as Label).text = _filePickerResults[index];
            };
            _filePickerList.itemsSource = _filePickerResults;
            _filePickerList.fixedItemHeight = 24;
            _filePickerList.selectionType = SelectionType.Single;
            _filePickerList.onSelectionChange += (items) =>
            {
                foreach (var item in items)
                {
                    if (item is string path)
                        InsertFilePath(path);
                }
            };

            scrollView.Add(_filePickerList);
            _filePickerPopup.Add(scrollView);
            rootVisualElement.Add(_filePickerPopup);
        }

        private void ShowFilePicker(string query)
        {
            UpdateFilePickerResults(query);
            _filePickerPopup.style.display = DisplayStyle.Flex;
        }

        private void HideFilePicker()
        {
            _filePickerPopup.style.display = DisplayStyle.None;
            _atSignIndex = -1;
        }

        private void UpdateFilePickerResults(string query)
        {
            _filePickerResults.Clear();
            var filter = string.IsNullOrEmpty(query) ? "" : query;
            var guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
            int count = 0;
            foreach (var guid in guids)
            {
                if (count >= 20) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                _filePickerResults.Add(path);
                count++;
            }
            _filePickerList.itemsSource = _filePickerResults;
#if UNITY_2022_2_OR_NEWER
            _filePickerList.RefreshItems();
#else
            _filePickerList.Rebuild();
#endif
        }

        private void InsertFilePath(string path)
        {
            var val = _messageInput.value ?? "";
            // Replace @query with the path
            if (_atSignIndex >= 0 && _atSignIndex < val.Length)
            {
                var before = val.Substring(0, _atSignIndex);
                _messageInput.SetValueWithoutNotify(before + path + " ");
            }
            else
            {
                _messageInput.SetValueWithoutNotify(val + path + " ");
            }
            HideFilePicker();
            _messageInput.Focus();
        }

        // ── Drag & Drop ──

        private void SetupDragAndDrop()
        {
            _chatScroll.RegisterCallback<DragEnterEvent>(e =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });

            _chatScroll.RegisterCallback<DragUpdatedEvent>(e =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });

            _chatScroll.RegisterCallback<DragPerformEvent>(e =>
            {
                DragAndDrop.AcceptDrag();
                var paths = DragAndDrop.paths;
                if (paths != null && paths.Length > 0)
                {
                    var current = _messageInput.value ?? "";
                    var insert = string.Join(" ", paths);
                    if (current.Length > 0 && !current.EndsWith(" "))
                        insert = " " + insert;
                    _messageInput.SetValueWithoutNotify(current + insert + " ");
                    _messageInput.Focus();
                }
            });

            // Also support drop on input bar
            var inputBar = rootVisualElement.Q<VisualElement>("input-bar");
            inputBar.RegisterCallback<DragEnterEvent>(e =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });
            inputBar.RegisterCallback<DragUpdatedEvent>(e =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });
            inputBar.RegisterCallback<DragPerformEvent>(e =>
            {
                DragAndDrop.AcceptDrag();
                var paths = DragAndDrop.paths;
                if (paths != null && paths.Length > 0)
                {
                    var current = _messageInput.value ?? "";
                    var insert = string.Join(" ", paths);
                    if (current.Length > 0 && !current.EndsWith(" "))
                        insert = " " + insert;
                    _messageInput.SetValueWithoutNotify(current + insert + " ");
                    _messageInput.Focus();
                }
            });
        }

        private void ScrollToBottom()
        {
            EditorApplication.delayCall += () =>
            {
                if (_chatScroll != null)
                    _chatScroll.verticalScroller.value = _chatScroll.verticalScroller.highValue;
            };
        }
        // ── Send / Receive Logic ──

        private async void OnSendClicked()
        {
            var text = _messageInput?.value?.Trim();
            if (string.IsNullOrEmpty(text) || _isProcessing) return;

            var settings = ClaudeUnitySettings.Instance;
            if (!settings.HasApiKey)
            {
                Debug.LogWarning("[ClaudeUnity] Cannot send: No API key configured");
                _settingsPanel.style.display = DisplayStyle.Flex;
                return;
            }

            // Remove welcome
            if (_welcomeElement != null)
            {
                _welcomeElement.RemoveFromHierarchy();
                _welcomeElement = null;
            }

            // Clear input and lock UI
            _messageInput.SetValueWithoutNotify("");
            _isProcessing = true;
            UpdateStatus("Thinking...");
            _sendBtn.SetEnabled(false);
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.Flex;

            // Add user message
            _session.AddUserMessage(text);
            AddMessageToUI(_session.Messages[_session.Messages.Count - 1]);
            ScrollToBottom();

            _cts = new CancellationTokenSource();

            try
            {
                await RunConversationLoop(settings, _cts.Token);
            }
            catch (OperationCanceledException) { /* user cancelled */ }
            catch (Exception ex)
            {
                Debug.LogError($"[ClaudeUnity] Error: {ex.Message}");
                var errorMsg = _session.AddAssistantMessage($"Error: {ex.Message}");
                AddMessageToUI(errorMsg);
            }
            finally
            {
                _isProcessing = false;
                UpdateStatus("");
                _sendBtn.SetEnabled(true);
                if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;
                SaveSession();
                ScrollToBottom();
                // Re-focus input after everything is done
                EditorApplication.delayCall += () => _messageInput?.Focus();
                // Now flush any deferred asset refresh / compilation
                DeferredRefresh.FlushIfNeeded();
            }
        }

        private async Task RunConversationLoop(ClaudeUnitySettings settings, CancellationToken ct)
        {
            var systemPrompt = SystemPromptBuilder.BuildSystemPrompt(settings.SkillsPath, settings.CustomSystemPromptSuffix);
            var tools = SystemPromptBuilder.BuildToolDefinitions();

            int maxTurns = 25; // safety limit (increased for iterative development)
            for (int turn = 0; turn < maxTurns && !ct.IsCancellationRequested; turn++)
            {
                // Build API messages from session
                var apiMessages = BuildApiMessages();

                // Stream response
                _streamingText = new StringBuilder();
                var toolUseBlocks = new List<ToolUseInfo>();
                string stopReason = null;

                _currentAssistantMsg = new ChatMessage("assistant", "");
                _session.Messages.Add(_currentAssistantMsg);

                // Create streaming UI
                var row = new VisualElement();
                row.AddToClassList("message-row");
                row.AddToClassList("message-row--assistant");
                var bubble = new VisualElement();
                bubble.AddToClassList("message-bubble");
                var roleLabel = new Label("CLAUDE");
                roleLabel.AddToClassList("message-role");
                bubble.Add(roleLabel);
                _streamingLabel = new Label("");
                _streamingLabel.AddToClassList("message-text");
                bubble.Add(_streamingLabel);
                _currentAssistantBubble = bubble;
                row.Add(bubble);
                _chatContainer.Add(row);
                ScrollToBottom();

                // Current tool being accumulated
                string currentToolId = null;
                string currentToolName = null;
                var toolInputBuilder = new StringBuilder();

                await _apiClient.SendMessageStreamAsync(
                    settings, apiMessages, systemPrompt, tools,
                    onEvent: (evt) =>
                    {
                        switch (evt.Type)
                        {
                            case StreamEventType.ContentBlockStart:
                                if (evt.BlockType == "tool_use")
                                {
                                    currentToolId = evt.ToolUseId;
                                    currentToolName = evt.ToolName;
                                    toolInputBuilder.Clear();
                                }
                                break;

                            case StreamEventType.ContentBlockDelta:
                                if (!string.IsNullOrEmpty(evt.TextDelta))
                                {
                                    _streamingText.Append(StripEmoji(evt.TextDelta));
                                    EnqueueMainThread(() =>
                                    {
                                        if (_streamingLabel != null)
                                            _streamingLabel.text = _streamingText.ToString();
                                        ScrollToBottom();
                                    });
                                }
                                if (!string.IsNullOrEmpty(evt.InputJsonDelta))
                                {
                                    toolInputBuilder.Append(evt.InputJsonDelta);
                                }
                                break;

                            case StreamEventType.ContentBlockStop:
                                if (currentToolId != null)
                                {
                                    toolUseBlocks.Add(new ToolUseInfo
                                    {
                                        Id = currentToolId,
                                        Name = currentToolName,
                                        InputJson = toolInputBuilder.ToString()
                                    });
                                    currentToolId = null;
                                    currentToolName = null;
                                }
                                break;

                            case StreamEventType.MessageDelta:
                                stopReason = evt.StopReason;
                                break;

                            case StreamEventType.Error:
                                Debug.LogError($"[ClaudeUnity] Stream error: {evt.ErrorMessage}");
                                EnqueueMainThread(() =>
                                {
                                    _streamingText.Append($"\n[Error: {evt.ErrorMessage}]");
                                    if (_streamingLabel != null)
                                        _streamingLabel.text = _streamingText.ToString();
                                });
                                break;
                        }
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[ClaudeUnity] API error: {error}");
                        EnqueueMainThread(() =>
                        {
                            _streamingText.Append($"\n[Error: {error}]");
                            if (_streamingLabel != null)
                                _streamingLabel.text = _streamingText.ToString();
                        });
                    },
                    ct);

                // Update the message model
                _currentAssistantMsg.TextContent = _streamingText.ToString();
                UpdateStatus("");

                // Hide empty text label to avoid blank bubble space
                if (_streamingText.Length == 0 && _streamingLabel != null)
                    _streamingLabel.style.display = DisplayStyle.None;

                // If no tool calls, finalize this bubble
                if (toolUseBlocks.Count == 0)
                {
                    // Remove completely empty bubbles (no text, no tools)
                    if (_streamingText.Length == 0)
                    {
                        row.RemoveFromHierarchy();
                        _session.Messages.Remove(_currentAssistantMsg);
                    }
                    else
                    {
                        // Add timestamp
                        var timeLabel = new Label(DateTime.Now.ToString("HH:mm"));
                        timeLabel.AddToClassList("message-time");
                        _currentAssistantBubble.Add(timeLabel);
                    }
                    break;
                }

                // Execute tool calls and build results
                UpdateStatus($"Executing {toolUseBlocks.Count} command(s)...");

                var toolResults = new List<ContentBlock>();
                var assistantBlocks = new List<ContentBlock>();

                // Add text block if any
                if (_streamingText.Length > 0)
                    assistantBlocks.Add(ContentBlock.Text(_streamingText.ToString()));

                // Create command executions and add to UI
                var cmdsContainer = new VisualElement();
                cmdsContainer.AddToClassList("commands-container");
                _currentAssistantBubble.Add(cmdsContainer);

                foreach (var tool in toolUseBlocks)
                {
                    // Parse input
                    JsonObject inputObj = null;
                    try { inputObj = SimpleJsonParser.Parse(tool.InputJson); }
                    catch { inputObj = new JsonObject(new Dictionary<string, object>()); }

                    var cmdExec = new CommandExecution
                    {
                        CommandId = tool.Id,
                        CommandType = tool.Name,
                        ParametersJson = tool.InputJson,
                        TargetName = inputObj.GetString("target") ?? inputObj.GetString("name") ?? "",
                        Status = CommandStatus.Pending,
                        StartedAt = DateTime.Now
                    };
                    _currentAssistantMsg.Commands.Add(cmdExec);

                    // Add tool_use to assistant blocks
                    assistantBlocks.Add(ContentBlock.ToolUse(tool.Id, tool.Name,
                        inputObj?.ToDictionary() ?? new Dictionary<string, object>()));

                    // Add badge to UI
                    var badge = CreateCommandBadge(cmdExec);
                    cmdsContainer.Add(badge);
                    ScrollToBottom();

                    // Execute
                    cmdExec.Status = CommandStatus.Executing;
                    UpdateBadge(badge, cmdExec);
                    UpdateStatus($"Executing: {tool.Name}...");

                    CommandResult result;
                    try
                    {
                        // Try UnitySkills first for richer skill execution
                        if (SkillBridge.IsAvailable && UnitySkills.SkillRouter.HasSkill(tool.Name))
                        {
                            var skillResult = SkillBridge.Execute(tool.Name, tool.InputJson);
                            result = _commandExecutor.ParseUnitySkillsResult(skillResult, tool.Name);
                        }
                        else
                        {
                            result = _commandExecutor.Execute(tool.Name, inputObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ClaudeUnity] Command '{tool.Name}' failed: {ex}");
                        result = CommandResult.Fail(ex.Message);
                    }

                    cmdExec.Status = result.Success ? CommandStatus.Success : CommandStatus.Failed;
                    cmdExec.ResultJson = result.DataJson;
                    cmdExec.ErrorMessage = result.ErrorMessage;
                    cmdExec.CompletedAt = DateTime.Now;

                    UpdateBadge(badge, cmdExec);

                    // Build tool_result
                    var resultContent = result.Success ? result.DataJson : $"Error: {result.ErrorMessage}";
                    toolResults.Add(ContentBlock.ToolResult(tool.Id, resultContent, !result.Success));
                }

                // Add assistant message with tool_use blocks to conversation
                // Replace the last message's content representation for API
                _currentAssistantMsg.TextContent = _streamingText.ToString();

                // We need to add the tool results as a user message for the next turn
                // Store the assistant blocks and tool results in the session for API message building
                _currentAssistantMsg._assistantBlocks = assistantBlocks;
                _currentAssistantMsg._toolResults = toolResults;

                // Add timestamp to this bubble
                var timeLbl = new Label(DateTime.Now.ToString("HH:mm"));
                timeLbl.AddToClassList("message-time");
                _currentAssistantBubble.Add(timeLbl);

                ScrollToBottom();

                // Continue the loop so AI can provide a summary after tool execution
                UpdateStatus("Claude is processing results...");
            }
        }

        private void UpdateBadge(VisualElement badge, CommandExecution cmd)
        {
            badge.ClearClassList();
            badge.AddToClassList("command-badge");
            badge.AddToClassList($"command-badge--{cmd.Status.ToString().ToLower()}");

            // Update dot
            var dot = badge.Q(className: "command-status-dot");
            if (dot != null)
            {
                dot.ClearClassList();
                dot.AddToClassList("command-status-dot");
                dot.AddToClassList($"command-status-dot--{cmd.Status.ToString().ToLower()}");
            }

            // Update status text
            var statusLabels = badge.Query(className: "command-status-text").ToList();
            foreach (var sl in statusLabels)
            {
                var label = sl as Label;
                if (label == null) continue;
                label.ClearClassList();
                label.AddToClassList("command-status-text");

                switch (cmd.Status)
                {
                    case CommandStatus.Pending: label.text = "Pending..."; break;
                    case CommandStatus.Executing: label.text = "Executing..."; break;
                    case CommandStatus.Success:
                        label.text = "Done";
                        label.AddToClassList("command-status-text--success");
                        break;
                    case CommandStatus.Failed:
                        label.text = "Failed";
                        label.AddToClassList("command-status-text--failed");
                        break;
                }
            }

            // Add error text if failed
            if (cmd.Status == CommandStatus.Failed && !string.IsNullOrEmpty(cmd.ErrorMessage))
            {
                var existing = badge.Q(className: "command-error-text");
                if (existing == null)
                {
                    var errorLabel = new Label(cmd.ErrorMessage);
                    errorLabel.AddToClassList("command-error-text");
                    badge.Add(errorLabel);
                }
            }
        }

        private List<ApiMessage> BuildApiMessages()
        {
            var messages = new List<ApiMessage>();
            foreach (var msg in _session.Messages)
            {
                if (msg.Role == "user")
                {
                    messages.Add(ApiMessage.User(msg.TextContent));
                }
                else if (msg.Role == "assistant")
                {
                    if (msg._assistantBlocks != null && msg._assistantBlocks.Count > 0)
                    {
                        // Message with tool_use blocks
                        messages.Add(ApiMessage.Assistant(msg._assistantBlocks));

                        // Add tool results as user message
                        if (msg._toolResults != null && msg._toolResults.Count > 0)
                            messages.Add(ApiMessage.UserToolResults(msg._toolResults));
                    }
                    else
                    {
                        // Plain text assistant message
                        var plainMsg = new ApiMessage { role = "assistant", content = msg.TextContent ?? "" };
                        messages.Add(plainMsg);
                    }
                }
            }
            return messages;
        }

        private void UpdateStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }

        private void CancelCurrentRequest()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            UpdateStatus("Cancelled");
        }

        private class ToolUseInfo
        {
            public string Id;
            public string Name;
            public string InputJson;
        }

        // ── Session Persistence ──

        private void SaveSession()
        {
            try
            {
                var json = JsonUtility.ToJson(_session);
                UnityEditor.SessionState.SetString(SESSION_KEY, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClaudeUnity] Failed to save session: {ex.Message}");
            }
        }

        private ConversationSession LoadSession()
        {
            try
            {
                var json = UnityEditor.SessionState.GetString(SESSION_KEY, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var session = JsonUtility.FromJson<ConversationSession>(json);
                    if (session != null && session.Messages != null && session.Messages.Count > 0)
                        return session;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClaudeUnity] Failed to load session: {ex.Message}");
            }
            return new ConversationSession();
        }
    }
}
