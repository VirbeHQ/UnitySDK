using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Virbe.Core.Custom;
using Virbe.Core.Data;
using Virbe.Core.Emotions;
using Virbe.Core.Gestures;

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
        public AudioParameters audioParameters;

        public bool HasAudio()
        {
            return speech != null;
        }

        public float GetAudioLength()
        {
            return speech?.Length > 0 ? ((float)speech.Length) / audioParameters.Channels / (audioParameters.SampleBits / 8f) / audioParameters.Frequency : 0;
        }

        [Serializable]
        public class Custom
        {
            public string payload;
            public string action;
            public string language;
            public JToken data;

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
                   // Debug.Log("Cannot parse response");
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
                    //Debug.Log("Cannot parse response");
                }

                return null;
            }

            public List<Virbe.Core.Custom.Input> ExtractVirbeInputs()
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
                                .Select(buttonDict => new Virbe.Core.Custom.Input((JObject)buttonDict))
                                .ToList();
                        }
                    }
                }
                catch (Exception)
                {
                    //Debug.Log("Cannot parse response");
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
                    //Debug.Log("Cannot parse response");
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