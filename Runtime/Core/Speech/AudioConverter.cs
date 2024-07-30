using System;
using UnityEngine;

namespace Virbe.Core.Speech
{
    internal static class AudioConverter
    {
        internal static string FromBytesToBase64(byte[] audioBytes)
        {
            return Convert.ToBase64String(audioBytes);
        }

        internal static byte[] FromBase64ToBytes(string audioBase64)
        {
            return audioBase64 != null ? Convert.FromBase64String(audioBase64) : null;
        }

        internal static float[] PCMBytesToFloats(byte[] bytes, int sampleBits)
        {
            int samples = sampleBits / 8;
            float[] floats = new float[bytes.Length / samples];
            for (int i = 0; i < bytes.Length; i += samples)
            {
                // if (BitConverter.IsLittleEndian) {
                //     Array.Reverse(bytes, i * 4, 4);
                // }
                
                if (sampleBits == 16)
                {
                    short s = BitConverter.ToInt16(bytes, i); // convert 2 bytes to short
                    floats[i / samples] = ((float)s) / (float)(Int16.MaxValue + 1); // convert short to float
                }
                else
                {
                    float s = BitConverter.ToSingle(bytes, i);
                    floats[i / samples] = ((float)s); // convert short to float
                }
            }

            return floats;
        }
    }
}