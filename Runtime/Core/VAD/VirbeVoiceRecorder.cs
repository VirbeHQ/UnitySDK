using System;
using System.Collections.Generic;
using UnityEngine;
using Virbe.Core.Logger;

namespace Virbe.Core.VAD
{
    public class VirbeVoiceRecorder : MonoBehaviour
    {
        class SampleQueueEntry
        {
            public float[] Data { get; set; }
            public float Time { get; set; }
        }
        public event Action<float[]> OnChunkAudioReady;
        public event Action<float[]> OnFullAudioReady;

        public event Action OnStartSpeaking;
        public event Action OnStopSpeaking;

        public bool IsUserSpeaking => _isRecordingSamples;

        [SerializeField] private bool keepConstantRecording = true;

        [Range(5, 15)]
        [SerializeField] private float maxRecordingTime = 15f;
        [SerializeField] private float keepRecordingAfterStopTime = 0.5f;
        [SerializeField] private float minRecordingSampleTime = 0.8f;

        [Header("(Optional) Where to play a user recorded speech")]
        [SerializeField] private AudioSource userSpeechAudioSource;

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(RoomCommunicationHandler));

        private float _recordingStartTime;
        private float _recordingStopRequestedTime;

        private bool _hasPermissionToRecord;
        private bool _hasMicrophoneAvailable;
        private AudioClip _currentRecording;
        private int _samplesOffset;
      
        private bool _isRecordingSamples;
        private int _recordingSegmentOffset;
        private bool _stopRecordRequested = false;
        private Queue<SampleQueueEntry> _samplesCache;
        private int _currentAddedEntries;
        private int _queueCapacity = 100;
        private List<string> _devices;

        private void Awake()
        {
            _samplesCache = new Queue<SampleQueueEntry>(_queueCapacity);

            _devices = new List<string>();
            foreach (var device in Microphone.devices)
            {
                _devices.Add(device);
            }
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            Mic.Instance.OnSampleReady += MicOnOnSampleReady;
        }

        private void OnDisable()
        {
            Mic.Instance.OnSampleReady -= MicOnOnSampleReady;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        public void Update()
        {
            if (_isRecordingSamples)
            {
                if (Time.time - _recordingStartTime > maxRecordingTime || (_stopRecordRequested &&
                                                                           Time.time - _recordingStopRequestedTime >=
                                                                           keepRecordingAfterStopTime))
                {
                    if (Mic.Instance.IsRecording && !keepConstantRecording)
                    {
                        Mic.Instance.StopRecording();
                    }
                    _logger.Log($"paused talking");
                    StopRecordingSamplesInternal();
                }
            }
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _logger.Log(deviceWasChanged ? "Device was changed" : "Reset was called");
            if (deviceWasChanged)
            {
                Mic.UpdateDevices();
                Mic.Instance.StopRecording();
            }
        }

        private void MicOnOnSampleReady(long index, float[] segment)
        {
            if(_currentAddedEntries < _queueCapacity)
            {
                ++_currentAddedEntries;
            }
            else
            {
                _samplesCache.Dequeue();
            }

            var entry = new SampleQueueEntry()
            {
                Data = new float[segment.Length],
                Time = Time.time
            };

            try
            {
                segment.CopyTo(entry.Data, 0);
                _samplesCache.Enqueue(entry);
            }
            catch (ArgumentException e)
            {
                _logger.LogError($"Cannot read mic samples: {e.Message}");
                return;
            }

            if (!_isRecordingSamples)
            {
                return;
            }

            _currentRecording.SetData(entry.Data, _recordingSegmentOffset);
            _recordingSegmentOffset += entry.Data.Length;
            OnChunkAudioReady?.Invoke(entry.Data);
          
        }

        public void StartRecordingSamples(float startTalkingTime = 0)
        {
            if (!_isRecordingSamples && Mic.Instance.IsRecording)
            {
                OnStartSpeaking?.Invoke();
                
                _stopRecordRequested = false;

                if (Mic.Instance.AudioClip.channels != 0 && Mic.Instance.AudioClip.frequency != 0)
                {
                    int clipLen = (int)(Mic.Instance.AudioClip.channels * Mic.Instance.AudioClip.frequency *
                                        maxRecordingTime);

                    _currentRecording = AudioClip.Create("clip", clipLen, Mic.Instance.AudioClip.channels,
                        Mic.Instance.AudioClip.frequency, false);

                    _recordingSegmentOffset = 0;
                    _recordingStartTime = Time.time;

                    if (!Mathf.Approximately(startTalkingTime, 0))
                    {
                        _recordingStartTime = startTalkingTime;
                        foreach (var entry in _samplesCache)
                        {
                            if(entry == null)
                            {
                                continue;
                            }
                            if (entry.Time >= startTalkingTime)
                            {
                                _currentRecording.SetData(entry.Data, _recordingSegmentOffset);
                                _recordingSegmentOffset += entry.Data.Length;
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                   
                    _isRecordingSamples = true;
                }
                else
                {
                    // TODO something is no yes with Mic AudioClip
                    _logger.LogError("Cannot create proper AudioClip for mic recording");
                }
            }
            else if (_stopRecordRequested)
            {
                _recordingStopRequestedTime = Time.time;
            }
        }

        public void StopRecordingSamples()
        {
            if (!_stopRecordRequested)
            {
                // TODO if recording was shorter than X do not send it to server
                _logger.Log($"paused talking");
                _stopRecordRequested = true;
                _recordingStopRequestedTime = Time.time;
            }
        }

        private void StopRecordingSamplesInternal()
        {
            if (_isRecordingSamples)
            {
                _isRecordingSamples = false;
                _stopRecordRequested = false;
                _recordingSegmentOffset = 0;
                var recordingStopTime = Time.time;

                if (_currentRecording != null && (recordingStopTime - _recordingStartTime > minRecordingSampleTime))
                {
                    float[] trimmedRecording = TrimToTimeFrame(
                        audioClip: _currentRecording,
                        timeFrame: recordingStopTime - _recordingStartTime,
                        samplesOffset: _samplesOffset
                    );

                    OnFullAudioReady?.Invoke(trimmedRecording);
                }
                OnStopSpeaking?.Invoke();

                Destroy(_currentRecording);
            }
        }

        public void StartVoiceCapture()
        {
            _logger.Log("recording");

            if (!Mic.Instance.IsRecording)
            {
                Mic.Instance.StartRecording(16000, 128);
            }

            StartRecordingSamples(Time.time -0.5f);
        }

        public void StopVoiceCapture()
        {
            _logger.Log("stop record");

            StopRecordingSamples();
        }

        private float[] TrimToTimeFrame(AudioClip audioClip, float timeFrame, int samplesOffset = 0)
        {
            var samplesLength = (int)(timeFrame * audioClip.frequency * audioClip.channels);
            var dataEndPosition = samplesOffset + samplesLength;
            var lengthDiff = audioClip.samples - dataEndPosition;
            if (lengthDiff < 0)
            {
                samplesLength += lengthDiff;
            }

            var samplesData = new float[samplesLength];
            audioClip.GetData(samplesData, samplesOffset);

            return samplesData;
        }
    }
}