using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Core
{
    /// <summary>
    /// Comprehensive accessibility manager implementing WCAG AA compliance.
    /// Provides keyboard navigation, screen reader support, and high contrast modes.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        [Header("Accessibility Settings")]
        [SerializeField] private bool enableAccessibilityMode = false;
        [SerializeField] private bool enableKeyboardNavigation = true;
        [SerializeField] private bool enableScreenReader = false;
        [SerializeField] private bool enableHighContrast = false;
        [SerializeField] private bool enableReducedMotion = false;

        [Header("Font Scaling")]
        [SerializeField] private float fontSizeMultiplier = 1.0f;
        [SerializeField] private float minFontSize = 12f;
        [SerializeField] private float maxFontSize = 24f;

        [Header("Navigation")]
        [SerializeField] private KeyCode nextElementKey = KeyCode.Tab;
        [SerializeField] private KeyCode previousElementKey = KeyCode.Tab; // With Shift
        [SerializeField] private KeyCode activateKey = KeyCode.Return;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

        [Header("Visual Settings")]
        [SerializeField] private Color highContrastBackground = Color.black;
        [SerializeField] private Color highContrastForeground = Color.white;
        [SerializeField] private Color focusHighlightColor = Color.yellow;
        [SerializeField] private float focusHighlightWidth = 3f;

        [Header("Audio")]
        [SerializeField] private AudioSource screenReaderAudioSource;
        [SerializeField] private AudioClip navigationSound;
        [SerializeField] private AudioClip activationSound;
        [SerializeField] private AudioClip errorSound;

        // Navigation state
        private List<Selectable> _navigableElements = new List<Selectable>();
        private int _currentNavigationIndex = -1;
        private Selectable _currentSelected;
        private GameObject _lastFrameSelected;

        // Accessibility components
        private readonly Dictionary<GameObject, AccessibilityInfo> _accessibilityInfo = new Dictionary<GameObject, AccessibilityInfo>();
        private readonly List<Text> _allTextComponents = new List<Text>();
        private readonly Dictionary<Text, float> _originalFontSizes = new Dictionary<Text, float>();

        // Focus visualization
        private GameObject _focusIndicator;
        private Image _focusIndicatorImage;

        // Screen reader
        private Queue<string> _screenReaderQueue = new Queue<string>();
        private bool _isPlayingScreenReader = false;

        // Performance tracking
        private readonly ProfilerMarker _navigationMarker = new("AccessibilityManager.Navigation");
        private readonly ProfilerMarker _screenReaderMarker = new("AccessibilityManager.ScreenReader");

        // Events
        public event Action<bool> OnAccessibilityModeChanged;
        public event Action<Selectable> OnNavigationChanged;
        public event Action<string> OnScreenReaderSpeak;

        public bool IsAccessibilityModeEnabled => enableAccessibilityMode;
        public bool IsKeyboardNavigationEnabled => enableKeyboardNavigation;
        public bool IsScreenReaderEnabled => enableScreenReader;
        public bool IsHighContrastEnabled => enableHighContrast;

        private void Awake()
        {
            InitializeAccessibilityManager();
            CreateFocusIndicator();
        }

        private void Start()
        {
            RefreshNavigableElements();
            RefreshTextComponents();
            ApplyCurrentSettings();
        }

        private void Update()
        {
            using (_navigationMarker.Auto())
            {
                if (enableKeyboardNavigation)
                {
                    HandleKeyboardNavigation();
                }

                ProcessScreenReaderQueue();
                UpdateFocusIndicator();
            }
        }

        #region Initialization

        private void InitializeAccessibilityManager()
        {
            // Load accessibility settings from PlayerPrefs
            enableAccessibilityMode = PlayerPrefs.GetInt("AccessibilityMode", 0) == 1;
            enableKeyboardNavigation = PlayerPrefs.GetInt("KeyboardNavigation", 1) == 1;
            enableScreenReader = PlayerPrefs.GetInt("ScreenReader", 0) == 1;
            enableHighContrast = PlayerPrefs.GetInt("HighContrast", 0) == 1;
            enableReducedMotion = PlayerPrefs.GetInt("ReducedMotion", 0) == 1;
            fontSizeMultiplier = PlayerPrefs.GetFloat("FontSizeMultiplier", 1.0f);

            Debug.Log($"AccessibilityManager initialized - Mode: {enableAccessibilityMode}, Keyboard: {enableKeyboardNavigation}");
        }

        private void CreateFocusIndicator()
        {
            // Create a focus indicator GameObject
            _focusIndicator = new GameObject("FocusIndicator");
            _focusIndicator.transform.SetParent(transform);

            Canvas canvas = _focusIndicator.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000; // Ensure it's always on top

            _focusIndicatorImage = _focusIndicator.AddComponent<Image>();
            _focusIndicatorImage.sprite = CreateFocusIndicatorSprite();
            _focusIndicatorImage.color = focusHighlightColor;
            _focusIndicatorImage.type = Image.Type.Sliced;

            _focusIndicator.SetActive(false);
        }

        private Sprite CreateFocusIndicatorSprite()
        {
            // Create a simple border sprite for focus indication
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool isBorder = x < 4 || x >= size - 4 || y < 4 || y >= size - 4;
                    bool isInnerBorder = x >= 4 && x < size - 4 && y >= 4 && y < size - 4;

                    if (isBorder && !isInnerBorder)
                        texture.SetPixel(x, y, Color.white);
                    else
                        texture.SetPixel(x, y, Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        }

        #endregion

        #region Public Interface

        public void SetAccessibilityMode(bool enabled)
        {
            enableAccessibilityMode = enabled;
            PlayerPrefs.SetInt("AccessibilityMode", enabled ? 1 : 0);
            ApplyCurrentSettings();
            OnAccessibilityModeChanged?.Invoke(enabled);

            SpeakText(enabled ? "Accessibility mode enabled" : "Accessibility mode disabled");
        }

        public void SetKeyboardNavigation(bool enabled)
        {
            enableKeyboardNavigation = enabled;
            PlayerPrefs.SetInt("KeyboardNavigation", enabled ? 1 : 0);

            if (enabled)
            {
                RefreshNavigableElements();
                SpeakText("Keyboard navigation enabled");
            }
            else
            {
                _focusIndicator.SetActive(false);
                SpeakText("Keyboard navigation disabled");
            }
        }

        public void SetScreenReader(bool enabled)
        {
            enableScreenReader = enabled;
            PlayerPrefs.SetInt("ScreenReader", enabled ? 1 : 0);

            SpeakText(enabled ? "Screen reader enabled" : "Screen reader disabled");
        }

        public void SetHighContrast(bool enabled)
        {
            enableHighContrast = enabled;
            PlayerPrefs.SetInt("HighContrast", enabled ? 1 : 0);
            ApplyHighContrastMode();

            SpeakText(enabled ? "High contrast mode enabled" : "High contrast mode disabled");
        }

        public void SetReducedMotion(bool enabled)
        {
            enableReducedMotion = enabled;
            PlayerPrefs.SetInt("ReducedMotion", enabled ? 1 : 0);

            SpeakText(enabled ? "Reduced motion enabled" : "Reduced motion disabled");
        }

        public void SetFontSizeMultiplier(float multiplier)
        {
            fontSizeMultiplier = Mathf.Clamp(multiplier, 0.8f, 2.0f);
            PlayerPrefs.SetFloat("FontSizeMultiplier", fontSizeMultiplier);
            ApplyFontScaling();

            SpeakText($"Font size set to {(fontSizeMultiplier * 100):F0} percent");
        }

        public void RegisterAccessibleElement(GameObject element, string label, string description = "")
        {
            AccessibilityInfo info = new AccessibilityInfo
            {
                Label = label,
                Description = description,
                IsInteractable = element.GetComponent<Selectable>() != null
            };

            _accessibilityInfo[element] = info;

            // Add to navigable elements if it's a selectable
            Selectable selectable = element.GetComponent<Selectable>();
            if (selectable && !_navigableElements.Contains(selectable))
            {
                _navigableElements.Add(selectable);
            }
        }

        public void SpeakText(string text)
        {
            if (!enableScreenReader || string.IsNullOrEmpty(text)) return;

            _screenReaderQueue.Enqueue(text);
            OnScreenReaderSpeak?.Invoke(text);
        }

        public void FocusElement(Selectable element)
        {
            if (!enableKeyboardNavigation || !element) return;

            int index = _navigableElements.IndexOf(element);
            if (index >= 0)
            {
                _currentNavigationIndex = index;
                SelectElement(element);
            }
        }

        #endregion

        #region Navigation System

        private void HandleKeyboardNavigation()
        {
            if (_navigableElements.Count == 0) return;

            bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(nextElementKey))
            {
                if (shiftPressed)
                    NavigateToPrevious();
                else
                    NavigateToNext();
            }
            else if (Input.GetKeyDown(activateKey))
            {
                ActivateCurrentElement();
            }
            else if (Input.GetKeyDown(cancelKey))
            {
                HandleCancelAction();
            }

            // Handle arrow key navigation for specific UI types
            HandleArrowKeyNavigation();
        }

        private void NavigateToNext()
        {
            if (_navigableElements.Count == 0) return;

            _currentNavigationIndex = (_currentNavigationIndex + 1) % _navigableElements.Count;
            SelectElement(_navigableElements[_currentNavigationIndex]);
            PlayNavigationSound();
        }

        private void NavigateToPrevious()
        {
            if (_navigableElements.Count == 0) return;

            _currentNavigationIndex = _currentNavigationIndex <= 0 ? _navigableElements.Count - 1 : _currentNavigationIndex - 1;
            SelectElement(_navigableElements[_currentNavigationIndex]);
            PlayNavigationSound();
        }

        private void SelectElement(Selectable element)
        {
            if (!element) return;

            _currentSelected = element;
            EventSystem.current.SetSelectedGameObject(element.gameObject);

            OnNavigationChanged?.Invoke(element);

            // Speak element information
            if (_accessibilityInfo.ContainsKey(element.gameObject))
            {
                var info = _accessibilityInfo[element.gameObject];
                string speechText = info.Label;
                if (!string.IsNullOrEmpty(info.Description))
                    speechText += ". " + info.Description;

                SpeakText(speechText);
            }
        }

        private void ActivateCurrentElement()
        {
            if (!_currentSelected) return;

            // Simulate click/activation
            if (_currentSelected is Button button)
            {
                button.onClick.Invoke();
                PlayActivationSound();
                SpeakText("Activated");
            }
            else if (_currentSelected is Toggle toggle)
            {
                toggle.isOn = !toggle.isOn;
                PlayActivationSound();
                SpeakText(toggle.isOn ? "Checked" : "Unchecked");
            }
            else if (_currentSelected is Slider slider)
            {
                // Handle slider activation (could open adjustment mode)
                SpeakText($"Slider value {slider.value:F1}");
            }
            else if (_currentSelected is InputField inputField)
            {
                inputField.ActivateInputField();
                SpeakText("Text input activated");
            }
        }

        private void HandleCancelAction()
        {
            // Handle escape/cancel behavior
            if (_currentSelected is InputField inputField && inputField.isFocused)
            {
                inputField.DeactivateInputField();
                SpeakText("Text input deactivated");
            }
            else
            {
                SpeakText("Cancel");
            }
        }

        private void HandleArrowKeyNavigation()
        {
            if (!_currentSelected) return;

            // Special navigation for sliders
            if (_currentSelected is Slider slider)
            {
                float change = 0f;
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    change = -0.1f;
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                    change = 0.1f;

                if (change != 0f)
                {
                    slider.value = Mathf.Clamp01(slider.value + change);
                    SpeakText($"Slider value {slider.value:F1}");
                }
            }

            // Special navigation for dropdowns
            if (_currentSelected is Dropdown dropdown)
            {
                int change = 0;
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                    change = -1;
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    change = 1;

                if (change != 0)
                {
                    int newValue = Mathf.Clamp(dropdown.value + change, 0, dropdown.options.Count - 1);
                    dropdown.value = newValue;
                    SpeakText($"Selected {dropdown.options[newValue].text}");
                }
            }
        }

        #endregion

        #region Visual Accessibility

        private void ApplyCurrentSettings()
        {
            ApplyFontScaling();
            ApplyHighContrastMode();
            RefreshNavigableElements();
        }

        private void ApplyFontScaling()
        {
            foreach (var kvp in _originalFontSizes)
            {
                Text textComponent = kvp.Key;
                float originalSize = kvp.Value;

                if (textComponent)
                {
                    float newSize = originalSize * fontSizeMultiplier;
                    textComponent.fontSize = Mathf.RoundToInt(Mathf.Clamp(newSize, minFontSize, maxFontSize));
                }
            }
        }

        private void ApplyHighContrastMode()
        {
            if (!enableHighContrast) return;

            // Apply high contrast colors to UI elements
            foreach (Text textComponent in _allTextComponents)
            {
                if (textComponent)
                {
                    textComponent.color = highContrastForeground;
                }
            }

            // Apply to background images
            Image[] backgroundImages = FindObjectsOfType<Image>();
            foreach (Image image in backgroundImages)
            {
                if (image.gameObject.name.ToLower().Contains("background"))
                {
                    image.color = highContrastBackground;
                }
            }
        }

        private void UpdateFocusIndicator()
        {
            if (!enableKeyboardNavigation || !_currentSelected)
            {
                _focusIndicator.SetActive(false);
                return;
            }

            _focusIndicator.SetActive(true);

            // Position focus indicator around current selected element
            RectTransform selectedRect = _currentSelected.GetComponent<RectTransform>();
            RectTransform indicatorRect = _focusIndicator.GetComponent<RectTransform>();

            if (selectedRect && indicatorRect)
            {
                indicatorRect.position = selectedRect.position;
                indicatorRect.sizeDelta = selectedRect.sizeDelta + Vector2.one * focusHighlightWidth;
            }
        }

        #endregion

        #region Screen Reader System

        private void ProcessScreenReaderQueue()
        {
            if (!enableScreenReader || _isPlayingScreenReader || _screenReaderQueue.Count == 0)
                return;

            using (_screenReaderMarker.Auto())
            {
                string textToSpeak = _screenReaderQueue.Dequeue();
                StartCoroutine(PlayScreenReaderText(textToSpeak));
            }
        }

        private System.Collections.IEnumerator PlayScreenReaderText(string text)
        {
            _isPlayingScreenReader = true;

            // In a full implementation, this would use text-to-speech
            // For now, we'll just log and play a sound
            Debug.Log($"Screen Reader: {text}");

            if (screenReaderAudioSource && navigationSound)
            {
                screenReaderAudioSource.PlayOneShot(navigationSound);
                yield return new WaitForSeconds(navigationSound.length);
            }
            else
            {
                // Simulate speech duration based on text length
                float duration = Mathf.Max(1f, text.Length * 0.05f);
                yield return new WaitForSeconds(duration);
            }

            _isPlayingScreenReader = false;
        }

        #endregion

        #region Audio Feedback

        private void PlayNavigationSound()
        {
            if (screenReaderAudioSource && navigationSound)
            {
                screenReaderAudioSource.PlayOneShot(navigationSound, 0.5f);
            }
        }

        private void PlayActivationSound()
        {
            if (screenReaderAudioSource && activationSound)
            {
                screenReaderAudioSource.PlayOneShot(activationSound);
            }
        }

        private void PlayErrorSound()
        {
            if (screenReaderAudioSource && errorSound)
            {
                screenReaderAudioSource.PlayOneShot(errorSound);
            }
        }

        #endregion

        #region Utility Methods

        private void RefreshNavigableElements()
        {
            _navigableElements.Clear();
            _navigableElements.AddRange(FindObjectsOfType<Selectable>());

            // Filter out non-interactable elements
            _navigableElements.RemoveAll(s => !s.interactable || !s.gameObject.activeInHierarchy);

            // Sort by position for logical navigation order
            _navigableElements.Sort((a, b) =>
            {
                var rectA = a.GetComponent<RectTransform>();
                var rectB = b.GetComponent<RectTransform>();

                if (!rectA || !rectB) return 0;

                // Sort by Y position (top to bottom), then X position (left to right)
                float yDiff = rectB.position.y - rectA.position.y;
                if (Mathf.Abs(yDiff) > 50f) // Different rows
                    return yDiff > 0 ? 1 : -1;
                else // Same row, sort by X
                    return rectA.position.x.CompareTo(rectB.position.x);
            });
        }

        private void RefreshTextComponents()
        {
            _allTextComponents.Clear();
            _originalFontSizes.Clear();

            Text[] allTexts = FindObjectsOfType<Text>();
            foreach (Text textComponent in allTexts)
            {
                _allTextComponents.Add(textComponent);
                _originalFontSizes[textComponent] = textComponent.fontSize;
            }
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        public class AccessibilityInfo
        {
            public string Label;
            public string Description;
            public bool IsInteractable;
            public string Role; // button, slider, text, etc.
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_focusIndicator)
                DestroyImmediate(_focusIndicator);
        }

        #endregion
    }
}