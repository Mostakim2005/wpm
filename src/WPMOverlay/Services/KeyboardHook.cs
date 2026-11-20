using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WPMOverlay.Services
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const uint LLKHF_INJECTED = 0x10;
        private const uint LLKHF_LOWER_IL_INJECTED = 0x02;

        // Virtual key codes for modifiers & system/non-typing keys
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_PAUSE = 0x13;
        private const int VK_CAPITAL = 0x14; // CapsLock
        private const int VK_ESCAPE = 0x1B;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_APPS = 0x5D;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int VK_NUMLOCK = 0x90;
        private const int VK_SCROLL = 0x91;

        private const int VK_BACK = 0x08;
        private const int VK_DELETE = 0x2E;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private bool _disposed;

        // Modifier key states tracked directly from hook events (no GetKeyState lag)
        private bool _lCtrlDown;
        private bool _rCtrlDown;
        private bool _lAltDown;
        private bool _rAltDown;
        private bool _lWinDown;
        private bool _rWinDown;

        public bool IsHookActive => _hookId != IntPtr.Zero;
        public int LastErrorCode { get; private set; }

        /// <summary>
        /// Invoked when a typing key is pressed: (virtualKey, isBackspace).
        /// Subscribers must be non-blocking and O(1) (Trap 3).
        /// </summary>
        public event Action<int, bool>? KeyPressed;

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;

            ResetModifierState();
            _proc = HookCallback;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);

            if (_hookId == IntPtr.Zero)
            {
                LastErrorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"SetWindowsHookEx failed with Win32 error {LastErrorCode}.");
            }
            else
            {
                LastErrorCode = 0;
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            ResetModifierState();
        }

        private void ResetModifierState()
        {
            _lCtrlDown = false;
            _rCtrlDown = false;
            _lAltDown = false;
            _rAltDown = false;
            _lWinDown = false;
            _rWinDown = false;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    int vkCode = (int)info.vkCode;

                    UpdateModifierState(vkCode, isKeyDown);

                    if (isKeyDown)
                    {
                        bool isInjected = (info.flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) != 0;
                        if (!isInjected && !IsShortcutChordActive() && !IsIgnoredKey(vkCode))
                        {
                            bool isBackspace = vkCode == VK_BACK || vkCode == VK_DELETE;
                            KeyPressed?.Invoke(vkCode, isBackspace);
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void UpdateModifierState(int vkCode, bool isDown)
        {
            switch (vkCode)
            {
                case VK_LCONTROL:
                    _lCtrlDown = isDown;
                    break;
                case VK_RCONTROL:
                    _rCtrlDown = isDown;
                    break;
                case VK_CONTROL:
                    _lCtrlDown = isDown;
                    break;
                case VK_LMENU:
                    _lAltDown = isDown;
                    break;
                case VK_RMENU:
                    _rAltDown = isDown;
                    break;
                case VK_MENU:
                    _lAltDown = isDown;
                    break;
                case VK_LWIN:
                    _lWinDown = isDown;
                    break;
                case VK_RWIN:
                    _rWinDown = isDown;
                    break;
            }
        }

        private bool IsShortcutChordActive()
        {
            if (_lWinDown || _rWinDown) return true;

            bool ctrlDown = _lCtrlDown || _rCtrlDown;
            // Allow AltGr (Left Ctrl + Right Alt) for international keyboard layouts
            bool isAltGr = _lCtrlDown && _rAltDown;
            if (ctrlDown && !isAltGr) return true;
            if (_lAltDown && !ctrlDown) return true;

            return false;
        }

        public static bool IsIgnoredKey(int vk)
        {
            // Navigation & editing control keys (PageUp..Down, PrintScreen, Insert)
            if (vk >= 0x21 && vk <= 0x28) return true;
            if (vk == 0x2C || vk == 0x2D) return true;

            // Function keys F1-F24
            if (vk >= 0x70 && vk <= 0x87) return true;

            // Browser & Media keys
            if (vk >= 0xA6 && vk <= 0xB7) return true;

            return vk switch
            {
                VK_SHIFT => true,
                VK_CONTROL => true,
                VK_MENU => true,
                VK_PAUSE => true,
                VK_CAPITAL => true,
                VK_ESCAPE => true,
                VK_LWIN => true,
                VK_RWIN => true,
                VK_APPS => true,
                VK_NUMLOCK => true,
                VK_SCROLL => true,
                VK_LSHIFT => true,
                VK_RSHIFT => true,
                VK_LCONTROL => true,
                VK_RCONTROL => true,
                VK_LMENU => true,
                VK_RMENU => true,
                _ => false
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
