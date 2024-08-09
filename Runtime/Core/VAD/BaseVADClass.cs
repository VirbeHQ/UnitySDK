using UnityEngine;

namespace Virbe.Core.VAD
{
    [RequireComponent(typeof(VirbeVoiceRecorder))]
    public abstract class BaseVADClass : MonoBehaviour
    {
        [SerializeField] protected bool disableWhenBeingIsSpeaking = false;
        [Tooltip("This is required only when disableWhenBeingIsSpeaking is set to TRUE")]
        [SerializeField] private VirbeBeing _being;

        [Tooltip("Enable VAD only when camera is pointing on Vad target")]
        [SerializeField] protected bool _waitForVADTarget = false;
        [SerializeField] private float _raycastSphereSize = .5f;
        [SerializeField] private float _castDistance = 2f;
        [SerializeField] private bool _pointingAtVadTarget = false;
        [SerializeField] private Transform _mainCamera;
        [SerializeField] private LayerMask _vadTargetLayer;

        protected VirbeVoiceRecorder _voiceRecorder;
        protected bool ShouldListenToUser;
        protected bool WasTalkingLastFrame;
        private RaycastHit[] _hitArray = new RaycastHit[64];

        protected virtual void Awake()
        {
            _voiceRecorder = GetComponent<VirbeVoiceRecorder>();
            Mic.Initialize();
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main?.transform;
            }
        }

        protected virtual void Update()
        {
            if (disableWhenBeingIsSpeaking && _being?.IsBeingSpeaking == true)
            {
                ShouldListenToUser = false;
                return;
            }
            if (_waitForVADTarget && !WasTalkingLastFrame)
            {
                if(_mainCamera != null)
                {
                    _pointingAtVadTarget = CheckIfPointingVadTarget(_mainCamera);
                    if (!_pointingAtVadTarget)
                    {
                        ShouldListenToUser = false;
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning($"{this.GetType()} can't check for VAD targets because main camera is null");
                }
            }
            ShouldListenToUser = true;
        }

        public void SetBeing(VirbeBeing being)
        {
            _being = being;
        }

        public void SetCamera(Camera camera)
        {
            _mainCamera = camera.transform;
        }

        private bool CheckIfPointingVadTarget(Transform camera)
        {
            var hitCount = Physics.SphereCastNonAlloc(camera.position, _raycastSphereSize, camera.forward, _hitArray, _castDistance, _vadTargetLayer.value);
            if (hitCount == 0) 
            {
                return false;
            }
            for(int i = 0; i < hitCount; i++)
            {
                var vadTarget = _hitArray[i].collider.GetComponent<VADActivateTarget>();
                if(vadTarget != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}