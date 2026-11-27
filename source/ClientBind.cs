using System;
using System.Collections.Generic;
using System.IO;

namespace KavrosAfkWarden
{
    public sealed class ClientCandidate
    {
        public long Handle;
        public int Pid;
        public string Title;
        public string Process;
        public string ClassName;
        public int Width;
        public int Height;
        public bool Foreground;
    }

    // One Warden stays on the client it was opened from. A passed hwnd or pid
    // is the client under the Companion overlay. It does not move to another
    // client, and a closed client stays closed until a new toggle names one.
    public static class ClientBind
    {
        public static long Choose(List<ClientCandidate> windows, long current, long passedHwnd, int passedPid)
        {
            if (windows == null) windows = new List<ClientCandidate>();
            if (current > 0)
            {
                if (Alive(windows, current)) return current;
                long rebound = Named(windows, passedHwnd, passedPid);
                if (rebound > 0) return rebound;
                return -1;
            }
            long named = Named(windows, passedHwnd, passedPid);
            if (named > 0) return named;
            long foreground = 0;
            long only = 0;
            int count = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                if (!Game(windows[i])) continue;
                count++;
                if (only == 0) only = windows[i].Handle;
                if (windows[i].Foreground) foreground = windows[i].Handle;
            }
            if (foreground > 0) return foreground;
            if (count == 1) return only;
            return 0;
        }

        private static long Named(List<ClientCandidate> windows, long passedHwnd, int passedPid)
        {
            if (passedHwnd > 0 && Alive(windows, passedHwnd)) return passedHwnd;
            if (passedPid <= 0) return 0;
            long foreground = 0;
            long only = 0;
            long largest = 0;
            int area = -1;
            int count = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                ClientCandidate window = windows[i];
                if (window.Pid != passedPid || !Game(window)) continue;
                count++;
                if (only == 0) only = window.Handle;
                if (window.Foreground) foreground = window.Handle;
                int size = window.Width * window.Height;
                if (size > area) { area = size; largest = window.Handle; }
            }
            if (foreground > 0) return foreground;
            if (count == 1) return only;
            if (count > 1) return largest;
            return 0;
        }

        private static bool Alive(List<ClientCandidate> windows, long handle)
        {
            for (int i = 0; i < windows.Count; i++)
                if (windows[i].Handle == handle && Game(windows[i])) return true;
            return false;
        }

        private static bool Game(ClientCandidate window)
        {
            if (window == null || window.Handle == 0) return false;
            return WindowScore.Score(window.Title, window.Process, window.ClassName, window.Width, window.Height) >= 100;
        }
    }

    public static class ClientLaunch
    {
        public static long Hwnd;
        public static int Pid;
        public static bool NoTray;

        public static void ReadArgs(string[] args)
        {
            if (args == null) return;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? "";
                if (Same(arg, "--no-tray")) { NoTray = true; continue; }
                string name;
                string value;
                if (!Split(arg, out name, out value))
                {
                    if (i + 1 >= args.Length) continue;
                    name = arg;
                    value = args[i + 1];
                    if (!IsHandle(name) && !IsPid(name)) continue;
                    i++;
                }
                long number;
                if (!long.TryParse(value, out number) || number <= 0) continue;
                if (IsHandle(name)) Hwnd = number;
                else if (IsPid(name)) Pid = (int)number;
            }
        }

        public static void Clear()
        {
            Hwnd = 0;
            Pid = 0;
        }

        public static void ReadEnvironment()
        {
            ApplyEnv("COMPANION_CLIENT_HWND", true);
            ApplyEnv("KAVROS_CLIENT_HWND", true);
            ApplyEnv("COMPANION_CLIENT_PID", false);
            ApplyEnv("KAVROS_CLIENT_PID", false);
        }

        public static void WriteRequest()
        {
            if (Hwnd <= 0 && Pid <= 0) return;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "bind.txt"), Hwnd.ToString() + "\n" + Pid.ToString());
            }
            catch (Exception) { }
        }

        public static void ReadRequest()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden", "bind.txt");
                if (!File.Exists(path)) return;
                string text = File.ReadAllText(path);
                File.Delete(path);
                string[] lines = text.Split('\n');
                long hwnd;
                int pid;
                if (lines.Length > 0 && long.TryParse(lines[0].Trim(), out hwnd) && hwnd > 0) Hwnd = hwnd;
                if (lines.Length > 1 && int.TryParse(lines[1].Trim(), out pid) && pid > 0) Pid = pid;
            }
            catch (Exception) { }
        }

        private static void ApplyEnv(string name, bool hwnd)
        {
            string text = Environment.GetEnvironmentVariable(name);
            long number;
            if (text == null || !long.TryParse(text.Trim(), out number) || number <= 0) return;
            if (hwnd) Hwnd = number;
            else Pid = (int)number;
        }

        private static bool Split(string arg, out string name, out string value)
        {
            name = "";
            value = "";
            int cut = arg.IndexOf('=');
            if (cut < 0) cut = arg.IndexOf(':');
            if (cut <= 0 || cut >= arg.Length - 1) return false;
            name = arg.Substring(0, cut);
            value = arg.Substring(cut + 1);
            return IsHandle(name) || IsPid(name);
        }

        private static bool IsHandle(string name)
        {
            return Same(name, "--hwnd") || Same(name, "--client-hwnd") || Same(name, "/hwnd") || Same(name, "hwnd") || Same(name, "--clienthwnd");
        }

        private static bool IsPid(string name)
        {
            return Same(name, "--pid") || Same(name, "--client-pid") || Same(name, "/pid") || Same(name, "pid") || Same(name, "--clientpid");
        }

        private static bool Same(string text, string value)
        {
            return string.Equals(text ?? "", value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
