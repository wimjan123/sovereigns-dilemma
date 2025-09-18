using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SovereignsDilemma.UI.Core;

namespace SovereignsDilemma.UI.Components
{
    /// <summary>
    /// Individual social media post component with engagement mechanics.
    /// Optimized for object pooling and real-time content display.
    /// </summary>
    public class SocialMediaPostComponent : MonoBehaviour
    {
        [Header("Post Content")]
        [SerializeField] private Text authorText;
        [SerializeField] private Text timestampText;
        [SerializeField] private Text contentText;
        [SerializeField] private Transform tagContainer;
        [SerializeField] private GameObject tagPrefab;

        [Header("Engagement Elements")]
        [SerializeField] private Button likeButton;
        [SerializeField] private Text likeCountText;
        [SerializeField] private Button shareButton;
        [SerializeField] private Text shareCountText;
        [SerializeField] private Button commentButton;
        [SerializeField] private Text commentCountText;

        [Header("Visual Elements")]
        [SerializeField] private Image authorAvatar;
        [SerializeField] private Image postTypeIcon;
        [SerializeField] private Image engagementBar;
        [SerializeField] private CanvasGroup postCanvasGroup;

        [Header("Accessibility")]
        [SerializeField] private Button accessibilityButton;
        [SerializeField] private Text screenReaderText;

        // Post data
        private SocialMediaPost _currentPost;
        private bool _isLiked = false;
        private bool _isShared = false;

        // UI state
        private readonly List<GameObject> _tagObjects = new List<GameObject>();
        private bool _compactMode = false;
        private bool _accessibilityMode = false;

        // Animation
        private float _engagementAnimationSpeed = 2f;
        private Color _originalContentColor;

        // Post type colors
        private readonly Dictionary<SocialMediaPostType, Color> _postTypeColors = new Dictionary<SocialMediaPostType, Color>
        {
            { SocialMediaPostType.General, Color.gray },
            { SocialMediaPostType.PoliticalNews, Color.blue },
            { SocialMediaPostType.OpinionPiece, Color.yellow },
            { SocialMediaPostType.Crisis, Color.red },
            { SocialMediaPostType.Campaign, Color.green },
            { SocialMediaPostType.PolicyAnnouncement, Color.cyan }
        };

        private void Awake()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        private void Start()
        {
            if (contentText)
                _originalContentColor = contentText.color;
        }

        private void Update()
        {
            UpdateEngagementVisualization();
        }

        #region Initialization

        private void InitializeComponent()
        {
            if (postCanvasGroup == null)
                postCanvasGroup = GetComponent<CanvasGroup>();

            if (postCanvasGroup == null)
                postCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void SetupEventHandlers()
        {
            if (likeButton)
                likeButton.onClick.AddListener(OnLikeClicked);

            if (shareButton)
                shareButton.onClick.AddListener(OnShareClicked);

            if (commentButton)
                commentButton.onClick.AddListener(OnCommentClicked);

            if (accessibilityButton)
                accessibilityButton.onClick.AddListener(OnAccessibilityClicked);
        }

        #endregion

        #region Public Interface

        public void SetupPost(SocialMediaPost post)
        {
            _currentPost = post;
            UpdateDisplay();
        }

        public void ResetPost()
        {
            _currentPost = null;
            _isLiked = false;
            _isShared = false;
            ClearTags();

            if (postCanvasGroup)
                postCanvasGroup.alpha = 1f;
        }

        public void SetCompactMode(bool compact)
        {
            _compactMode = compact;

            // Hide less essential elements in compact mode
            if (tagContainer) tagContainer.gameObject.SetActive(!compact);
            if (engagementBar) engagementBar.gameObject.SetActive(!compact);
            if (authorAvatar) authorAvatar.gameObject.SetActive(!compact);

            // Reduce font size for content
            if (contentText && compact)
            {
                contentText.fontSize = Mathf.Max(10, contentText.fontSize - 2);
            }
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            _accessibilityMode = enabled;

            if (enabled)
            {
                // Increase text size
                if (contentText)
                    contentText.fontSize = Mathf.RoundToInt(contentText.fontSize * 1.2f);

                if (authorText)
                    authorText.fontSize = Mathf.RoundToInt(authorText.fontSize * 1.2f);

                // Ensure high contrast
                if (contentText)
                    contentText.color = Color.black;

                // Show accessibility button
                if (accessibilityButton)
                    accessibilityButton.gameObject.SetActive(true);
            }
            else
            {
                // Reset to original colors
                if (contentText)
                    contentText.color = _originalContentColor;

                // Hide accessibility button
                if (accessibilityButton)
                    accessibilityButton.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            if (_currentPost == null) return;

            UpdateAuthorInfo();
            UpdateContent();
            UpdateTimestamp();
            UpdateEngagementCounts();
            UpdatePostTypeVisualization();
            UpdateTags();
            UpdateScreenReaderContent();
        }

        private void UpdateAuthorInfo()
        {
            if (authorText)
                authorText.text = _currentPost.Author;

            if (authorAvatar)
            {
                // Generate a simple avatar based on author name hash
                Color avatarColor = GenerateAvatarColor(_currentPost.Author);
                authorAvatar.color = avatarColor;
            }
        }

        private void UpdateContent()
        {
            if (contentText)
                contentText.text = _currentPost.Content;
        }

        private void UpdateTimestamp()
        {
            if (timestampText)
            {
                TimeSpan timeAgo = DateTime.UtcNow - _currentPost.Timestamp;
                timestampText.text = FormatTimeAgo(timeAgo);
            }
        }

        private void UpdateEngagementCounts()
        {
            if (likeCountText)
            {
                likeCountText.text = FormatEngagementCount(_currentPost.Likes);
                UpdateButtonState(likeButton, _isLiked);
            }

            if (shareCountText)
            {
                shareCountText.text = FormatEngagementCount(_currentPost.Shares);
                UpdateButtonState(shareButton, _isShared);
            }

            if (commentCountText)
                commentCountText.text = FormatEngagementCount(_currentPost.Comments);
        }

        private void UpdatePostTypeVisualization()
        {
            if (postTypeIcon)
            {
                if (_postTypeColors.ContainsKey(_currentPost.Type))
                {
                    postTypeIcon.color = _postTypeColors[_currentPost.Type];
                    postTypeIcon.gameObject.SetActive(true);
                }
                else
                {
                    postTypeIcon.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateTags()
        {
            ClearTags();

            if (_currentPost.Tags == null || tagContainer == null || tagPrefab == null)
                return;

            foreach (string tag in _currentPost.Tags)
            {
                CreateTagElement(tag);
            }
        }

        private void UpdateScreenReaderContent()
        {
            if (!screenReaderText) return;

            string accessibleContent = $"Post by {_currentPost.Author}, " +
                                     $"posted {FormatTimeAgo(DateTime.UtcNow - _currentPost.Timestamp)} ago. " +
                                     $"Content: {_currentPost.Content}. " +
                                     $"Type: {_currentPost.Type}. " +
                                     $"{_currentPost.Likes} likes, {_currentPost.Shares} shares, {_currentPost.Comments} comments.";

            if (_currentPost.Tags != null && _currentPost.Tags.Count > 0)
            {
                accessibleContent += $" Tags: {string.Join(", ", _currentPost.Tags)}.";
            }

            screenReaderText.text = accessibleContent;
        }

        #endregion

        #region Tag Management

        private void CreateTagElement(string tag)
        {
            GameObject tagObj = Instantiate(tagPrefab, tagContainer);
            Text tagText = tagObj.GetComponentInChildren<Text>();

            if (tagText)
            {
                tagText.text = $"#{tag}";

                if (_accessibilityMode)
                {
                    tagText.fontSize = Mathf.RoundToInt(tagText.fontSize * 1.2f);
                }
            }

            _tagObjects.Add(tagObj);
        }

        private void ClearTags()
        {
            foreach (GameObject tagObj in _tagObjects)
            {
                if (tagObj) DestroyImmediate(tagObj);
            }
            _tagObjects.Clear();
        }

        #endregion

        #region Engagement Mechanics

        private void OnLikeClicked()
        {
            _isLiked = !_isLiked;

            if (_isLiked)
            {
                _currentPost.Likes++;
                AnimateEngagementAction(likeButton, Color.red);
            }
            else
            {
                _currentPost.Likes = Mathf.Max(0, _currentPost.Likes - 1);
            }

            UpdateEngagementCounts();
            TriggerEngagementEffect();
        }

        private void OnShareClicked()
        {
            if (_isShared) return; // Can only share once

            _isShared = true;
            _currentPost.Shares++;
            _currentPost.Engagement = Mathf.Min(1f, _currentPost.Engagement + 0.1f);

            AnimateEngagementAction(shareButton, Color.green);
            UpdateEngagementCounts();
            TriggerEngagementEffect();
        }

        private void OnCommentClicked()
        {
            // This would open a comment dialog in a full implementation
            // For now, just simulate adding engagement
            _currentPost.Comments++;
            _currentPost.Engagement = Mathf.Min(1f, _currentPost.Engagement + 0.05f);

            AnimateEngagementAction(commentButton, Color.blue);
            UpdateEngagementCounts();
            TriggerEngagementEffect();
        }

        private void OnAccessibilityClicked()
        {
            // Read out the post content (would integrate with text-to-speech)
            Debug.Log($"Screen reader: {screenReaderText.text}");
        }

        private void AnimateEngagementAction(Button button, Color highlightColor)
        {
            if (!button) return;

            // Simple color animation for feedback
            StartCoroutine(AnimateButtonHighlight(button, highlightColor));
        }

        private System.Collections.IEnumerator AnimateButtonHighlight(Button button, Color highlightColor)
        {
            Image buttonImage = button.GetComponent<Image>();
            if (!buttonImage) yield break;

            Color originalColor = buttonImage.color;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float pingPong = Mathf.PingPong(t * 2f, 1f);
                buttonImage.color = Color.Lerp(originalColor, highlightColor, pingPong * 0.5f);
                yield return null;
            }

            buttonImage.color = originalColor;
        }

        private void UpdateButtonState(Button button, bool isActive)
        {
            if (!button) return;

            ColorBlock colors = button.colors;
            colors.normalColor = isActive ? Color.yellow : Color.white;
            button.colors = colors;
        }

        private void TriggerEngagementEffect()
        {
            // Update overall engagement visualization
            if (_currentPost != null)
            {
                float totalEngagement = _currentPost.Likes + _currentPost.Shares + _currentPost.Comments;
                _currentPost.Engagement = Mathf.Clamp01(totalEngagement / 100f); // Normalize to 0-1
            }
        }

        #endregion

        #region Visual Updates

        private void UpdateEngagementVisualization()
        {
            if (!engagementBar || _currentPost == null) return;

            // Animate engagement bar fill
            Image barFill = engagementBar;
            if (barFill)
            {
                float targetFill = _currentPost.Engagement;
                float currentFill = barFill.fillAmount;
                barFill.fillAmount = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * _engagementAnimationSpeed);

                // Color based on engagement level
                Color barColor = Color.Lerp(Color.gray, Color.gold, _currentPost.Engagement);
                barFill.color = barColor;
            }
        }

        #endregion

        #region Utility Methods

        private Color GenerateAvatarColor(string authorName)
        {
            // Generate consistent color based on author name
            int hash = authorName.GetHashCode();
            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(hash);

            Color color = Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 0.8f);

            UnityEngine.Random.state = oldState;
            return color;
        }

        private string FormatTimeAgo(TimeSpan timeAgo)
        {
            if (timeAgo.TotalMinutes < 1)
                return "now";
            else if (timeAgo.TotalMinutes < 60)
                return $"{(int)timeAgo.TotalMinutes}m";
            else if (timeAgo.TotalHours < 24)
                return $"{(int)timeAgo.TotalHours}h";
            else
                return $"{(int)timeAgo.TotalDays}d";
        }

        private string FormatEngagementCount(int count)
        {
            if (count < 1000)
                return count.ToString();
            else if (count < 1000000)
                return $"{count / 1000f:F1}K";
            else
                return $"{count / 1000000f:F1}M";
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            ClearTags();
        }

        #endregion
    }
}