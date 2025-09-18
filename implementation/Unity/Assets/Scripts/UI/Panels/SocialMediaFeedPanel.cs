using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SovereignsDilemma.UI.Core;
using SovereignsDilemma.UI.Components;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Panels
{
    /// <summary>
    /// Social media feed panel displaying AI-generated political posts with engagement mechanics.
    /// Implements object pooling for performance and real-time content updates.
    /// </summary>
    public class SocialMediaFeedPanel : MonoBehaviour, IUIComponent
    {
        [Header("Feed Configuration")]
        [SerializeField] private ScrollRect feedScrollRect;
        [SerializeField] private Transform feedContent;
        [SerializeField] private GameObject postPrefab;
        [SerializeField] private int maxPostsDisplayed = 50;
        [SerializeField] private int postsPerBatch = 10;

        [Header("Feed Controls")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button autoUpdateToggle;
        [SerializeField] private Dropdown filterDropdown;
        [SerializeField] private InputField searchInput;

        [Header("Feed Stats")]
        [SerializeField] private Text totalPostsText;
        [SerializeField] private Text activeEventsText;
        [SerializeField] private Text tensionLevelText;
        [SerializeField] private Text sentimentText;

        [Header("Trending Topics")]
        [SerializeField] private Transform trendingContainer;
        [SerializeField] private GameObject trendingTopicPrefab;

        [Header("Auto-Update Settings")]
        [SerializeField] private float autoUpdateInterval = 15f;
        [SerializeField] private bool enableAutoUpdate = true;
        [SerializeField] private AnimationCurve newPostAnimation = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Performance")]
        [SerializeField] private int poolSize = 20;
        [SerializeField] private bool enableObjectPooling = true;
        [SerializeField] private float cullDistance = 1000f;

        // Data state
        private SocialMediaData _currentData;
        private bool _hasData = false;
        private bool _isVisible = true;

        // Feed management
        private readonly Queue<SocialMediaPostComponent> _postPool = new Queue<SocialMediaPostComponent>();
        private readonly List<SocialMediaPostComponent> _activePosts = new List<SocialMediaPostComponent>();
        private readonly List<SocialMediaPost> _allPosts = new List<SocialMediaPost>();

        // Filtering and search
        private SocialMediaPostType _currentFilter = SocialMediaPostType.General;
        private string _currentSearchTerm = "";

        // Auto-update state
        private float _lastUpdateTime;
        private bool _autoUpdateEnabled = true;

        // Animation state
        private readonly Queue<SocialMediaPostComponent> _newPostAnimationQueue = new Queue<SocialMediaPostComponent>();

        // Trending topics
        private readonly List<GameObject> _trendingTopicObjects = new List<GameObject>();

        // Performance tracking
        private readonly ProfilerMarker _updateMarker = new("SocialMediaFeed.Update");
        private readonly ProfilerMarker _poolingMarker = new("SocialMediaFeed.Pooling");

        public Type GetDataType() => typeof(SocialMediaData);
        public bool IsVisible() => _isVisible;

        private void Awake()
        {
            InitializePanel();
            SetupEventHandlers();
            InitializeObjectPool();
        }

        private void Start()
        {
            InitializeFilterDropdown();
            SetupAutoUpdate();
        }

        private void Update()
        {
            using (_updateMarker.Auto())
            {
                HandleAutoUpdate();
                ProcessNewPostAnimations();
                OptimizeActivePostRendering();
            }
        }

        #region Initialization

        private void InitializePanel()
        {
            _lastUpdateTime = Time.time;
            _autoUpdateEnabled = enableAutoUpdate;

            // Initialize with sample data for preview
            GenerateSamplePosts();
        }

        private void SetupEventHandlers()
        {
            if (refreshButton)
                refreshButton.onClick.AddListener(RefreshFeed);

            if (autoUpdateToggle)
                autoUpdateToggle.onClick.AddListener(ToggleAutoUpdate);

            if (filterDropdown)
                filterDropdown.onValueChanged.AddListener(OnFilterChanged);

            if (searchInput)
                searchInput.onValueChanged.AddListener(OnSearchChanged);
        }

        private void InitializeObjectPool()
        {
            if (!enableObjectPooling || !postPrefab) return;

            using (_poolingMarker.Auto())
            {
                for (int i = 0; i < poolSize; i++)
                {
                    GameObject postObj = Instantiate(postPrefab, feedContent);
                    SocialMediaPostComponent postComponent = postObj.GetComponent<SocialMediaPostComponent>();

                    if (postComponent == null)
                        postComponent = postObj.AddComponent<SocialMediaPostComponent>();

                    postObj.SetActive(false);
                    _postPool.Enqueue(postComponent);
                }
            }
        }

        private void InitializeFilterDropdown()
        {
            if (!filterDropdown) return;

            filterDropdown.ClearOptions();
            var options = new List<string> { "All" };

            foreach (SocialMediaPostType postType in Enum.GetValues(typeof(SocialMediaPostType)))
            {
                options.Add(postType.ToString());
            }

            filterDropdown.AddOptions(options);
        }

        private void SetupAutoUpdate()
        {
            if (autoUpdateToggle)
            {
                var colors = autoUpdateToggle.colors;
                colors.normalColor = _autoUpdateEnabled ? Color.green : Color.gray;
                autoUpdateToggle.colors = colors;
            }
        }

        #endregion

        #region Public Interface

        public void UpdateData(object data)
        {
            if (data is SocialMediaData socialMediaData)
            {
                UpdateData(socialMediaData);
            }
        }

        public void UpdateData(SocialMediaData data)
        {
            _currentData = data;
            _hasData = data.HasData;

            if (_hasData)
            {
                UpdateFeedContent();
                UpdateFeedStats();
                UpdateTrendingTopics();
            }
        }

        public void SetVisibility(bool visible)
        {
            _isVisible = visible;
            gameObject.SetActive(visible);
        }

        public void SetCompactMode(bool compact)
        {
            // Hide trending topics and reduce post detail in compact mode
            if (trendingContainer) trendingContainer.gameObject.SetActive(!compact);

            foreach (var post in _activePosts)
            {
                if (post) post.SetCompactMode(compact);
            }
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            foreach (var post in _activePosts)
            {
                if (post) post.EnableAccessibilityMode(enabled);
            }
        }

        #endregion

        #region Feed Management

        private void UpdateFeedContent()
        {
            if (_currentData.RecentPosts == null) return;

            // Add new posts to our collection
            foreach (var newPost in _currentData.RecentPosts)
            {
                if (!_allPosts.Any(p => p.Content == newPost.Content && p.Timestamp == newPost.Timestamp))
                {
                    _allPosts.Add(newPost);
                }
            }

            // Sort posts by timestamp (newest first)
            _allPosts.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            // Limit total posts to prevent memory issues
            if (_allPosts.Count > maxPostsDisplayed * 2)
            {
                _allPosts.RemoveRange(maxPostsDisplayed * 2, _allPosts.Count - maxPostsDisplayed * 2);
            }

            RefreshDisplayedPosts();
        }

        private void RefreshDisplayedPosts()
        {
            // Return all active posts to pool
            ReturnAllPostsToPool();

            // Get filtered posts
            var filteredPosts = GetFilteredPosts();

            // Limit to max displayed
            var postsToShow = filteredPosts.Take(maxPostsDisplayed).ToList();

            // Create or reuse post components
            for (int i = 0; i < postsToShow.Count; i++)
            {
                var post = postsToShow[i];
                var postComponent = GetPostFromPool();

                if (postComponent)
                {
                    postComponent.SetupPost(post);
                    postComponent.transform.SetSiblingIndex(i);
                    _activePosts.Add(postComponent);

                    // Queue new posts for animation
                    if (post.Timestamp > DateTime.UtcNow.AddMinutes(-1))
                    {
                        _newPostAnimationQueue.Enqueue(postComponent);
                    }
                }
            }

            // Update scroll position to show new posts
            if (_newPostAnimationQueue.Count > 0)
            {
                feedScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private List<SocialMediaPost> GetFilteredPosts()
        {
            var filteredPosts = _allPosts.AsEnumerable();

            // Apply type filter
            if (_currentFilter != SocialMediaPostType.General || filterDropdown.value > 0)
            {
                if (filterDropdown.value > 0)
                {
                    var selectedType = (SocialMediaPostType)(filterDropdown.value - 1);
                    filteredPosts = filteredPosts.Where(p => p.Type == selectedType);
                }
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_currentSearchTerm))
            {
                string searchLower = _currentSearchTerm.ToLower();
                filteredPosts = filteredPosts.Where(p =>
                    p.Content.ToLower().Contains(searchLower) ||
                    p.Author.ToLower().Contains(searchLower) ||
                    p.Tags.Any(tag => tag.ToLower().Contains(searchLower))
                );
            }

            return filteredPosts.ToList();
        }

        #endregion

        #region Object Pooling

        private SocialMediaPostComponent GetPostFromPool()
        {
            if (!enableObjectPooling) return CreateNewPost();

            using (_poolingMarker.Auto())
            {
                if (_postPool.Count > 0)
                {
                    var post = _postPool.Dequeue();
                    post.gameObject.SetActive(true);
                    return post;
                }
                else
                {
                    return CreateNewPost();
                }
            }
        }

        private SocialMediaPostComponent CreateNewPost()
        {
            if (!postPrefab) return null;

            GameObject postObj = Instantiate(postPrefab, feedContent);
            SocialMediaPostComponent postComponent = postObj.GetComponent<SocialMediaPostComponent>();

            if (postComponent == null)
                postComponent = postObj.AddComponent<SocialMediaPostComponent>();

            return postComponent;
        }

        private void ReturnPostToPool(SocialMediaPostComponent post)
        {
            if (!enableObjectPooling)
            {
                if (post && post.gameObject) DestroyImmediate(post.gameObject);
                return;
            }

            if (post && post.gameObject)
            {
                post.gameObject.SetActive(false);
                post.ResetPost();
                _postPool.Enqueue(post);
            }
        }

        private void ReturnAllPostsToPool()
        {
            foreach (var post in _activePosts)
            {
                ReturnPostToPool(post);
            }
            _activePosts.Clear();
        }

        #endregion

        #region Auto-Update System

        private void HandleAutoUpdate()
        {
            if (!_autoUpdateEnabled) return;

            if (Time.time - _lastUpdateTime >= autoUpdateInterval)
            {
                GenerateNewPost();
                _lastUpdateTime = Time.time;
            }
        }

        private void GenerateNewPost()
        {
            // This would typically call the AI service to generate new content
            // For now, we'll generate placeholder posts

            var newPost = new SocialMediaPost
            {
                Content = GenerateRandomPostContent(),
                Author = GenerateRandomAuthor(),
                Timestamp = DateTime.UtcNow,
                Engagement = UnityEngine.Random.Range(0.1f, 0.9f),
                Likes = UnityEngine.Random.Range(5, 500),
                Shares = UnityEngine.Random.Range(0, 50),
                Comments = UnityEngine.Random.Range(0, 100),
                Type = GetRandomPostType(),
                Tags = GenerateRandomTags()
            };

            _allPosts.Insert(0, newPost);

            if (_allPosts.Count > maxPostsDisplayed * 2)
            {
                _allPosts.RemoveAt(_allPosts.Count - 1);
            }

            // Update current data
            if (_currentData.RecentPosts == null)
                _currentData.RecentPosts = new List<SocialMediaPost>();

            _currentData.RecentPosts.Insert(0, newPost);

            RefreshDisplayedPosts();
        }

        #endregion

        #region Animation System

        private void ProcessNewPostAnimations()
        {
            if (_newPostAnimationQueue.Count == 0) return;

            var post = _newPostAnimationQueue.Peek();
            if (post && post.gameObject.activeInHierarchy)
            {
                // Simple fade-in animation
                CanvasGroup canvasGroup = post.GetComponent<CanvasGroup>();
                if (!canvasGroup) canvasGroup = post.gameObject.AddComponent<CanvasGroup>();

                float animationTime = 0.5f;
                float alpha = Mathf.Lerp(0f, 1f, Time.deltaTime / animationTime);
                canvasGroup.alpha = alpha;

                if (alpha >= 0.95f)
                {
                    canvasGroup.alpha = 1f;
                    _newPostAnimationQueue.Dequeue();
                }
            }
            else
            {
                _newPostAnimationQueue.Dequeue();
            }
        }

        #endregion

        #region Optimization

        private void OptimizeActivePostRendering()
        {
            // Cull posts that are far outside the viewport
            if (!feedScrollRect) return;

            var viewport = feedScrollRect.viewport;
            var viewportBounds = new Bounds(viewport.position, viewport.rect.size);

            foreach (var post in _activePosts)
            {
                if (!post) continue;

                var postRect = post.GetComponent<RectTransform>();
                if (!postRect) continue;

                float distance = Vector3.Distance(postRect.position, viewport.position);
                bool shouldRender = distance <= cullDistance;

                // Enable/disable components to save performance
                var canvasGroup = post.GetComponent<CanvasGroup>();
                if (canvasGroup)
                {
                    canvasGroup.alpha = shouldRender ? 1f : 0f;
                    canvasGroup.interactable = shouldRender;
                    canvasGroup.blocksRaycasts = shouldRender;
                }
            }
        }

        #endregion

        #region UI Event Handlers

        private void RefreshFeed()
        {
            RefreshDisplayedPosts();
        }

        private void ToggleAutoUpdate()
        {
            _autoUpdateEnabled = !_autoUpdateEnabled;
            SetupAutoUpdate();
        }

        private void OnFilterChanged(int filterIndex)
        {
            if (filterIndex == 0)
            {
                _currentFilter = SocialMediaPostType.General;
            }
            else
            {
                _currentFilter = (SocialMediaPostType)(filterIndex - 1);
            }

            RefreshDisplayedPosts();
        }

        private void OnSearchChanged(string searchTerm)
        {
            _currentSearchTerm = searchTerm;
            RefreshDisplayedPosts();
        }

        #endregion

        #region Stats and Trending

        private void UpdateFeedStats()
        {
            if (totalPostsText)
                totalPostsText.text = $"Posts: {_allPosts.Count}";

            if (activeEventsText)
                activeEventsText.text = $"Events: {_currentData.ActiveEvents}";

            if (tensionLevelText)
            {
                tensionLevelText.text = $"Tension: {_currentData.PoliticalTension:P1}";
                tensionLevelText.color = Color.Lerp(Color.green, Color.red, _currentData.PoliticalTension);
            }

            if (sentimentText)
            {
                string sentimentStr = _currentData.OverallSentiment > 0.1f ? "Positive" :
                                    _currentData.OverallSentiment < -0.1f ? "Negative" : "Neutral";
                sentimentText.text = $"Sentiment: {sentimentStr}";
                sentimentText.color = _currentData.OverallSentiment > 0 ? Color.green :
                                    _currentData.OverallSentiment < 0 ? Color.red : Color.yellow;
            }
        }

        private void UpdateTrendingTopics()
        {
            if (!trendingContainer || _currentData.TrendingTopics == null) return;

            ClearTrendingTopics();

            var sortedTopics = _currentData.TrendingTopics
                .OrderByDescending(kvp => kvp.Value)
                .Take(5);

            foreach (var topic in sortedTopics)
            {
                CreateTrendingTopicItem(topic.Key, topic.Value);
            }
        }

        private void CreateTrendingTopicItem(string topic, int engagement)
        {
            if (!trendingTopicPrefab) return;

            GameObject topicObj = Instantiate(trendingTopicPrefab, trendingContainer);

            Text topicText = topicObj.GetComponentInChildren<Text>();
            if (topicText)
            {
                topicText.text = $"#{topic} ({engagement})";
            }

            _trendingTopicObjects.Add(topicObj);
        }

        private void ClearTrendingTopics()
        {
            foreach (var topicObj in _trendingTopicObjects)
            {
                if (topicObj) DestroyImmediate(topicObj);
            }
            _trendingTopicObjects.Clear();
        }

        #endregion

        #region Sample Data Generation

        private void GenerateSamplePosts()
        {
            for (int i = 0; i < 15; i++)
            {
                var post = new SocialMediaPost
                {
                    Content = GenerateRandomPostContent(),
                    Author = GenerateRandomAuthor(),
                    Timestamp = DateTime.UtcNow.AddMinutes(-UnityEngine.Random.Range(1, 120)),
                    Engagement = UnityEngine.Random.Range(0.1f, 0.9f),
                    Likes = UnityEngine.Random.Range(5, 500),
                    Shares = UnityEngine.Random.Range(0, 50),
                    Comments = UnityEngine.Random.Range(0, 100),
                    Type = GetRandomPostType(),
                    Tags = GenerateRandomTags()
                };

                _allPosts.Add(post);
            }

            _currentData = new SocialMediaData(true);
            _currentData.RecentPosts = new List<SocialMediaPost>(_allPosts);
            _currentData.ActiveEvents = UnityEngine.Random.Range(3, 12);
            _currentData.PoliticalTension = UnityEngine.Random.Range(0.2f, 0.8f);
            _currentData.OverallSentiment = UnityEngine.Random.Range(-0.5f, 0.5f);
            _currentData.TrendingTopics = new Dictionary<string, int>
            {
                { "ClimateAction", 1240 },
                { "EconomicPolicy", 980 },
                { "Healthcare", 756 },
                { "Education", 634 },
                { "Immigration", 589 }
            };
            _hasData = true;
        }

        private string GenerateRandomPostContent()
        {
            string[] samples = {
                "Breaking: New climate policy announced by government coalition",
                "Economic indicators show positive growth in renewable sector",
                "Citizens rally for better healthcare accessibility",
                "Education reform bill passes first reading in parliament",
                "Local communities organize environmental cleanup initiative",
                "Political analysts discuss coalition stability ahead of elections",
                "Tech industry leaders call for digital innovation policies",
                "Agricultural sector adapts to new sustainability guidelines"
            };

            return samples[UnityEngine.Random.Range(0, samples.Length)];
        }

        private string GenerateRandomAuthor()
        {
            string[] names = {
                "NewsNetherlands", "PolitiekNL", "CitizenVoice", "DutchDaily",
                "ParliamentWatch", "EcoActivist", "PolicyExpert", "LocalReporter"
            };

            return names[UnityEngine.Random.Range(0, names.Length)];
        }

        private SocialMediaPostType GetRandomPostType()
        {
            var types = Enum.GetValues(typeof(SocialMediaPostType));
            return (SocialMediaPostType)types.GetValue(UnityEngine.Random.Range(0, types.Length));
        }

        private List<string> GenerateRandomTags()
        {
            string[] allTags = {
                "politics", "climate", "economy", "healthcare", "education",
                "environment", "policy", "government", "coalition", "reform"
            };

            var tags = new List<string>();
            int tagCount = UnityEngine.Random.Range(1, 4);

            for (int i = 0; i < tagCount; i++)
            {
                string tag = allTags[UnityEngine.Random.Range(0, allTags.Length)];
                if (!tags.Contains(tag))
                    tags.Add(tag);
            }

            return tags;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            ReturnAllPostsToPool();
            ClearTrendingTopics();
        }

        #endregion
    }
}