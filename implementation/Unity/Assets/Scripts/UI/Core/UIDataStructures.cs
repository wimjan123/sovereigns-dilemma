using System;
using System.Collections.Generic;
using UnityEngine;

namespace SovereignsDilemma.UI.Core
{
    /// <summary>
    /// Data structures for UI data binding and component communication.
    /// Provides efficient data transfer between simulation and UI systems.
    /// </summary>

    // Base interface for UI components
    public interface IUIComponent
    {
        Type GetDataType();
        void UpdateData(object data);
        bool IsVisible();
        void SetVisibility(bool visible);
    }

    // Base interface for UI data
    public interface IUIData
    {
        bool HasData { get; }
        DateTime LastUpdated { get; }
    }

    // Voter Analytics Data
    [Serializable]
    public struct VoterAnalyticsData : IUIData
    {
        public int TotalVoters;
        public int ActiveVoters;
        public int DormantVoters;
        public float[] AgeDistribution; // [18-25, 26-35, 36-50, 51-65, 65+]
        public float[] EducationDistribution; // [Primary, Secondary, Vocational, Higher]
        public float[] IncomeDistribution; // [Low, Middle, High]
        public float AveragePoliticalEngagement;
        public float AverageSocialInfluence;
        public bool HasData { get; set; }
        public DateTime LastUpdated { get; set; }

        public VoterAnalyticsData(bool initialize)
        {
            TotalVoters = 0;
            ActiveVoters = 0;
            DormantVoters = 0;
            AgeDistribution = new float[5];
            EducationDistribution = new float[4];
            IncomeDistribution = new float[3];
            AveragePoliticalEngagement = 0f;
            AverageSocialInfluence = 0f;
            HasData = false;
            LastUpdated = DateTime.UtcNow;
        }
    }

    // Political Spectrum Data
    [Serializable]
    public struct PoliticalSpectrumData : IUIData
    {
        public float[] EconomicSpectrum; // Distribution across economic left-right spectrum (10 bins)
        public float[] SocialSpectrum; // Distribution across social conservative-progressive spectrum (10 bins)
        public float[] EnvironmentalSpectrum; // Distribution across environmental spectrum (10 bins)
        public Dictionary<string, float> PartySupport; // Party name -> support percentage
        public float PoliticalTension; // Overall political tension level (0-1)
        public Vector2 MedianPosition; // Median voter position (economic, social)
        public bool HasData { get; set; }
        public DateTime LastUpdated { get; set; }

        public PoliticalSpectrumData(bool initialize)
        {
            EconomicSpectrum = new float[10];
            SocialSpectrum = new float[10];
            EnvironmentalSpectrum = new float[10];
            PartySupport = new Dictionary<string, float>();
            PoliticalTension = 0f;
            MedianPosition = Vector2.zero;
            HasData = false;
            LastUpdated = DateTime.UtcNow;
        }
    }

    // Social Media Data
    [Serializable]
    public struct SocialMediaData : IUIData
    {
        public List<SocialMediaPost> RecentPosts;
        public int ActiveEvents;
        public float PoliticalTension;
        public Dictionary<string, int> TrendingTopics; // Topic -> engagement count
        public float OverallSentiment; // -1 (negative) to 1 (positive)
        public bool HasData { get; set; }
        public DateTime LastUpdated { get; set; }

        public SocialMediaData(bool initialize)
        {
            RecentPosts = new List<SocialMediaPost>();
            ActiveEvents = 0;
            PoliticalTension = 0f;
            TrendingTopics = new Dictionary<string, int>();
            OverallSentiment = 0f;
            HasData = false;
            LastUpdated = DateTime.UtcNow;
        }
    }

    // Performance Data
    [Serializable]
    public struct PerformanceData : IUIData
    {
        public float CurrentFPS;
        public float AverageFPS;
        public float MemoryUsage; // MB
        public float CPUUsage; // Percentage
        public int EventQueueSize;
        public int EventsProcessed;
        public float AIResponseTime; // Seconds
        public int ActiveVoterCount;
        public bool HasData { get; set; }
        public DateTime LastUpdated { get; set; }

        public PerformanceData(bool initialize)
        {
            CurrentFPS = 0f;
            AverageFPS = 0f;
            MemoryUsage = 0f;
            CPUUsage = 0f;
            EventQueueSize = 0;
            EventsProcessed = 0;
            AIResponseTime = 0f;
            ActiveVoterCount = 0;
            HasData = false;
            LastUpdated = DateTime.UtcNow;
        }
    }

    // Social Media Post
    [Serializable]
    public class SocialMediaPost
    {
        public string Content;
        public string Author;
        public DateTime Timestamp;
        public float Engagement; // 0-1 scale
        public int Likes;
        public int Shares;
        public int Comments;
        public SocialMediaPostType Type;
        public List<string> Tags;

        public SocialMediaPost()
        {
            Content = "";
            Author = "Anonymous";
            Timestamp = DateTime.UtcNow;
            Engagement = 0f;
            Likes = 0;
            Shares = 0;
            Comments = 0;
            Type = SocialMediaPostType.General;
            Tags = new List<string>();
        }
    }

    public enum SocialMediaPostType
    {
        General,
        PoliticalNews,
        OpinionPiece,
        Crisis,
        Campaign,
        PolicyAnnouncement
    }

    // UI Configuration Data
    [Serializable]
    public struct UIConfigurationData : IUIData
    {
        public bool PerformanceMonitorEnabled;
        public float UIUpdateFrequency;
        public bool AccessibilityMode;
        public bool HighContrastMode;
        public float UIScale;
        public UILayoutMode PreferredLayoutMode;
        public Dictionary<string, bool> PanelVisibility;
        public bool HasData { get; set; }
        public DateTime LastUpdated { get; set; }

        public UIConfigurationData(bool initialize)
        {
            PerformanceMonitorEnabled = false;
            UIUpdateFrequency = 0.1f;
            AccessibilityMode = false;
            HighContrastMode = false;
            UIScale = 1.0f;
            PreferredLayoutMode = UILayoutMode.Standard;
            PanelVisibility = new Dictionary<string, bool>();
            HasData = false;
            LastUpdated = DateTime.UtcNow;
        }
    }

    // AI Service Configuration Data
    [Serializable]
    public struct AIServiceConfigurationData : IUIData
    {
        public string SelectedProvider;
        public bool IsConnected;
        public float ResponseTime;
        public bool OfflineModeEnabled;
        public int CacheHitRate; // Percentage
        public string LastError;
        public Dictionary<string, bool> ProviderAvailability;
        public bool HasData { get; set; }
        public DateTime LastUpdated { get; set; }

        public AIServiceConfigurationData(bool initialize)
        {
            SelectedProvider = "NVIDIA NIM";
            IsConnected = false;
            ResponseTime = 0f;
            OfflineModeEnabled = false;
            CacheHitRate = 0;
            LastError = "";
            ProviderAvailability = new Dictionary<string, bool>();
            HasData = false;
            LastUpdated = DateTime.UtcNow;
        }
    }

    // Chart and visualization data structures
    [Serializable]
    public class ChartDataPoint
    {
        public float X;
        public float Y;
        public string Label;
        public Color Color;

        public ChartDataPoint(float x, float y, string label = "", Color color = default)
        {
            X = x;
            Y = y;
            Label = label;
            Color = color == default ? Color.white : color;
        }
    }

    [Serializable]
    public class ChartDataSeries
    {
        public string Name;
        public List<ChartDataPoint> Points;
        public Color SeriesColor;
        public ChartType Type;

        public ChartDataSeries(string name, ChartType type = ChartType.Line)
        {
            Name = name;
            Points = new List<ChartDataPoint>();
            SeriesColor = Color.white;
            Type = type;
        }
    }

    public enum ChartType
    {
        Line,
        Bar,
        Pie,
        Scatter,
        Heatmap
    }

    // Heat map data for political spectrum visualization
    [Serializable]
    public struct HeatmapData
    {
        public int Width;
        public int Height;
        public float[,] Values; // 2D array of values (0-1)
        public Color[] ColorMap; // Color gradient for visualization
        public string XAxisLabel;
        public string YAxisLabel;

        public HeatmapData(int width, int height)
        {
            Width = width;
            Height = height;
            Values = new float[width, height];
            ColorMap = new Color[0];
            XAxisLabel = "";
            YAxisLabel = "";
        }
    }

    // Animation and transition data
    [Serializable]
    public struct UIAnimationData
    {
        public AnimationType Type;
        public float Duration;
        public AnimationCurve Curve;
        public Vector3 StartValue;
        public Vector3 EndValue;
        public bool IsPlaying;

        public UIAnimationData(AnimationType type, float duration)
        {
            Type = type;
            Duration = duration;
            Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            StartValue = Vector3.zero;
            EndValue = Vector3.zero;
            IsPlaying = false;
        }
    }

    public enum AnimationType
    {
        FadeIn,
        FadeOut,
        SlideIn,
        SlideOut,
        Scale,
        Rotate,
        Color
    }

    // Accessibility data
    [Serializable]
    public struct AccessibilityData
    {
        public bool ScreenReaderMode;
        public bool HighContrastMode;
        public bool KeyboardNavigationMode;
        public float FontSizeMultiplier;
        public bool ReducedMotion;
        public Dictionary<string, string> AriaLabels;

        public AccessibilityData(bool initialize)
        {
            ScreenReaderMode = false;
            HighContrastMode = false;
            KeyboardNavigationMode = false;
            FontSizeMultiplier = 1.0f;
            ReducedMotion = false;
            AriaLabels = new Dictionary<string, string>();
        }
    }

    // Event data for UI notifications
    [Serializable]
    public class UINotificationData
    {
        public string Title;
        public string Message;
        public NotificationType Type;
        public float Duration;
        public DateTime Timestamp;
        public bool IsDismissible;
        public string ActionText;
        public Action OnActionClicked;

        public UINotificationData(string title, string message, NotificationType type = NotificationType.Info, float duration = 5f)
        {
            Title = title;
            Message = message;
            Type = type;
            Duration = duration;
            Timestamp = DateTime.UtcNow;
            IsDismissible = true;
            ActionText = "";
            OnActionClicked = null;
        }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        Political
    }

    // Responsive layout data
    [Serializable]
    public struct ResponsiveLayoutData
    {
        public UILayoutMode CurrentMode;
        public Vector2 ScreenSize;
        public float AspectRatio;
        public bool IsPortrait;
        public Dictionary<string, RectTransform> PanelTransforms;

        public ResponsiveLayoutData(UILayoutMode mode, Vector2 screenSize)
        {
            CurrentMode = mode;
            ScreenSize = screenSize;
            AspectRatio = screenSize.x / screenSize.y;
            IsPortrait = AspectRatio < 1.0f;
            PanelTransforms = new Dictionary<string, RectTransform>();
        }
    }
}