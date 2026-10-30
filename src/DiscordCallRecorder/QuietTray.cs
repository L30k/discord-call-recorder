using System.Runtime.InteropServices;

namespace DiscordCallRecorder;

// Shell notification API allows NIIF_NOSOUND, which WinForms NotifyIcon doesn't expose.
internal sealed class QuietTray : NativeWindow, IDisposable
{
    private const int Callback = 0x8001;
    private readonly ContextMenuStrip menu;
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NotifyData data;
    private Icon icon;
    private string text = "Discord Recorder • Aguardando";
    public QuietTray(ContextMenuStrip menu, Icon icon)
    {
        this.menu = menu; this.icon = icon;
        CreateHandle(new CreateParams { Caption = "Discord Recorder Tray" });
        data = new NotifyData { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = Handle, Id = 1, CallbackMessage = Callback,
            Flags = 1 | 2 | 4, Icon = icon.Handle, Tip = text, Info = "", InfoTitle = "" };
        Shell_NotifyIcon(0, ref data);
    }
    public string Text
    {
        set { if (text == value) return; text = value; data.Tip = value; data.Flags = 4; Shell_NotifyIcon(1, ref data); }
    }
    public Icon Icon
    {
        set { if (ReferenceEquals(icon, value)) return; icon = value; data.Icon = value.Handle; data.Flags = 2; Shell_NotifyIcon(1, ref data); }
    }
    public void Notify(string title, string message)
    {
        data.Flags = 0x10; data.InfoTitle = title[..Math.Min(63, title.Length)];
        data.Info = message[..Math.Min(255, message.Length)]; data.InfoFlags = 0x10; // NIIF_NOSOUND
        data.Timeout = 4000; Shell_NotifyIcon(1, ref data);
    }
    protected override void WndProc(ref Message m)
    {
        if ((uint)m.Msg == taskbarCreated)
        { data.Flags = 1 | 2 | 4; Shell_NotifyIcon(0, ref data); }
        if (m.Msg == Callback && ((int)m.LParam == 0x205 || (int)m.LParam == 0x202))
        { SetForegroundWindow(Handle); menu.Show(Cursor.Position); }
        base.WndProc(ref m);
    }
    public void Dispose() { Shell_NotifyIcon(2, ref data); menu.Dispose(); DestroyHandle(); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, CallbackMessage; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
