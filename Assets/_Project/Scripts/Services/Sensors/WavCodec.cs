using System;
using UnityEngine;

namespace LostPetNetwork.Services.Sensors
{
    /// <summary>Reads and writes 16-bit mono PCM WAV files (the format used for SOS voice notes).</summary>
    public static class WavCodec
    {
        const int HeaderSize = 44;

        public static byte[] Encode(float[] samples, int sampleRate)
        {
            var bytes = new byte[HeaderSize + samples.Length * 2];
            WriteAscii(bytes, 0, "RIFF");
            WriteInt(bytes, 4, bytes.Length - 8);
            WriteAscii(bytes, 8, "WAVE");
            WriteAscii(bytes, 12, "fmt ");
            WriteInt(bytes, 16, 16);
            WriteShort(bytes, 20, 1);          // PCM
            WriteShort(bytes, 22, 1);          // mono
            WriteInt(bytes, 24, sampleRate);
            WriteInt(bytes, 28, sampleRate * 2);
            WriteShort(bytes, 32, 2);
            WriteShort(bytes, 34, 16);
            WriteAscii(bytes, 36, "data");
            WriteInt(bytes, 40, samples.Length * 2);
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
                WriteShort(bytes, HeaderSize + i * 2, s);
            }
            return bytes;
        }

        /// <summary>Returns false (and no samples) when the data is not a mono 16-bit PCM WAV.</summary>
        public static bool TryDecode(byte[] wav, out float[] samples, out int sampleRate)
        {
            samples = null;
            sampleRate = 0;
            if (wav == null || wav.Length < HeaderSize) return false;
            if (Ascii(wav, 0) != "RIFF" || Ascii(wav, 8) != "WAVE" || Ascii(wav, 12) != "fmt ") return false;
            if (ReadShort(wav, 20) != 1 || ReadShort(wav, 22) != 1 || ReadShort(wav, 34) != 16) return false;
            sampleRate = ReadInt(wav, 24);
            if (sampleRate <= 0) return false;

            int pos = 12;
            while (pos + 8 <= wav.Length)
            {
                string id = Ascii(wav, pos);
                int size = ReadInt(wav, pos + 4);
                if (id == "data")
                {
                    int available = Math.Min(size, wav.Length - (pos + 8));
                    int count = available / 2;
                    samples = new float[count];
                    for (int i = 0; i < count; i++) samples[i] = ReadShort(wav, pos + 8 + i * 2) / 32768f;
                    return true;
                }
                if (size < 0) return false;
                pos += 8 + size;
            }
            return false;
        }

        static void WriteAscii(byte[] b, int at, string s) { for (int i = 0; i < s.Length; i++) b[at + i] = (byte)s[i]; }
        static void WriteInt(byte[] b, int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24); }
        static void WriteShort(byte[] b, int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); }
        static string Ascii(byte[] b, int at) => new string(new[] { (char)b[at], (char)b[at + 1], (char)b[at + 2], (char)b[at + 3] });
        static int ReadInt(byte[] b, int at) => b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
        static short ReadShort(byte[] b, int at) => (short)(b[at] | (b[at + 1] << 8));
    }
}
