using UnityEngine;
using UnityEngine.UI;

namespace Virbe.UI.Components.QuickReply
{
    public interface IQuickReplyListener
    {
        void OnQuickReplyClicked(Core.Custom.Button button);
    }

    public class QuickReplyLoader : MonoBehaviour
    {
        private Core.Custom.Button _button;
        [SerializeField] private Text Text;
        [SerializeField] private Button Button;


        public void SetQuickReply(Core.Custom.Button button, IQuickReplyListener quickReplyListener)
        {
            _button = button;
            Text.text = button.Title;
            Button.onClick.RemoveAllListeners();
            Button.onClick.AddListener(() => { quickReplyListener.OnQuickReplyClicked(_button); });
        }
    }
}