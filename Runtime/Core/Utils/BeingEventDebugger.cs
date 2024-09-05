using UnityEngine;
using Virbe.Core.Data;

namespace Virbe.Core.Utils
{
    public class BeingEventDebugger : MonoBehaviour
    {
        [SerializeField] private VirbeBeing _being;
        private void OnEnable()
        {
            _being.OnUiAction += (x) => UiAction(x);
            _being.OnCustomAction += (x) => CustomAction(x);
            _being.OnBehaviourAction += (x) => BehaviourAction(x);
            _being.OnEngineEvent += (x) => EngineAction(x);
            _being.OnSignal += (x) => SignalAction(x);
            _being.OnNamedAction += (x) => NamedAction(x);
        }

        private void UiAction(VirbeUiAction action)
        {
            Debug.Log($"Virbe Debugger - Event: UiAction - called from C# event {action.Name}");
        }

        public void UiActionUnity(VirbeUiAction action)
        {
            Debug.Log($"Virbe Debugger - Event: UiAction - called from Unity event {action.Name}");
        }

        private void CustomAction(CustomAction action)
        {
            Debug.Log($"Virbe Debugger - Event: CustomAction - called from C# event {action.Name}");
        }

        public void CustomActionUnity(CustomAction action)
        {
            Debug.Log($"Virbe Debugger - Event: CustomAction - called from Unity event {action.Name}");
        }

        private void BehaviourAction(VirbeBehaviorAction action)
        {
            Debug.Log($"Virbe Debugger - Event: BehaviourAction - called from C# event {action.Name}");
        }

        public void BehaviourActionUnity(VirbeBehaviorAction action)
        {
            Debug.Log($"Virbe Debugger - Event: BehaviourAction - called from Unity event {action.Name}");
        }

        private void EngineAction(EngineEvent action)
        {
            Debug.Log($"Virbe Debugger - Event: EngineAction - called from C# event {action.State}");
        }

        public void EngineActionUnity(EngineEvent action)
        {
            Debug.Log($"Virbe Debugger - Event: EngineAction - called from Unity event {action.State}");
        }

        private void SignalAction(Signal action)
        {
            Debug.Log($"Virbe Debugger - Event: SignalAction - called from C# event {action.Name}");
        }

        public void SignalActionUnity(Signal action)
        {
            Debug.Log($"Virbe Debugger - Event: SignalAction - called from Unity event {action.Name}");
        }

        private void NamedAction(NamedAction action)
        {
            Debug.Log($"Virbe Debugger - Event: NamedAction - called from C# event {action.Name}");
        }

        public void NamedActionUnity(NamedAction action)
        {
            Debug.Log($"Virbe Debugger - Event: NamedAction - called from Unity event {action.Name}");
        }
    }
}