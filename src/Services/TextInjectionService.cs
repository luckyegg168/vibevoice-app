using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace VibeVoice.Services;

/// <summary>
/// Text injection service using Windows SendInput API.
/// Injects text into the currently focused window.
/// </summary>
public class TextInjectionService
{
    private readonly ILogger<TextInjectionService> _logger;
    private readonly SettingsService _settingsService;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy, mouseData;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_UNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUT_UNION u;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public TextInjectionService(ILogger<TextInjectionService> logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task InjectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) return;

        var delay = _settingsService.Current.TypeDelayMs;
        _logger.LogInformation("Injecting {Length} chars with {Delay}ms delay", text.Length, delay);

        foreach (char c in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendChar(c);
            if (delay > 0)
                await Task.Delay(delay, cancellationToken);
        }
    }

    private void SendChar(char c)
    {
        var inputs = new INPUT[2];

        // Key down
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUT_UNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        // Key up
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUT_UNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        uint sent = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        if (sent != 2)
        {
            int err = Marshal.GetLastWin32Error();
            _logger.LogWarning("SendInput returned {Sent} for char '{Char}', error={Err}", sent, c, err);
        }
    }
}
