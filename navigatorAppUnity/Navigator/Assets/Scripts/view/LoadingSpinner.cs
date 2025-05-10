using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace view
{

    public class LoadingSpinner : MonoBehaviour
    {
        private float rotationSpeed = 270f;
        private Color spinnerColor = Color.white;
        private float spinnerSize = 0.5f;
        
        private RectTransform rectTransform;
        private bool isSpinning = false;
        
        public bool IsSpinning => isSpinning;
        
        private void Awake()
        {
           
            
            rectTransform = GetComponent<RectTransform>();
            
            
            Vector2 size = rectTransform.sizeDelta;
            float minSize = Mathf.Min(size.x, size.y);
            rectTransform.sizeDelta = new Vector2(minSize * spinnerSize, minSize * spinnerSize);
            
            isSpinning = false;
            gameObject.SetActive(false);

        }
        

        
        /// <summary>
        /// Starts the spinning animation.
        /// </summary>
        public void StartSpinning()
        {
            gameObject.SetActive(true);

            if (isSpinning)
                return;
            
            isSpinning = true;
            StopAllCoroutines();
            StartCoroutine(SpinCoroutine());
        }
        
        /// <summary>
        /// Stops the spinning animation.
        /// </summary>
        public void StopSpinning()
        {
            gameObject.SetActive(false);

            if (!isSpinning)
                return;
            
            isSpinning = false;
            StopAllCoroutines();
        }
        
        /// <summary>
        /// Coroutine that handles the spinning animation.
        /// </summary>
        private IEnumerator SpinCoroutine()
        {
            while (isSpinning)
            {
                rectTransform.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
                Debug.Log(-rotationSpeed * Time.deltaTime);
                yield return null;
            }
        }
        
        /// <summary>
        /// Toggle spinner visibility and animation.
        /// </summary>
        public void ToggleSpinner()
        {
            if (isSpinning)
                StopSpinning();
            else
                StartSpinning();
        }
        
        private void OnDisable()
        {
            StopSpinning();
        }
    }
}