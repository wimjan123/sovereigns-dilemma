// Unity Performance Optimization Script for The Sovereign's Dilemma
// This script implements systematic performance improvements

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Collections;

namespace SovereignsDilemma.Optimization
{
    public class PerformanceOptimizer : MonoBehaviour
    {
        [Header("Graphics Optimization")]
        public bool enableOcclusionCulling = true;
        public bool enableGPUInstancing = true;
        public bool optimizeShaders = true;
        public int targetFrameRate = 60;

        [Header("Memory Optimization")]
        public bool enableObjectPooling = true;
        public bool optimizeGarbageCollection = true;
        public int maxPoolSize = 1000;

        [Header("AI Optimization")]
        public bool enableAIBatching = true;
        public float aiBatchInterval = 0.1f;
        public int maxBatchSize = 50;

        [Header("Monitoring")]
        public bool enablePerformanceMonitoring = true;
        public float monitoringInterval = 1.0f;

        private Dictionary<string, Queue<GameObject>> objectPools;
        private List<System.Func<IEnumerator>> aiBatchQueue;
        private PerformanceMonitor performanceMonitor;

        void Start()
        {
            InitializeOptimizations();
        }

        void InitializeOptimizations()
        {
            // Graphics optimizations
            if (enableOcclusionCulling)
            {
                SetupOcclusionCulling();
            }

            if (enableGPUInstancing)
            {
                SetupGPUInstancing();
            }

            // Memory optimizations
            if (enableObjectPooling)
            {
                InitializeObjectPools();
            }

            if (optimizeGarbageCollection)
            {
                OptimizeGarbageCollection();
            }

            // AI optimizations
            if (enableAIBatching)
            {
                InitializeAIBatching();
            }

            // Performance monitoring
            if (enablePerformanceMonitoring)
            {
                InitializePerformanceMonitoring();
            }

            // Set target frame rate
            Application.targetFrameRate = targetFrameRate;

            Debug.Log("Performance optimizations initialized successfully");
        }

        void SetupOcclusionCulling()
        {
            // Enable occlusion culling for cameras
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                cam.useOcclusionCulling = true;
            }

            Debug.Log("Occlusion culling enabled for all cameras");
        }

        void SetupGPUInstancing()
        {
            // Enable GPU instancing for renderers
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer.material != null)
                {
                    renderer.material.enableInstancing = true;
                }
            }

            Debug.Log($"GPU instancing enabled for {renderers.Length} renderers");
        }

        void InitializeObjectPools()
        {
            objectPools = new Dictionary<string, Queue<GameObject>>();

            // Common pooled objects
            string[] pooledObjectTypes = {
                "VoterUI", "PoliticalEvent", "AIResponse", "NotificationPopup"
            };

            foreach (string objectType in pooledObjectTypes)
            {
                objectPools[objectType] = new Queue<GameObject>();
            }

            Debug.Log($"Object pools initialized for {pooledObjectTypes.Length} object types");
        }

        void OptimizeGarbageCollection()
        {
            // Configure garbage collection settings
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            // Schedule periodic GC during low activity
            StartCoroutine(PeriodicGarbageCollection());

            Debug.Log("Garbage collection optimization enabled");
        }

        IEnumerator PeriodicGarbageCollection()
        {
            while (true)
            {
                yield return new WaitForSeconds(30.0f); // Every 30 seconds

                // Only run GC during low frame rate (indicating low activity)
                if (1.0f / Time.unscaledDeltaTime < targetFrameRate * 0.8f)
                {
                    System.GC.Collect();
                }
            }
        }

        void InitializeAIBatching()
        {
            aiBatchQueue = new List<System.Func<IEnumerator>>();
            StartCoroutine(ProcessAIBatches());

            Debug.Log("AI request batching initialized");
        }

        IEnumerator ProcessAIBatches()
        {
            while (true)
            {
                yield return new WaitForSeconds(aiBatchInterval);

                if (aiBatchQueue.Count > 0)
                {
                    int batchSize = Mathf.Min(aiBatchQueue.Count, maxBatchSize);

                    for (int i = 0; i < batchSize; i++)
                    {
                        StartCoroutine(aiBatchQueue[i]());
                    }

                    aiBatchQueue.RemoveRange(0, batchSize);
                }
            }
        }

        void InitializePerformanceMonitoring()
        {
            GameObject monitorGO = new GameObject("PerformanceMonitor");
            performanceMonitor = monitorGO.AddComponent<PerformanceMonitor>();
            performanceMonitor.Initialize(monitoringInterval);

            Debug.Log("Performance monitoring initialized");
        }

        // Public methods for object pooling
        public GameObject GetPooledObject(string objectType)
        {
            if (objectPools.ContainsKey(objectType) && objectPools[objectType].Count > 0)
            {
                GameObject pooledObject = objectPools[objectType].Dequeue();
                pooledObject.SetActive(true);
                return pooledObject;
            }

            return null;
        }

        public void ReturnToPool(string objectType, GameObject obj)
        {
            if (objectPools.ContainsKey(objectType) && objectPools[objectType].Count < maxPoolSize)
            {
                obj.SetActive(false);
                objectPools[objectType].Enqueue(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        // Public method for AI request batching
        public void QueueAIRequest(System.Func<IEnumerator> aiRequest)
        {
            if (aiBatchQueue.Count < maxBatchSize * 2) // Prevent queue overflow
            {
                aiBatchQueue.Add(aiRequest);
            }
        }
    }

    public class PerformanceMonitor : MonoBehaviour
    {
        private float monitoringInterval;
        private float lastMonitorTime;
        private List<float> fpsHistory;
        private List<float> memoryHistory;

        public void Initialize(float interval)
        {
            monitoringInterval = interval;
            fpsHistory = new List<float>();
            memoryHistory = new List<float>();
            lastMonitorTime = Time.time;
        }

        void Update()
        {
            if (Time.time - lastMonitorTime >= monitoringInterval)
            {
                RecordPerformanceMetrics();
                lastMonitorTime = Time.time;
            }
        }

        void RecordPerformanceMetrics()
        {
            // Record FPS
            float currentFPS = 1.0f / Time.unscaledDeltaTime;
            fpsHistory.Add(currentFPS);

            // Record memory usage (in MB)
            float memoryUsage = System.GC.GetTotalMemory(false) / (1024f * 1024f);
            memoryHistory.Add(memoryUsage);

            // Keep only last 60 samples (1 minute of data)
            if (fpsHistory.Count > 60)
            {
                fpsHistory.RemoveAt(0);
            }
            if (memoryHistory.Count > 60)
            {
                memoryHistory.RemoveAt(0);
            }

            // Log performance warnings
            if (currentFPS < 30)
            {
                Debug.LogWarning($"Low FPS detected: {currentFPS:F1}");
            }

            if (memoryUsage > 1024) // 1GB
            {
                Debug.LogWarning($"High memory usage detected: {memoryUsage:F1}MB");
            }
        }

        public float GetAverageFPS()
        {
            if (fpsHistory.Count == 0) return 0;
            float sum = 0;
            foreach (float fps in fpsHistory)
            {
                sum += fps;
            }
            return sum / fpsHistory.Count;
        }

        public float GetAverageMemoryUsage()
        {
            if (memoryHistory.Count == 0) return 0;
            float sum = 0;
            foreach (float memory in memoryHistory)
            {
                sum += memory;
            }
            return sum / memoryHistory.Count;
        }
    }
}
