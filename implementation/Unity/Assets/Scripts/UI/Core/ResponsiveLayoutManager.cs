using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Core
{
    /// <summary>
    /// Responsive layout manager for adaptive UI design across different screen sizes and resolutions.
    /// Implements fluid layouts that adjust to various display configurations and accessibility needs.
    /// </summary>
    public class ResponsiveLayoutManager : MonoBehaviour
    {
        [Header("Layout Configuration")]
        [SerializeField] private UILayoutMode currentLayoutMode = UILayoutMode.Standard;
        [SerializeField] private bool autoDetectLayoutMode = true;
        [SerializeField] private float layoutTransitionDuration = 0.3f;

        [Header("Breakpoints")]
        [SerializeField] private int mobileBreakpoint = 768;
        [SerializeField] private int tabletBreakpoint = 1024;
        [SerializeField] private int desktopBreakpoint = 1920;
        [SerializeField] private float ultrawideRatio = 2.1f;

        [Header("Layout Presets")]
        [SerializeField] private LayoutPreset mobileLayout;
        [SerializeField] private LayoutPreset portraitLayout;
        [SerializeField] private LayoutPreset standardLayout;
        [SerializeField] private LayoutPreset ultrawideLayout;

        [Header("Responsive Panels")]
        [SerializeField] private List<ResponsivePanel> responsivePanels;

        [Header("Accessibility")]
        [SerializeField] private bool enableAccessibilityMode = false;
        [SerializeField] private float accessibilityScaleMultiplier = 1.2f;
        [SerializeField] private bool enableHighContrastMode = false;

        // Layout management
        private readonly ProfilerMarker _layoutUpdateMarker = new("ResponsiveLayout.Update");
        private Vector2 _lastScreenSize;
        private UILayoutMode _lastLayoutMode;
        private bool _isTransitioning;

        // Animation and transitions
        private readonly List<LayoutTransition> _activeTransitions = new List<LayoutTransition>();
        private readonly Dictionary<RectTransform, LayoutState> _originalStates = new Dictionary<RectTransform, LayoutState>();

        // Performance optimization
        private float _lastLayoutCheck;
        private const float LAYOUT_CHECK_INTERVAL = 0.1f; // Check layout changes 10 times per second

        private void Awake()
        {
            InitializeLayoutSystem();
        }

        private void Start()
        {
            CacheOriginalStates();
            DetectAndApplyInitialLayout();
        }

        private void Update()
        {
            using (_layoutUpdateMarker.Auto())
            {
                if (Time.time - _lastLayoutCheck >= LAYOUT_CHECK_INTERVAL)
                {
                    CheckForLayoutChanges();
                    _lastLayoutCheck = Time.time;
                }

                UpdateActiveTransitions();
            }
        }

        #region Initialization

        private void InitializeLayoutSystem()
        {
            _lastScreenSize = new Vector2(Screen.width, Screen.height);
            _lastLayoutMode = currentLayoutMode;

            // Initialize responsive panels if not assigned
            if (responsivePanels == null || responsivePanels.Count == 0)
            {
                AutoDiscoverResponsivePanels();
            }

            // Initialize layout presets if not configured
            InitializeDefaultLayoutPresets();

            Debug.Log($"ResponsiveLayoutManager initialized with {responsivePanels.Count} panels");
        }

        private void AutoDiscoverResponsivePanels()
        {
            responsivePanels = new List<ResponsivePanel>();

            // Find all UI panels that should be responsive
            var panels = FindObjectsOfType<Canvas>();
            foreach (var panel in panels)
            {
                if (panel.renderMode == RenderMode.ScreenSpaceOverlay ||
                    panel.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    var responsivePanel = panel.GetComponent<ResponsivePanel>();
                    if (responsivePanel == null)
                    {
                        responsivePanel = panel.gameObject.AddComponent<ResponsivePanel>();
                    }
                    responsivePanels.Add(responsivePanel);
                }
            }
        }

        private void InitializeDefaultLayoutPresets()
        {
            if (mobileLayout == null) mobileLayout = CreateDefaultMobileLayout();
            if (portraitLayout == null) portraitLayout = CreateDefaultPortraitLayout();
            if (standardLayout == null) standardLayout = CreateDefaultStandardLayout();
            if (ultrawideLayout == null) ultrawideLayout = CreateDefaultUltrawideLayout();
        }

        private void CacheOriginalStates()
        {
            foreach (var panel in responsivePanels)
            {
                if (panel.transform is RectTransform rectTransform)
                {
                    _originalStates[rectTransform] = new LayoutState
                    {
                        AnchoredPosition = rectTransform.anchoredPosition,
                        SizeDelta = rectTransform.sizeDelta,
                        AnchorMin = rectTransform.anchorMin,
                        AnchorMax = rectTransform.anchorMax,
                        Scale = rectTransform.localScale
                    };
                }
            }
        }

        #endregion

        #region Layout Detection and Application

        private void CheckForLayoutChanges()
        {
            if (!autoDetectLayoutMode) return;

            var currentScreenSize = new Vector2(Screen.width, Screen.height);
            var newLayoutMode = DetermineOptimalLayoutMode(currentScreenSize);

            if (newLayoutMode != currentLayoutMode ||
                Vector2.Distance(currentScreenSize, _lastScreenSize) > 10f)
            {
                ApplyLayoutMode(newLayoutMode);
                _lastScreenSize = currentScreenSize;
            }
        }

        private UILayoutMode DetermineOptimalLayoutMode(Vector2 screenSize)
        {
            var width = screenSize.x;
            var height = screenSize.y;
            var aspectRatio = width / height;

            // Check for mobile
            if (width <= mobileBreakpoint)
                return UILayoutMode.Mobile;

            // Check for portrait orientation
            if (aspectRatio < 1.2f)
                return UILayoutMode.Portrait;

            // Check for ultrawide
            if (aspectRatio >= ultrawideRatio)
                return UILayoutMode.Ultrawide;

            // Default to standard
            return UILayoutMode.Standard;
        }

        private void DetectAndApplyInitialLayout()
        {
            if (autoDetectLayoutMode)
            {
                currentLayoutMode = DetermineOptimalLayoutMode(_lastScreenSize);
            }

            ApplyLayoutMode(currentLayoutMode, false); // No animation for initial layout
        }

        public void SetLayoutMode(UILayoutMode layoutMode, bool animated = true)
        {
            autoDetectLayoutMode = false; // Disable auto-detection when manually set
            ApplyLayoutMode(layoutMode, animated);
        }

        private void ApplyLayoutMode(UILayoutMode layoutMode, bool animated = true)
        {
            if (_isTransitioning && animated) return; // Prevent overlapping transitions

            var previousMode = currentLayoutMode;
            currentLayoutMode = layoutMode;

            var layoutPreset = GetLayoutPreset(layoutMode);
            if (layoutPreset == null)
            {
                Debug.LogWarning($"No layout preset found for {layoutMode}");
                return;
            }

            if (animated && layoutTransitionDuration > 0f)
            {
                StartLayoutTransition(layoutPreset, previousMode, layoutMode);
            }
            else
            {
                ApplyLayoutImmediately(layoutPreset);
            }

            // Notify panels of layout change
            NotifyPanelsOfLayoutChange(layoutMode);

            Debug.Log($"Applied layout mode: {layoutMode}");
        }

        #endregion

        #region Layout Presets

        private LayoutPreset GetLayoutPreset(UILayoutMode layoutMode)
        {
            return layoutMode switch
            {
                UILayoutMode.Mobile => mobileLayout,
                UILayoutMode.Portrait => portraitLayout,
                UILayoutMode.Standard => standardLayout,
                UILayoutMode.Ultrawide => ultrawideLayout,
                _ => standardLayout
            };
        }

        private LayoutPreset CreateDefaultMobileLayout()
        {
            return new LayoutPreset
            {
                Name = "Mobile",
                PanelConfigurations = new List<PanelConfiguration>
                {
                    new PanelConfiguration
                    {
                        PanelName = "VoterAnalytics",
                        Position = new Vector2(0, 0.75f),
                        Size = new Vector2(1f, 0.25f),
                        IsVisible = true,
                        IsCompact = true
                    },
                    new PanelConfiguration
                    {
                        PanelName = "PoliticalSpectrum",
                        Position = new Vector2(0, 0.5f),
                        Size = new Vector2(1f, 0.25f),
                        IsVisible = true,
                        IsCompact = true
                    },
                    new PanelConfiguration
                    {
                        PanelName = "SocialMedia",
                        Position = new Vector2(0, 0),
                        Size = new Vector2(1f, 0.5f),
                        IsVisible = false // Hidden on mobile
                    }
                },
                ScaleModifier = 1.1f,
                FontSizeModifier = 1.2f
            };
        }

        private LayoutPreset CreateDefaultPortraitLayout()
        {
            return new LayoutPreset
            {
                Name = "Portrait",
                PanelConfigurations = new List<PanelConfiguration>
                {
                    new PanelConfiguration
                    {
                        PanelName = "VoterAnalytics",
                        Position = new Vector2(0, 0.7f),
                        Size = new Vector2(1f, 0.3f),
                        IsVisible = true,
                        IsCompact = false
                    },
                    new PanelConfiguration
                    {
                        PanelName = "PoliticalSpectrum",
                        Position = new Vector2(0, 0.35f),
                        Size = new Vector2(1f, 0.35f),
                        IsVisible = true,
                        IsCompact = true
                    },
                    new PanelConfiguration
                    {
                        PanelName = "SocialMedia",
                        Position = new Vector2(0, 0),
                        Size = new Vector2(1f, 0.35f),
                        IsVisible = true
                    }
                },
                ScaleModifier = 1.0f,
                FontSizeModifier = 1.0f
            };
        }

        private LayoutPreset CreateDefaultStandardLayout()
        {
            return new LayoutPreset
            {
                Name = "Standard",
                PanelConfigurations = new List<PanelConfiguration>
                {
                    new PanelConfiguration
                    {
                        PanelName = "VoterAnalytics",
                        Position = new Vector2(0, 0.5f),
                        Size = new Vector2(0.5f, 0.5f),
                        IsVisible = true,
                        IsCompact = false
                    },
                    new PanelConfiguration
                    {
                        PanelName = "PoliticalSpectrum",
                        Position = new Vector2(0.5f, 0.5f),
                        Size = new Vector2(0.5f, 0.5f),
                        IsVisible = true,
                        IsCompact = false
                    },
                    new PanelConfiguration
                    {
                        PanelName = "SocialMedia",
                        Position = new Vector2(0, 0),
                        Size = new Vector2(1f, 0.5f),
                        IsVisible = true
                    }
                },
                ScaleModifier = 1.0f,
                FontSizeModifier = 1.0f
            };
        }

        private LayoutPreset CreateDefaultUltrawideLayout()
        {
            return new LayoutPreset
            {
                Name = "Ultrawide",
                PanelConfigurations = new List<PanelConfiguration>
                {
                    new PanelConfiguration
                    {
                        PanelName = "VoterAnalytics",
                        Position = new Vector2(0, 0.5f),
                        Size = new Vector2(0.33f, 0.5f),
                        IsVisible = true,
                        IsCompact = false
                    },
                    new PanelConfiguration
                    {
                        PanelName = "PoliticalSpectrum",
                        Position = new Vector2(0.33f, 0.5f),
                        Size = new Vector2(0.34f, 0.5f),
                        IsVisible = true,
                        IsCompact = false
                    },
                    new PanelConfiguration
                    {
                        PanelName = "SocialMedia",
                        Position = new Vector2(0.67f, 0.5f),
                        Size = new Vector2(0.33f, 0.5f),
                        IsVisible = true
                    },
                    new PanelConfiguration
                    {
                        PanelName = "Performance",
                        Position = new Vector2(0, 0),
                        Size = new Vector2(1f, 0.5f),
                        IsVisible = true,
                        IsExtended = true
                    }
                },
                ScaleModifier = 0.9f,
                FontSizeModifier = 0.95f
            };
        }

        #endregion

        #region Layout Application

        private void ApplyLayoutImmediately(LayoutPreset preset)
        {
            foreach (var config in preset.PanelConfigurations)
            {
                var panel = FindPanelByName(config.PanelName);
                if (panel != null)
                {
                    ApplyPanelConfiguration(panel, config, preset);
                }
            }
        }

        private void StartLayoutTransition(LayoutPreset targetPreset, UILayoutMode fromMode, UILayoutMode toMode)
        {
            _isTransitioning = true;

            foreach (var config in targetPreset.PanelConfigurations)
            {
                var panel = FindPanelByName(config.PanelName);
                if (panel != null && panel.transform is RectTransform rectTransform)
                {
                    var transition = new LayoutTransition
                    {
                        TargetTransform = rectTransform,
                        StartState = GetCurrentLayoutState(rectTransform),
                        TargetState = CreateTargetLayoutState(config, targetPreset),
                        Duration = layoutTransitionDuration,
                        StartTime = Time.time,
                        EaseType = LeanTweenType.easeOutQuart
                    };

                    _activeTransitions.Add(transition);
                }
            }
        }

        private void UpdateActiveTransitions()
        {
            if (_activeTransitions.Count == 0) return;

            for (int i = _activeTransitions.Count - 1; i >= 0; i--)
            {
                var transition = _activeTransitions[i];
                var elapsed = Time.time - transition.StartTime;
                var progress = Mathf.Clamp01(elapsed / transition.Duration);

                // Apply easing
                var easedProgress = ApplyEasing(progress, transition.EaseType);

                // Interpolate layout state
                var currentState = LerpLayoutState(transition.StartState, transition.TargetState, easedProgress);
                ApplyLayoutState(transition.TargetTransform, currentState);

                // Remove completed transitions
                if (progress >= 1f)
                {
                    _activeTransitions.RemoveAt(i);
                }
            }

            // Check if all transitions are complete
            if (_activeTransitions.Count == 0)
            {
                _isTransitioning = false;
            }
        }

        private void ApplyPanelConfiguration(ResponsivePanel panel, PanelConfiguration config, LayoutPreset preset)
        {
            if (panel.transform is RectTransform rectTransform)
            {
                // Apply position and size
                var parentRect = rectTransform.parent as RectTransform;
                if (parentRect != null)
                {
                    var parentSize = parentRect.rect.size;

                    rectTransform.anchorMin = config.Position;
                    rectTransform.anchorMax = config.Position + config.Size;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                }

                // Apply scale
                var scale = preset.ScaleModifier;
                if (enableAccessibilityMode)
                {
                    scale *= accessibilityScaleMultiplier;
                }
                rectTransform.localScale = Vector3.one * scale;
            }

            // Apply panel-specific settings
            panel.SetVisibility(config.IsVisible);
            panel.SetCompactMode(config.IsCompact);
            panel.SetExtendedMode(config.IsExtended);

            // Apply font size modifications
            ApplyFontSizeModifier(panel, preset.FontSizeModifier);
        }

        #endregion

        #region Helper Methods

        private ResponsivePanel FindPanelByName(string panelName)
        {
            return responsivePanels.Find(p => p.name.Contains(panelName) || p.PanelIdentifier == panelName);
        }

        private LayoutState GetCurrentLayoutState(RectTransform rectTransform)
        {
            return new LayoutState
            {
                AnchoredPosition = rectTransform.anchoredPosition,
                SizeDelta = rectTransform.sizeDelta,
                AnchorMin = rectTransform.anchorMin,
                AnchorMax = rectTransform.anchorMax,
                Scale = rectTransform.localScale
            };
        }

        private LayoutState CreateTargetLayoutState(PanelConfiguration config, LayoutPreset preset)
        {
            var scale = preset.ScaleModifier;
            if (enableAccessibilityMode)
            {
                scale *= accessibilityScaleMultiplier;
            }

            return new LayoutState
            {
                AnchoredPosition = Vector2.zero,
                SizeDelta = Vector2.zero,
                AnchorMin = config.Position,
                AnchorMax = config.Position + config.Size,
                Scale = Vector3.one * scale
            };
        }

        private LayoutState LerpLayoutState(LayoutState start, LayoutState target, float t)
        {
            return new LayoutState
            {
                AnchoredPosition = Vector2.Lerp(start.AnchoredPosition, target.AnchoredPosition, t),
                SizeDelta = Vector2.Lerp(start.SizeDelta, target.SizeDelta, t),
                AnchorMin = Vector2.Lerp(start.AnchorMin, target.AnchorMin, t),
                AnchorMax = Vector2.Lerp(start.AnchorMax, target.AnchorMax, t),
                Scale = Vector3.Lerp(start.Scale, target.Scale, t)
            };
        }

        private void ApplyLayoutState(RectTransform rectTransform, LayoutState state)
        {
            rectTransform.anchoredPosition = state.AnchoredPosition;
            rectTransform.sizeDelta = state.SizeDelta;
            rectTransform.anchorMin = state.AnchorMin;
            rectTransform.anchorMax = state.AnchorMax;
            rectTransform.localScale = state.Scale;
        }

        private float ApplyEasing(float t, LeanTweenType easeType)
        {
            return easeType switch
            {
                LeanTweenType.easeOutQuart => 1f - Mathf.Pow(1f - t, 4f),
                LeanTweenType.easeInOut => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f,
                _ => t
            };
        }

        private void ApplyFontSizeModifier(ResponsivePanel panel, float fontSizeModifier)
        {
            var texts = panel.GetComponentsInChildren<Text>();
            foreach (var text in texts)
            {
                if (!_originalStates.ContainsKey(text.rectTransform))
                {
                    // Store original font size in a component or dictionary
                    var originalSize = text.fontSize;
                    text.fontSize = Mathf.RoundToInt(originalSize * fontSizeModifier);
                }
            }
        }

        private void NotifyPanelsOfLayoutChange(UILayoutMode layoutMode)
        {
            foreach (var panel in responsivePanels)
            {
                panel.OnLayoutModeChanged(layoutMode);
            }
        }

        #endregion

        #region Public Interface

        public UILayoutMode GetCurrentLayoutMode()
        {
            return currentLayoutMode;
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            enableAccessibilityMode = enabled;
            ApplyLayoutMode(currentLayoutMode); // Reapply current layout with accessibility changes
        }

        public void EnableHighContrastMode(bool enabled)
        {
            enableHighContrastMode = enabled;
            // Apply high contrast theme changes here
        }

        public void SetAccessibilityScaleMultiplier(float multiplier)
        {
            accessibilityScaleMultiplier = Mathf.Clamp(multiplier, 0.8f, 2.0f);
            if (enableAccessibilityMode)
            {
                ApplyLayoutMode(currentLayoutMode);
            }
        }

        public bool IsTransitioning()
        {
            return _isTransitioning;
        }

        #endregion
    }

    // Supporting data structures
    [Serializable]
    public class LayoutPreset
    {
        public string Name;
        public List<PanelConfiguration> PanelConfigurations = new List<PanelConfiguration>();
        public float ScaleModifier = 1.0f;
        public float FontSizeModifier = 1.0f;
    }

    [Serializable]
    public class PanelConfiguration
    {
        public string PanelName;
        public Vector2 Position; // Normalized anchors (0-1)
        public Vector2 Size; // Normalized size (0-1)
        public bool IsVisible = true;
        public bool IsCompact = false;
        public bool IsExtended = false;
    }

    public struct LayoutState
    {
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector3 Scale;
    }

    public class LayoutTransition
    {
        public RectTransform TargetTransform;
        public LayoutState StartState;
        public LayoutState TargetState;
        public float Duration;
        public float StartTime;
        public LeanTweenType EaseType;
    }

    public enum LeanTweenType
    {
        linear,
        easeOutQuart,
        easeInOut
    }
}