using UnityEngine;

namespace LostPetNetwork.UI
{
    public static class UiUtil
    {
        /// <summary>Removes all children immediately (detached now, destroyed at end of frame).</summary>
        public static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }
    }
}
