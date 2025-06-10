using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
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

        private void InitializeComponents()
        {

            dialogRectTransform = promptPanel.GetComponent<RectTransform>();
            promptText.text = "Location services are disabled. Please enable GPS in your device settings.";
        }

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

        private void SetupEventHandlers()
        {
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsButtonPressed);
            
            if (discardButton != null)
                discardButton.onClick.AddListener(OnDiscardButtonPressed);
        }

        public void Open()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(true);
                StartCoroutine(SlideDialog(dialogRectTransform, dialogRectTransform.anchoredPosition, visiblePos, slideDuration));
            }
        }

        public void Close()
        {
            if (promptPanel != null)
            {
                StartCoroutine(SlideDialogAndHide(dialogRectTransform, dialogRectTransform.anchoredPosition, hiddenPos, slideDuration));
            }
        }

        public void SetPromptText(string text)
        {
            if (promptText != null)
            {
                promptText.text = text;
            }
        }

        private void OnSettingsButtonPressed()
        {
            OnSettingsButtonClicked?.Invoke();
        }

        private void OnDiscardButtonPressed()
        {
            OnDiscardButtonClicked?.Invoke();
        }

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
