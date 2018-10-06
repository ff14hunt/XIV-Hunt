using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using FFXIV_GameSense.Properties;

namespace FFXIV_GameSense
{
    static class SoundPlayer
    {
        public static WaveOutEvent WaveDevice { get; private set; }

        public static async Task Play(AudioFileReader reader)
        {
            while (WaveDevice?.PlaybackState == PlaybackState.Playing)
                await Task.Delay(10);
            WaveDevice = new WaveOutEvent
            {
                Volume = Settings.Default.Volume,
                DeviceNumber = FindSelectedDeviceNumber()
            };
            reader.Position = 0;
            WaveDevice.Init(reader);
            WaveDevice.Play();
            WaveDevice.PlaybackStopped += delegate 
            {
                WaveDevice.Dispose();
            };
        }

        private static int FindSelectedDeviceNumber()
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++)
                if (WaveOut.GetCapabilities(i).ProductName == Settings.Default.AudioDevice)
                    return i;
            return -1;
        }
    }
}
