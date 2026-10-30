using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace DiscordCallRecorder;

internal sealed record CallTarget(int ProcessId, long StartTicks, string DeviceId);

internal static class CallDetector
{
    public static CallTarget? Find()
    {
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        using (device)
        {
            var sessions = device.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            using (var session = sessions[i])
            {
                if (session.State != NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive) continue;
                try
                {
                    using var process = Process.GetProcessById((int)session.GetProcessID);
                    if (!IsDiscord(process.ProcessName)) continue;
                    var root = RootProcess(process.Id);
                    using var parent = Process.GetProcessById(root);
                    return new(root, parent.StartTime.ToUniversalTime().Ticks, device.ID);
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }
        }
        return null;
    }

    private static bool IsDiscord(string name) => name.Equals("Discord", StringComparison.OrdinalIgnoreCase)
        || name.Equals("DiscordPTB", StringComparison.OrdinalIgnoreCase)
        || name.Equals("DiscordCanary", StringComparison.OrdinalIgnoreCase);

    private static int RootProcess(int pid)
    {
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return pid;
        try
        {
            var entries = new Dictionary<int, ProcessEntry>();
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (Process32First(snapshot, ref entry)) do { entries[(int)entry.Id] = entry; } while (Process32Next(snapshot, ref entry));
            var visited = new HashSet<int>();
            while (visited.Add(pid) && entries.TryGetValue(pid, out var current)
                && entries.TryGetValue((int)current.ParentId, out var parent)
                && IsDiscord(Path.GetFileNameWithoutExtension(parent.Exe))) pid = (int)parent.Id;
            return pid;
        }
        finally { CloseHandle(snapshot); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Id;
        public UIntPtr Heap;
        public uint Module, Threads, ParentId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}

// Stable changes only: notifications and signal amplitude are never call triggers.
internal sealed class Debouncer<T> where T : class
{
    private T? candidate;
    private TimeSpan since;
    public T? Current { get; private set; }
    public bool Update(T? observed, TimeSpan now)
    {
        if (!Equals(observed, candidate)) { candidate = observed; since = now; }
        if (Equals(Current, candidate) || now - since < TimeSpan.FromSeconds(candidate is null ? 3 : 2)) return false;
        Current = candidate;
        return true;
    }
    public void Reset() { candidate = Current = null; since = TimeSpan.Zero; }
}
