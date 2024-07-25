using System;
using UnityEngine;
using UnityEngine.Events;
using Virbe.Core;
using Virbe.Core.Api;
using Virbe.Core.Custom;
using Virbe.UI.Components.BottomBar;
using Virbe.UI.Components.ProductCard;
using Virbe.UI.Components.QuickReply;
using Virbe.Core.VAD;
using Cysharp.Threading.Tasks;

namespace Virbe.UI.Layouts
{
    public class VirbePluginUIConnector : MonoBehaviour, IQuickReplyListener, IProductCardListener
    {
        [SerializeField] [Tooltip("Define which being should consume this layout events")]
        protected VirbeBeing _virbeBeing;

        [SerializeField] [Tooltip("Which voice recorder to use to start and stop recording on button press")]
        protected VirbeVoiceRecorder _virbeVoiceRecorder;
        
        [SerializeField] private BottomBarManager bottomBarManager;

        [SerializeField] private TextSubmitEvent onCustomTextSubmitEvent = new TextSubmitEvent();
        [SerializeField] private QuickReplyEvent onCustomQuickReplyEvent = new QuickReplyEvent();
        [SerializeField] private ProductLearnMoreEvent onCustomProductLearnMoreEvent = new ProductLearnMoreEvent();

        public void SwitchBeing(VirbeBeing being)
        {
            this._virbeBeing = being;
        }

        public void SwitchRecorder(VirbeVoiceRecorder recorder)
        {
            this._virbeVoiceRecorder = recorder;
        }

        public void CancelButtonPressDown()
        {
            _virbeBeing?.StopCurrentAndScheduledActions();
        }

        public void RecordButtonPressDown()
        {
            _virbeVoiceRecorder?.StartVoiceCapture();
        }

        public void RecordButtonPressUp()
        {
            _virbeVoiceRecorder?.StopVoiceCapture();
        }

        public void SubmitText(string text)
        {
            _virbeBeing?.StopCurrentAndScheduledActions();
            _virbeBeing?.SendText(text).Forget();
            onCustomTextSubmitEvent.Invoke(text);
        }
        
        public void SubmitInput(Core.Custom.Input input, string inputValue)
        {
            _virbeBeing.SubmitInput(input.SubmitButton.Payload, input.StoreKey, inputValue);
        }

        public void CancelInput(Core.Custom.Input input)
        {
            _virbeBeing.SendText(input.CancelButton.Payload).Forget();
        }
        
        public void OnQuickReplyClicked(Button button)
        {
            _virbeBeing?.StopCurrentAndScheduledActions();
            _virbeBeing?.SendText(button.Payload).Forget();
            onCustomQuickReplyEvent.Invoke(button);
            if (!String.IsNullOrEmpty(button.CallbackURL))
            {
                Application.OpenURL(button.CallbackURL);
            }
        }

        public void OnLearnMoreClicked(Card card)
        {
            if (!String.IsNullOrEmpty(card.CallbackURL))
            {
                Application.OpenURL(card.CallbackURL);
            }
            onCustomProductLearnMoreEvent.Invoke(card);
        }

        private void Update()
        {
            if (_virbeBeing != null && bottomBarManager != null)
            {
                bottomBarManager.SetBeingIsSpeaking(_virbeBeing.IsBeingSpeaking);
            }
        }
    }
}