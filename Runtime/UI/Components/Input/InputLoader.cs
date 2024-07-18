using UI.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Virbe.UI.Components.Input
{
    public class InputLoader : MonoBehaviour
    {
        Core.Custom.Input _input;
        
        [SerializeField] private Text InputLabel;
        [SerializeField] private Text InputValue;
        [SerializeField] private Text SubmitLabel;
        [SerializeField] private Button SubmitButton;
        [SerializeField] private Text CancelLabel;
        [SerializeField] private Button CancelButton;
        private InputClickListener _inputClickListener;


        public interface InputClickListener
        {
            void OnSubmitClicked(Core.Custom.Input input, string value);
            void OnCancelClicked(Core.Custom.Input  input);
        }

        public async void SetupInput(Core.Custom.Input input, InputClickListener inputClickListener)
        {
            Clear();
            
            _input = input;
            _inputClickListener = inputClickListener;

            InputLabel.text = input.InputLabel;
            
            SubmitLabel.text = input.SubmitButton.Title;
            SubmitButton.onClick.AddListener(() => { _inputClickListener.OnSubmitClicked(_input, InputValue.text); });
            
            CancelLabel.text = input.CancelButton.Title;
            CancelButton.onClick.AddListener(() => { _inputClickListener.OnCancelClicked(_input); });
            
        }

        public void SendInputValue(string value)
        {
            _inputClickListener.OnSubmitClicked(_input, value); 
        }

        public void Clear()
        {
            InputLabel.text = "";
            SubmitLabel.text = "";
            CancelLabel.text = "";
            SubmitButton.onClick.RemoveAllListeners();
            CancelButton.onClick.RemoveAllListeners(); 
        }
    }
    
}