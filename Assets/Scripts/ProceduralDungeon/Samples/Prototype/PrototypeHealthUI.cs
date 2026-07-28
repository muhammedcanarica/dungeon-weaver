using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralDungeon
{
    public sealed class PrototypeHealthUI : MonoBehaviour
    {
        private const float SegmentSize = 24f;
        private const float SegmentSpacing = 4f;

        [SerializeField] private PrototypePlayerHealth playerHealth;
        [SerializeField] private Color filledColor = new Color(0.24f, 0.93f, 0.48f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.13f, 0.17f, 0.23f, 0.9f);

        private readonly List<Image> segments = new List<Image>();
        private RectTransform root;
        private Sprite segmentSprite;
        private bool subscribed;

        public int DisplayedHealth { get; private set; }
        public int DisplayedMaxHealth { get; private set; }
        public int SegmentCount => segments.Count;

        private void OnEnable()
        {
            ResolveHealth();
            Subscribe();
            SyncFromHealth();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (segmentSprite != null) Destroy(segmentSprite);
        }

        public bool IsSegmentFilled(int index)
        {
            return index >= 0 && index < DisplayedHealth;
        }

        public bool IsSegmentRaycastTarget(int index)
        {
            return index >= 0 && index < segments.Count && segments[index].raycastTarget;
        }

        private void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            SetDisplayedHealth(currentHealth, maxHealth);
        }

        private void ResolveHealth()
        {
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PrototypePlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogWarning("[PrototypeHealthUI] Disabled because PrototypePlayerHealth is missing.", this);
                enabled = false;
            }
        }

        private void Subscribe()
        {
            if (playerHealth == null || subscribed) return;
            playerHealth.HealthChanged += HandleHealthChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (playerHealth != null && subscribed) playerHealth.HealthChanged -= HandleHealthChanged;
            subscribed = false;
        }

        private void SyncFromHealth()
        {
            if (playerHealth != null) SetDisplayedHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void SetDisplayedHealth(int currentHealth, int maxHealth)
        {
            DisplayedMaxHealth = Mathf.Max(1, maxHealth);
            DisplayedHealth = Mathf.Clamp(currentHealth, 0, DisplayedMaxHealth);
            EnsureSegments(DisplayedMaxHealth);
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].color = IsSegmentFilled(i) ? filledColor : emptyColor;
            }
        }

        private void EnsureSegments(int count)
        {
            EnsureRoot();
            if (segments.Count == count) return;

            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            segments.Clear();
            for (int i = 0; i < count; i++)
            {
                var segmentObject = new GameObject($"Health Segment {i + 1}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                segmentObject.transform.SetParent(root, false);
                RectTransform rectTransform = segmentObject.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0f, 0.5f);
                rectTransform.anchorMax = new Vector2(0f, 0.5f);
                rectTransform.pivot = new Vector2(0f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(i * (SegmentSize + SegmentSpacing), 0f);
                rectTransform.sizeDelta = new Vector2(SegmentSize, SegmentSize);

                Image image = segmentObject.GetComponent<Image>();
                image.sprite = GetSegmentSprite();
                image.raycastTarget = false;
                segments.Add(image);
            }

            root.sizeDelta = new Vector2(count * SegmentSize + (count - 1) * SegmentSpacing,
                SegmentSize);
        }

        private void EnsureRoot()
        {
            if (root != null) return;
            var rootObject = new GameObject("Prototype Health UI", typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.one;
            root.anchorMax = Vector2.one;
            root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-24f, -24f);
        }

        private Sprite GetSegmentSprite()
        {
            if (segmentSprite == null)
            {
                segmentSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            }

            return segmentSprite;
        }
    }
}
