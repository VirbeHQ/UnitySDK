using UnityEngine;

namespace Virbe.Core.VAD
{
    public class PushToTalk : MonoBehaviour
    {
       
        private VirbeVoiceRecorder _voiceRecorder;
        private bool _isRecording = false;
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C) && !_isRecording)
            {
                _isRecording = true;
                _voiceRecorder.StartVoiceCapture();
            }
            else if(_isRecording && Input.GetKeyUp(KeyCode.C))
            {
                _isRecording = false;
                _voiceRecorder.StopVoiceCapture();
            }
          
        }
    }
}