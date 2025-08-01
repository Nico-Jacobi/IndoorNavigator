using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
    /// <summary>
    /// Controls a sliding dialog prompting the user about location services.
    /// Slides in/out a panel with customizable prompt text and buttons for settings and discard.
    /// </summary>givbe 
    public class LocationPromptDialog : MonoBehaviour
    {
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private RectTransform dialogRectTransform;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private TextMeshProUGUI promptText;
        
        [SerializeField] private float slideDuration = 0.3f;
        
        private Vector2 hiddenPos;
        private Vector2 visiblePos;
        
        public event Action OnSettingsButtonClicked;
        public event Action OnDiscardButtonClicked;

        private void Awake()
        {
            InitializeComponents();
            InitializePositions();
            SetupEventHandlers();
            
            // Start hidden
            Close();
        }

        /// <summary>
        /// Sets up references and default prompt text.
        /// </summary>
        private void InitializeComponents()
        {

            dialogRectTransform = promptPanel.GetComponent<RectTransform>();
            promptText.text = "Location services are disabled. Please enable GPS in your device settings.";
        }

        /// <summary>
        /// Calculates visible and off-screen hidden positions for sliding animation.
        /// </summary>
        private void InitializePositions()
        {
            if (dialogRectTransform == null) return;
            
            visiblePos = dialogRectTransform.anchoredPosition;
            
            Canvas canvas = GetComponentInParent<Canvas>();

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            hiddenPos = visiblePos + new Vector2(canvasWidth + dialogRectTransform.rect.width + 100f, 0);
            
            
            
            // Start hidden
            dialogRectTransform.anchoredPosition = hiddenPos;
        }

        /// <summary>
        /// Hooks up button clicks to their respective event triggers.
        /// </summary>
        private void SetupEventHandlers()
        {
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsButtonPressed);
            
            if (discardButton != null)
                discardButton.onClick.AddListener(OnDiscardButtonPressed);
        }

        /// <summary>
        /// Shows the dialog by sliding it into view.
        /// </summary>
        public void Open()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(true);
                StartCoroutine(SlideDialog(dialogRectTransform, dialogRectTransform.anchoredPosition, visiblePos, slideDuration));
            }
        }

        /// <summary>
        /// Hides the dialog by sliding it out and then disabling it.
        /// </summary>
        public void Close()
        {
            if (promptPanel != null)
            {
                StartCoroutine(SlideDialogAndHide(dialogRectTransform, dialogRectTransform.anchoredPosition, hiddenPos, slideDuration));
            }
        }

        /// <summary>
        /// Updates the dialog's prompt message.
        /// </summary>
        public void SetPromptText(string text)
        {
            if (promptText != null)
            {
                promptText.text = text;
            }
        }

        /// <summary>
        /// opens the location / gps settings for the user to activate.
        /// </summary>
        private void OnSettingsButtonPressed()
        {
            OnSettingsButtonClicked?.Invoke();
        }

        /// <summary>
        /// closes the dialog
        /// </summary>
        private void OnDiscardButtonPressed()
        {
            OnDiscardButtonClicked?.Invoke();
        }

        /// <summary>
        /// Smoothly animates the dialog sliding from one position to another.
        /// </summary>
        private IEnumerator SlideDialog(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            if (rect == null) yield break;
            
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
        /// Slides the dialog out and disables the panel after animation.
        /// </summary>
        private IEnumerator SlideDialogAndHide(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            if (rect == null) yield break;
            
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, time / duration);
                yield return null;
            }
            rect.anchoredPosition = to;
            
            // Hide after animation completes
            if (promptPanel != null)
                promptPanel.SetActive(false);
        }
    }
}
