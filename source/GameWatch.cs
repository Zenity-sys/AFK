using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;

namespace KavrosAfkWarden
{
    internal sealed class GameWindow
    {
        internal IntPtr Handle;
        internal string Title = "";
        internal string ProcessName = "";
        internal string ClassName = "";
        internal uint ProcessId;
        internal Rectangle Client;
        internal string Label;
        public override string ToString() { return string.IsNullOrEmpty(Label) ? Title : Label; }
    }

    internal static class WindowLocator
    {
        internal static List<GameWindow> Enumerate(int excludedProcessId)
        {
            List<GameWindow> list = new List<GameWindow>();
            Dictionary<uint, string> names = new Dictionary<uint, string>();
            NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                if (!NativeMethods.IsWindowVisible(window)) return true;
                uint pid;
                NativeMethods.GetWindowThreadProcessId(window, out pid);
                if (pid == excludedProcessId) return true;
                StringBuilder title = new StringBuilder(512);
                NativeMethods.GetWindowText(window, title, title.Capacity);
                if (title.Length == 0) return true;
                StringBuilder className = new StringBuilder(256);
                NativeMethods.GetClassName(window, className, className.Capacity);
                string process;
                if (!names.TryGetValue(pid, out process))
                {
                    try { using (Process found = Process.GetProcessById((int)pid)) process = found.ProcessName; }
                    catch (Exception) { process = ""; }
                    names[pid] = process;
                }
                GameWindow game = new GameWindow();
                game.Handle = window;
                game.Title = title.ToString();
                game.ProcessName = process;
                game.ClassName = className.ToString();
                game.ProcessId = pid;
                game.Client = NativeMethods.ClientBounds(window);
                list.Add(game);
                return true;
            }, IntPtr.Zero);
            list.Sort(delegate(GameWindow a, GameWindow b) { return Score(b).CompareTo(Score(a)); });
            return list;
        }

        internal static int Score(GameWindow window)
        {
            if (window == null) return -1000;
            return WindowScore.Score(window.Title, window.ProcessName, window.ClassName, window.Client.Width, window.Client.Height);
        }

        internal static GameWindow Choose(List<GameWindow> windows, IntPtr current, string rememberedTitle)
        {
            GameWindow currentWindow = null;
            GameWindow titled = null;
            GameWindow only = null;
            int count = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                if (Score(windows[i]) < 100) continue;
                count++;
                if (only == null) only = windows[i];
                if (windows[i].Handle == current) currentWindow = windows[i];
                if (rememberedTitle != null && rememberedTitle.Length > 0 && string.Equals(windows[i].Title, rememberedTitle, StringComparison.Ordinal)) titled = windows[i];
            }
            if (currentWindow != null) return currentWindow;
            if (titled != null) return titled;
            if (count == 1) return only;
            return null;
        }

        internal static string ClientLabel(GameWindow window, int index)
        {
            uint pid = window == null ? 0 : window.ProcessId;
            string title = window == null || window.Title == null ? "" : window.Title;
            return ClientIdentity.Label(pid, index, title);
        }

        internal static bool IsForeground(IntPtr game)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || game == IntPtr.Zero) return false;
            return foreground == game || NativeMethods.GetAncestor(foreground, 2) == game;
        }

        internal static bool OverGame(IntPtr game, int x, int y)
        {
            IntPtr hit = NativeMethods.WindowFromPoint(new NativeMethods.PointStruct { X = x, Y = y });
            if (hit == IntPtr.Zero) return false;
            return hit == game || NativeMethods.GetAncestor(hit, 2) == game;
        }
    }

    internal sealed class FrameGrab
    {
        internal CaptureRoute Route = CaptureRoute.Missing;
        internal bool Focused;
        internal bool Input;
        internal ImageBuffer Image;
        internal string Note = "Waiting for the Kavros window.";
        internal int LastTickMs;
        internal HudReading Reading = new HudReading();
        internal bool XpActivity;
        internal bool PackActivity;
        internal bool SkillView;
        internal string SkillTrace = "xp=none pack=none";
        internal Rectangle PanelBounds;

        private readonly object gate = new object();
        private IntPtr requestHwnd;
        private WardenSettings requestSettings = new WardenSettings();
        private bool requestCapture = true;
        private Rectangle requestPanel;
        private Thread worker;
        private volatile bool alive;
        private HudReading published = new HudReading();
        private CaptureRoute publishedRoute = CaptureRoute.Missing;
        private bool publishedFocused;
        private bool publishedInput;
        private string publishedNote = "Waiting for the Kavros window.";
        private bool publishedXp;
        private bool publishedPack;
        private bool publishedSkillView;
        private string publishedSkillTrace = "xp=none pack=none";
        private readonly SkillSense skills = new SkillSense();
        private int publishedMs;
        private readonly bool[] keyDown = new bool[256];
        private IntRect lifeBox;
        private IntRect prayerBox;
        private IntRect buffStrip;
        private IntRect xpBox;
        private IntRect packBox;
        private IntRect packHold;
        private IntRect chatBox;
        private IntRect metricsBox;
        private readonly WindowSurfaceCapture windowSurface = new WindowSurfaceCapture();
        private bool useSurface;
        private long lastMetricsXp;
        private bool haveMetricsXp;
        private IntRect heldLife;
        private IntRect heldPrayer;
        private bool haveBoxes;
        private int missedLocks;
        private DateTime nextFullSearch = DateTime.MinValue;
        private DateTime nextDebugImage = DateTime.MinValue;
        private readonly Bitmap[] pads = new Bitmap[6];
        private readonly int[][] pixelsBySlot = new int[6][];
        private int failedFull;
        private int lockedSamples;
        private IntPtr lastClient;
        private Size lastSize;
        private WardenSettings lastSettings;
        private uint lastInputStamp;
        private bool pollForeground;
        private DateTime publishedAt;
        private IntPtr publishedWindow;
        internal DateTime FrameTime;

        internal void Start()
        {
            if (worker != null) return;
            alive = true;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "AfkCapture";
            worker.Start();
        }

        internal void Stop()
        {
            alive = false;
            if (worker != null && worker.IsAlive)
            {
                try { worker.Join(400); } catch (Exception) { }
            }
        }

        private void DisposeBuffers()
        {
            for (int i = 0; i < pads.Length; i++)
            {
                if (pads[i] != null) pads[i].Dispose();
                pads[i] = null;
            }
        }

        internal void Tick(IntPtr hwnd, WardenSettings settings, bool capture)
        {
            lock (gate)
            {
                requestHwnd = hwnd;
                requestSettings = settings == null ? new WardenSettings() : settings.Snapshot();
                requestCapture = capture;
                requestPanel = PanelBounds;
                Reading = publishedWindow==hwnd ? published : new HudReading();
                FrameTime = publishedAt;
                Route = publishedRoute;
                Focused = publishedFocused;
                Input = publishedInput;
                publishedInput = false;
                Note = publishedNote;
                XpActivity = publishedXp;
                PackActivity = publishedPack;
                SkillView = publishedSkillView;
                SkillTrace = publishedSkillTrace;
                LastTickMs = publishedMs;
                publishedXp = false;
                publishedPack = false;
                Image = null;
            }
        }

        private void Loop()
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; } catch (Exception) { }
            DateTime nextSample = DateTime.MinValue;
            try { while (alive)
            {
                PollKeys();
                if (DateTime.UtcNow >= nextSample)
                {
                    var clock = Stopwatch.StartNew();
                    try { Sample(); }
                    catch (Exception) { }
                    lock (gate) { publishedMs = (int)clock.ElapsedMilliseconds; }
                    nextSample = DateTime.UtcNow.AddMilliseconds(CapturePlan.SampleMilliseconds);
                }
                Thread.Sleep(35);
            } }
            finally { DisposeBuffers(); }
        }

        private void PollKeys()
        {
            IntPtr hwnd;
            lock (gate) { hwnd = requestSettings.Paused ? IntPtr.Zero : requestHwnd; }
            bool foreground = hwnd != IntPtr.Zero && WindowLocator.IsForeground(hwnd);
            if (!foreground) { pollForeground = false; return; }
            NativeMethods.LastInput last = new NativeMethods.LastInput();
            last.Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.LastInput));
            if (!NativeMethods.GetLastInputInfo(ref last)) return;
            if (pollForeground && last.Time == lastInputStamp) return;
            bool wasForeground = pollForeground;
            pollForeground = true;
            lastInputStamp = last.Time;
            bool edge = false;
            bool click = false;
            for (int vk = 1; vk < 256; vk++)
            {
                if (!WatchKey(vk)) continue;
                short state = NativeMethods.GetAsyncKeyState(vk);
                bool pressed = (state & 0x8000) != 0;
                // Bit 0 catches a press that went down and up between two polls.
                bool tapped = (state & 1) != 0;
                if (((pressed && !keyDown[vk]) || tapped) && foreground)
                {
                    edge = true;
                    if (vk <= 6) click = true;
                }
                keyDown[vk] = pressed;
            }
            // Keys only count once the game was already in front, so typing in
            // another app right before switching does not. A mouse click that
            // brings the game to the front is a click on the game and counts.
            if (edge && (wasForeground || click)) lock (gate) { publishedInput = true; }
        }

        private static bool WatchKey(int vk)
        {
            if (vk == 1 || vk == 2 || vk == 4 || vk == 5 || vk == 6) return true;
            if (vk >= 8 && vk <= 254 && vk != 16 && vk != 17 && vk != 18) return true;
            return false;
        }

        private void Sample()
        {
            IntPtr hwnd;
            WardenSettings settings;
            bool capture;
            Rectangle panel;
            lock (gate)
            {
                hwnd = requestHwnd;
                settings = requestSettings;
                capture = requestCapture;
                panel = requestPanel;
            }
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                lastClient = IntPtr.Zero;
                haveBoxes = false;
                Publish(new HudReading(), CaptureRoute.Missing, false, "Waiting for the Kavros window.");
                return;
            }
            bool iconic = NativeMethods.IsIconic(hwnd);
            bool focused = WindowLocator.IsForeground(hwnd);
            if (!capture || iconic)
            {
                Publish(new HudReading(), iconic ? CaptureRoute.Minimized : CaptureRoute.Covered, focused, iconic ? "Minimized" : "Paused while the panel is dragged.");
                return;
            }
            Rectangle client = NativeMethods.ClientBounds(hwnd);
            if (client.Width < 80 || client.Height < 80)
            {
                Publish(new HudReading(), CaptureRoute.Covered, focused, "Waiting for the Kavros window.");
                return;
            }
            // A visible client, focused or not, is a screen copy. A covered client
            // is left unread so the desktop does not flash. PrintWindow is not used.
            if (lastClient != hwnd || lastSize != client.Size || !settings.SamePins(lastSettings))
            {
                lastClient=hwnd; lastSize=client.Size; lastSettings=settings;
                lifeBox=prayerBox=buffStrip=xpBox=packBox=packHold=chatBox=metricsBox=new IntRect();
                heldLife=heldPrayer=new IntRect();
                haveBoxes=false; missedLocks=0; nextFullSearch=DateTime.MinValue;
            }
            bool visible = RectOnGame(hwnd, client, new IntRect(0, 0, client.Width, client.Height));
            if (CapturePlan.OfferWindowSurface) windowSurface.Ensure(hwnd);
            useSurface = windowSurface.Available;
            CaptureRoute route = CapturePolicy.Choose(true, false, visible, !visible, useSurface);
            HudReading reading = new HudReading();
            if (CapturePolicy.PixelsAvailable(route))
            {
                bool full = CapturePlan.WantFull(haveBoxes, DateTime.UtcNow, nextFullSearch);
                if (full) reading = FullSearch(hwnd, client, settings, panel);
                else if (haveBoxes) reading = LockedSearch(hwnd, client, settings);
            }
            bool saw = reading.Hp != null || reading.Prayer != null || reading.StripFound || reading.BuffBarFound
                || reading.MetricsFound || (reading.ChatHits != null && reading.ChatHits.Count > 0);
            if (!saw)
            {
                string missed = route == CaptureRoute.WindowSurface || visible
                    ? "Waiting for the hitpoint and prayer numbers."
                    : "Kavros is covered. Hitpoints, prayer and potions stay on the last good reading.";
                Publish(new HudReading(), route == CaptureRoute.WindowSurface ? route : (visible ? CaptureRoute.Screen : CaptureRoute.Covered), focused, missed);
                return;
            }
            string note = route == CaptureRoute.WindowSurface
                ? "Watching the Kavros window."
                : (focused ? "Watching the visible Kavros window." : "Watching the visible Kavros window in the background.");
            Publish(reading, route, focused, note);
        }

        private ImageBuffer Grab(IntPtr hwnd, Rectangle client, IntRect local, int slot)
        {
            if (local.Width < 4 || local.Height < 4) return null;
            if (useSurface)
            {
                ImageBuffer fromWindow = windowSurface.Copy(local.X, local.Y, local.Width, local.Height);
                if (CapturePlan.AcceptFrame(fromWindow)) return fromWindow;
            }
            if (!RectOnGame(hwnd, client, local)) return null;
            ImageBuffer shot = CopyScreen(ScreenOf(client, local), slot);
            if (!CapturePlan.AcceptFrame(shot)) return null;
            return shot;
        }

        private bool RectOnGame(IntPtr hwnd, Rectangle client, IntRect local)
        {
            Rectangle screen = ScreenOf(client, local);
            if (screen.Width < 4 || screen.Height < 4) return false;
            int hits = 0;
            int[] xs = new int[] { screen.Left + 1, Math.Max(screen.Left + 1, screen.Right - 2) };
            int[] ys = new int[] { screen.Top + 1, Math.Max(screen.Top + 1, screen.Bottom - 2) };
            for (int y = 0; y < ys.Length; y++)
                for (int x = 0; x < xs.Length; x++)
                    if (WindowLocator.OverGame(hwnd, xs[x], ys[y])) hits++;
            return hits >= 3;
        }

        private HudReading FullSearch(IntPtr hwnd, Rectangle client, WardenSettings settings, Rectangle panel)
        {
            nextFullSearch = DateTime.UtcNow.AddSeconds(CapturePlan.NextFullSeconds(false, false, failedFull + 1));
            ImageBuffer image = Grab(hwnd, client, new IntRect(0, 0, client.Width, client.Height), 0);
            if (!CapturePlan.AcceptFrame(image))
            {
                haveBoxes = false;
                failedFull++;
                return new HudReading();
            }
            BlankPanel(image, client, panel);
            // Chat and the XP tracker first. The buff bar is not part of this pass.
            HudReading reading = HudReader.Read(image, settings, false);
            ReadSide(image, reading, 0);
            if (reading.LifeText.Width > 8) lifeBox = reading.LifeText;
            if (reading.PrayerText.Width > 8) prayerBox = reading.PrayerText;
            IntRect foundBuff = Union(reading.BuffRects);
            if(settings.HasBuffPin) buffStrip=new IntRect((int)(settings.BuffPinX*image.Width),(int)(settings.BuffPinY*image.Height),(int)Math.Ceiling(settings.BuffPinW*image.Width),(int)Math.Ceiling(settings.BuffPinH*image.Height));
            else if(foundBuff.Width>0 && RowHasTimer(reading))
            {
                // Keep a few neighbouring slots in view when buffs reorder after a dose.
                int margin=Math.Max(16,foundBuff.Height*2);
                buffStrip=new IntRect(Math.Max(0,foundBuff.X-margin),Math.Max(0,foundBuff.Y-3),
                    Math.Min(image.Width,foundBuff.Right+margin)-Math.Max(0,foundBuff.X-margin),
                    Math.Min(image.Height,foundBuff.Bottom+4)-Math.Max(0,foundBuff.Y-3));
            }
            if (xpBox.Width < 8)
                xpBox = new IntRect(0, 0, image.Width, image.Height);
            NoteActivity(image);
            if (skills.XpRect.Width > 8)
            {
                // Follow the drop wherever it was, with room for the float, and
                // keep scanning the rest of the client on the next full pass.
                IntRect xp = skills.XpRect;
                int left = Math.Max(0, xp.X - Math.Max(80, image.Width / 5));
                int top = Math.Max(0, xp.Y - Math.Max(40, image.Height / 5));
                int right = Math.Min(image.Width, xp.Right + Math.Max(160, image.Width / 4));
                int bottom = Math.Min(image.Height, xp.Bottom + Math.Max(80, image.Height / 5));
                xpBox = new IntRect(left, top, Math.Max(32, right - left), Math.Max(32, bottom - top));
            }
            IntRect pack = SceneActivity.PackWatch.Find(image);
            if (pack.Width > 8)
            {
                if (packBox.Width < 8 || SameGrid(pack, packBox) || (packHold.Width > 8 && SameGrid(pack, packHold)))
                    packBox = pack;
                packHold = pack;
            }
            RememberBars(reading);
            haveBoxes = lifeBox.Width > 8 || prayerBox.Width > 8 || buffStrip.Width>8;
            bool complete = reading.HpMax.HasValue && reading.PrayerMax.HasValue;
            failedFull = complete ? 0 : failedFull + 1;
            nextFullSearch=DateTime.UtcNow.AddSeconds(CapturePlan.NextFullSeconds(complete, buffStrip.Width>8, failedFull));
            MaybeDebug(image, settings, reading);
            return reading;
        }

        private HudReading LockedSearch(IntPtr hwnd, Rectangle client, WardenSettings settings)
        {
            HudReading reading = new HudReading();
            reading.StripFound = lifeBox.Width>8 || prayerBox.Width>8;
            lockedSamples++;
            bool slow = lockedSamples % CapturePlan.SlowSlotEvery == 0;
            ImageBuffer life = null, prayer = null;
            if (CapturePlan.ShareGrab(lifeBox, prayerBox))
            {
                // Life and prayer share the HUD row: one screen copy for both.
                IntRect both = Union(lifeBox, prayerBox);
                ImageBuffer row = Grab(hwnd, client, both, 1);
                if (row != null)
                {
                    life = Slice(row, lifeBox.X - both.X, lifeBox.Y - both.Y, lifeBox.Width, lifeBox.Height);
                    prayer = Slice(row, prayerBox.X - both.X, prayerBox.Y - both.Y, prayerBox.Width, prayerBox.Height);
                }
            }
            else
            {
                life = Grab(hwnd, client, lifeBox, 1);
                prayer = Grab(hwnd, client, prayerBox, 2);
            }
            if (life != null) HudReader.ReadBox(life, true, reading);
            if (prayer != null) HudReader.ReadBox(prayer, false, reading);
            if (buffStrip.Width > 8 && slow)
            {
                ImageBuffer strip = Grab(hwnd, client, buffStrip, 3);
                if (strip != null)
                {
                    WardenSettings pin = new WardenSettings();
                    pin.BuffPinX = 0;
                    pin.BuffPinY = 0;
                    pin.BuffPinW = 1;
                    pin.BuffPinH = 1;
                    HudReading buffs = new HudReading();
                    HudReader.ReadBuffBar(strip, pin, buffs);
                    reading.Buffs = buffs.Buffs;
                    reading.BuffRects = buffs.BuffRects;
                    reading.BuffBarFound = buffs.BuffBarFound;
                    reading.BuffTrace = buffs.BuffTrace;
                    reading.PotionCount = buffs.PotionCount;
                }
                // A single miss does not walk the lock onto scenery. The strip
                // moves only when a later search finds a buff row that has timers.
            }
            if (reading.HpMax == null || reading.PrayerMax == null || (buffStrip.Width>8 && slow && !reading.BuffBarFound))
            {
                missedLocks++;
                if (missedLocks == 3 && nextFullSearch>DateTime.UtcNow.AddSeconds(3))
                    nextFullSearch=DateTime.UtcNow.AddSeconds(3);
            }
            else missedLocks = 0;
            RememberBars(reading);
            if (!slow) return reading;
            if (chatBox.Width > 8)
            {
                ImageBuffer chat = Grab(hwnd, client, chatBox, 4);
                if (chat != null)
                {
                    List<ChatHit> hits = ChatReader.Read(chat);
                    for (int i = 0; i < hits.Count; i++) hits[i].Y += chatBox.Y;
                    reading.ChatHits = hits;
                }
            }
            if (metricsBox.Width > 8)
            {
                ImageBuffer metrics = Grab(hwnd, client, metricsBox, 5);
                if (metrics != null)
                {
                    MetricsReader.Result read = MetricsReader.Read(metrics);
                    if (read.Found)
                    {
                        reading.MetricsFound = true;
                        reading.MetricsXp = read.Xp;
                        reading.MetricsNote = read.Note;
                        if (haveMetricsXp && read.Xp > lastMetricsXp) reading.MetricsGain = true;
                        lastMetricsXp = read.Xp;
                        haveMetricsXp = true;
                        if (reading.MetricsGain) publishedXp = true;
                    }
                }
            }
            ImageBuffer pack = packBox.Width > 8 ? Grab(hwnd, client, packBox, 3) : null;
            skills.Observe(null, pack);
            PublishSkills();
            return reading;
        }

        private static bool SameGrid(IntRect a, IntRect b)
        {
            int ax = a.X + a.Width / 2;
            int ay = a.Y + a.Height / 2;
            int bx = b.X + b.Width / 2;
            int by = b.Y + b.Height / 2;
            int dx = ax - bx;
            int dy = ay - by;
            return dx * dx + dy * dy < 80 * 80;
        }

        private void RememberBars(HudReading reading)
        {
            if (reading == null) return;
            if (reading.LifeBar.Width >= 8 && reading.LifeBar.Y > 0) heldLife = reading.LifeBar;
            else if (heldLife.Width >= 8) reading.LifeBar = heldLife;
            if (reading.PrayerBar.Width >= 8 && reading.PrayerBar.Y > 0) heldPrayer = reading.PrayerBar;
            else if (heldPrayer.Width >= 8) reading.PrayerBar = heldPrayer;
        }

        private static bool RowHasTimer(HudReading reading)
        {
            if (reading == null || reading.Buffs == null) return false;
            for (int i = 0; i < reading.Buffs.Count; i++)
                if (reading.Buffs[i].HasTime) return true;
            return false;
        }

        private static IntRect Union(IntRect a, IntRect b)
        {
            int x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            return new IntRect(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
        }

        private static ImageBuffer Slice(ImageBuffer image, int x, int y, int w, int h)
        {
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > image.Width) w = image.Width - x;
            if (y + h > image.Height) h = image.Height - y;
            if (w < 4 || h < 4) return null;
            return image.Crop(x, y, w, h);
        }

        private void NoteActivity(ImageBuffer image)
        {
            // Idle follows the RuneMetrics total, the backpack, hitpoints and input.
            skills.Observe(null, image);
            PublishSkills();
        }

        private void ReadSide(ImageBuffer image, HudReading reading, int yOffset)
        {
            if (image == null || reading == null) return;
            List<ChatHit> hits = ChatReader.Read(image);
            for (int i = 0; i < hits.Count; i++) hits[i].Y += yOffset;
            reading.ChatHits = hits;
            if (hits.Count > 0)
            {
                int top = hits[0].Y;
                int bottom = hits[0].Y;
                for (int i = 1; i < hits.Count; i++)
                {
                    if (hits[i].Y < top) top = hits[i].Y;
                    if (hits[i].Y > bottom) bottom = hits[i].Y;
                }
                int y = Math.Max(0, top - 30);
                int h = Math.Min(image.Height - y, bottom - top + 80);
                chatBox = new IntRect(0, y, image.Width, Math.Max(40, h));
            }
            MetricsReader.Result metrics = MetricsReader.Read(image);
            reading.MetricsNote = metrics.Note;
            if (metrics.Found)
            {
                reading.MetricsFound = true;
                reading.MetricsXp = metrics.Xp;
                reading.MetricsNote = metrics.Note;
                if (haveMetricsXp && metrics.Xp > lastMetricsXp) reading.MetricsGain = true;
                lastMetricsXp = metrics.Xp;
                haveMetricsXp = true;
                if (reading.MetricsGain) publishedXp = true;
                int mx = Math.Max(0, metrics.X);
                int my = Math.Max(0, metrics.Y);
                metricsBox = new IntRect(mx, my, Math.Min(image.Width - mx, Math.Max(40, metrics.W)), Math.Min(image.Height - my, Math.Max(24, metrics.H)));
            }
        }

        private void PublishSkills()
        {
            lock (gate)
            {
                if (skills.XpEvent) publishedXp = true;
                if (skills.PackEvent) publishedPack = true;
                publishedSkillTrace = skills.Trace;
                publishedSkillView = skills.View;
            }
        }

        private void MaybeDebug(ImageBuffer image, WardenSettings settings, HudReading reading)
        {
            if (settings == null || !settings.SaveDebug || DateTime.UtcNow < nextDebugImage) return;
            nextDebugImage = DateTime.UtcNow.AddSeconds(30);
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                System.IO.Directory.CreateDirectory(dir);
                int w = Math.Max(1, image.Width / 2);
                int h = Math.Max(1, image.Height / 2);
                using (Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                {
                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        int stride = data.Stride;
                        byte[] raw = new byte[stride * h];
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                int color = image.Argb(Math.Min(image.Width - 1, x * 2), Math.Min(image.Height - 1, y * 2));
                                int i = y * stride + x * 3;
                                raw[i] = (byte)(color & 255);
                                raw[i + 1] = (byte)((color >> 8) & 255);
                                raw[i + 2] = (byte)((color >> 16) & 255);
                            }
                        System.Runtime.InteropServices.Marshal.Copy(raw, 0, data.Scan0, raw.Length);
                    }
                    finally { bitmap.UnlockBits(data); }
                    bitmap.Save(System.IO.Path.Combine(dir, "debug-capture.jpg"), ImageFormat.Jpeg);
                }
            }
            catch (Exception) { }
        }

        private void Publish(HudReading reading, CaptureRoute route, bool focused, string note)
        {
            lock (gate)
            {
                publishedWindow=lastClient;
                publishedAt = DateTime.UtcNow;
                published = reading ?? new HudReading();
                publishedRoute = route;
                publishedFocused = focused;
                publishedNote = note;
            }
        }

        private static void BlankPanel(ImageBuffer image, Rectangle client, Rectangle panel)
        {
            if (image == null || panel.Width < 8 || panel.Height < 8) return;
            int x = panel.X - client.X;
            int y = panel.Y - client.Y;
            image.Fill(x, y, panel.Width, panel.Height, ImageBuffer.Pack(0, 0, 0));
        }

        private static IntRect Pad(IntRect box, int pad)
        {
            return new IntRect(box.X - pad, box.Y - pad, box.Width + pad * 2, box.Height + pad * 2);
        }

        private static IntRect Union(List<IntRect> boxes)
        {
            if (boxes == null || boxes.Count == 0) return new IntRect();
            int x = boxes[0].X, y = boxes[0].Y, right = boxes[0].Right, bottom = boxes[0].Bottom;
            for (int i = 1; i < boxes.Count; i++)
            {
                if (boxes[i].X < x) x = boxes[i].X;
                if (boxes[i].Y < y) y = boxes[i].Y;
                if (boxes[i].Right > right) right = boxes[i].Right;
                if (boxes[i].Bottom > bottom) bottom = boxes[i].Bottom;
            }
            return new IntRect(x - 2, y - 2, right - x + 4, bottom - y + 4);
        }

        private static Rectangle ScreenOf(Rectangle client, IntRect box)
        {
            if (box.Width < 4 || box.Height < 4) return Rectangle.Empty;
            return new Rectangle(client.X + box.X, client.Y + box.Y, box.Width, box.Height);
        }

        private ImageBuffer CopyScreen(Rectangle bounds, int slot)
        {
            if (bounds.Width < 4 || bounds.Height < 4 || bounds.Width > 4096 || bounds.Height > 4096) return null;
            if (slot < 0 || slot >= pads.Length) slot = 0;
            if (pads[slot] == null || pads[slot].Width != bounds.Width || pads[slot].Height != bounds.Height)
            {
                if (pads[slot] != null) pads[slot].Dispose();
                pads[slot] = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                pixelsBySlot[slot] = new int[bounds.Width*bounds.Height];
            }
            using (Graphics graphics = Graphics.FromImage(pads[slot]))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            BitmapData data = pads[slot].LockBits(new Rectangle(0, 0, pads[slot].Width, pads[slot].Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int width=bounds.Width,height=bounds.Height;
                int[] pixels=pixelsBySlot[slot];
                if(data.Stride==width*4)
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                else for(int y=0;y<height;y++)
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*width,width);
                return new ImageBuffer(width,height,pixels);
            }
            finally { pads[slot].UnlockBits(data); }
        }
    }
}
