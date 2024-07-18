using System;
using UnityEngine;

namespace Virbe.Core.VAD
{
    public static class AudioUtils
    {
        public static float ComputeRMS(float[] buffer, int offset, ref int length)
        {
            // sum of squares
            float sos = 0f;
            float val;
            if (offset + length > buffer.Length)
            {
                length = buffer.Length - offset;
            }

            for (int i = 0; i < length; i++)
            {
                val = buffer[offset];
                sos += val * val;
                offset++;
            }

            // return sqrt of average
            return Mathf.Sqrt(sos / length);
        }

        public static float ComputeDB(float[] buffer, int offset, ref int length, float refValue = 1f )
        {
            float rms;
            rms = ComputeRMS(buffer, offset, ref length);
            // could divide rms by reference power, simplified version here with ref power of 1f.
            // will return negative values: 0db is the maximum.
            return 20 * Mathf.Log10(rms / refValue);
        }

        public static void CalculateFFT(Complex[] samples, ref float[] result, bool reverse)
        {
            int power = (int)Mathf.Log(samples.Length, 2);
            int count = 1;
            for (int i = 0; i < power; i++)
                count <<= 1;

            int mid = count >> 1; // mid = count / 2;
            int j = 0;
            for (int i = 0; i < count - 1; i++)
            {
                if (i < j)
                {
                    (samples[i], samples[j]) = (samples[j], samples[i]);
                }

                int k = mid;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }

                j += k;
            }

            Complex r = new Complex(-1, 0);
            int l2 = 1;
            for (int l = 0; l < power; l++)
            {
                int l1 = l2;
                l2 <<= 1;
                Complex r2 = new Complex(1, 0);
                for (int n = 0; n < l1; n++)
                {
                    for (int i = n; i < count; i += l2)
                    {
                        int i1 = i + l1;
                        Complex tmp = r2 * samples[i1];
                        samples[i1] = samples[i] - tmp;
                        samples[i] += tmp;
                    }

                    r2 = r2 * r;
                }

                r.img = Math.Sqrt((1d - r.real) / 2d);
                if (!reverse)
                    r.img = -r.img;
                r.real = Math.Sqrt((1d + r.real) / 2d);
            }

            if (!reverse)
            {
                double scale = 1d / count;
                for (int i = 0; i < count; i++)
                    samples[i] *= scale;
                for (int i = 0; i < samples.Length / 2; i++)
                {
                    result[i] = (float)samples[i].magnitude;
                }
            }
            else
            {
                for (int i = 0; i < samples.Length / 2; i++)
                {
                    result[i] = (float)(Math.Sign(samples[i].real) * samples[i].magnitude);
                }
            }
        }

        public static void Float2Complex(float[] input, ref Complex[] result)
        {
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = new Complex(input[i], 0);
            }
        }
    }
}