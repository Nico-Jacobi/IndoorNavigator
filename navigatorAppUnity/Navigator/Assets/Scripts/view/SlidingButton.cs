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
    
    /// <summary>
    /// this is a button designed for toggling data collection on and off.
    /// When activated, it tracks and stores position data using Kalman and simple filters.
    /// Usually remains deactivated by default, as its only for diagnosics / collecting position data / not for the "normal" user.
    /// </summary>
    
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

        /// <summary>
        /// Calculates the hidden position to slide the button off-screen to the left.
        /// </summary>
        private void CalculateHiddenPosition()
        {
            float canvasWidth = canvasRect != null ? canvasRect.rect.width : Screen.width;
            float buttonWidth = rect.rect.width;
            hiddenPos = new Vector2(-canvasWidth / 2 - buttonWidth / 2 - hiddenOffsetX, visiblePos.y);
        }

        /// <summary>
        /// Slide the button to the visible position if currently hidden.
        /// </summary
        public void Show()
        {
            if (!isVisible && currentSlideCoroutine == null)
                currentSlideCoroutine = StartCoroutine(SlideToPosition(visiblePos, true));
        }

        /// <summary>
        /// Slide the button to the hidden position if currently visible.
        /// </summary>
        public void Hide()
        {
            if (isVisible && currentSlideCoroutine == null)
                currentSlideCoroutine = StartCoroutine(SlideToPosition(hiddenPos, false));
        }

        /// <summary>
        /// Toggles button visibility by sliding in or out.
        /// </summary>
        public void ToggleVisibility()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }

        
        /// <summary>
        /// Coroutine that animates sliding the button to a target anchored position.
        /// </summary>
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

        
        /// <summary>
        /// Button click handler toggling active/inactive state.
        /// Starts or stops position tracking accordingly.
        /// </summary>
        private void OnButtonPressed()
        {
            isActive = !isActive;
            UpdateVisualState();

            if (isActive)
                OnActivated();
            else
                OnDeactivated();
        }

        /// <summary>
        /// Updates the button's visual state including text and colors.
        /// </summary>
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

        /// <summary>
        /// Called when the button is activated.
        /// Clears previous tracking data and starts tracking coroutine.
        /// </summary>
        private void OnActivated()
        {
            //Debug.Log("Button activated!");
            kalmanPositions.Clear();
            simplePositions.Clear();
            trackingCoroutine = StartCoroutine(TrackPositions());
        }

        /// <summary>
        /// Called when the button is deactivated.
        /// Stops tracking coroutine and dumps position data to JSON files.
        /// </summary>
        private void OnDeactivated()
        {
            //Debug.Log("Button deactivated!");
            if (trackingCoroutine != null)
                StopCoroutine(trackingCoroutine);

            DumpPositionDataToJson();
        }

        /// <summary>
        /// Coroutine that periodically records position estimates from filters.
        /// </summary>
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

        
        /// <summary>
        /// Dumps tracked position lists to JSON files with timestamped filenames.
        /// </summary>
        private void DumpPositionDataToJson()
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
    
            // Save datasets individually
            controller.IOManager.SaveAsJson(kalmanPositions, $"kalman_positions_{timestamp}", true);
            controller.IOManager.SaveAsJson(simplePositions, $"simple_positions_{timestamp}", true);
        }
        
        
    }
}
