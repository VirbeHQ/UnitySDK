using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Virbe.Core.ThirdParty.SavWav;

namespace Virbe.Core.VAD
{
    public class VirbeVoiceRecorder : MonoBehaviour
    {
        class SampleQueueEntry
        {
            public float[] Data { get; set; }
            public float Time { get; set; }
        }

        private float _recordingStartTime;
        private float _recordingStopRequestedTime;

        private bool _hasPermissionToRecord;
        private bool _hasMicrophoneAvailable;
        private AudioClip _currentRecording;
        private int _samplesOffset;

        public delegate void MicRecordingError(Exception exception);

        public MicRecordingError ONMicRecordingError;

        [Header("Virbe Being to send to a user recorded speech")] [SerializeField]
        private VirbeBeing virbeBeing;

        [SerializeField] private bool keepConstantRecording = true;
        [SerializeField] [Range(5, 15)] private float maxRecordingTime = 15f;

        [SerializeField] private float keepRecordingAfterStopTime = 0.5f;
        [SerializeField] private float minRecordingSampleTime = 0.8f;

        [Header("(Optional) Where to play a user recorded speech")] [SerializeField]
        private AudioSource userSpeechAudioSource;

        [SerializeField] private bool _saveWaveSamples = false;
        private bool _isRecordingSamples;
        private int _recordingSegmentOffset;
        private bool _stopRecordRequested = false;
        private Queue<SampleQueueEntry> _samplesCache;
        private int _currentAddedEntries;
        private int _queueCapacity = 100;
        private SttConnectionProtocol _connectionProtocol;

        /// <summary>
        /// List of all the available Mic devices
        /// </summary>
        public List<string> Devices { get; private set; }

        private void Awake()
        {
            _samplesCache = new Queue<SampleQueueEntry>(_queueCapacity);

            UpdateDevices();
        }
        private void Start()
        {
            _connectionProtocol = virbeBeing.ApiBeingConfig.SttProtocol;
        }

        public void UpdateDevices()
        {
            Devices = new List<string>();
            foreach (var device in Microphone.devices)
            {
                Devices.Add(device);
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


        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            Debug.Log(deviceWasChanged ? "Device was changed" : "Reset was called");
            if (deviceWasChanged)
            {
                // AudioConfiguration config = AudioSettings.GetConfiguration();
                // AudioSettings.Reset(config);

                Mic.UpdateDevices();
                Mic.Instance.StopRecording();
                // if (Mic.Instance.HasBeenRecording())
                // {
                // Mic.Instance.StartRecording(16000, 32);
                // }
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
            segment.CopyTo(entry.Data, 0);
            _samplesCache.Enqueue(entry);

            if (!_isRecordingSamples)
            {
                return;
            }

            try
            {
                if(_connectionProtocol == SttConnectionProtocol.socket_io)
                {
                    //int clipLen = (int)(Mic.Instance.AudioClip.channels * Mic.Instance.AudioClip.frequency * (Mic.Instance.SampleDurationMS/1000f + 0.1f));
                    //var recording = AudioClip.Create("clip", clipLen, Mic.Instance.AudioClip.channels, Mic.Instance.AudioClip.frequency, false);
                    //recording.SetData(entry.Data, 0);

                    var recordingBytes = SavWav.GetWavF(
                        samples: entry.Data,
                        frequency: (uint)Mic.Instance.AudioClip.frequency,
                        channels: (ushort)Mic.Instance.AudioClip.channels,
                        length: out _
                    );
                    virbeBeing.SendSpeechChunk(recordingBytes);
                }
                else
                {
                    _currentRecording.SetData(entry.Data, _recordingSegmentOffset);
                    _recordingSegmentOffset += entry.Data.Length;
                }

            }
            catch (System.ArgumentException e)
            {
                Debug.LogError($"Cannot read mic samples: {e.Message}");
            }
        }

        public void SetBeing(VirbeBeing being)
        {
            virbeBeing = being;
        }

        public void StartRecordingSamples(float startTalkingTime = 0)
        {
            if (!_isRecordingSamples && Mic.Instance.IsRecording)
            {
                virbeBeing?.UserHasStartedSpeaking();
                
                _stopRecordRequested = false;

                if (Mic.Instance.AudioClip.channels != 0 && Mic.Instance.AudioClip.frequency != 0)
                {
                    int clipLen = (int)(Mic.Instance.AudioClip.channels * Mic.Instance.AudioClip.frequency *
                                        maxRecordingTime); //Currently 15s

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
                    Debug.LogError("Cannot create proper AudioClip for mic recording");
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
                Debug.Log($"paused talking");
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

                    var recordingBytes = SavWav.GetWavF(
                        samples: trimmedRecording,
                        frequency: (uint)_currentRecording.frequency,
                        channels: (ushort)_currentRecording.channels,
                        length: out _
                    );

                    if (_saveWaveSamples)
                    {
                        var path = Path.Combine(Application.dataPath,"TestRecordings");
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        File.WriteAllBytes($"{Application.dataPath}/TestRecordings/{DateTime.Now:HH_mm_ss}.wav",
                            recordingBytes);
                    }
                    if(_connectionProtocol == SttConnectionProtocol.http)
                    {
                        virbeBeing.SendSpeechBytes(recordingBytes).Forget();
                    }
                }
                virbeBeing?.UserHasStoppedSpeaking();
                Destroy(_currentRecording);
            }
        }

        public void StartVoiceCapture()
        {
            Debug.Log("record");
            if (virbeBeing == null)
            {
                Debug.LogError("Set virbe being before record start");
                return;
            }

            if (!Mic.Instance.IsRecording)
            {
                Mic.Instance.StartRecording(16000, 128);
            }

            StartRecordingSamples(Time.time -0.5f);
        }

        public void StopVoiceCapture()
        {
            Debug.Log("stop record");
            if (virbeBeing == null)
            {
                Debug.LogError("Set virbe being before record start");
                return;
            }

            StopRecordingSamples();
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
                    Debug.Log($"paused talking");

                    StopRecordingSamplesInternal();
                }
            }
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

        public bool isUserSpeaking()
        {
            return _isRecordingSamples;
        }

        public bool isBeingSpeaking()
        {
            return virbeBeing != null && virbeBeing.isBeingSpeaking();
        }
    }
}