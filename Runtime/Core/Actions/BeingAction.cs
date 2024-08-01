using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Virbe.Core.Custom;
using Virbe.Core.Emotions;
using Virbe.Core.Gestures;
using VirbeInput = Virbe.Core.Custom.Input;

namespace Virbe.Core.Actions
{
    [Serializable]
    public struct BeingAction
    {
        public string text;
        public byte[] speech;
        public List<Mark> marks;
        public List<Button> buttons;
        public List<Card> cards;

        public Custom custom;
        
        // public static readonly int channels = 1;
        // public static readonly int samples = 4; // 16 bit sample per byte (8 bit)
        // public static readonly int frequency = 22050;

        public bool HasAudio()
        {
            return speech != null;
        }

        public float GetAudioLength(IApiBeingConfig beingConfig)
        {
            return speech?.Length > 0
                ? ((float)speech.Length) / beingConfig.FallbackTTSData.AudioChannels / (beingConfig.FallbackTTSData.AudioSampleBits / 8f) /
                  beingConfig.FallbackTTSData.AudioFrequency
                : 0;
        }

        [Serializable]
        public class Mark
        {
            public string type;
            public int time;
            public string value;
        }
        [Serializable]
        public class Custom
        {
            [CanBeNull] public string payload;
            [CanBeNull] public string action;
            [CanBeNull] public string language;
            [CanBeNull] public JToken data;

            public List<Emotion> ExtractVirbeEmotion()
            {
                try
                {
                    if (payload == "virbe" && data != null)
                    {
                        var behavioursTokens = data?["behaviours"];
                        if (behavioursTokens != null)
                        {
                            return behavioursTokens
                                .Where(element => (string)element["type"] == "emotion")
                                .Select(token => new Emotion(token))
                                .ToList();
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Cannot parse response");
                }

                return null;
            }

            public List<Gesture> ExtractVirbeGestures()
            {
                try
                {
                    if (payload == "virbe" && data != null)
                    {
                        var behavioursTokens = data?["behaviours"];
                        if (behavioursTokens != null)
                        {
                            return behavioursTokens
                                .Where(element => (string)element["type"] == "gesture")
                                .Select(gestureToken => new Gesture(gestureToken))
                                .ToList();
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Cannot parse response");
                }

                return null;
            }

            public List<VirbeInput> ExtractVirbeInputs()
            {
                try
                {
                    if (payload == "virbe" && data != null)
                    {
                        var uiObjects = data?["ui"];
                        if (uiObjects != null)
                        {
                            return uiObjects?
                                .Where(element => (string)element["type"] == "input")
                                .Select(buttonDict => new VirbeInput((JObject)buttonDict))
                                .ToList();
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Cannot parse response");
                }

                return null;
            }

            public List<Slide> ExtractSlides()
            {
                try
                {
                    if (payload == "virbe" && data != null)
                    {
                        var uiObjects = data?["ui"];
                        if (uiObjects != null)
                        {
                            return uiObjects
                                .Where(element =>
                                    (string)element["type"] == "slide")
                                .Select(slideDict => new Slide((JObject)slideDict))
                                .ToList();
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Cannot parse response");
                }

                return null;
            }

            public JToken ExtractRawJArray()
            {
                return data;
            }
        }
    }
}