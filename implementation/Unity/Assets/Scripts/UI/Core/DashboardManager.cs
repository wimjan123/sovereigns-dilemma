using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Entities;
using Unity.Profiling;
using SovereignsDilemma.Political.Systems;
using SovereignsDilemma.Core.EventBus;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.UI.Core
{
    /// <summary>
    /// Central dashboard manager for The Sovereign's Dilemma political simulation interface.
    /// Implements multi-canvas architecture with responsive layouts and efficient data binding.
    /// </summary>
    public class DashboardManager : MonoBehaviour, IEventHandler<VoterOpinionChangedEvent>,
        IEventHandler<PoliticalEventOccurredEvent>, IEventHandler<PerformanceMetricsUpdatedEvent>
    {
        [Header("Canvas References")]
        [SerializeField] private Canvas backgroundCanvas;
        [SerializeField] private Canvas mainUICanvas;
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private Canvas performanceCanvas;

        [Header("Dashboard Panels")]
        [SerializeField] private VoterAnalyticsPanel voterAnalyticsPanel;
        [SerializeField] private PoliticalSpectrumPanel politicalSpectrumPanel;
        [SerializeField] private SocialMediaPanel socialMediaPanel;
        [SerializeField] private PerformanceMonitorPanel performanceMonitorPanel;
        [SerializeField] private ConfigurationPanel configurationPanel;

        [Header("Layout Configuration")]
        [SerializeField] private ResponsiveLayoutManager layoutManager;
        [SerializeField] private CanvasScaler mainCanvasScaler;
        [SerializeField] private GraphicRaycaster mainRaycaster;

        [Header("Performance Settings")]
        [SerializeField] private bool enablePerformanceOptimization = true;
        [SerializeField] private float uiUpdateInterval = 0.1f; // 10 FPS for UI updates
        [SerializeField] private int maxUIUpdatesPerFrame = 5;

        // Core systems
        private World _world;
        private EventBusSystem _eventBus;
        private FullScaleVoterSystem _voterSystem;
        private PoliticalEventSystem _eventSystem;

        // Data binding system
        private UIDataBindingManager _dataBindingManager;
        private Dictionary<Type, List<IUIComponent>> _componentSubscriptions;

        // Performance tracking
        private readonly ProfilerMarker _uiUpdateMarker = new("SovereignsDilemma.UI.Update");
        private float _lastUpdateTime;
        private int _uiUpdatesThisFrame;

        // UI state management
        private DashboardState _currentState;
        private Dictionary<string, UIPanelState> _panelStates;
        private bool _isInitialized;

        // Responsive design
        private Vector2 _lastScreenSize;
        private UILayoutMode _currentLayoutMode;

        private void Awake()
        {
            InitializeComponents();
            SetupEventSystem();
            ConfigureCanvases();
        }

        private void Start()
        {
            InitializeSystems();
            SetupDataBinding();
            InitializeLayout();
            SubscribeToEvents();

            _isInitialized = true;
            Debug.Log("Dashboard Manager initialized successfully");
        }

        private void Update()
        {
            if (!_isInitialized) return;

            using (_uiUpdateMarker.Auto())
            {
                HandleResponsiveLayout();
                UpdateDataBinding();
                UpdatePerformanceMetrics();

                _uiUpdatesThisFrame = 0; // Reset for next frame
            }
        }

        #region Initialization

        private void InitializeComponents()
        {
            _componentSubscriptions = new Dictionary<Type, List<IUIComponent>>();
            _panelStates = new Dictionary<string, UIPanelState>();
            _currentState = new DashboardState();
            _lastScreenSize = new Vector2(Screen.width, Screen.height);

            // Initialize data binding manager
            _dataBindingManager = new UIDataBindingManager();
        }

        private void SetupEventSystem()
        {
            // Ensure EventSystem exists
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
                Debug.Log("Created EventSystem for UI interaction");
            }
        }

        private void ConfigureCanvases()
        {
            // Configure canvas sort orders
            if (backgroundCanvas) backgroundCanvas.sortingOrder = 0;
            if (mainUICanvas) mainUICanvas.sortingOrder = 10;
            if (overlayCanvas) overlayCanvas.sortingOrder = 20;
            if (performanceCanvas) performanceCanvas.sortingOrder = 30;

            // Configure canvas scalers for responsive design
            if (mainCanvasScaler)
            {
                mainCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                mainCanvasScaler.referenceResolution = new Vector2(1920, 1080);
                mainCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                mainCanvasScaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void InitializeSystems()
        {
            // Get ECS world and systems
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != null)
            {
                _eventBus = _world.GetExistingSystemManaged<EventBusSystem>();
                _voterSystem = _world.GetExistingSystemManaged<FullScaleVoterSystem>();
                _eventSystem = _world.GetExistingSystemManaged<PoliticalEventSystem>();
            }

            if (_eventBus == null)
            {
                Debug.LogError("EventBusSystem not found! UI will not receive data updates.");
            }
        }

        private void SetupDataBinding()
        {
            // Register UI components for data binding
            RegisterUIComponents();

            // Set up update intervals for different data types
            _dataBindingManager.SetUpdateInterval<VoterDemographicsData>(0.5f); // 2 FPS
            _dataBindingManager.SetUpdateInterval<PoliticalSpectrumData>(0.3f); // ~3 FPS
            _dataBindingManager.SetUpdateInterval<PerformanceData>(0.1f); // 10 FPS
            _dataBindingManager.SetUpdateInterval<SocialMediaData>(1.0f); // 1 FPS
        }

        private void InitializeLayout()
        {
            if (layoutManager)
            {
                _currentLayoutMode = DetermineLayoutMode();
                layoutManager.SetLayoutMode(_currentLayoutMode);
                ApplyLayoutConfiguration();
            }
        }

        private void SubscribeToEvents()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe<VoterOpinionChangedEvent>(this, "UI");
                _eventBus.Subscribe<PoliticalEventOccurredEvent>(this, "UI");
                _eventBus.Subscribe<PerformanceMetricsUpdatedEvent>(this, "UI");
            }
        }

        #endregion

        #region Data Binding and Updates

        private void UpdateDataBinding()
        {
            if (Time.time - _lastUpdateTime < uiUpdateInterval) return;

            _dataBindingManager.Update();
            _lastUpdateTime = Time.time;

            // Update panels based on available data
            UpdateVoterAnalytics();
            UpdatePoliticalSpectrum();
            UpdateSocialMedia();
            UpdatePerformanceDisplay();
        }

        private void UpdateVoterAnalytics()
        {
            if (!voterAnalyticsPanel || _uiUpdatesThisFrame >= maxUIUpdatesPerFrame) return;

            var voterData = GatherVoterAnalyticsData();
            if (voterData.HasData)
            {
                voterAnalyticsPanel.UpdateData(voterData);
                _uiUpdatesThisFrame++;
            }
        }

        private void UpdatePoliticalSpectrum()
        {
            if (!politicalSpectrumPanel || _uiUpdatesThisFrame >= maxUIUpdatesPerFrame) return;

            var spectrumData = GatherPoliticalSpectrumData();
            if (spectrumData.HasData)
            {
                politicalSpectrumPanel.UpdateData(spectrumData);
                _uiUpdatesThisFrame++;
            }
        }

        private void UpdateSocialMedia()
        {
            if (!socialMediaPanel || _uiUpdatesThisFrame >= maxUIUpdatesPerFrame) return;

            var socialData = GatherSocialMediaData();
            if (socialData.HasData)
            {
                socialMediaPanel.UpdateData(socialData);
                _uiUpdatesThisFrame++;
            }
        }

        private void UpdatePerformanceDisplay()
        {
            if (!performanceMonitorPanel || _uiUpdatesThisFrame >= maxUIUpdatesPerFrame) return;

            var perfData = GatherPerformanceData();
            if (perfData.HasData)
            {
                performanceMonitorPanel.UpdateData(perfData);
                _uiUpdatesThisFrame++;
            }
        }

        #endregion

        #region Responsive Layout

        private void HandleResponsiveLayout()
        {
            var currentScreenSize = new Vector2(Screen.width, Screen.height);
            if (Vector2.Distance(currentScreenSize, _lastScreenSize) > 10f) // Screen size changed
            {
                _lastScreenSize = currentScreenSize;
                var newLayoutMode = DetermineLayoutMode();

                if (newLayoutMode != _currentLayoutMode)
                {
                    _currentLayoutMode = newLayoutMode;
                    ApplyLayoutConfiguration();
                }
            }
        }

        private UILayoutMode DetermineLayoutMode()
        {
            float aspectRatio = (float)Screen.width / Screen.height;

            if (Screen.width < 1024)
                return UILayoutMode.Mobile;
            else if (aspectRatio < 1.5f)
                return UILayoutMode.Portrait;
            else if (aspectRatio > 2.1f)
                return UILayoutMode.Ultrawide;
            else
                return UILayoutMode.Standard;
        }

        private void ApplyLayoutConfiguration()
        {
            if (!layoutManager) return;

            layoutManager.SetLayoutMode(_currentLayoutMode);

            // Adjust panel arrangements based on layout mode
            switch (_currentLayoutMode)
            {
                case UILayoutMode.Mobile:
                    SetMobileLayout();
                    break;
                case UILayoutMode.Portrait:
                    SetPortraitLayout();
                    break;
                case UILayoutMode.Ultrawide:
                    SetUltrawideLayout();
                    break;
                default:
                    SetStandardLayout();
                    break;
            }

            Debug.Log($"Applied {_currentLayoutMode} layout configuration");
        }

        private void SetMobileLayout()
        {
            // Simplified mobile layout - stack panels vertically
            if (voterAnalyticsPanel) voterAnalyticsPanel.SetCompactMode(true);
            if (politicalSpectrumPanel) politicalSpectrumPanel.SetCompactMode(true);
            if (socialMediaPanel) socialMediaPanel.SetVisibility(false); // Hide on mobile
        }

        private void SetPortraitLayout()
        {
            // Portrait layout - optimize for vertical space
            if (voterAnalyticsPanel) voterAnalyticsPanel.SetCompactMode(false);
            if (politicalSpectrumPanel) politicalSpectrumPanel.SetCompactMode(true);
            if (socialMediaPanel) socialMediaPanel.SetVisibility(true);
        }

        private void SetUltrawideLayout()
        {
            // Ultrawide layout - take advantage of horizontal space
            if (voterAnalyticsPanel) voterAnalyticsPanel.SetCompactMode(false);
            if (politicalSpectrumPanel) politicalSpectrumPanel.SetCompactMode(false);
            if (socialMediaPanel) socialMediaPanel.SetVisibility(true);
            if (performanceMonitorPanel) performanceMonitorPanel.SetExtendedMode(true);
        }

        private void SetStandardLayout()
        {
            // Standard 16:9 layout
            if (voterAnalyticsPanel) voterAnalyticsPanel.SetCompactMode(false);
            if (politicalSpectrumPanel) politicalSpectrumPanel.SetCompactMode(false);
            if (socialMediaPanel) socialMediaPanel.SetVisibility(true);
        }

        #endregion

        #region Data Gathering

        private VoterAnalyticsData GatherVoterAnalyticsData()
        {
            var data = new VoterAnalyticsData();

            if (_voterSystem != null)
            {
                var lodMetrics = _voterSystem.GetLODMetrics();
                data.TotalVoters = lodMetrics.HighDetailCount + lodMetrics.MediumDetailCount +
                                 lodMetrics.LowDetailCount + lodMetrics.DormantCount;
                data.ActiveVoters = lodMetrics.HighDetailCount + lodMetrics.MediumDetailCount;
                data.HasData = true;

                // Additional demographic data would be gathered here
                data.AgeDistribution = CalculateAgeDistribution();
                data.EducationDistribution = CalculateEducationDistribution();
                data.IncomeDistribution = CalculateIncomeDistribution();
            }

            return data;
        }

        private PoliticalSpectrumData GatherPoliticalSpectrumData()
        {
            var data = new PoliticalSpectrumData();

            if (_voterSystem != null)
            {
                // Gather political opinion distribution
                data.EconomicSpectrum = CalculateEconomicSpectrum();
                data.SocialSpectrum = CalculateSocialSpectrum();
                data.EnvironmentalSpectrum = CalculateEnvironmentalSpectrum();
                data.PartySupport = CalculatePartySupport();
                data.HasData = true;
            }

            return data;
        }

        private SocialMediaData GatherSocialMediaData()
        {
            var data = new SocialMediaData();

            if (_eventSystem != null)
            {
                var eventMetrics = _eventSystem.GetMetrics();
                data.ActiveEvents = eventMetrics.ActiveEventsCount;
                data.PoliticalTension = eventMetrics.CurrentPoliticalTension;
                data.RecentPosts = GenerateRecentSocialMediaPosts();
                data.HasData = true;
            }

            return data;
        }

        private PerformanceData GatherPerformanceData()
        {
            var data = new PerformanceData();

            data.CurrentFPS = 1f / Time.unscaledDeltaTime;
            data.MemoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(false) / (1024f * 1024f);
            data.HasData = true;

            if (_eventBus != null)
            {
                var busMetrics = _eventBus.GetMetrics();
                data.EventQueueSize = busMetrics.QueueSize;
                data.EventsProcessed = busMetrics.TotalEventsProcessed;
            }

            return data;
        }

        #endregion

        #region Helper Methods

        private void RegisterUIComponents()
        {
            // Register all UI components for data binding
            if (voterAnalyticsPanel) RegisterComponent(voterAnalyticsPanel);
            if (politicalSpectrumPanel) RegisterComponent(politicalSpectrumPanel);
            if (socialMediaPanel) RegisterComponent(socialMediaPanel);
            if (performanceMonitorPanel) RegisterComponent(performanceMonitorPanel);
            if (configurationPanel) RegisterComponent(configurationPanel);
        }

        private void RegisterComponent(IUIComponent component)
        {
            var dataType = component.GetDataType();
            if (!_componentSubscriptions.ContainsKey(dataType))
            {
                _componentSubscriptions[dataType] = new List<IUIComponent>();
            }
            _componentSubscriptions[dataType].Add(component);
        }

        private void UpdatePerformanceMetrics()
        {
            if (enablePerformanceOptimization)
            {
                PerformanceProfiler.RecordMeasurement("UI_FPS", 1f / Time.unscaledDeltaTime);
                PerformanceProfiler.RecordMeasurement("UI_UpdatesPerFrame", _uiUpdatesThisFrame);
            }
        }

        // Placeholder methods for data calculations - would be implemented with actual data
        private float[] CalculateAgeDistribution() => new float[5] { 0.2f, 0.3f, 0.25f, 0.15f, 0.1f };
        private float[] CalculateEducationDistribution() => new float[4] { 0.1f, 0.4f, 0.3f, 0.2f };
        private float[] CalculateIncomeDistribution() => new float[3] { 0.3f, 0.5f, 0.2f };
        private float[] CalculateEconomicSpectrum() => new float[10] { 0.05f, 0.1f, 0.15f, 0.2f, 0.2f, 0.15f, 0.1f, 0.03f, 0.02f, 0.01f };
        private float[] CalculateSocialSpectrum() => new float[10] { 0.03f, 0.07f, 0.12f, 0.18f, 0.25f, 0.2f, 0.1f, 0.03f, 0.01f, 0.01f };
        private float[] CalculateEnvironmentalSpectrum() => new float[10] { 0.02f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.15f, 0.05f, 0.02f, 0.01f };
        private Dictionary<string, float> CalculatePartySupport() => new Dictionary<string, float>
        {
            { "VVD", 0.22f }, { "PVV", 0.17f }, { "D66", 0.15f }, { "CDA", 0.09f },
            { "GL", 0.06f }, { "PvdA", 0.06f }, { "SP", 0.06f }, { "Others", 0.19f }
        };

        private List<SocialMediaPost> GenerateRecentSocialMediaPosts()
        {
            // Would integrate with AI system to generate realistic posts
            return new List<SocialMediaPost>
            {
                new SocialMediaPost { Content = "Climate policy debate heating up...", Engagement = 0.7f, Timestamp = DateTime.Now.AddMinutes(-5) },
                new SocialMediaPost { Content = "Housing market concerns grow...", Engagement = 0.6f, Timestamp = DateTime.Now.AddMinutes(-15) },
                new SocialMediaPost { Content = "Immigration policy discussion...", Engagement = 0.5f, Timestamp = DateTime.Now.AddMinutes(-25) }
            };
        }

        #endregion

        #region Event Handlers

        public void Handle(VoterOpinionChangedEvent eventData)
        {
            // Queue voter analytics update
            _dataBindingManager.QueueUpdate<VoterAnalyticsData>();
            _dataBindingManager.QueueUpdate<PoliticalSpectrumData>();
        }

        public void Handle(PoliticalEventOccurredEvent eventData)
        {
            // Queue social media and political updates
            _dataBindingManager.QueueUpdate<SocialMediaData>();
            _dataBindingManager.QueueUpdate<PoliticalSpectrumData>();
        }

        public void Handle(PerformanceMetricsUpdatedEvent eventData)
        {
            // Queue performance display update
            _dataBindingManager.QueueUpdate<PerformanceData>();
        }

        public void Handle(IEvent eventData)
        {
            // Generic event handler - route to specific handlers
            switch (eventData)
            {
                case VoterOpinionChangedEvent voterEvent:
                    Handle(voterEvent);
                    break;
                case PoliticalEventOccurredEvent politicalEvent:
                    Handle(politicalEvent);
                    break;
                case PerformanceMetricsUpdatedEvent perfEvent:
                    Handle(perfEvent);
                    break;
            }
        }

        #endregion

        #region Public Interface

        public void SetPanelVisibility(string panelName, bool visible)
        {
            if (!_panelStates.ContainsKey(panelName))
            {
                _panelStates[panelName] = new UIPanelState();
            }
            _panelStates[panelName].IsVisible = visible;

            // Apply visibility to actual panel
            ApplyPanelState(panelName);
        }

        public void TogglePerformanceMonitor()
        {
            if (performanceMonitorPanel)
            {
                bool isVisible = performanceMonitorPanel.IsVisible();
                performanceMonitorPanel.SetVisibility(!isVisible);
            }
        }

        public DashboardState GetCurrentState()
        {
            return _currentState;
        }

        public void ForceLayoutRefresh()
        {
            _currentLayoutMode = DetermineLayoutMode();
            ApplyLayoutConfiguration();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<VoterOpinionChangedEvent>(this);
                _eventBus.Unsubscribe<PoliticalEventOccurredEvent>(this);
                _eventBus.Unsubscribe<PerformanceMetricsUpdatedEvent>(this);
            }

            _dataBindingManager?.Dispose();
        }

        private void ApplyPanelState(string panelName)
        {
            // Implementation would apply visibility and other state to named panel
            // This is a placeholder for the actual panel state management
        }

        #endregion
    }

    // Supporting data structures
    public enum UILayoutMode
    {
        Mobile,
        Portrait,
        Standard,
        Ultrawide
    }

    [Serializable]
    public class DashboardState
    {
        public bool IsPerformanceMonitorVisible = false;
        public UILayoutMode LayoutMode = UILayoutMode.Standard;
        public Dictionary<string, bool> PanelVisibility = new Dictionary<string, bool>();
    }

    [Serializable]
    public class UIPanelState
    {
        public bool IsVisible = true;
        public bool IsCompact = false;
        public Vector2 Position = Vector2.zero;
        public Vector2 Size = Vector2.one;
    }
}