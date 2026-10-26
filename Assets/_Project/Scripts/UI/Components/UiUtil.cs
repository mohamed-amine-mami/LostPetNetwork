using LostPetNetwork.Services.Reports;
using UnityEngine;

namespace LostPetNetwork.UI
{
    public static class UiUtil
    {
        /// <summary>A message for the user: the repository's own friendly text when it has one, otherwise the fallback.</summary>
        public static string Describe(System.Exception e, string fallback) =>
            e is RepositoryException ? e.Message : fallback;

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
