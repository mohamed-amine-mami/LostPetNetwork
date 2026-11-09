using System;
using LostPetNetwork.Services.Vets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>One clinic in the "Vets near you" list, with Call and Directions buttons.</summary>
    public class VetRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text placeLabel;
        [SerializeField] TMP_Text hoursLabel;
        [SerializeField] TMP_Text distanceLabel;
        [SerializeField] GameObject openAllDayTag;
        [SerializeField] GameObject demoTag;
        [SerializeField] Button callButton;
        [SerializeField] Button directionsButton;

        public VetClinic Clinic { get; private set; }

        public void Bind(NearbyVet vet, Action<VetClinic> onCall, Action<VetClinic> onDirections)
        {
            var c = vet.Clinic;
            Clinic = c;
            nameLabel.text = c.Name;
            placeLabel.text = c.Place;
            placeLabel.gameObject.SetActive(!string.IsNullOrEmpty(c.Place));
            hoursLabel.text = c.Is24h ? "Open 24 hours" : c.OpeningHours;
            hoursLabel.gameObject.SetActive(!string.IsNullOrEmpty(hoursLabel.text));
            string distance = vet.DistanceLabel;
            distanceLabel.text = distance;
            distanceLabel.gameObject.SetActive(!string.IsNullOrEmpty(distance));
            openAllDayTag.SetActive(c.Is24h);
            if (demoTag != null) demoTag.SetActive(c.IsDemo);

            bool canCall = c.DialUrl() != null;
            callButton.gameObject.SetActive(canCall);
            callButton.onClick.RemoveAllListeners();
            callButton.onClick.AddListener(() => onCall?.Invoke(c));
            directionsButton.onClick.RemoveAllListeners();
            directionsButton.onClick.AddListener(() => onDirections?.Invoke(c));
        }
    }
}
