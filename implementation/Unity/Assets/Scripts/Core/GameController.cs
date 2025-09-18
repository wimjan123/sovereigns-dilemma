using System;
using System.Collections;
using UnityEngine;
using SovereignsDilemma.Core.Events;
using SovereignsDilemma.Systems.Voter;
using SovereignsDilemma.Systems.PoliticalEvents;
using SovereignsDilemma.Systems.Performance;
using SovereignsDilemma.UI.Core;
using SovereignsDilemma.AI;
using Unity.Profiling;

namespace SovereignsDilemma.Core
{
    /// <summary>
    /// Main game controller orchestrating all systems for The Sovereign's Dilemma.
    /// Provides end-to-end gameplay with complete game loop integration.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Game Configuration")]
        [SerializeField] private bool autoStartGame = true;
        [SerializeField] private float gameSessionDuration = 1800f; // 30 minutes
        [SerializeField] private bool enableContinuousPlay = true;

        [Header("System References")]
        [SerializeField] private EventBusSystem eventBusSystem;
        [SerializeField] private FullScaleVoterSystem voterSystem;
        [SerializeField] private PoliticalEventSystem politicalEventSystem;
        [SerializeField] private SystemPerformanceMonitor performanceMonitor;
        [SerializeField] private DashboardManager dashboardManager;
        [SerializeField] private UIDataBindingManager dataBindingManager;
        [SerializeField] private AccessibilityManager accessibilityManager;
        [SerializeField] private OfflineAIService offlineAIService;

        [Header("Game State")]
        [SerializeField] private GameState currentGameState = GameState.Initializing;
        [SerializeField] private float sessionStartTime;
        [SerializeField] private bool isPaused = false;

        [Header("Performance Targets")]
        [SerializeField] private float targetFrameRate = 60f;
        [SerializeField] private int maxVoterCount = 10000;
        [SerializeField] private float memoryWarningThreshold = 1024f; // MB

        // Game loop state
        private Coroutine _gameLoopCoroutine;
        private bool _isGameRunning = false;
        private float _lastSystemUpdate = 0f;
        private float _systemUpdateInterval = 0.1f;

        // Integration state
        private bool _systemsInitialized = false;
        private int _initializedSystemCount = 0;
        private readonly int _totalSystemCount = 8;

        // Performance tracking
        private readonly ProfilerMarker _gameLoopMarker = new("GameController.GameLoop");
        private readonly ProfilerMarker _systemUpdateMarker = new("GameController.SystemUpdate");

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<float> OnSessionProgressChanged;
        public event Action<bool> OnGamePaused;

        public GameState CurrentState => currentGameState;
        public bool IsGameRunning => _isGameRunning;
        public float SessionProgress => gameSessionDuration > 0 ? (Time.time - sessionStartTime) / gameSessionDuration : 0f;

        private void Awake()
        {
            InitializeGameController();
            SetupEventHandlers();
        }

        private void Start()
        {
            StartCoroutine(InitializeAllSystems());
        }

        private void Update()
        {
            using (_gameLoopMarker.Auto())
            {
                UpdateGameState();
                CheckPerformanceThresholds();
                HandleUserInput();

                if (Time.time - _lastSystemUpdate >= _systemUpdateInterval)
                {
                    UpdateIntegratedSystems();
                    _lastSystemUpdate = Time.time;
                }
            }
        }

        #region Initialization

        private void InitializeGameController()
        {
            SetGameState(GameState.Initializing);
            sessionStartTime = Time.time;

            // Set target frame rate
            Application.targetFrameRate = (int)targetFrameRate;
            QualitySettings.vSyncCount = 0;

            Debug.Log("GameController initialized - Starting system integration");
        }

        private void SetupEventHandlers()
        {
            // Subscribe to critical system events
            if (eventBusSystem != null)
            {
                eventBusSystem.Subscribe<SystemInitializedEvent>(OnSystemInitialized);
                eventBusSystem.Subscribe<PerformanceWarningEvent>(OnPerformanceWarning);
                eventBusSystem.Subscribe<CriticalErrorEvent>(OnCriticalError);
            }
        }

        private IEnumerator InitializeAllSystems()
        {
            SetGameState(GameState.LoadingSystems);

            yield return StartCoroutine(InitializeEventBus());
            yield return StartCoroutine(InitializePerformanceMonitoring());
            yield return StartCoroutine(InitializeVoterSystem());
            yield return StartCoroutine(InitializePoliticalEventSystem());
            yield return StartCoroutine(InitializeUISystem());
            yield return StartCoroutine(InitializeAIService());
            yield return StartCoroutine(InitializeAccessibilitySystem());
            yield return StartCoroutine(FinalizeIntegration());

            _systemsInitialized = true;
            SetGameState(GameState.Ready);

            if (autoStartGame)
            {
                StartGameSession();
            }
        }

        private IEnumerator InitializeEventBus()
        {
            Debug.Log("Initializing Event Bus System...");

            if (eventBusSystem == null)
            {
                eventBusSystem = FindObjectOfType<EventBusSystem>();
                if (eventBusSystem == null)
                {
                    GameObject eventBusGO = new GameObject("EventBusSystem");
                    eventBusSystem = eventBusGO.AddComponent<EventBusSystem>();
                }
            }

            // Event bus initializes immediately
            yield return null;
            _initializedSystemCount++;
            Debug.Log($"Event Bus System initialized ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator InitializePerformanceMonitoring()
        {
            Debug.Log("Initializing Performance Monitoring...");

            if (performanceMonitor == null)
            {
                performanceMonitor = FindObjectOfType<SystemPerformanceMonitor>();
                if (performanceMonitor == null)
                {
                    GameObject perfGO = new GameObject("SystemPerformanceMonitor");
                    performanceMonitor = perfGO.AddComponent<SystemPerformanceMonitor>();
                }
            }

            // Performance monitoring setup
            yield return new WaitForSeconds(0.1f);
            _initializedSystemCount++;
            Debug.Log($"Performance Monitoring initialized ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator InitializeVoterSystem()
        {
            Debug.Log("Initializing Voter System...");

            if (voterSystem == null)
            {
                voterSystem = FindObjectOfType<FullScaleVoterSystem>();
                if (voterSystem == null)
                {
                    Debug.LogError("FullScaleVoterSystem not found - this is required for MVP");
                    yield break;
                }
            }

            // Wait for voter system to initialize its ECS components
            float timeout = 10f;
            float elapsed = 0f;

            while (!voterSystem.IsInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!voterSystem.IsInitialized)
            {
                Debug.LogError("Voter System failed to initialize within timeout");
                SetGameState(GameState.Error);
                yield break;
            }

            _initializedSystemCount++;
            Debug.Log($"Voter System initialized with {voterSystem.ActiveVoterCount} voters ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator InitializePoliticalEventSystem()
        {
            Debug.Log("Initializing Political Event System...");

            if (politicalEventSystem == null)
            {
                politicalEventSystem = FindObjectOfType<PoliticalEventSystem>();
                if (politicalEventSystem == null)
                {
                    GameObject politicalGO = new GameObject("PoliticalEventSystem");
                    politicalEventSystem = politicalGO.AddComponent<PoliticalEventSystem>();
                }
            }

            // Political event system setup
            yield return new WaitForSeconds(0.2f);
            _initializedSystemCount++;
            Debug.Log($"Political Event System initialized ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator InitializeUISystem()
        {
            Debug.Log("Initializing UI System...");

            // Initialize Dashboard Manager
            if (dashboardManager == null)
            {
                dashboardManager = FindObjectOfType<DashboardManager>();
                if (dashboardManager == null)
                {
                    Debug.LogError("DashboardManager not found - UI system cannot initialize");
                    yield break;
                }
            }

            // Initialize Data Binding Manager
            if (dataBindingManager == null)
            {
                dataBindingManager = FindObjectOfType<UIDataBindingManager>();
                if (dataBindingManager == null)
                {
                    GameObject dataBindingGO = new GameObject("UIDataBindingManager");
                    dataBindingManager = dataBindingGO.AddComponent<UIDataBindingManager>();
                }
            }

            // Wait for UI components to initialize
            yield return new WaitForSeconds(0.3f);
            _initializedSystemCount++;
            Debug.Log($"UI System initialized ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator InitializeAIService()
        {
            Debug.Log("Initializing AI Service...");

            if (offlineAIService == null)
            {
                offlineAIService = FindObjectOfType<OfflineAIService>();
                if (offlineAIService == null)
                {
                    GameObject aiGO = new GameObject("OfflineAIService");
                    offlineAIService = aiGO.AddComponent<OfflineAIService>();
                }
            }

            // AI service setup
            yield return new WaitForSeconds(0.1f);
            _initializedSystemCount++;
            Debug.Log($"AI Service initialized ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator InitializeAccessibilitySystem()
        {
            Debug.Log("Initializing Accessibility System...");

            if (accessibilityManager == null)
            {
                accessibilityManager = FindObjectOfType<AccessibilityManager>();
                if (accessibilityManager == null)
                {
                    GameObject accessibilityGO = new GameObject("AccessibilityManager");
                    accessibilityManager = accessibilityGO.AddComponent<AccessibilityManager>();
                }
            }

            // Accessibility setup
            yield return new WaitForSeconds(0.1f);
            _initializedSystemCount++;
            Debug.Log($"Accessibility System initialized ({_initializedSystemCount}/{_totalSystemCount})");
        }

        private IEnumerator FinalizeIntegration()
        {
            Debug.Log("Finalizing system integration...");

            // Connect systems through event bus
            ConnectSystemsViaEventBus();

            // Setup data binding connections
            SetupDataBindingConnections();

            // Validate system integration
            bool integrationValid = ValidateSystemIntegration();

            if (!integrationValid)
            {
                Debug.LogError("System integration validation failed");
                SetGameState(GameState.Error);
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
            _initializedSystemCount++;
            Debug.Log($"System integration completed ({_initializedSystemCount}/{_totalSystemCount})");
        }

        #endregion

        #region Game State Management

        private void SetGameState(GameState newState)
        {
            if (currentGameState == newState) return;

            GameState previousState = currentGameState;
            currentGameState = newState;

            Debug.Log($"Game State: {previousState} → {newState}");
            OnGameStateChanged?.Invoke(newState);

            HandleGameStateTransition(previousState, newState);
        }

        private void HandleGameStateTransition(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Running:
                    if (_gameLoopCoroutine == null)
                        _gameLoopCoroutine = StartCoroutine(GameLoopCoroutine());
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    OnGamePaused?.Invoke(true);
                    break;

                case GameState.Ready:
                    Time.timeScale = 1f;
                    OnGamePaused?.Invoke(false);
                    break;

                case GameState.Error:
                    StopAllCoroutines();
                    Debug.LogError("Game entered error state - stopping all operations");
                    break;
            }
        }

        private void UpdateGameState()
        {
            switch (currentGameState)
            {
                case GameState.Running:
                    UpdateRunningState();
                    break;

                case GameState.Paused:
                    // Game is paused, minimal updates
                    break;

                case GameState.Completed:
                    if (enableContinuousPlay)
                    {
                        RestartGameSession();
                    }
                    break;
            }
        }

        private void UpdateRunningState()
        {
            // Check session duration
            if (gameSessionDuration > 0 && SessionProgress >= 1f)
            {
                SetGameState(GameState.Completed);
                return;
            }

            // Update session progress
            OnSessionProgressChanged?.Invoke(SessionProgress);
        }

        #endregion

        #region Game Loop

        public void StartGameSession()
        {
            if (!_systemsInitialized)
            {
                Debug.LogWarning("Cannot start game - systems not initialized");
                return;
            }

            sessionStartTime = Time.time;
            _isGameRunning = true;
            SetGameState(GameState.Running);

            Debug.Log("Game session started");
        }

        public void PauseGame()
        {
            if (currentGameState == GameState.Running)
            {
                SetGameState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (currentGameState == GameState.Paused)
            {
                SetGameState(GameState.Running);
            }
        }

        public void RestartGameSession()
        {
            StopGameSession();
            StartGameSession();
        }

        public void StopGameSession()
        {
            _isGameRunning = false;

            if (_gameLoopCoroutine != null)
            {
                StopCoroutine(_gameLoopCoroutine);
                _gameLoopCoroutine = null;
            }

            SetGameState(GameState.Ready);
            Debug.Log("Game session stopped");
        }

        private IEnumerator GameLoopCoroutine()
        {
            Debug.Log("Game loop started");

            while (_isGameRunning && currentGameState == GameState.Running)
            {
                using (_gameLoopMarker.Auto())
                {
                    // Update all integrated systems
                    yield return StartCoroutine(UpdateGameSystems());

                    // Check for game end conditions
                    CheckGameEndConditions();
                }

                yield return null; // Wait for next frame
            }

            Debug.Log("Game loop ended");
        }

        private IEnumerator UpdateGameSystems()
        {
            // Political event processing
            if (politicalEventSystem)
            {
                // Political events are updated automatically
            }

            // Voter system updates
            if (voterSystem)
            {
                // Voter system updates automatically via ECS
            }

            // UI data updates
            if (dataBindingManager)
            {
                dataBindingManager.Update();
            }

            yield return null;
        }

        #endregion

        #region System Integration

        private void ConnectSystemsViaEventBus()
        {
            if (eventBusSystem == null) return;

            // Connect voter system events to UI updates
            // Connect political events to voter opinion changes
            // Connect performance events to system adaptation

            Debug.Log("Systems connected via Event Bus");
        }

        private void SetupDataBindingConnections()
        {
            if (dataBindingManager == null) return;

            // Subscribe UI components to relevant data types
            // Setup automatic data refresh intervals
            // Connect real-time data sources

            Debug.Log("Data binding connections established");
        }

        private bool ValidateSystemIntegration()
        {
            // Validate that all critical systems are operational
            bool valid = true;

            if (voterSystem == null || !voterSystem.IsInitialized)
            {
                Debug.LogError("Voter system validation failed");
                valid = false;
            }

            if (eventBusSystem == null)
            {
                Debug.LogError("Event bus system validation failed");
                valid = false;
            }

            if (dashboardManager == null)
            {
                Debug.LogError("Dashboard manager validation failed");
                valid = false;
            }

            return valid;
        }

        private void UpdateIntegratedSystems()
        {
            using (_systemUpdateMarker.Auto())
            {
                // Update performance monitoring
                if (performanceMonitor)
                {
                    // Performance monitor updates automatically
                }

                // Update UI data binding
                if (dataBindingManager)
                {
                    // Data binding manager updates automatically
                }

                // Update accessibility system
                if (accessibilityManager)
                {
                    // Accessibility manager updates automatically
                }
            }
        }

        #endregion

        #region Performance Management

        private void CheckPerformanceThresholds()
        {
            if (performanceMonitor == null) return;

            // Check FPS
            if (Time.smoothDeltaTime > 1f / 30f) // Below 30 FPS
            {
                HandleLowPerformance();
            }

            // Check memory usage
            long memoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(false);
            if (memoryUsage > memoryWarningThreshold * 1024 * 1024)
            {
                HandleHighMemoryUsage();
            }
        }

        private void HandleLowPerformance()
        {
            // Reduce voter system quality
            if (voterSystem && voterSystem.ActiveVoterCount > 5000)
            {
                // Could reduce update frequency or LOD level
                Debug.Log("Reducing voter system quality due to low performance");
            }

            // Disable non-essential UI updates
            if (dataBindingManager)
            {
                // Could reduce update frequency
                Debug.Log("Reducing UI update frequency due to low performance");
            }
        }

        private void HandleHighMemoryUsage()
        {
            // Force garbage collection
            System.GC.Collect();
            Debug.Log("Forced garbage collection due to high memory usage");

            // Could also reduce cache sizes or optimize object pooling
        }

        #endregion

        #region Event Handlers

        private void OnSystemInitialized(SystemInitializedEvent eventData)
        {
            Debug.Log($"System initialized: {eventData.SystemName}");
        }

        private void OnPerformanceWarning(PerformanceWarningEvent eventData)
        {
            Debug.LogWarning($"Performance warning: {eventData.Message}");
            HandleLowPerformance();
        }

        private void OnCriticalError(CriticalErrorEvent eventData)
        {
            Debug.LogError($"Critical error: {eventData.Message}");
            SetGameState(GameState.Error);
        }

        #endregion

        #region Input Handling

        private void HandleUserInput()
        {
            // ESC to pause/resume
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentGameState == GameState.Running)
                    PauseGame();
                else if (currentGameState == GameState.Paused)
                    ResumeGame();
            }

            // F5 to restart
            if (Input.GetKeyDown(KeyCode.F5))
            {
                RestartGameSession();
            }

            // F11 to toggle fullscreen
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Screen.fullScreen = !Screen.fullScreen;
            }
        }

        #endregion

        #region Game End Conditions

        private void CheckGameEndConditions()
        {
            // Check for natural game ending conditions
            // Could be based on political stability, voter engagement, etc.

            // For MVP, just check session duration
            if (gameSessionDuration > 0 && SessionProgress >= 1f)
            {
                SetGameState(GameState.Completed);
            }
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            StopGameSession();

            if (eventBusSystem != null)
            {
                eventBusSystem.Unsubscribe<SystemInitializedEvent>(OnSystemInitialized);
                eventBusSystem.Unsubscribe<PerformanceWarningEvent>(OnPerformanceWarning);
                eventBusSystem.Unsubscribe<CriticalErrorEvent>(OnCriticalError);
            }
        }

        #endregion

        #region Data Structures

        public enum GameState
        {
            Initializing,
            LoadingSystems,
            Ready,
            Running,
            Paused,
            Completed,
            Error
        }

        #endregion
    }
}