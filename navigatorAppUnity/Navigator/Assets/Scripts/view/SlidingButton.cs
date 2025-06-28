using System.Collections;
using System.Collections.Generic;
using controller;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using model;
using Newtonsoft.Json;
using TMPro;

namespace view
{
    public class SlidingButton : MonoBehaviour
    {
        public float slideDuration = 0.3f;
        public float hiddenOffsetX = 50f;

        private Button button;
        private RectTransform rect;
        private RectTransform canvasRect;
        private Vector2 visiblePos;
        private Vector2 hiddenPos;

        private bool isVisible = true;
        private Coroutine currentSlideCoroutine;

        public Registry registry;

        private bool isActive = false;
        private TMP_Text statusText;

        public Color activeColor = Color.green;
        public Color inactiveColor = Color.red;

        private Coroutine trackingCoroutine;

        private List<Position> kalmanPositions = new();
        private List<Position> simplePositions = new();

        private void Awake()
        {
            button = GetComponent<Button>();
            rect = GetComponent<RectTransform>();
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();

            if (button != null)
                button.onClick.AddListener(OnButtonPressed);

            statusText = GetComponentInChildren<TMP_Text>();
            UpdateVisualState();
        }

        private void Start()
        {
            visiblePos = rect.anchoredPosition;
            CalculateHiddenPosition();
            isActive = false;
            UpdateVisualState();
        }

        private void CalculateHiddenPosition()
        {
            float canvasWidth = canvasRect != null ? canvasRect.rect.width : Screen.width;
            float buttonWidth = rect.rect.width;
            hiddenPos = new Vector2(-canvasWidth / 2 - buttonWidth / 2 - hiddenOffsetX, visiblePos.y);
        }

        public void Show()
        {
            if (!isVisible && currentSlideCoroutine == null)
                currentSlideCoroutine = StartCoroutine(SlideToPosition(visiblePos, true));
        }

        public void Hide()
        {
            if (isVisible && currentSlideCoroutine == null)
                currentSlideCoroutine = StartCoroutine(SlideToPosition(hiddenPos, false));
        }

        public void ToggleVisibility()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }

        private IEnumerator SlideToPosition(Vector2 targetPos, bool willBeVisible)
        {
            Vector2 startPos = rect.anchoredPosition;
            float time = 0f;

            while (time < slideDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / slideDuration);
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            rect.anchoredPosition = targetPos;
            isVisible = willBeVisible;
            currentSlideCoroutine = null;
        }

        private void OnButtonPressed()
        {
            isActive = !isActive;
            UpdateVisualState();

            if (isActive)
                OnActivated();
            else
                OnDeactivated();
        }

        private void UpdateVisualState()
        {
            if (statusText != null)
            {
                statusText.text = isActive ? "Active" : "Inactive";
                statusText.color = isActive ? activeColor : inactiveColor;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = isActive ? activeColor : inactiveColor;
            button.colors = colors;
        }

        private void OnActivated()
        {
            Debug.Log("Button activated!");
            kalmanPositions.Clear();
            simplePositions.Clear();
            trackingCoroutine = StartCoroutine(TrackPositions());
        }

        private void OnDeactivated()
        {
            Debug.Log("Button deactivated!");
            if (trackingCoroutine != null)
                StopCoroutine(trackingCoroutine);

            DumpPositionDataToJson();
        }

        private IEnumerator TrackPositions()
        {
            WaitForSeconds interval = new(0.25f);

            while (true)
            {
                kalmanPositions.Add(registry.kalmanFilter.GetEstimate());
                simplePositions.Add(registry.simplePositionFilter.GetEstimate());
                yield return interval;
            }
        }

        
        private void DumpPositionDataToJson()
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
    
            // Save datasets individually
            controller.IOManager.SaveAsJson(kalmanPositions, $"kalman_positions_{timestamp}", true);
            controller.IOManager.SaveAsJson(simplePositions, $"simple_positions_{timestamp}", true);
        }
        
        
    }
}
