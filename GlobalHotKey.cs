using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ShowWrite
{
    public class GlobalHotKey : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;
        private const int WM_HOTKEY = 0x0312;

        private readonly IntPtr _windowHandle;
        private readonly int _hotKeyId;
        private bool _isRegistered;
        private IntPtr _oldWndProc;
        private WndProcDelegate? _wndProcDelegate;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public event Action? HotKeyPressed;

        public GlobalHotKey(IntPtr windowHandle, int hotKeyId = 1)
        {
            _windowHandle = windowHandle;
            _hotKeyId = hotKeyId;
        }

        public bool Register(string shortcutKey)
        {
            if (_isRegistered)
                Unregister();

            var (modifiers, key) = ParseShortcut(shortcutKey);
            if (key == 0)
                return false;

            _isRegistered = RegisterHotKey(_windowHandle, _hotKeyId, modifiers, key);
            
            if (_isRegistered && _oldWndProc == IntPtr.Zero)
            {
                _wndProcDelegate = WndProc;
                _oldWndProc = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
            }

            return _isRegistered;
        }

        public void Unregister()
        {
            if (_isRegistered)
            {
                UnregisterHotKey(_windowHandle, _hotKeyId);
                _isRegistered = false;
            }
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _hotKeyId)
            {
                HotKeyPressed?.Invoke();
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private static (uint modifiers, uint key) ParseShortcut(string shortcut)
        {
            if (string.IsNullOrEmpty(shortcut))
                return (0, 0);

            uint modifiers = 0;
            uint key = 0;

            var parts = shortcut.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                switch (trimmed.ToLower())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= 0x0002;
                        break;
                    case "alt":
                        modifiers |= 0x0001;
                        break;
                    case "shift":
                        modifiers |= 0x0004;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= 0x0008;
                        break;
                    default:
                        key = ParseKey(trimmed);
                        break;
                }
            }

            return (modifiers, key);
        }

        private static uint ParseKey(string keyName)
        {
            return keyName.ToUpper() switch
            {
                "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44,
                "E" => 0x45, "F" => 0x46, "G" => 0x47, "H" => 0x48,
                "I" => 0x49, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
                "M" => 0x4D, "N" => 0x4E, "O" => 0x4F, "P" => 0x50,
                "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
                "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58,
                "Y" => 0x59, "Z" => 0x5A,
                "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33,
                "4" => 0x34, "5" => 0x35, "6" => 0x36, "7" => 0x37,
                "8" => 0x38, "9" => 0x39,
                _ => 0
            };
        }

        public void Dispose()
        {
            Unregister();
            
            if (_oldWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }
    }
}
