using UnityEngine;
using Virbe.Core.VAD;

namespace Virbe.Core
{
    public class VoiceBeingController : MonoBehaviour
    {
        [SerializeField] private VirbeVoiceRecorder _voiceRecorder;
        [SerializeField] private VirbeBeing _being;

        protected virtual void OnEnable()
        {
            _voiceRecorder.OnStartSpeaking += _being.UserHasStartedSpeaking;
            _voiceRecorder.OnStopSpeaking += _being.UserHasStoppedSpeaking;
            _voiceRecorder.OnChunkAudioReady += SendChunk;
            _voiceRecorder.OnFullAudioReady += SendFullAudio;
        }

        protected virtual void OnDisable()
        {
            _voiceRecorder.OnStartSpeaking -= _being.UserHasStartedSpeaking;
            _voiceRecorder.OnStopSpeaking -= _being.UserHasStoppedSpeaking;
            _voiceRecorder.OnChunkAudioReady -= SendChunk;
            _voiceRecorder.OnFullAudioReady -= SendFullAudio;
        }

        private void SendFullAudio(float[] audio) => _being.SendSpeechBytes(audio, false);
        private void SendChunk(float[] audio) => _being.SendSpeechBytes(audio, true);
    }
}