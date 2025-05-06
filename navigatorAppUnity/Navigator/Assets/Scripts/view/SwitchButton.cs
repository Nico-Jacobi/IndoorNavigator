namespace View
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using System.Collections;

    /// <summary>
    /// Toggle switch with animation.
    /// </summary>
    public class ModernToggleSwitch : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI Elements")]
        [SerializeField] private RectTransform handle; 
        [SerializeField] private Image offBackgroundImage; 
        [SerializeField] private Image onBackgroundImage; 
        
        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.2f; // Animation time
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Curve for smoothness
        
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
        private void UpdateVisualState(bool instant)
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            float targetX = isOn ? handle.parent.GetComponent<RectTransform>().rect.width - handle.rect.width * 0.6f : 0f;
            float targetOnAlpha = isOn ? 1f : 0f;
            float targetOffAlpha = isOn ? 0f : 1f;
            
            if (instant)
            {
                handle.anchoredPosition = new Vector2(targetX, handle.anchoredPosition.y);
                SetBackgroundAlpha(onBackgroundImage, targetOnAlpha);
                SetBackgroundAlpha(offBackgroundImage, targetOffAlpha);
            }
            else
            {
                animationCoroutine = StartCoroutine(AnimateToggle(targetX, targetOnAlpha, targetOffAlpha));
            }
        }

        /// <summary>
        /// Animates the toggle transition.
        /// </summary>
        private IEnumerator AnimateToggle(float targetX, float targetOnAlpha, float targetOffAlpha)
        {
            float startTime = Time.time;
            float endTime = startTime + animationDuration;
            
            float startX = handle.anchoredPosition.x;
            float startOnAlpha = onBackgroundImage.color.a;
            float startOffAlpha = offBackgroundImage.color.a;
            
            while (Time.time < endTime)
            {
                float normalizedTime = (Time.time - startTime) / animationDuration;
                float curveValue = animationCurve.Evaluate(normalizedTime);
                
                float newX = Mathf.Lerp(startX, targetX, curveValue);
                handle.anchoredPosition = new Vector2(newX, handle.anchoredPosition.y);
                
                SetBackgroundAlpha(onBackgroundImage, Mathf.Lerp(startOnAlpha, targetOnAlpha, curveValue));
                SetBackgroundAlpha(offBackgroundImage, Mathf.Lerp(startOffAlpha, targetOffAlpha, curveValue));
                
                yield return null;
            }

            handle.anchoredPosition = new Vector2(targetX, handle.anchoredPosition.y);
            SetBackgroundAlpha(onBackgroundImage, targetOnAlpha);
            SetBackgroundAlpha(offBackgroundImage, targetOffAlpha);
            
            animationCoroutine = null;
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
