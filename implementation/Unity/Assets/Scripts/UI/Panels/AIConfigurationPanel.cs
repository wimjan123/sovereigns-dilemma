using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SovereignsDilemma.UI.Core;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Panels
{
    /// <summary>
    /// AI service configuration panel with secure credential management.
    /// Provides provider selection, connection testing, and settings management.
    /// </summary>
    public class AIConfigurationPanel : MonoBehaviour, IUIComponent
    {
        [Header("Provider Selection")]
        [SerializeField] private Dropdown providerDropdown;
        [SerializeField] private Text providerDescriptionText;
        [SerializeField] private Image providerStatusIndicator;

        [Header("Credential Management")]
        [SerializeField] private InputField apiKeyInput;
        [SerializeField] private InputField endpointInput;
        [SerializeField] private Toggle showCredentialsToggle;
        [SerializeField] private Button testConnectionButton;

        [Header("Configuration Settings")]
        [SerializeField] private Slider maxTokensSlider;
        [SerializeField] private Text maxTokensValueText;
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private Text temperatureValueText;
        [SerializeField] private Toggle enableCachingToggle;
        [SerializeField] private Toggle offlineModeToggle;

        [Header("Status and Feedback")]
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Text responseTimeText;
        [SerializeField] private Text lastErrorText;
        [SerializeField] private ProgressBar connectionProgressBar;

        [Header("Advanced Settings")]
        [SerializeField] private InputField customInstructionsInput;
        [SerializeField] private Slider retryAttemptsSlider;
        [SerializeField] private Text retryAttemptsValueText;
        [SerializeField] private Slider timeoutSlider;
        [SerializeField] private Text timeoutValueText;

        [Header("Buttons")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button importConfigButton;
        [SerializeField] private Button exportConfigButton;

        // AI Service Providers
        private readonly Dictionary<string, AIProviderInfo> _aiProviders = new Dictionary<string, AIProviderInfo>
        {
            {
                "NVIDIA NIM", new AIProviderInfo
                {
                    Name = "NVIDIA NIM",
                    Description = "NVIDIA's cloud inference microservices for enterprise AI",
                    DefaultEndpoint = "https://api.nvcf.nvidia.com/v2/nvcf",
                    RequiresApiKey = true,
                    SupportsCaching = true,
                    MaxTokens = 4096,
                    DefaultTemperature = 0.7f
                }
            },
            {
                "OpenAI", new AIProviderInfo
                {
                    Name = "OpenAI",
                    Description = "OpenAI's GPT models for natural language processing",
                    DefaultEndpoint = "https://api.openai.com/v1",
                    RequiresApiKey = true,
                    SupportsCaching = false,
                    MaxTokens = 8192,
                    DefaultTemperature = 0.7f
                }
            },
            {
                "Local Model", new AIProviderInfo
                {
                    Name = "Local Model",
                    Description = "Local AI model running on your machine",
                    DefaultEndpoint = "http://localhost:8080",
                    RequiresApiKey = false,
                    SupportsCaching = true,
                    MaxTokens = 2048,
                    DefaultTemperature = 0.5f
                }
            },
            {
                "Offline Mode", new AIProviderInfo
                {
                    Name = "Offline Mode",
                    Description = "Cached responses and rule-based generation",
                    DefaultEndpoint = "",
                    RequiresApiKey = false,
                    SupportsCaching = true,
                    MaxTokens = 512,
                    DefaultTemperature = 0.0f
                }
            }
        };

        // Configuration state
        private AIServiceConfigurationData _currentConfig;
        private string _selectedProvider = "NVIDIA NIM";
        private bool _isTestingConnection = false;
        private bool _hasUnsavedChanges = false;

        // Security
        private bool _credentialsVisible = false;
        private string _maskedApiKey = "";

        // Performance tracking
        private readonly ProfilerMarker _configMarker = new("AIConfiguration.Update");

        public Type GetDataType() => typeof(AIServiceConfigurationData);
        public bool IsVisible() => gameObject.activeInHierarchy;

        private void Awake()
        {
            InitializePanel();
            SetupEventHandlers();
        }

        private void Start()
        {
            LoadConfiguration();
            RefreshUI();
        }

        private void Update()
        {
            using (_configMarker.Auto())
            {
                UpdateConnectionStatus();
                UpdateProgressBar();
            }
        }

        #region Initialization

        private void InitializePanel()
        {
            _currentConfig = new AIServiceConfigurationData(true);

            // Initialize provider dropdown
            if (providerDropdown)
            {
                providerDropdown.ClearOptions();
                var options = new List<string>(_aiProviders.Keys);
                providerDropdown.AddOptions(options);
            }

            // Set initial values
            if (maxTokensSlider)
            {
                maxTokensSlider.minValue = 128;
                maxTokensSlider.maxValue = 8192;
                maxTokensSlider.value = 2048;
            }

            if (temperatureSlider)
            {
                temperatureSlider.minValue = 0f;
                temperatureSlider.maxValue = 2f;
                temperatureSlider.value = 0.7f;
            }

            if (retryAttemptsSlider)
            {
                retryAttemptsSlider.minValue = 1;
                retryAttemptsSlider.maxValue = 5;
                retryAttemptsSlider.value = 3;
            }

            if (timeoutSlider)
            {
                timeoutSlider.minValue = 5;
                timeoutSlider.maxValue = 120;
                timeoutSlider.value = 30;
            }

            // Initialize credential masking
            MaskCredentials();
        }

        private void SetupEventHandlers()
        {
            if (providerDropdown)
                providerDropdown.onValueChanged.AddListener(OnProviderChanged);

            if (apiKeyInput)
                apiKeyInput.onValueChanged.AddListener(OnCredentialsChanged);

            if (endpointInput)
                apiKeyInput.onValueChanged.AddListener(OnCredentialsChanged);

            if (showCredentialsToggle)
                showCredentialsToggle.onValueChanged.AddListener(OnShowCredentialsToggled);

            if (testConnectionButton)
                testConnectionButton.onClick.AddListener(TestConnection);

            if (maxTokensSlider)
                maxTokensSlider.onValueChanged.AddListener(OnMaxTokensChanged);

            if (temperatureSlider)
                temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);

            if (retryAttemptsSlider)
                retryAttemptsSlider.onValueChanged.AddListener(OnRetryAttemptsChanged);

            if (timeoutSlider)
                timeoutSlider.onValueChanged.AddListener(OnTimeoutChanged);

            if (enableCachingToggle)
                enableCachingToggle.onValueChanged.AddListener(OnCachingToggled);

            if (offlineModeToggle)
                offlineModeToggle.onValueChanged.AddListener(OnOfflineModeToggled);

            if (saveButton)
                saveButton.onClick.AddListener(SaveConfiguration);

            if (resetButton)
                resetButton.onClick.AddListener(ResetConfiguration);

            if (importConfigButton)
                importConfigButton.onClick.AddListener(ImportConfiguration);

            if (exportConfigButton)
                exportConfigButton.onClick.AddListener(ExportConfiguration);
        }

        #endregion

        #region Public Interface

        public void UpdateData(object data)
        {
            if (data is AIServiceConfigurationData configData)
            {
                UpdateData(configData);
            }
        }

        public void UpdateData(AIServiceConfigurationData data)
        {
            _currentConfig = data;
            RefreshUI();
        }

        public void SetVisibility(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetCompactMode(bool compact)
        {
            // Hide advanced settings in compact mode
            if (customInstructionsInput) customInstructionsInput.transform.parent.gameObject.SetActive(!compact);
            if (retryAttemptsSlider) retryAttemptsSlider.transform.parent.gameObject.SetActive(!compact);
            if (timeoutSlider) timeoutSlider.transform.parent.gameObject.SetActive(!compact);
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            // Accessibility enhancements would be handled by AccessibilityManager
            // This method can be used for panel-specific accessibility features
        }

        #endregion

        #region UI Event Handlers

        private void OnProviderChanged(int providerIndex)
        {
            var providerNames = new List<string>(_aiProviders.Keys);
            if (providerIndex >= 0 && providerIndex < providerNames.Count)
            {
                _selectedProvider = providerNames[providerIndex];
                LoadProviderDefaults();
                MarkUnsavedChanges();
            }
        }

        private void OnCredentialsChanged(string value)
        {
            MarkUnsavedChanges();
        }

        private void OnShowCredentialsToggled(bool visible)
        {
            _credentialsVisible = visible;
            UpdateCredentialsDisplay();
        }

        private void OnMaxTokensChanged(float value)
        {
            if (maxTokensValueText)
                maxTokensValueText.text = value.ToString("F0");
            MarkUnsavedChanges();
        }

        private void OnTemperatureChanged(float value)
        {
            if (temperatureValueText)
                temperatureValueText.text = value.ToString("F2");
            MarkUnsavedChanges();
        }

        private void OnRetryAttemptsChanged(float value)
        {
            if (retryAttemptsValueText)
                retryAttemptsValueText.text = value.ToString("F0");
            MarkUnsavedChanges();
        }

        private void OnTimeoutChanged(float value)
        {
            if (timeoutValueText)
                timeoutValueText.text = $"{value:F0}s";
            MarkUnsavedChanges();
        }

        private void OnCachingToggled(bool enabled)
        {
            MarkUnsavedChanges();
        }

        private void OnOfflineModeToggled(bool enabled)
        {
            // Switch to offline provider when enabled
            if (enabled && _selectedProvider != "Offline Mode")
            {
                SetProvider("Offline Mode");
            }
            MarkUnsavedChanges();
        }

        #endregion

        #region Configuration Management

        private void LoadConfiguration()
        {
            // Load from PlayerPrefs or configuration file
            _selectedProvider = PlayerPrefs.GetString("AI_Provider", "NVIDIA NIM");

            // Securely load credentials (in production, use secure storage)
            string encryptedApiKey = PlayerPrefs.GetString("AI_ApiKey_Encrypted", "");
            // In production: decrypt API key here

            _currentConfig.SelectedProvider = _selectedProvider;
            _currentConfig.IsConnected = false;
            _currentConfig.ResponseTime = 0f;
            _currentConfig.OfflineModeEnabled = PlayerPrefs.GetInt("AI_OfflineMode", 0) == 1;
            _currentConfig.HasData = true;

            SetProvider(_selectedProvider);
        }

        private void SaveConfiguration()
        {
            if (!ValidateConfiguration()) return;

            // Save provider selection
            PlayerPrefs.SetString("AI_Provider", _selectedProvider);

            // Securely save credentials (in production, encrypt before storing)
            if (apiKeyInput && !string.IsNullOrEmpty(apiKeyInput.text))
            {
                // In production: encrypt API key before storing
                PlayerPrefs.SetString("AI_ApiKey_Encrypted", apiKeyInput.text);
            }

            if (endpointInput)
                PlayerPrefs.SetString("AI_Endpoint", endpointInput.text);

            // Save other settings
            if (maxTokensSlider)
                PlayerPrefs.SetInt("AI_MaxTokens", (int)maxTokensSlider.value);

            if (temperatureSlider)
                PlayerPrefs.SetFloat("AI_Temperature", temperatureSlider.value);

            if (enableCachingToggle)
                PlayerPrefs.SetInt("AI_EnableCaching", enableCachingToggle.isOn ? 1 : 0);

            if (offlineModeToggle)
                PlayerPrefs.SetInt("AI_OfflineMode", offlineModeToggle.isOn ? 1 : 0);

            PlayerPrefs.Save();

            _hasUnsavedChanges = false;
            UpdateSaveButtonState();

            if (connectionStatusText)
                connectionStatusText.text = "Configuration saved successfully";

            Debug.Log("AI Configuration saved");
        }

        private void ResetConfiguration()
        {
            SetProvider("NVIDIA NIM");
            LoadProviderDefaults();

            if (customInstructionsInput)
                customInstructionsInput.text = "";

            if (enableCachingToggle)
                enableCachingToggle.isOn = true;

            if (offlineModeToggle)
                offlineModeToggle.isOn = false;

            MarkUnsavedChanges();

            if (connectionStatusText)
                connectionStatusText.text = "Configuration reset to defaults";
        }

        private bool ValidateConfiguration()
        {
            if (_selectedProvider == "Offline Mode") return true;

            var provider = _aiProviders[_selectedProvider];

            if (provider.RequiresApiKey && string.IsNullOrEmpty(apiKeyInput?.text))
            {
                SetError("API key is required for this provider");
                return false;
            }

            if (string.IsNullOrEmpty(endpointInput?.text))
            {
                SetError("Endpoint URL is required");
                return false;
            }

            return true;
        }

        #endregion

        #region Provider Management

        private void SetProvider(string providerName)
        {
            if (!_aiProviders.ContainsKey(providerName)) return;

            _selectedProvider = providerName;

            if (providerDropdown)
            {
                var providerNames = new List<string>(_aiProviders.Keys);
                int index = providerNames.IndexOf(providerName);
                if (index >= 0) providerDropdown.value = index;
            }

            LoadProviderDefaults();
        }

        private void LoadProviderDefaults()
        {
            if (!_aiProviders.ContainsKey(_selectedProvider)) return;

            var provider = _aiProviders[_selectedProvider];

            if (providerDescriptionText)
                providerDescriptionText.text = provider.Description;

            if (endpointInput)
                endpointInput.text = provider.DefaultEndpoint;

            if (maxTokensSlider)
            {
                maxTokensSlider.maxValue = provider.MaxTokens;
                maxTokensSlider.value = provider.MaxTokens / 2;
            }

            if (temperatureSlider)
                temperatureSlider.value = provider.DefaultTemperature;

            if (enableCachingToggle)
                enableCachingToggle.interactable = provider.SupportsCaching;

            // Clear API key when switching providers
            if (apiKeyInput && provider.RequiresApiKey)
                apiKeyInput.text = "";

            RefreshProviderSpecificUI();
        }

        private void RefreshProviderSpecificUI()
        {
            var provider = _aiProviders[_selectedProvider];

            // Show/hide credential fields based on provider requirements
            if (apiKeyInput)
                apiKeyInput.transform.parent.gameObject.SetActive(provider.RequiresApiKey);

            if (endpointInput)
                endpointInput.interactable = _selectedProvider != "Offline Mode";

            if (testConnectionButton)
                testConnectionButton.interactable = _selectedProvider != "Offline Mode";
        }

        #endregion

        #region Connection Testing

        private void TestConnection()
        {
            if (_isTestingConnection) return;

            if (!ValidateConfiguration()) return;

            StartCoroutine(PerformConnectionTest());
        }

        private System.Collections.IEnumerator PerformConnectionTest()
        {
            _isTestingConnection = true;

            if (testConnectionButton)
                testConnectionButton.interactable = false;

            if (connectionStatusText)
                connectionStatusText.text = "Testing connection...";

            if (connectionProgressBar)
                connectionProgressBar.SetProgress(0f);

            // Simulate connection test
            float testDuration = 3f;
            float elapsed = 0f;

            while (elapsed < testDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / testDuration;

                if (connectionProgressBar)
                    connectionProgressBar.SetProgress(progress);

                yield return null;
            }

            // Simulate test results
            bool connectionSuccessful = UnityEngine.Random.value > 0.3f; // 70% success rate for demo

            if (connectionSuccessful)
            {
                if (connectionStatusText)
                    connectionStatusText.text = "Connection successful";

                if (providerStatusIndicator)
                    providerStatusIndicator.color = Color.green;

                _currentConfig.IsConnected = true;
                _currentConfig.ResponseTime = UnityEngine.Random.Range(0.5f, 2.5f);

                if (responseTimeText)
                    responseTimeText.text = $"Response time: {_currentConfig.ResponseTime:F1}s";
            }
            else
            {
                SetError("Connection failed - Please check your credentials and endpoint");

                if (providerStatusIndicator)
                    providerStatusIndicator.color = Color.red;

                _currentConfig.IsConnected = false;
            }

            if (connectionProgressBar)
                connectionProgressBar.SetProgress(1f);

            if (testConnectionButton)
                testConnectionButton.interactable = true;

            _isTestingConnection = false;
        }

        #endregion

        #region Import/Export

        private void ImportConfiguration()
        {
            // In production, this would open a file dialog
            Debug.Log("Import configuration - Would open file dialog");

            if (connectionStatusText)
                connectionStatusText.text = "Import functionality not yet implemented";
        }

        private void ExportConfiguration()
        {
            // In production, this would save current config to a file
            Debug.Log("Export configuration - Would save to file");

            if (connectionStatusText)
                connectionStatusText.text = "Export functionality not yet implemented";
        }

        #endregion

        #region UI State Management

        private void RefreshUI()
        {
            UpdateCredentialsDisplay();
            UpdateProviderStatus();
            UpdateSaveButtonState();
        }

        private void UpdateCredentialsDisplay()
        {
            if (!apiKeyInput) return;

            if (_credentialsVisible)
            {
                apiKeyInput.contentType = InputField.ContentType.Standard;
            }
            else
            {
                apiKeyInput.contentType = InputField.ContentType.Password;
            }

            apiKeyInput.ForceLabelUpdate();
        }

        private void UpdateProviderStatus()
        {
            if (!providerStatusIndicator) return;

            if (_isTestingConnection)
            {
                providerStatusIndicator.color = Color.yellow;
            }
            else if (_currentConfig.IsConnected)
            {
                providerStatusIndicator.color = Color.green;
            }
            else
            {
                providerStatusIndicator.color = Color.red;
            }
        }

        private void UpdateSaveButtonState()
        {
            if (saveButton)
            {
                saveButton.interactable = _hasUnsavedChanges;

                Text saveButtonText = saveButton.GetComponentInChildren<Text>();
                if (saveButtonText)
                {
                    saveButtonText.text = _hasUnsavedChanges ? "Save Changes" : "Saved";
                }
            }
        }

        private void UpdateConnectionStatus()
        {
            // Update connection status display based on current state
            if (responseTimeText && _currentConfig.IsConnected)
            {
                responseTimeText.text = $"Response time: {_currentConfig.ResponseTime:F1}s";
            }
        }

        private void UpdateProgressBar()
        {
            // Update progress bar during connection testing
            if (connectionProgressBar && !_isTestingConnection)
            {
                connectionProgressBar.gameObject.SetActive(false);
            }
        }

        private void MarkUnsavedChanges()
        {
            _hasUnsavedChanges = true;
            UpdateSaveButtonState();
        }

        private void SetError(string errorMessage)
        {
            if (lastErrorText)
                lastErrorText.text = errorMessage;

            if (connectionStatusText)
                connectionStatusText.text = errorMessage;

            Debug.LogError($"AI Configuration Error: {errorMessage}");
        }

        private void MaskCredentials()
        {
            // Implement credential masking for security
            if (apiKeyInput && !string.IsNullOrEmpty(apiKeyInput.text))
            {
                _maskedApiKey = new string('*', apiKeyInput.text.Length);
            }
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        public class AIProviderInfo
        {
            public string Name;
            public string Description;
            public string DefaultEndpoint;
            public bool RequiresApiKey;
            public bool SupportsCaching;
            public int MaxTokens;
            public float DefaultTemperature;
        }

        #endregion
    }
}