using System;
using System.IO;

namespace SandGuard.Audio.Editor.Synth
{
    /// <summary>16비트 PCM WAV. Unity가 그대로 임포트한다.</summary>
    public static class WavWriter
    {
        public static void Write(string path, Clip clip)
        {
            int ch = clip.Channels, n = clip.Length, sr = Dsp.Sr;
            int dataBytes = n * ch * 2;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(sr);
            w.Write(sr * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            for (int i = 0; i < n; i++)
                for (int c = 0; c < ch; c++)
                {
                    float v = clip[c][i];
                    if (float.IsNaN(v) || float.IsInfinity(v)) v = 0;
                    w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, v)) * 32767f));
                }
        }
    }
}
