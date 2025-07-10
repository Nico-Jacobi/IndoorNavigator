using controller;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
    public class FloatingButtons : MonoBehaviour
    {
        public Registry registry;

        public Button startNavigationButton;
        public Button gotoPositionButton;

        public float slideDuration = 0.3f;

        private RectTransform rect;
        private RectTransform canvasRect;
        private Vector2 visiblePos;
        private Vector2 hiddenPos;

        private bool lastPositionFound = false;
        private bool isVisible = true;
        private Coroutine currentSlideCoroutine;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            
            // Get the canvas RectTransform for proper positioning calculations
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();

            startNavigationButton.onClick.AddListener(OnStartNavigationClicked);
            gotoPositionButton.onClick.AddListener(OnGotoPositionClicked);
            
            DeactivateGotoPositionButton();
            DeactivateNavigationButton();
        }

        private void Start()
        {
            // Store the intended visible position
            visiblePos = rect.anchoredPosition;
            
            // Calculate hidden position - completely off the right edge of the screen
            float canvasWidth = canvasRect != null ? canvasRect.rect.width : Screen.width;
            float buttonWidth = rect.rect.width;
            
            // Move to right edge of canvas plus some padding
            hiddenPos = new Vector2(canvasWidth/2 + buttonWidth/2 + 50f, visiblePos.y);
        }


        private void Update()
        {
            bool currentFound = registry.wifiPositionTracker.foundPosition;

            if (currentFound != lastPositionFound)  //to only check this on each update, for efficiency
            {
                if (currentFound)
                {
                    ActivateNavigationButton();
                    ActivateGotoPositionButton();
                }
                else
                {
                    DeactivateNavigationButton();
                    DeactivateGotoPositionButton();
                }

                lastPositionFound = currentFound;
            }
        }

        
        public void Show()
        {
            if (!isVisible && currentSlideCoroutine == null)
            {
                currentSlideCoroutine = StartCoroutine(SlideToPosition(visiblePos, true));
            }
        }

        public void Hide()
        {
            if (isVisible && currentSlideCoroutine == null)
            {
                currentSlideCoroutine = StartCoroutine(SlideToPosition(hiddenPos, false));
            }
        }

        public void Toggle()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }
        
        public void ActivateGotoPositionButton()
        {
            gotoPositionButton.interactable = true;
        }

        public void DeactivateGotoPositionButton()
        {
            gotoPositionButton.interactable = false;
        }
        
        public void ActivateNavigationButton()
        {
            startNavigationButton.interactable = true;
        }

        public void DeactivateNavigationButton()
        {
            startNavigationButton.interactable = false;
        }
        

        private IEnumerator SlideToPosition(Vector2 targetPos, bool willBeVisible)
        {
            Vector2 startPos = rect.anchoredPosition;
            float time = 0f;

            while (time < slideDuration)
            {
                time += Time.deltaTime;
                float t = time / slideDuration;
                
                // Use easing for smoother animation
                t = Mathf.SmoothStep(0f, 1f, t);
                
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // Ensure final position is exact
            rect.anchoredPosition = targetPos;
            
            // Update state after animation completes
            isVisible = willBeVisible;
            currentSlideCoroutine = null;
        }

        private void OnStartNavigationClicked()
        {
            registry.navigationDialog.ShowDialog();
        }

        private void OnGotoPositionClicked()
        {
            registry.cameraController.GotoPrediction();
            registry.topMenu.UpdateUI();
        }

        // Force stop any running animation
        public void StopSliding()
        {
            if (currentSlideCoroutine != null)
            {
                StopCoroutine(currentSlideCoroutine);
                currentSlideCoroutine = null;
            }
        }

        // Optional: Method to recalculate positions if canvas size changes
        public void RecalculatePositions()
        {
            if (canvasRect != null)
            {
                float canvasWidth = canvasRect.rect.width;
                float buttonWidth = rect.rect.width;
                hiddenPos = new Vector2(canvasWidth/2 + buttonWidth/2 + 50f, visiblePos.y);
            }
        }
        
    }
}