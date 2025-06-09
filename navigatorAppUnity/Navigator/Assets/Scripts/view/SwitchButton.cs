namespace View
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using System.Collections;

    /// <summary>
    /// Toggle switch with animation and separate handles for on/off states.
    /// </summary>
    public class SwitchButton : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI Elements")]
        [SerializeField] private RectTransform onHandle;  // Handle shown when switch is ON
        [SerializeField] private RectTransform offHandle; // Handle shown when switch is OFF
        [SerializeField] private Image offBackgroundImage; 
        [SerializeField] private Image onBackgroundImage; 
        
        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.2f; // Animation time
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Curve for smoothness
        
        [Header("Positioning")]
        [SerializeField] private float handlePadding = 0f; // Padding from edges
        
        [Header("State")]
        [SerializeField] private bool isOn = false; // Current state

        // Events
        public System.Action<bool> OnValueChanged;

        // Animation coroutine reference
        private Coroutine animationCoroutine;

        private void Start()
        {
            UpdateVisualState(true); // Set initial state
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Toggle(); // Toggle state on click
        }

        /// <summary>
        /// Toggles the switch state with animation.
        /// </summary>
        public void Toggle()
        {
            isOn = !isOn;
            UpdateVisualState(false);
            OnValueChanged?.Invoke(isOn);
        }

        /// <summary>
        /// Updates visuals based on state.
        /// </summary>
        public void UpdateVisualState(bool instant)
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            // Get the active handle based on current state
            RectTransform activeHandle = isOn ? onHandle : offHandle;
            RectTransform inactiveHandle = isOn ? offHandle : onHandle;
            
            // Calculate target positions with proper padding
            RectTransform parentRect = activeHandle.parent.GetComponent<RectTransform>();
            float containerWidth = parentRect.rect.width * 0.5f;
            float handleWidth = activeHandle.rect.width * 0.5f;
            
            // OFF = left position with padding, ON = right position with padding
            float leftPosition = -containerWidth * 2.3f ;
            float rightPosition = -containerWidth * 0.5f ;
            
            float targetX = isOn ? rightPosition : leftPosition;
            float targetOnAlpha = isOn ? 1f : 0f;
            float targetOffAlpha = isOn ? 0f : 1f;
            
            if (instant)
            {
                // Position active handle
                activeHandle.anchoredPosition = new Vector2(targetX, activeHandle.anchoredPosition.y);
                // Hide inactive handle
                inactiveHandle.gameObject.SetActive(false);
                activeHandle.gameObject.SetActive(true);
                
                // Update background alphas
                SetBackgroundAlpha(onBackgroundImage, targetOnAlpha);
                SetBackgroundAlpha(offBackgroundImage, targetOffAlpha);
            }
            else
            {
                animationCoroutine = StartCoroutine(AnimateToggle(activeHandle, inactiveHandle, targetX, targetOnAlpha, targetOffAlpha));
            }
        }

        /// <summary>
        /// Animates the toggle transition between handles.
        /// </summary>
        private IEnumerator AnimateToggle(RectTransform activeHandle, RectTransform inactiveHandle, float targetX, float targetOnAlpha, float targetOffAlpha)
        {
            float startTime = Time.time;
            float endTime = startTime + animationDuration;
            
            // Get starting positions and alphas
            float startX = inactiveHandle.anchoredPosition.x;
            float startOnAlpha = onBackgroundImage.color.a;
            float startOffAlpha = offBackgroundImage.color.a;
            
            // Make sure both handles are visible during animation
            activeHandle.gameObject.SetActive(true);
            inactiveHandle.gameObject.SetActive(true);
            
            // Set initial positions
            activeHandle.anchoredPosition = new Vector2(startX, activeHandle.anchoredPosition.y);
            
            while (Time.time < endTime)
            {
                float normalizedTime = (Time.time - startTime) / animationDuration;
                float curveValue = animationCurve.Evaluate(normalizedTime);
                
                // Animate active handle position
                float newX = Mathf.Lerp(startX, targetX, curveValue);
                activeHandle.anchoredPosition = new Vector2(newX, activeHandle.anchoredPosition.y);
                
                // Fade out inactive handle and fade in active handle based on animation progress
                float handleAlpha = curveValue;
                SetHandleAlpha(activeHandle, handleAlpha);
                SetHandleAlpha(inactiveHandle, 1f - handleAlpha);
                
                // Animate background alphas
                SetBackgroundAlpha(onBackgroundImage, Mathf.Lerp(startOnAlpha, targetOnAlpha, curveValue));
                SetBackgroundAlpha(offBackgroundImage, Mathf.Lerp(startOffAlpha, targetOffAlpha, curveValue));
                
                yield return null;
            }

            // Finalize positions and visibility
            activeHandle.anchoredPosition = new Vector2(targetX, activeHandle.anchoredPosition.y);
            SetHandleAlpha(activeHandle, 1f);
            inactiveHandle.gameObject.SetActive(false);
            
            SetBackgroundAlpha(onBackgroundImage, targetOnAlpha);
            SetBackgroundAlpha(offBackgroundImage, targetOffAlpha);
            
            animationCoroutine = null;
        }

        /// <summary>
        /// Sets the alpha of a handle (works with Image, CanvasGroup, or individual Image components).
        /// </summary>
        private void SetHandleAlpha(RectTransform handle, float alpha)
        {
            // Try to find CanvasGroup first for best performance
            CanvasGroup canvasGroup = handle.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
                return;
            }
            
            // Fall back to Image component
            Image handleImage = handle.GetComponent<Image>();
            if (handleImage != null)
            {
                Color color = handleImage.color;
                color.a = alpha;
                handleImage.color = color;
            }
            
            // Also handle child images
            Image[] childImages = handle.GetComponentsInChildren<Image>();
            foreach (Image img in childImages)
            {
                Color color = img.color;
                color.a = alpha;
                img.color = color;
            }
        }

        /// <summary>
        /// Sets the alpha of an image.
        /// </summary>
        private void SetBackgroundAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        /// <summary>
        /// Gets the current toggle state.
        /// </summary>
        public bool IsOn() => isOn;

        /// <summary>
        /// Sets the toggle state.
        /// </summary>
        public void SetValue(bool value, bool animate = true)
        {
            if (isOn == value) return;
            
            isOn = value;
            UpdateVisualState(!animate);
            
            OnValueChanged?.Invoke(isOn);
        }
    }
}