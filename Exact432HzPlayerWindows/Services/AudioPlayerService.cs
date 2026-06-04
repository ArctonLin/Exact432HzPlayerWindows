using System;
using ManagedBass;
using ManagedBass.Fx;

namespace Exact432HzPlayerWindows.Services
{
    public class AudioPlayerService : IDisposable
    {
        private int _stream;
        private SyncProcedure? _endSync;
        public event EventHandler? PlaybackEnded;

        private double _volume = 1.0;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                if (_stream != 0)
                {
                    Bass.ChannelSetAttribute(_stream, ChannelAttribute.Volume, _volume);
                }
            }
        }

        public AudioPlayerService()
        {
            Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero);
            
            // Try load common plugins (some have underscores, some don't depending on the source)
            string[] plugins = { 
                "bassflac.dll", 
                "bass_aac.dll", "bassaac.dll",
                "basswma.dll", 
                "bass_opus.dll", "bassopus.dll", 
                "bass_ape.dll", "bassape.dll", 
                "bass_alac.dll", "bassalac.dll" 
            };
            
            foreach (var plugin in plugins)
            {
                int handle = Bass.PluginLoad(plugin);
                // If it fails (handle == 0), it just means we don't have the dll, which is fine, we continue.
            }
        }

        public static string GetDurationString(string filePath)
        {
            int stream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode);
            if (stream != 0)
            {
                long len = Bass.ChannelGetLength(stream);
                double seconds = Bass.ChannelBytes2Seconds(stream, len);
                Bass.StreamFree(stream);
                return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            }
            return "--:--";
        }

        public static string GetAudioDetails(string filePath)
        {
            int stream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
            if (stream == 0) return "Unable to load audio details.";

            try
            {
                var info = Bass.ChannelGetInfo(stream);
                long lenBytes = Bass.ChannelGetLength(stream);
                double seconds = Bass.ChannelBytes2Seconds(stream, lenBytes);
                var duration = TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
                
                long fileSize = new System.IO.FileInfo(filePath).Length;
                int bitrate = seconds > 0 ? (int)Math.Round((fileSize * 8.0) / (seconds * 1000.0)) : 0; // kbps

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"File: {System.IO.Path.GetFileName(filePath)}");
                sb.AppendLine($"Duration: {duration}");
                sb.AppendLine($"Sample Rate: {info.Frequency} Hz");
                sb.AppendLine($"Channels: {(info.Channels == 1 ? "Mono" : info.Channels == 2 ? "Stereo" : info.Channels.ToString())}");
                sb.AppendLine($"Resolution: {(info.Resolution == 0 ? 16 : info.Resolution)} bits");
                sb.AppendLine($"Estimated Bitrate: ~{bitrate} kbps");
                
                return sb.ToString();
            }
            finally
            {
                Bass.StreamFree(stream);
            }
        }

        public void Play(string filePath, double maxFreq)
        {
            Stop();

            int decodeStream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
            if (decodeStream == 0) return;

            _stream = BassFx.TempoCreate(decodeStream, BassFlags.FxFreeSource | BassFlags.AutoFree);
            
            if (_stream != 0)
            {
                // Ratio calculation for pitch shift without tempo change
                double ratio = 432.0 / maxFreq;
                double semitones = 12.0 * Math.Log(ratio, 2);

                Bass.ChannelSetAttribute(_stream, (ChannelAttribute)65537, semitones); // BASS_ATTRIB_TEMPO_PITCH
                Bass.ChannelSetAttribute(_stream, ChannelAttribute.Volume, _volume);
                
                // Keep a reference to the delegate so the GC doesn't collect it
                _endSync = new SyncProcedure((handle, channel, data, user) => 
                {
                    PlaybackEnded?.Invoke(this, EventArgs.Empty);
                });

                // Set sync for end of stream
                Bass.ChannelSetSync(_stream, SyncFlags.End | SyncFlags.Mixtime, 0, _endSync);

                Bass.ChannelPlay(_stream);
            }
        }

        public void Stop()
        {
            if (_stream != 0)
            {
                Bass.ChannelStop(_stream);
                Bass.StreamFree(_stream);
                _stream = 0;
            }
        }

        public void Pause()
        {
            if (_stream != 0) Bass.ChannelPause(_stream);
        }

        public void Resume()
        {
            if (_stream != 0) Bass.ChannelPlay(_stream);
        }

        public double GetPositionSeconds()
        {
            if (_stream == 0) return 0;
            long pos = Bass.ChannelGetPosition(_stream);
            return Bass.ChannelBytes2Seconds(_stream, pos);
        }

        public double GetTotalSeconds()
        {
            if (_stream == 0) return 0;
            long len = Bass.ChannelGetLength(_stream);
            return Bass.ChannelBytes2Seconds(_stream, len);
        }

        public void SetPositionSeconds(double seconds)
        {
            if (_stream == 0) return;
            long pos = Bass.ChannelSeconds2Bytes(_stream, seconds);
            Bass.ChannelSetPosition(_stream, pos);
        }

        public void Dispose()
        {
            Stop();
            Bass.Free();
        }
    }
}
