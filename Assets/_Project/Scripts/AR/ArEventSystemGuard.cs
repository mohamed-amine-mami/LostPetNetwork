using UnityEngine;
using UnityEngine.EventSystems;

namespace LostPetNetwork.AR
{
    /// <summary>
    /// AR scenes are opened on top of the main scene, which already has an EventSystem. A second one would
    /// log warnings, so this one switches itself off when it is not needed.
    /// </summary>
    public class ArEventSystemGuard : MonoBehaviour
    {
        void Awake()
        {
            var all = FindObjectsByType<EventSystem>();
            if (all.Length > 1) gameObject.SetActive(false);
        }
    }
}
