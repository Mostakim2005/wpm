using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WPMOverlay.Services
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        // Virtual key codes for ignored modifiers
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

        private const int VK_BACK = 0x08;
        private const int VK_DELETE = 0x2E;

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

        /// <summary>
        /// Invoked when a typing key is pressed: (virtualKey, isBackspace)
        /// </summary>
        public event Action<int, bool>? KeyPressed;

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;

            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            string? moduleName = curModule?.ModuleName;

            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(moduleName), 0);
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (!IsIgnoredModifier(vkCode))
                {
                    bool isBackspace = (vkCode == VK_BACK || vkCode == VK_DELETE);
                    KeyPressed?.Invoke(vkCode, isBackspace);
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool IsIgnoredModifier(int vk)
        {
            return vk switch
            {
                VK_SHIFT => true,
                VK_CONTROL => true,
                VK_MENU => true,
                VK_CAPITAL => true,
                VK_LWIN => true,
                VK_RWIN => true,
                VK_APPS => true,
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

        ~KeyboardHook()
        {
            Stop();
        }
    }
}
