using System.Runtime.InteropServices;

namespace DiscordCallRecorder;

// Fixed-size timeline: silence is implicit, so gaps never shift later audio forward.
internal sealed class TimelineBuffer
{
    private readonly float[] samples = new float[48000 * 4];
    private readonly long[] positions = Enumerable.Repeat(-1L, 48000 * 4).ToArray();
    private readonly object gate = new();
    public void Add(ReadOnlySpan<byte> bytes, long start, bool silent)
    {
        var data = MemoryMarshal.Cast<byte, float>(bytes);
        lock (gate)
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            long frame = start + i / 2;
            if (frame < 0) continue;
            int index = (int)(frame % samples.Length);
            float value = silent ? 0 : (data[i] + data[i + 1]) * 0.5f;
            samples[index] = float.IsFinite(value) ? value : 0;
            positions[index] = frame;
        }
    }
    public void Mix(Span<float> output, long start, float gain = 0.7f)
    {
        lock (gate)
        for (var i = 0; i < output.Length; i++)
        {
            long frame = start + i;
            int index = (int)(frame % samples.Length);
            if (positions[index] == frame) output[i] += samples[index] * gain;
        }
    }
}
