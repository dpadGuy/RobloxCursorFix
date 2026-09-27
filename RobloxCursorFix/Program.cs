using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

class RobloxCursorFix
{
    // WinAPI
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);

    // Constants
    const int SM_CXSCREEN = 0;
    const int SM_CYSCREEN = 1;
    const int VK_OEM_3 = 0xC0; // Backtick (`) / tilde key
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    const int SW_HIDE = 0;

    // State
    static bool running = true;
    static bool enabled = false;
    static Process magnifierProcess = null;

    static void Main()
    {
        Console.WriteLine("Backtick (`) toggle cursor lock started");
        Console.WriteLine("Press ` to toggle ON/OFF");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c REG ADD \"HKCU\\Software\\Microsoft\\ScreenMagnifier\" /v Magnification /t REG_DWORD /d 100 /f",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {

        }

        new Thread(KeyListener) { IsBackground = true }.Start();
        new Thread(RobloxWatcher) { IsBackground = true }.Start();

        while (running)
        {
            if (enabled)
            {
                int centerX = GetSystemMetrics(SM_CXSCREEN) / 2;
                int centerY = GetSystemMetrics(SM_CYSCREEN) / 2;
                SetCursorPos(centerX, centerY);
            }

            Thread.Sleep(5);
        }
    }

    static void KeyListener()
    {
        bool lastState = false;

        while (running)
        {
            bool backtickPressed = (GetAsyncKeyState(VK_OEM_3) & 0x8000) != 0;

            if (backtickPressed && !lastState)
            {
                enabled = !enabled;

                if (enabled)
                {
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                }
                else
                {
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                }
            }

            lastState = backtickPressed;
            Thread.Sleep(50);
        }
    }

    static void RobloxWatcher()
    {
        while (running)
        {
            bool robloxRunning =
                Process.GetProcessesByName("RobloxPlayerBeta").Length > 0 ||
                Process.GetProcessesByName("Roblox").Length > 0;

            if (robloxRunning)
            {
                // Start Magnifier if not running
                if (magnifierProcess == null || magnifierProcess.HasExited)
                {
                    try
                    {
                        magnifierProcess = Process.Start("magnify.exe");
                        Console.WriteLine("[+] Magnifier started.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[!] Failed to start Magnifier: " + ex.Message);
                    }
                }

                // 👇 Continuously hide Magnifier while Roblox is running
                while (robloxRunning)
                {
                    IntPtr hWnd = FindWindow("MagUIClass", null);
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_HIDE);
                    }

                    Thread.Sleep(100); // check 10x per second

                    // Re-check Roblox state
                    robloxRunning =
                        Process.GetProcessesByName("RobloxPlayerBeta").Length > 0 ||
                        Process.GetProcessesByName("Roblox").Length > 0;
                }

                // Roblox closed → kill Magnifier
                if (magnifierProcess != null && !magnifierProcess.HasExited)
                {
                    try
                    {
                        magnifierProcess.Kill();
                        magnifierProcess = null;
                        Console.WriteLine("[+] Magnifier closed.");
                    }
                    catch { }
                }
            }

            Thread.Sleep(1000); // check Roblox state every second
        }
    }
}
