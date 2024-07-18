using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
using UnityEngine.UI;
#endif
namespace Virbe.Core.VAD
{
    public class Mic : MonoBehaviour
    {
#region MEMBERS

        private bool _hasPermissionToRecord;

        /// <summary>
        /// Whether the microphone is running
        /// </summary>
        public bool IsRecording { get; private set; }

        /// <summary>
        /// The frequency at which the mic is operating
        /// </summary>
        public int Frequency { get; private set; }

        /// <summary>
        /// Last populated audio sample
        /// </summary>
        public float[] Sample { get; private set; }

        /// <summary>
        /// Sample duration/length in milliseconds
        /// </summary>
        public int SampleDurationMS { get; private set; }

        /// <summary>
        /// The length of the sample float array
        /// </summary>
        public int SampleLength => Frequency * SampleDurationMS / 1000;

        /// <summary>
        /// The AudioClip currently being streamed in the Mic
        /// </summary>
        public AudioClip AudioClip { get; private set; }

        /// <summary>
        /// List of all the available Mic devices
        /// </summary>
        public static List<string> Devices { get; private set; }
        /// <summary>
        /// Gets the name of the Mic device currently in use
        /// </summary>
        public string CurrentDeviceName { get; private set; }

        public string PreferredDeviceName;


        long m_SampleCount = 0;

#endregion

#region EVENTS

        /// <summary>
        /// Invoked when the instance starts Recording.
        /// </summary>
        public event Action OnStartRecording;

        /// <summary>
        /// Invoked everytime an audio frame is collected. Includes the frame.
        /// </summary>
        public event Action<long, float[]> OnSampleReady;

        /// <summary>
        /// Invoked when the instance stop Recording.
        /// </summary>
        public event Action OnStopRecording;

#endregion

#region METHODS

        static Mic m_Instance;

        public static Mic Instance
        {
            get
            {
                if(m_Instance == null)
                {
                    Initialize();
                }
                return m_Instance;
            }
        }


        void Awake()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (m_Instance == null)
                m_Instance = GameObject.FindObjectOfType<Mic>();
            if (m_Instance == null)
            {
                m_Instance = new GameObject("UniMic.Mic").AddComponent<Mic>();
                DontDestroyOnLoad(m_Instance.gameObject);
            }
            UpdateDevices();
        }

        private void RequestMicPermission(Action onComplete, Action<Exception> onError)
        {
#if UNITY_ANDROID
            AndroidRuntimePermissions.RequestPermissionAsync(Permission.Microphone, (permission, result) =>
            {
                if (result == AndroidRuntimePermissions.Permission.Granted) onComplete();
                else onError(new Exception("Microphone permission not granted."));
            });
#else
            StartCoroutine(RequestUserAuthorization(UserAuthorization.Microphone, onComplete));
#endif
        }

        private IEnumerator RequestUserAuthorization(UserAuthorization userAuthorization, Action onComplete)
        {
            yield return Application.RequestUserAuthorization(userAuthorization);
            onComplete();
        }

        private bool CheckHasMicPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidRuntimePermissions.CheckPermission(UnityEngine.Android.Permission.Microphone) ==
                   AndroidRuntimePermissions.Permission.Granted;
#else
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
        }

        private void CheckPermission()
        {
            _hasPermissionToRecord = CheckHasMicPermission();
        }

        public static void UpdateDevices()
        {
            Devices = new List<string>();
            foreach (var device in Microphone.devices)
                Devices.Add(device);
        }

        public void ChangeDevice(string preferredDeviceName)
        {
            if (preferredDeviceName != null)
            {
                if (Devices.Exists((deviceName => deviceName.Equals(preferredDeviceName))))
                {
                    PreferredDeviceName = preferredDeviceName;
                }
                else
                {
                    Debug.LogWarning($"Device {preferredDeviceName} does not exist!");
                }
            }

            StartRecording(Frequency, SampleDurationMS);
        }

        /// <summary>
        /// Starts to stream the input of the current Mic device
        /// </summary>
        public void StartRecording(int frequency, int sampleLen)
        {
            CheckPermission();

            if (!_hasPermissionToRecord)
            {
                RequestMicPermission(
                    () => StartRecording(frequency, sampleLen),
                    exception =>
                    {
                        throw new Exception("Cannot configure recording as there you have no permission to record.");
                    }
                );

                return;
            }

            StopRecording();

            if (PreferredDeviceName != null && Devices.Exists((deviceName => deviceName.Equals(PreferredDeviceName))))
            {
                CurrentDeviceName = PreferredDeviceName;
                Debug.Log($"Start recording with preferred device: {PreferredDeviceName}");
            }
            else if (!Devices.Exists((deviceName => deviceName.Equals(CurrentDeviceName))))
            {
                // Device must have disconnected need to find a new one
                CurrentDeviceName = Devices[0];
                Debug.Log(
                    $"Previous device might have been disconnected. Start recording with default: {CurrentDeviceName}");
            }
            else
            {
                Debug.Log($"Start recording with a previous device: {CurrentDeviceName}");
            }

            try
            {
                Debug.Log($"Starting recording for frequency: {frequency} and sampleLen: {sampleLen}");
                Frequency = frequency;
                SampleDurationMS = sampleLen;

                AudioClip = Microphone.Start(CurrentDeviceName, true, 10, Frequency);
                Sample = new float[Frequency / 1000 * SampleDurationMS * AudioClip.channels];

                StartCoroutine(ReadRawAudio());

                IsRecording = true;

                if (OnStartRecording != null)
                    OnStartRecording.Invoke();
            }
            catch (ArgumentException e)
            {
                // TODO recording failed what should we do    
                Debug.LogError($"Microphone error not started: {e.Message}");
            }
        }

        public bool HasBeenRecording() => Microphone.IsRecording(CurrentDeviceName) || IsRecording;

        public int GetCurrentMicPosition() => Microphone.GetPosition(CurrentDeviceName);

        public void GetMicCaps(out int minFreq, out int maxFreq) => Microphone.GetDeviceCaps(CurrentDeviceName, out minFreq, out maxFreq);

        public float MeasureCurrentDB(int _sampleWindow = 128, float refValue = 1f)
        {
            float[] waveData = new float[_sampleWindow];
            int micPosition = GetCurrentMicPosition() - (_sampleWindow + 1); 
            if (micPosition < 0) return 0;
            AudioClip.GetData(waveData, micPosition);

            return AudioUtils.ComputeDB(waveData, 0, ref _sampleWindow, refValue);
        }

        /// <summary>
        /// Ends the Mic stream.
        /// </summary>
        public void StopRecording()
        {
            IsRecording = false;

            if (Microphone.IsRecording(CurrentDeviceName))
            {
                Microphone.End(CurrentDeviceName);
            }

            Destroy(AudioClip);
            AudioClip = null;

            StopCoroutine(ReadRawAudio());

            if (OnStopRecording != null)
                OnStopRecording.Invoke();
        }

        IEnumerator ReadRawAudio()
        {
            int loops = 0;
            int readAbsPos = 0;
            int prevPos = 0;
            float[] temp = new float[Sample.Length];

            bool isSystemMicrophoneRecording;
            try
            {
                isSystemMicrophoneRecording = Microphone.IsRecording(CurrentDeviceName);
            }
            catch (ArgumentException exception)
            {
                isSystemMicrophoneRecording = false;
                // TODO mic not recording but it's not stopped restarting microphone
                Debug.LogError($"Microphone error from coroutin ReadRawAudio: {exception.Message}");
            }

            while (AudioClip != null && isSystemMicrophoneRecording)
            {
                bool isNewDataAvailable = true;

                while (isNewDataAvailable)
                {
                    int currPos = GetCurrentMicPosition();
                    if (currPos < prevPos)
                        loops++;
                    prevPos = currPos;

                    var currAbsPos = loops * AudioClip.samples + currPos;
                    var nextReadAbsPos = readAbsPos + temp.Length;

                    if (nextReadAbsPos < currAbsPos)
                    {
                        AudioClip.GetData(temp, readAbsPos % AudioClip.samples);

                        Sample = temp;
                        m_SampleCount++;
                        OnSampleReady?.Invoke(m_SampleCount, Sample);

                        readAbsPos = nextReadAbsPos;
                        isNewDataAvailable = true;
                    }
                    else
                        isNewDataAvailable = false;
                }

                yield return null;
            }
        }

#endregion
    }
}