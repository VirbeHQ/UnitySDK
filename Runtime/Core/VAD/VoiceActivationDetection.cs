using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virbe.Core.VAD
{
    [RequireComponent(typeof(VirbeVoiceRecorder))]
    public class VoiceActivationDetection: MonoBehaviour
    {
        [Header("Disable when being is speaking")] [SerializeField]
        private bool disableWhenBeingIsSpeaking = false;
        
        [SerializeField] private float _speakToBackgroundDiff = 3.3f;
        [SerializeField] private float _talkingPauseTimeToStop = .75f;
        [SerializeField] private float _talkingBeginOffsetTime = .2f; //Seconds
        [SerializeField] private float _minRmsTreshold = 30f;
        [SerializeField] private float _rmsWindowTimeSize = 3;
        [SerializeField] private int _pitchTreshold = 80;

        private VirbeVoiceRecorder _voiceRecorder;
        private int _recordingLength = 60;
        private int _recordingWindowBeginOffset;
        /// <summary>
        /// frame time = sample_size / _frequency
        /// </summary>
        private int _frequency = 16_000;

        private int _amplitudeArrayLength = 256;
        private int _spectrumArrayLength = 2048;
        private float _pitchThreshold = 0f;

        private float[] _amplitudes;
        private Complex[] _spectrum;
        private float[] _spectrumSamples;
        private float[] _spectrumResult;

        private Queue<float> _rmsValues;

        private float _currentTalkingPauseTime;
        private bool _stopCalled;

        private string _microphone;
        private int _currentCapacity;
        private int _defaultCapacity;

        private void Awake()
        {
            _voiceRecorder = GetComponent<VirbeVoiceRecorder>();
        }

        public void ChangeSpeakToBackground(float value)
        {
            _speakToBackgroundDiff = value;
        }

        private void OnEnable()
        {
            var mic = Mic.Instance;
            if (!mic.IsRecording)
            {
                mic.StartRecording(16000, 32);
            }
        }

        private void OnDisable()
        {
            var mic = Mic.Instance;
            if (mic.IsRecording)
            {
                mic.StopRecording();    
            }
        }

        void Start()
        {
            
            Application.targetFrameRate = 60;
            _amplitudes = new float[_amplitudeArrayLength];
            _spectrum = new Complex[_spectrumArrayLength];
            _spectrumSamples = new float[_spectrumArrayLength];
            _spectrumResult = new float[_spectrumArrayLength];
            var fpsTarget = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            _currentCapacity = 0;
            _defaultCapacity = (int)(fpsTarget * _rmsWindowTimeSize);
            _recordingWindowBeginOffset = (int)(_frequency * _talkingBeginOffsetTime);
            _rmsValues = new Queue<float>(_defaultCapacity);
            Debug.Log($"Sound process frame time is {(float)_amplitudeArrayLength / _frequency}");
        }

        private void Update()
        {
            if (disableWhenBeingIsSpeaking && _voiceRecorder.isBeingSpeaking())
            {
                return;
            }

            var talking = VoiceDetection();
            if (talking)
            {
                if (!_voiceRecorder.isUserSpeaking())
                {
                    Debug.Log($"Started talking");
                    // TODO missing begin talking
                    // int samplesOffset = Microphone.GetPosition(_microphone) - _recordingWindowBeginOffset;
                    // samplesOffset = samplesOffset > 0 ? samplesOffset : 0;
                    _voiceRecorder.StartRecordingSamples();
                }

                _currentTalkingPauseTime = 0;
                _stopCalled = false;
            }
            else if (!talking && _voiceRecorder.isUserSpeaking())
            {
                if (_currentTalkingPauseTime < _talkingPauseTimeToStop)
                {
                    _currentTalkingPauseTime += Time.deltaTime;
                }
                else if (!_stopCalled)
                {
                    _stopCalled = true;
                    Debug.Log($"stopped talking");
                    _voiceRecorder.StopRecordingSamples();
                }
            }
        }

        private bool VoiceDetection()
        {
            int mic_pos = Mic.Instance.GetCurrentMicPosition() - (_amplitudeArrayLength + 1);
            if (mic_pos < 0) 
            {
                return false;
            }

            try
            {
                Mic.Instance.AudioClip.GetData(_amplitudes, mic_pos);
            }
            catch (Exception e)
            {
                Debug.LogError($"There was audio error {e.Message}");
                // TODO need new audio clip 
                return false;
            }

            var sum = 0f;
            for (int i = 0; i < _amplitudes.Length; ++i)
            {
                sum += _amplitudes[i] * _amplitudes[i];
            }

            var rms = Mathf.Sqrt(sum / _amplitudes.Length);

            var hasLargeDiff = false;
            var addValue = rms;
            var haveMaxElements = _currentCapacity >= _defaultCapacity - 1;
            if (!haveMaxElements)
            {
                _currentCapacity++;
            }
            else
            {
                sum = 0f;
                foreach (var val in _rmsValues)
                {
                    sum += val;
                }

                var avgRms = sum / _currentCapacity;
                hasLargeDiff = rms > (avgRms * _speakToBackgroundDiff);
                if (hasLargeDiff)
                {
                    addValue = avgRms;
                }

                _rmsValues.Dequeue();
            }

            _rmsValues.Enqueue(addValue);

            if (!haveMaxElements)
            {
                return false;
            }

            if (rms < _minRmsTreshold || !hasLargeDiff)
            {
                return false;
            }

            //calculate frequencies
            // Different mic_position for Spectrum analysis
            Mic.Instance.AudioClip.GetData(_spectrumSamples, mic_pos);
            AudioUtils.Float2Complex(_spectrumSamples, ref _spectrum);
            AudioUtils.CalculateFFT(_spectrum, ref _spectrumResult, false);
            var maxV = 0f;
            var maxN = 0;
            for (int i = 0; i < _spectrumResult.Length; i++)
            {
                if (_spectrumResult[i] > maxV)
                {
                    maxV = _spectrumResult[i];
                    maxN = i;
                }
            }

            float freqN = maxN;
            if (maxN > 0 && maxN < _spectrumResult.Length - 1)
            {
                var dL = _spectrumResult[maxN - 1] / _spectrumResult[maxN];
                var dR = _spectrumResult[maxN + 1] / _spectrumResult[maxN];
                freqN += 0.5f * (dR * dR - dL * dL);
            }

            // TODO SampleRate from microphone
            var pitchValue = freqN * (AudioSettings.outputSampleRate / 2) / _spectrumResult.Length;

            return pitchValue > _pitchTreshold;

            //var dbValue = 20 * Mathf.Log10(rms / _refRmsValue);
        }
    }
}