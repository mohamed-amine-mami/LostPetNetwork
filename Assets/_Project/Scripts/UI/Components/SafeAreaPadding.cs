using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>Keeps UI inside the phone's safe area (notch, home indicator).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPadding : MonoBehaviour
    {
        Rect _applied;

        void Update()
        {
            if (Screen.safeArea == _applied) return;
            _applied = Screen.safeArea;
            var rt = (RectTransform)transform;
            Vector2 min = _applied.position;
            Vector2 max = _applied.position + _applied.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
