using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirbeDemo.GUI.RecordButton
{
    [RequireComponent(typeof(Button))]
    public class SignalIndicationHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private GameObject signalIndicatorGameObject = null;

        public void Start()
        {
            SetIndicatorActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SetIndicatorActive(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetIndicatorActive(false);
        }

        // TODO Consider attaching activation/deactivation methods to this script instead of accessing whole game object by reference.
        private void SetIndicatorActive(bool active)
        {
            if (!signalIndicatorGameObject) throw new Exception($"{nameof(signalIndicatorGameObject)} is unassigned");

            signalIndicatorGameObject.SetActive(active);
        }
    }
}