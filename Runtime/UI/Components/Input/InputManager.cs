using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Virbe.Core;
using Virbe.Core.Actions;
using Virbe.UI.Layouts;

namespace Virbe.UI.Components.Input
{
    public class InputManager : VirbeUIComponent, InputLoader.InputClickListener
    {
        [SerializeField] private InputLoader inputLoader;
        [SerializeField] private SubmitInputEvent onSubmitInputEvent = new SubmitInputEvent();
        [SerializeField] private CancelInputEvent onCancelInputEvent = new CancelInputEvent();

        private VirbePluginUIConnector _virbePluginUIConnector;

        private void Awake()
        {
            _virbePluginUIConnector = GetComponentInParent<VirbePluginUIConnector>();
            Assert.IsNotNull(_virbePluginUIConnector,
                "VirbePluginUIConnector component is required in the parent Layout component");
        }

        public override void BeingStateChanged(BeingState beingState)
        {
            // Ignore at the moment
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            ClearInput();
            List<Core.Custom.Input> extractInputs = beingAction.custom?.ExtractVirbeInputs();
            if (extractInputs != null && extractInputs.Count > 0)
            {
                LoadInput(extractInputs[0]);
            }
        }

        private void LoadInput(Core.Custom.Input input)
        {
            gameObject.SetActive(true);
            inputLoader.SetupInput(input, this);
        }

        public void ClearInput()
        {
            gameObject.SetActive(false);
            inputLoader.Clear();
        }

        public override void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void OnSubmitClicked(Core.Custom.Input input, string value)
        {
            onSubmitInputEvent?.Invoke(input, value);
            SetVisible(false);
            ClearInput();
        }

        public void OnCancelClicked(Core.Custom.Input input)
        {
            onCancelInputEvent?.Invoke(input);
            SetVisible(false);
            ClearInput();
        }
    }
}