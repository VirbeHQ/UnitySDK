using System;
using UnityEngine;

namespace Virbe.Core.VAD
{
    public class VADActivateTarget : MonoBehaviour
    {
        private Collider _targetCollider;
        private const float _maxWidthSize = 1f;
        private void Awake()
        {
            _targetCollider = GetComponent<Collider>();
            if(_targetCollider == null ) 
            {
                var collider = gameObject.AddComponent<BoxCollider> ();
                collider.isTrigger = true;
                var bounds = new Bounds();
                foreach(var renderer in GetComponentsInChildren<Renderer>()) 
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                collider.center = bounds.center;
                bounds.size = new Vector3(Mathf.Clamp(bounds.size.x, 0, _maxWidthSize), bounds.size.y, bounds.size.z) ;
                collider.size = bounds.size;
                _targetCollider = collider;
            }
        }

        public void SetMask(string maskName)
        {
            gameObject.layer = LayerMask.NameToLayer(maskName);
        }
    }
}