using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ManagedBass;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace Exact432HzPlayerWindows.Services
{
    public class AudioAnalyzer
    {
        private static readonly double[] ToneFreq = new double[]
        {
            16.35,17.32,18.35,19.45,20.6,21.83,23.12,24.5,25.96,27.5,29.14,30.87,32.7,34.65,36.71,38.89,41.2,43.65,46.25,49,51.91,55,58.27,61.74,65.41,69.3,73.42,77.78,82.41,87.31,92.5,98,103.83,110,116.54,123.47,130.81,138.59,146.83,155.56,164.81,174.61,185,196,207.65,220,233.08,246.94,261.63,277.18,293.66,311.13,329.63,349.23,369.99,392,415.3,440,466.16,493.88,523.25,554.37,587.33,622.25,659.25,698.46,739.99,783.99,830.61,880,932.33,987.77,1046.5,1108.73,1174.66,1244.51,1318.51,1396.91,1479.98,1567.98,1661.22,1760,1864.66,1975.53,2093,2217.46,2349.32,2489.02,2637.02,2793.83,2959.96,3135.96,3322.44,3520,3729.31,3951.07,4186.01,4434.92,4698.63,4978.03,5274.04,5587.65,5919.91,6271.93,6644.88,7040,7458.62,7902.13
        };

        private static string GetCacheFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "Exact432HzPlayerWindows");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "cache.json");
        }

        private static Dictionary<string, double> LoadCache()
        {
            var path = GetCacheFilePath();
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
                }
                catch { }
            }
            return new Dictionary<string, double>();
        }

        private static void SaveCache(Dictionary<string, double> cache)
        {
            try
            {
                var path = GetCacheFilePath();
                var json = JsonSerializer.Serialize(cache);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public static void ClearCache()
        {
            var path = GetCacheFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static async Task<double> AnalyzeToneAsync(string filePath)
        {
            var cache = LoadCache();
            if (cache.ContainsKey(filePath))
            {
                return cache[filePath];
            }

            double maxFreq = await Task.Run(() =>
            {
                int decodeHandle = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Mono | BassFlags.Float);
                if (decodeHandle == 0) return 440.0; // Default if fail

                Bass.ChannelGetInfo(decodeHandle, out ChannelInfo info);
                long bytesToRead = Bass.ChannelSeconds2Bytes(decodeHandle, 100.0);
                long fileLengthBytes = Bass.ChannelGetLength(decodeHandle);
                
                if (bytesToRead > fileLengthBytes) bytesToRead = fileLengthBytes;
                if (bytesToRead <= 0) 
                {
                    Bass.StreamFree(decodeHandle);
                    return 440.0;
                }

                float[] buffer = new float[bytesToRead / 4];
                Bass.ChannelGetData(decodeHandle, buffer, (int)bytesToRead);
                Bass.StreamFree(decodeHandle);

                int samplesLen = buffer.Length;
                int powerOf2 = 1;
                while (powerOf2 < samplesLen) powerOf2 <<= 1;

                Complex32[] complexSamples = new Complex32[powerOf2];
                for (int i = 0; i < samplesLen; i++)
                {
                    complexSamples[i] = new Complex32(buffer[i], 0);
                }

                Fourier.Forward(complexSamples, FourierOptions.NoScaling);

                float[] fftRealNormed = new float[powerOf2];
                for (int i = 0; i < powerOf2; i++)
                {
                    fftRealNormed[i] = complexSamples[i].Magnitude / samplesLen;
                }

                double freqstep = (double)info.Frequency / powerOf2;
                
                double maxSum = 0;
                double maxFreqResult = 440.0;

                for (double frequency = 424.0; frequency <= 448.1; frequency += 0.1)
                {
                    double sum = 0;
                    foreach (double baseFreq in ToneFreq)
                    {
                        double freq = baseFreq * frequency / 440.0;
                        int index = (int)(freq / freqstep);
                        if (index >= 0 && index < powerOf2)
                        {
                            sum += fftRealNormed[index];
                        }
                    }

                    if (sum > maxSum)
                    {
                        maxSum = sum;
                        maxFreqResult = frequency;
                    }
                }
                
                return Math.Round(maxFreqResult, 1);
            });

            cache[filePath] = maxFreq;
            SaveCache(cache);
            return maxFreq;
        }
    }
}
