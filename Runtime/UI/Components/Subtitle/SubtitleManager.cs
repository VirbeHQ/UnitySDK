using UnityEngine;
using UnityEngine.UI;
using Virbe.Core;
using Virbe.Core.Actions;

namespace Virbe.UI.Components.Subtitle
{
    public class SubtitleManager : VirbeUIComponent
    {
        [SerializeField] private Text subtitle;

        public override void BeingStateChanged(BeingState beingState)
        {
            // Ignore at the moment
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            SetMessage(beingAction.text);
        }

        public void SetMessage(string message)
        {
            if (subtitle)
            {
                if (!string.IsNullOrEmpty(message?.Trim()))
                {
                    subtitle.text = message;
                    SetVisible(true);
                }
                else
                {
                    subtitle.text = "";
                    SetVisible(false);
                }
            }
        }

        public override void SetVisible(bool visible)
        {
            if (subtitle)
            {
                this.gameObject.SetActive(!string.IsNullOrEmpty(subtitle.text?.Trim()) && visible);
            }
        }
    }
}