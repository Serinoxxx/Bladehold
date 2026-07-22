using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    public class UILevelCarousel : MonoBehaviour
    {
        public RectTransform[] items;
        public int currentIndex = 0;
        public float spacing = 500f;
        public float centerScale = 1.2f;
        public float sideScale = 0.7f;
        public float lerpSpeed = 10f;
        
        private float[] targetX;
        private float currentX;

        void Start()
        {
            if (items == null || items.Length == 0) return;
            targetX = new float[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                targetX[i] = i * spacing;
            }
        }

        void Update()
        {
            if (items == null || items.Length == 0) return;
            
            float targetScroll = -currentIndex * spacing;
            currentX = Mathf.Lerp(currentX, targetScroll, Time.deltaTime * lerpSpeed);

            for (int i = 0; i < items.Length; i++)
            {
                float pos = targetX[i] + currentX;
                float dist = Mathf.Abs(pos);
                
                // Scale based on distance from center
                float scale = Mathf.Lerp(centerScale, sideScale, dist / spacing);
                scale = Mathf.Clamp(scale, sideScale, centerScale);
                
                items[i].anchoredPosition = new Vector2(pos, 0);
                items[i].localScale = new Vector3(scale, scale, 1);
                
                // Alpha fade
                CanvasGroup cg = items[i].GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float alpha = Mathf.Lerp(1f, 0.3f, dist / spacing);
                    cg.alpha = alpha;
                }
                
                // Bring center to front
                if (dist < spacing * 0.5f)
                {
                    items[i].SetAsLastSibling();
                }
            }
        }

        public void Next()
        {
            currentIndex = Mathf.Min(currentIndex + 1, items.Length - 1);
        }

        public void Previous()
        {
            currentIndex = Mathf.Max(currentIndex - 1, 0);
        }
    }
}
