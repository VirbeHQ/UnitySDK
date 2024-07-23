using UnityEngine;
using UnityEngine.UI;
using Virbe.Core;
using Virbe.Core.Actions;

namespace Virbe.UI.Components.BottomBar
{
    public class BottomBarManager : VirbeUIComponent
    {
        public GameObject RecordButton;
        public GameObject CancelButton;
        public InputField InputText;


        public void SetUserDetectedText(string text)
        {
            if (InputText != null)
            {
                InputText.SetTextWithoutNotify(text);
            }
        }

        public void SetVoiceRecording(bool isRecording)
        {
            if (InputText != null && isRecording)
            {
                InputText.SetTextWithoutNotify("Listening...");
            }
        }

        public void SetBeingIsSpeaking(bool isSpeaking)
        {
            CancelButton.SetActive(isSpeaking);
            RecordButton.SetActive(!isSpeaking);
        }

        public override void BeingStateChanged(BeingState beingState)
        {
            // Ignore at the moment
        }

        public override void UserActionPlayed(UserAction beingAction)
        {
            SetUserDetectedText(beingAction.text);
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            // Ignore at the moment
        }

        public override void SetVisible(bool visible)
        {
            // Ignore at the moment
        }
    }
}