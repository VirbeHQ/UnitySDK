using UnityEngine;

namespace Virbe.Core.VAD
{
    [RequireComponent(typeof(AudioSource))]
    public class MicAudioSource : MonoBehaviour {
        void Start() {
            var audioSource = gameObject.GetComponent<AudioSource>();

            var mic = Mic.Instance;
            mic.StartRecording();

            mic.OnSampleReady += (index, segment) => {
                var clip = AudioClip.Create("clip", 1600, mic.AudioClip.channels, mic.AudioClip.frequency, false);
                clip.SetData(segment, 0);
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.mute = false;
                audioSource.Play();
            };
        }
    }
}