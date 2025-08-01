using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
    public class NoKnowSignalFound : MonoBehaviour
    {
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private RectTransform dialogRectTransform;
        [SerializeField] private Button closeButton;

        private DateTime lastShowTime = DateTime.MinValue;
        private const float cooldown = 10f; // seconds
        private float slideDuration = 0.3f;
        
        private bool shouldShow = false;
        private bool shouldHide = false;
        
        private Vector2 hiddenPos;
        private Vector2 visiblePos;

        private void Awake()
        {
            if (dialogPanel == null)
            {
                Debug.LogError("Dialog Panel reference missing!");
            }
            else
            {
                dialogPanel.SetActive(false);
            }

            if (closeButton == null)
            {
                Debug.LogError("Close Button reference missing!");
            }
            else
            {
                closeButton.onClick.AddListener(Close);
            }
            
            // Get RectTransform if not assigned
            if (dialogRectTransform == null)
            {
                dialogRectTransform = dialogPanel.GetComponent<RectTransform>();
            }

            InitializePositions();
            Hide();
        }
        
        /// <summary>
        /// Calculate and set visible and hidden positions for the dialog sliding animation.
        /// </summary>
        private void InitializePositions()
        {
            if (dialogRectTransform == null) return;
            
            visiblePos = dialogRectTransform.anchoredPosition;
            
            // Calculate hidden position - move completely off-screen to the right
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            
            hiddenPos = visiblePos + new Vector2(canvasWidth + dialogRectTransform.rect.width + 100f, 0);
            
            // Start hidden
            dialogRectTransform.anchoredPosition = hiddenPos;
        }
        
        private void Update()
        {
            // handle queued UI updates on main thread
            if (shouldShow)
            {
                shouldShow = false;
                ShowDialog();
            }
            
            if (shouldHide)
            {
                shouldHide = false;
                HideDialog();
            }
        }

        
        /// <summary>
        /// Requests showing the dialog respecting cooldown to avoid spam.
        /// </summary>
        public void Show()
        {
            var timeSinceLastShow = (DateTime.Now - lastShowTime).TotalSeconds;
            
            if (timeSinceLastShow < cooldown)
            {
                Debug.Log("Show blocked: cooldown active.");
                return;
            }

            lastShowTime = DateTime.Now;
            shouldShow = true; // queue for main thread
        }

        
        /// <summary>
        /// Requests hiding the dialog.
        /// </summary>
        public void Hide()
        {
            shouldHide = true; // queue for main thread
        }

        
        /// <summary>
        /// Activates dialog and starts slide-in animation.
        /// </summary>
        private void ShowDialog()
        {
            if (dialogPanel != null)
            {
                dialogPanel.SetActive(true);
                StartCoroutine(SlideDialog(dialogRectTransform, dialogRectTransform.anchoredPosition, visiblePos, slideDuration));
            }
        }

        
        /// <summary>
        /// Starts slide-out animation and disables dialog after animation.
        /// </summary>
        private void HideDialog()
        {
            if (dialogPanel != null)
            {
                StartCoroutine(SlideDialogAndHide(dialogRectTransform, dialogRectTransform.anchoredPosition, hiddenPos, slideDuration));
            }
        }

        /// <summary>
        /// Coroutine for sliding the dialog from one position to another.
        /// </summary>
        private IEnumerator SlideDialog(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, time / duration);
                yield return null;
            }
            rect.anchoredPosition = to;
        }

        /// <summary>
        /// Coroutine to slide dialog out and then disable the panel.
        /// </summary>
        private IEnumerator SlideDialogAndHide(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, time / duration);
                yield return null;
            }
            rect.anchoredPosition = to;
            
            // Hide after animation completes
            dialogPanel.SetActive(false);
        }

        public void Close()
        {
            Hide();
        }
    }
}