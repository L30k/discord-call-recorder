using System.Buffers.Binary;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace DiscordCallRecorder;

// Capture the endpoint's native format, then normalize it for the mixer.
// Linear interpolation is sufficient for speech; each packet remains anchored to its QPC time.
internal sealed class MicrophoneConverter
{
    private readonly WaveFormat format;
    private readonly bool floating;
    private readonly float[] converted = new float[48000 * 2];
    public MicrophoneConverter(WaveFormat format)
    {
        this.format = format;
        floating = format.Encoding == WaveFormatEncoding.IeeeFloat
            || format is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        bool pcm = format.Encoding == WaveFormatEncoding.Pcm
            || format is WaveFormatExtensible pcmExt && pcmExt.SubFormat == new Guid("00000001-0000-0010-8000-00aa00389b71");
        if ((!floating && !pcm) || (floating && format.BitsPerSample != 32)
            || (!floating && format.BitsPerSample is not (16 or 24 or 32)))
            throw new NotSupportedException(Localization.Format("Formato do microfone não suportado: {0}", format));
        Log.Write($"Formato nativo do microfone: {format}");
    }
    public ReadOnlySpan<byte> Convert(ReadOnlySpan<byte> input)
    {
        int frames = input.Length / format.BlockAlign;
        int count = (int)Math.Round(frames * 48000.0 / format.SampleRate);
        if (count * 2 > converted.Length) throw new IOException(Localization.Text("Pacote de áudio excedeu o buffer de um segundo."));
        for (int i = 0; i < count; i++)
        {
            double position = i * format.SampleRate / 48000.0;
            int first = Math.Min((int)position, frames - 1), second = Math.Min(first + 1, frames - 1);
            float a = ReadMono(input, first), b = ReadMono(input, second);
            float sample = a + (b - a) * (float)(position - first);
            converted[i * 2] = converted[i * 2 + 1] = sample;
        }
        return MemoryMarshal.AsBytes(converted.AsSpan(0, count * 2));
    }
    private float ReadMono(ReadOnlySpan<byte> input, int frame)
    {
        float value = 0;
        for (int channel = 0; channel < format.Channels; channel++)
        {
            var sample = input.Slice(frame * format.BlockAlign + channel * (format.BitsPerSample / 8));
            value += floating ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample)) : format.BitsPerSample switch
            {
                16 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f,
                24 => ((sample[0] | sample[1] << 8 | sample[2] << 16) << 8 >> 8) / 8388608f,
                32 => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648f,
                _ => 0
            };
        }
        return value / format.Channels;
    }
}
