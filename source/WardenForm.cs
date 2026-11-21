using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace KavrosAfkWarden
{
    internal static class Program
    {
        private static Mutex singleInstance;
        private static EventWaitHandle activateEvent;
        private static EventWaitHandle ackEvent;
        private static WardenForm host;
        private static Thread activateWaiter;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test")
                return WardenTests.Run(args.Length > 1 ? args[1] : "test-results.txt", FindRoot());
            NativeMethods.MakePerMonitorAware();
            CrashLog.Install();
            NetSecurity.EnableModernTls();
            try
            {
                AppUpdates.Start();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                if (!ClaimInstance()) return 0;
                Application.Run(host);
                return 0;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                return 1;
            }

        }

        private static bool ClaimInstance()
        {
            singleInstance = new Mutex(false, AppInfo.MutexName);
            activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.MutexName + ".Activate");
            ackEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.MutexName + ".Ack");
            bool owned = false;
            try { owned = singleInstance.WaitOne(0); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                // A launcher started by a foreground Companion can pass its activation
                // right to the running Warden before signalling the existing window.
                try
                {
                    string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KavrosAfkWarden","instance.pid");
                    int pid;
                    if(File.Exists(path) && int.TryParse(File.ReadAllText(path),out pid))
                        using(var old=System.Diagnostics.Process.GetProcessById(pid))
                        using(var self=System.Diagnostics.Process.GetCurrentProcess())
                            if(old.SessionId==self.SessionId && old.ProcessName==self.ProcessName)
                                NativeMethods.AllowSetForegroundWindow(pid);
                }
                catch(Exception) { }
                activateEvent.Set();
                ackEvent.WaitOne(2500);
                return false;
            }
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "instance.pid"), System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            }
            catch (Exception) { }
            host = new WardenForm();
            IntPtr readyHandle=host.Handle;
            activateWaiter = new Thread(WaitForActivate);
            activateWaiter.IsBackground = true;
            activateWaiter.Start();
            return true;
        }

        private static void WaitForActivate()
        {
            while (true)
            {
                try { if (!activateEvent.WaitOne(500)) continue; }
                catch (Exception) { return; }
                WardenForm form = host;
                if (form == null || !form.IsHandleCreated) continue;
                try
                {
                    form.BeginInvoke(new Action(delegate
                    {
                        try { form.BringUp(); } catch (Exception) { }
                        try { ackEvent.Set(); } catch (Exception) { }
                    }));
                }
                catch (Exception) { }
            }
        }

        private static string FindRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "companion.module.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            return "";
        }
    }

    internal sealed class StatusRow : PaintedLabel
    {
        internal string Pill = "";
        internal string Value = "";
        internal PillTone Tone = PillTone.Example;

        internal StatusRow()
        {
            Renderer = Draw;
            Height = 28;
            BackColor = KavrosTheme.Bg;
        }

        private string paintedPill, paintedValue;
        private PillTone paintedTone;

        // Repaint only when the text or tone changed. The panel sits over the
        // game, so an unchanged row is not redrawn twice a second.
        internal void Commit()
        {
            if (Pill == paintedPill && Value == paintedValue && Tone == paintedTone) return;
            Invalidate();
        }

        private void Draw(Graphics g, PaintedLabel label)
        {
            paintedPill = Pill;
            paintedValue = Value;
            paintedTone = Tone;
            using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, ClientRectangle);
            int width = KavrosTheme.DrawPill(g, 0, Height / 2, Pill, Tone, KavrosTheme.Bg);
            TextRenderer.DrawText(g, Value, KavrosTheme.Ui(9F, false), new Rectangle(width + 10, 0, Math.Max(1, Width - width - 12), Height), KavrosTheme.Text, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class WatchPanel : Form
    {
        private readonly PaintedLabel title = new PaintedLabel();
        private readonly PaintedLabel note = new PaintedLabel();
        private readonly PaintedLabel metricsHint = new PaintedLabel();
        internal readonly StatusRow Idle = new StatusRow();
        internal readonly StatusRow Hp = new StatusRow();
        internal readonly StatusRow Prayer = new StatusRow();
        internal readonly StatusRow Potion = new StatusRow();
        internal readonly StatusRow Afk = new StatusRow();
        private readonly OpaqueButton pause = new OpaqueButton();
        private readonly OpaqueButton exit = new OpaqueButton();
        private readonly OpaqueButton settings = new OpaqueButton();
        private readonly OpaqueButton collapse = new OpaqueButton();
        private bool collapsed;
        private bool dragging;
        internal event Action SettingsRequested;
        internal event Action ExitRequested;
        internal event Action PauseRequested;
        internal event Action Moved;

        internal WatchPanel()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            Font = KavrosTheme.Ui(9F, false);
            ClientSize = new Size(400, 278);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            title.SetBounds(0, 0, 400, 32);
            title.Renderer = DrawTitle;
            title.Text = "AFK WARDEN";
            title.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
            };
            Controls.Add(title);
            settings.Text = "Settings";
            settings.Tone = ButtonTone.Ghost;
            settings.Surface = KavrosTheme.BgRaise;
            settings.Click += delegate { if (SettingsRequested != null) SettingsRequested(); };
            Controls.Add(settings);
            collapse.Text = "-";
            collapse.Tone = ButtonTone.Ghost;
            collapse.Surface = KavrosTheme.BgRaise;
            collapse.Click += delegate { SetCollapsed(!collapsed); };
            Controls.Add(collapse);
            LayoutHeader();
            Resize += delegate { LayoutHeader(); };
            note.SetBounds(12, 40, 376, 22);
            note.ForeColor = KavrosTheme.TextSoft;
            note.Font = KavrosTheme.Ui(8.5F, false);
            note.BackColor = KavrosTheme.Bg;
            note.Renderer = DrawGame;
            Controls.Add(note);
            Place(Idle, 66);
            Place(Hp, 98);
            Place(Prayer, 130);
            Place(Potion, 162);
            Place(Afk, 194);
            metricsHint.SetBounds(12, 224, 376, 16);
            metricsHint.Text = "Keep RuneMetrics XP open";
            metricsHint.Font = KavrosTheme.Ui(7.5F, false);
            metricsHint.ForeColor = Color.FromArgb(120, 128, 136);
            metricsHint.BackColor = KavrosTheme.Bg;
            metricsHint.Renderer = DrawHint;
            Controls.Add(metricsHint);
            pause.Text = "Pause";
            pause.Tone = ButtonTone.Primary;
            pause.SetBounds(12, 244, 184, 24);
            pause.Click += delegate { if (PauseRequested != null) PauseRequested(); };
            Controls.Add(pause);
            exit.Text = "Exit";
            exit.Tone = ButtonTone.Secondary;
            exit.SetBounds(204, 244, 184, 24);
            exit.Click += delegate { if (ExitRequested != null) ExitRequested(); };
            Controls.Add(exit);
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
        }

        internal bool IsDragging { get { return dragging; } }
        internal void SetPaused(bool paused) { pause.Text = paused ? "Resume" : "Pause"; }
        internal void SetNote(string text) { note.Text = text; }
        private bool hintWanted = true;
        internal void SetMetricsHint(bool show)
        {
            hintWanted = show;
            metricsHint.Visible = show && !collapsed;
        }
        private bool gameFound;
        private string gameName = "";
        internal void SetGame(bool found, string name)
        {
            if (gameFound == found && gameName == (name ?? "")) return;
            gameFound = found;
            gameName = name ?? "";
            note.Invalidate();
        }

        private void Place(StatusRow row, int y)
        {
            row.SetBounds(12, y, 376, 28);
            Controls.Add(row);
        }

        private void SetCollapsed(bool value)
        {
            collapsed = value;
            collapse.Text = collapsed ? "+" : "-";
            note.Visible = !collapsed;
            metricsHint.Visible = !collapsed && hintWanted;
            Idle.Visible = Hp.Visible = Prayer.Visible = Potion.Visible = Afk.Visible = !collapsed;
            pause.Visible = exit.Visible = !collapsed;
            ClientSize = new Size(400, collapsed ? 32 : 278);
        }

        private void LayoutHeader()
        {
            int width = ClientSize.Width;
            if (width < 80) return;
            title.SetBounds(0, 0, width, 32);
            collapse.SetBounds(width - 32, 4, 26, 24);
            settings.SetBounds(width - 110, 4, 72, 24);
            settings.BringToFront();
            collapse.BringToFront();
        }

        private void DrawTitle(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.BgRaise)) g.FillRectangle(brush, label.ClientRectangle);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) g.FillRectangle(gold, 0, 0, label.Width, 2);
            KavrosTheme.DrawTracked(g, "AFK WARDEN", KavrosTheme.Display(8F), 12, 8, KavrosTheme.Text, KavrosTheme.BgRaise, 1.4F, Math.Max(40, label.Width - 120));
        }

        private void DrawGame(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, label.ClientRectangle);
            string pill = gameFound ? "GAME DETECTED" : "NO GAME";
            int width = KavrosTheme.DrawPill(g, 0, label.Height / 2, pill, gameFound ? PillTone.Ok : PillTone.Bad, KavrosTheme.Bg);
            TextRenderer.DrawText(g, gameName, KavrosTheme.Ui(8.5F, false), new Rectangle(width + 8, 0, Math.Max(1, label.Width - width - 8), label.Height), KavrosTheme.Text, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private void DrawHint(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, label.ClientRectangle);
            TextRenderer.DrawText(g, label.Text, KavrosTheme.Ui(7.5F, false), label.ClientRectangle, Color.FromArgb(120, 128, 136), KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0231) dragging = true;
            if (message.Msg == 0x0232)
            {
                dragging = false;
                if (Moved != null) Moved();
            }
            base.WndProc(ref message);
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams parameters = base.CreateParams; parameters.ExStyle |= 0x80 | 0x02000000; return parameters; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen rule = new Pen(KavrosTheme.LineSoft)) e.Graphics.DrawLine(rule, 0, title.Height - 1, Width, title.Height - 1);
            using (Pen frame = new Pen(KavrosTheme.Frame)) e.Graphics.DrawRectangle(frame, 0, 0, Width - 1, Height - 1);
        }
    }

    internal sealed class WardenForm : Form
    {
        private readonly ComboBox windows = new ComboBox();
        private readonly PaintedLabel connection = new PaintedLabel();
        private KavrosCheckBox pause = new KavrosCheckBox();
        private KavrosCheckBox idleOn = new KavrosCheckBox();
        private KavrosCheckBox skillingOn = new KavrosCheckBox();
        private KavrosCheckBox hpOn = new KavrosCheckBox();
        private KavrosCheckBox prayerOn = new KavrosCheckBox();
        private KavrosCheckBox afkOn = new KavrosCheckBox();
        private KavrosCheckBox hpPercent = new KavrosCheckBox();
        private KavrosCheckBox prayerPercent = new KavrosCheckBox();
        private KavrosCheckBox soundMaster = new KavrosCheckBox();
        private KavrosCheckBox notifyMaster = new KavrosCheckBox();
        private KavrosCheckBox idleSound = new KavrosCheckBox();
        private KavrosCheckBox idleNotify = new KavrosCheckBox();
        private KavrosCheckBox hpSound = new KavrosCheckBox();
        private KavrosCheckBox hpNotify = new KavrosCheckBox();
        private KavrosCheckBox prayerSound = new KavrosCheckBox();
        private KavrosCheckBox prayerNotify = new KavrosCheckBox();
        private KavrosCheckBox afkSound = new KavrosCheckBox();
        private KavrosCheckBox afkNotify = new KavrosCheckBox();
        private KavrosCheckBox potionOn = new KavrosCheckBox();
        private KavrosCheckBox potionSound = new KavrosCheckBox();
        private KavrosCheckBox potionNotify = new KavrosCheckBox();
        private KavrosCheckBox updates = new KavrosCheckBox();
        private KavrosCheckBox saveDebug = new KavrosCheckBox();
        private readonly NumberField idleSeconds = new NumberField();
        private readonly NumberField hpThreshold = new NumberField();
        private readonly NumberField prayerThreshold = new NumberField();
        private KavrosCheckBox grandmaster, treePerk;
        private Label afkDuration;
        private readonly NumberField afkLead = new NumberField();
        private readonly NumberField potionSeconds = new NumberField();
        private readonly NumberField cooldown = new NumberField();
        private readonly NumberField banner = new NumberField();
        private readonly VolumeSlider volume = new VolumeSlider();
        private readonly Label status = new Label();
        private readonly WatchPanel panel = new WatchPanel();
        private readonly AlertPopup popup = new AlertPopup();
        private readonly AlertSound sound = new AlertSound();
        private readonly SystemToast toast = new SystemToast();
        private readonly FrameGrab grab = new FrameGrab();
        private Rectangle dockedGame;
        private bool haveDockedGame;
        private SceneActivity activity = new SceneActivity();
        private DateTime lastDebugSave = DateTime.MinValue;
        private DateTime metricsAbsentSince = DateTime.MinValue;
        private IntPtr potsClient = IntPtr.Zero;
        private DateTime lastClientSearch = DateTime.MinValue;
        private int? shownHp;
        private int? shownHpMax;
        private int? shownPrayer;
        private int? shownPrayerMax;
        private PotionDisplay potionDisplay=new PotionDisplay();
        private IntPtr observedClient, panelOwner;
        private DateTime lastFrame;
        private bool everAttached;
        private ReadGate hpGate = new ReadGate();
        private ReadGate prayerGate = new ReadGate();
        private readonly BuffClock buffClock = new BuffClock();
        private readonly ChatPotionBook chatPots = new ChatPotionBook();
        private WardenEngine engine = new WardenEngine();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray;
        private readonly string settingsPath;
        private WardenSettings settings = new WardenSettings();
        private List<GameWindow> found = new List<GameWindow>();
        private IntPtr selected = IntPtr.Zero;
        private bool loading = true;
        private bool closing;
        private bool picking;
        private string clientTitle = "";
        private DateTime nextUpdateCheck = DateTime.UtcNow.AddHours(3);
        private HudReading lastHud = new HudReading();

        internal WardenForm()
        {
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden", "settings.txt");
            settings = new WardenSettings();
            clientTitle = "";
            sound.Volume = settings.Volume;
            Text = "AFK Warden";
            Font = KavrosTheme.Ui(9F, false);
            BackColor = KavrosTheme.Bg;
            ForeColor = KavrosTheme.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            ClientSize = new Size(680, 1020);
            Icon = LoadIcon();
            BuildControls();
            ApplySettingsToControls();
            loading = false;
            ArrangeSettings();
            panel.SettingsRequested += delegate { ShowSettings(); };
            panel.ExitRequested += Exit;
            panel.PauseRequested += delegate { pause.Checked = !pause.Checked; };
            panel.Moved += delegate
            {
                if (selected != IntPtr.Zero && NativeMethods.IsWindow(selected))
                {
                    Rectangle game = NativeMethods.ClientBounds(selected);
                    settings.PanelOffsetX = panel.Left - game.Left;
                    settings.PanelOffsetY = panel.Top - game.Top;
                    Save();
                }
            };
            panel.SetPaused(settings.Paused);
            if (settings.PanelX != int.MinValue) panel.Location = new Point(settings.PanelX, settings.PanelY);
            else
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                panel.Location = new Point(work.Right - panel.Width - 24, work.Top + 72);
            }
            panel.TopMost = false;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open panel", null, delegate { ShowPanel(); });
            menu.Items.Add("Settings", null, delegate { ShowSettings(); });
            menu.Items.Add("Exit", null, delegate { Exit(); });
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "AFK Warden " + AppInfo.DisplayVersion;
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowPanel(); };
            HandleCreated += delegate { KavrosTheme.ApplyWindowChrome(Handle); };
            Shown += delegate
            {
                StartupClock.MarkShown();
                panel.SetNote("Starting...");
                Hide();
                BringUp();
                if (!started)
                {
                    started = true;
                    timer.Interval = CapturePlan.SampleMilliseconds;
                    timer.Start();
                    grab.Start();
                }
                ThreadPool.QueueUserWorkItem(delegate { Bootstrap(); });
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "startup.txt"), "shownMs=" + StartupClock.ShownMs.ToString() + "\r\n");
                }
                catch (Exception) { }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); ShowPanel(); }
            };
            FormClosed += delegate { closing = true; try { timer.Stop(); } catch (Exception) { } try { Save(); } catch (Exception) { } try { CompanionStatus.Write("stopped", settings.Paused); } catch (Exception) { } try { grab.Stop(); } catch (Exception) { } try { tray.Visible = false; tray.Dispose(); } catch (Exception) { } try { panel.Dispose(); } catch (Exception) { } try { popup.Dispose(); } catch (Exception) { } };
            timer.Interval = CapturePlan.SampleMilliseconds;
            timer.Tick += delegate { Tick(); };
        }

        private bool started;
        private bool windowDragging;
        private int windowSearch;

        private void DiscoverClients()
        {
            lastClientSearch = DateTime.UtcNow;
            int ticket = System.Threading.Interlocked.Increment(ref windowSearch);
            int self = System.Diagnostics.Process.GetCurrentProcess().Id;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<GameWindow> list = null;
                try { list = WindowLocator.Enumerate(self); }
                catch (Exception) { list = new List<GameWindow>(); }
                try
                {
                    if (IsDisposed) return;
                    BeginInvoke(new Action(delegate
                    {
                        if (ticket != windowSearch || IsDisposed) return;
                        ApplyWindows(list);
                    }));
                }
                catch (Exception) { }
            });
        }

        private void BuildControls()
        {
            PaintedLabel subtitle = new PaintedLabel();
            subtitle.SetBounds(92, 36, 300, 28);
            subtitle.Renderer = delegate(Graphics g, PaintedLabel label)
            {
                using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, label.ClientRectangle);
                KavrosTheme.DrawTracked(g, "AFK WARDEN", KavrosTheme.Display(13F), 0, 2, KavrosTheme.Text, KavrosTheme.Bg, 1.6F, label.Width);
            };
            Controls.Add(subtitle);

            windows.SetBounds(28, 128, 480, 28);
            windows.DropDownStyle = ComboBoxStyle.DropDownList;
            windows.DrawMode = DrawMode.OwnerDrawFixed;
            windows.ItemHeight = 22;
            windows.IntegralHeight = false;
            KavrosTheme.StyleCombo(windows);
            windows.DrawItem += DrawWindowItem;
            windows.SelectedIndexChanged += delegate { if (!loading) AttachSelected(); };
            Controls.Add(windows);
            AddButton("Refresh", 520, 128, 128, ButtonTone.Secondary, delegate { RefreshWindows(); });
            connection.SetBounds(28, 164, 620, 28);
            connection.Renderer = DrawConnection;
            connection.Text = "Waiting for Kavros";
            Controls.Add(connection);
            pause = Check("Pause watching", 28, 200, 160, settings.Paused);
            AddButton("Pick client", 210, 196, 120, ButtonTone.Secondary, delegate { picking = true; SetStatus("Click the Kavros window to watch."); });

            idleOn = Check("Idle alert", 28, 292, 160, settings.IdleEnabled);
            skillingOn = Check("Skilling mode (XP and backpack)", 210, 292, 280, settings.SkillingMode);
            skillingOn.CheckedChanged += delegate { if (!loading) SwitchIdleMode(); };
            Caption("After", 28, 320);
            Number(idleSeconds, 88, 316, 3, 600, settings.ActiveIdleSeconds);
            Caption("seconds", 172, 320);
            idleSound = Check("Sound", 500, 316, 84, settings.IdleSound);
            idleNotify = Check("Notify", 590, 316, 78, settings.IdleNotify);

            hpOn = Check("Low hitpoints", 28, 356, 180, settings.HpEnabled);
            Caption("Below", 28, 384);
            Number(hpThreshold, 88, 380, 1, settings.HpPercent ? 999 : 9999999, settings.HpThreshold);
            hpPercent = Check("Percent", 176, 380, 120, settings.HpPercent);
            hpSound = Check("Sound", 500, 380, 84, settings.HpSound);
            hpNotify = Check("Notify", 590, 380, 78, settings.HpNotify);

            prayerOn = Check("Low prayer", 28, 420, 180, settings.PrayerEnabled);
            Caption("Below", 28, 448);
            Number(prayerThreshold, 88, 444, 1, settings.PrayerPercent ? 999 : 9999999, settings.PrayerThreshold);
            prayerPercent = Check("Percent", 176, 444, 120, settings.PrayerPercent);
            prayerSound = Check("Sound", 500, 444, 84, settings.PrayerSound);
            prayerNotify = Check("Notify", 590, 444, 78, settings.PrayerNotify);

            potionOn = Check("Potion alerts from chat", 28, 484, 220, settings.PotionEnabled);
            Caption("Keep the chatbox visible, with game messages on.", 250, 488);
            Caption("Below", 28, 512);
            Number(potionSeconds, 88, 508, 1, 600, settings.PotionSeconds);
            Caption("seconds", 172, 512);
            potionSound = Check("Sound", 500, 508, 84, settings.PotionSound);
            potionNotify = Check("Notify", 590, 508, 78, settings.PotionNotify);

            afkOn = Check("AFK teleport warning", 28, 548, 240, settings.AfkEnabled);
            afkDuration = Caption(settings.AfkSeconds / 60 + " min timer", 28, 576);
            grandmaster = Check("Grandmaster (+25 min)", 28, 604, 200, settings.Grandmaster);
            treePerk = Check("Tree perk (+15 min)", 232, 604, 205, settings.TreePerk);
            Caption("lead", 176, 576);
            Number(afkLead, 220, 572, 5, Math.Max(5, settings.AfkSeconds - 5), settings.AfkLeadSeconds);
            Caption("sec", 304, 576);
            afkSound = Check("Sound", 500, 572, 84, settings.AfkSound);
            afkNotify = Check("Notify", 590, 572, 78, settings.AfkNotify);

            soundMaster = Check("Play sounds", 28, 700, 160, settings.SoundMaster);
            notifyMaster = Check("Show notifications", 210, 700, 200, settings.NotifyMaster);
            Caption("Cooldown", 28, 734);
            Number(cooldown, 112, 730, 0, 600, settings.CooldownSeconds);
            Caption("sec", 196, 734);
            Caption("Stays", 250, 734);
            Number(banner, 310, 730, 2, 120, settings.BannerSeconds);
            Caption("sec", 394, 734);
            Caption("Volume", 28, 768);
            volume.SetBounds(112, 764, 250, 22);
            volume.SetValue(settings.Volume);
            volume.ValueChanged += delegate { if (!loading) { sound.Volume = volume.Value; Save(); } };
            Controls.Add(volume);
            updates = Check("Check for updates at startup", 28, 800, 320, settings.CheckForUpdates);
            saveDebug = Check("Save debug capture", 360, 800, 240, settings.SaveDebug);
            AddButton("Pin hitpoints", 28, 840, 120, ButtonTone.Secondary, delegate { Pin(0); });
            AddButton("Pin prayer", 156, 840, 110, ButtonTone.Secondary, delegate { Pin(1); });
            AddButton("Pin buff bar", 274, 840, 130, ButtonTone.Secondary, delegate { Pin(2); });
            AddButton("Clear pins", 412, 840, 110, ButtonTone.Secondary, delegate { settings.HpPinW = 0; settings.PrayerPinW = 0; settings.BuffPinW = 0; Save(); SetStatus("Pins cleared. Automatic search is on."); });

            AddButton("Test notification", 28, 888, 160, ButtonTone.Primary, delegate { popup.Present(SampleAlert(), (int)banner.Value); });
            AddButton("Test sound", 198, 888, 120, ButtonTone.Secondary, delegate { sound.Play("hp"); });
            AddButton("Check for updates", 328, 888, 170, ButtonTone.Secondary, delegate { AppUpdates.Check(this, true, SetStatus); });
            status.SetBounds(28, 928, 620, 40);
            status.ForeColor = KavrosTheme.TextSoft;
            status.BackColor = KavrosTheme.Bg;
            status.Font = KavrosTheme.Ui(8.5F, false);
            status.Text = "Live alerts obey the switches above. Test buttons always play.";
            Controls.Add(status);

            hpPercent.CheckedChanged += delegate { if (!loading) hpThreshold.Maximum = hpPercent.Checked ? 999 : 9999999; };
            prayerPercent.CheckedChanged += delegate { if (!loading) prayerThreshold.Maximum = prayerPercent.Checked ? 999 : 9999999; };
            Wire(grandmaster, treePerk, pause, idleOn, hpOn, prayerOn, afkOn, potionOn, hpPercent, prayerPercent, soundMaster, notifyMaster, idleSound, idleNotify, hpSound, hpNotify, prayerSound, prayerNotify, afkSound, afkNotify, potionSound, potionNotify, updates, saveDebug);
            idleSeconds.ValueChanged += delegate { if (!loading) Save(); };
            hpThreshold.ValueChanged += delegate { if (!loading) Save(); };
            prayerThreshold.ValueChanged += delegate { if (!loading) Save(); };
            afkLead.ValueChanged += delegate { if (!loading) Save(); };
            potionSeconds.ValueChanged += delegate { if (!loading) Save(); };
            cooldown.ValueChanged += delegate { if (!loading) Save(); };
            banner.ValueChanged += delegate { if (!loading) Save(); };
        }

        private void ApplySettingsToControls()
        {
            pause.Checked = settings.Paused;
            idleOn.Checked = settings.IdleEnabled;
            skillingOn.Checked = settings.SkillingMode;
            hpOn.Checked = settings.HpEnabled;
            prayerOn.Checked = settings.PrayerEnabled;
            afkOn.Checked = settings.AfkEnabled;
            hpPercent.Checked = settings.HpPercent;
            prayerPercent.Checked = settings.PrayerPercent;
            soundMaster.Checked = settings.SoundMaster;
            notifyMaster.Checked = settings.NotifyMaster;
            idleSound.Checked = settings.IdleSound;
            idleNotify.Checked = settings.IdleNotify;
            hpSound.Checked = settings.HpSound;
            hpNotify.Checked = settings.HpNotify;
            prayerSound.Checked = settings.PrayerSound;
            prayerNotify.Checked = settings.PrayerNotify;
            afkSound.Checked = settings.AfkSound;
            afkNotify.Checked = settings.AfkNotify;
            potionOn.Checked = settings.PotionEnabled;
            potionSound.Checked = settings.PotionSound;
            potionNotify.Checked = settings.PotionNotify;
            updates.Checked = settings.CheckForUpdates;
            saveDebug.Checked = settings.SaveDebug;
            idleSeconds.SetValue(Clamp(settings.ActiveIdleSeconds, idleSeconds), false);
            hpThreshold.Maximum = settings.HpPercent ? 999 : 9999999;
            hpThreshold.SetValue(Clamp(settings.HpThreshold, hpThreshold), false);
            prayerThreshold.Maximum = settings.PrayerPercent ? 999 : 9999999;
            prayerThreshold.SetValue(Clamp(settings.PrayerThreshold, prayerThreshold), false);
            grandmaster.Checked = settings.Grandmaster;
            treePerk.Checked = settings.TreePerk;
            afkDuration.Text = settings.AfkSeconds / 60 + " min timer";
            afkLead.Maximum = settings.AfkSeconds - 5;
            afkLead.SetValue(Clamp(settings.AfkLeadSeconds, afkLead), false);
            potionSeconds.SetValue(Clamp(settings.PotionSeconds, potionSeconds), false);
            cooldown.SetValue(Clamp(settings.CooldownSeconds, cooldown), false);
            banner.SetValue(Clamp(settings.BannerSeconds, banner), false);
            volume.SetValue(settings.Volume);
        }

        private static int Clamp(int value, NumberField number)
        {
            if (value < number.Minimum) return number.Minimum;
            if (value > number.Maximum) return number.Maximum;
            return value;
        }

        private void Wire(params CheckBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                CheckBox box = boxes[i];
                box.CheckedChanged += delegate { if (!loading) Save(); };
            }
        }

        private KavrosCheckBox Check(string text, int x, int y, int width, bool value)
        {
            KavrosCheckBox box = new KavrosCheckBox();
            box.Text = text;
            box.SetBounds(x, y, width, 22);
            box.Checked = value;
            box.Font = KavrosTheme.Ui(9F, false);
            box.BackColor = KavrosTheme.Panel;
            Controls.Add(box);
            return box;
        }

        private Label Caption(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            label.ForeColor = KavrosTheme.TextSoft;
            label.BackColor = KavrosTheme.Panel;
            label.Font = KavrosTheme.Ui(8.5F, false);
            Controls.Add(label);
            return label;
        }

        private void Number(NumberField number, int x, int y, int min, int max, int value)
        {
            number.SetBounds(x, y, 72, 24);
            number.SetRange(min, max, value);
            Controls.Add(number);
        }

        private void AddButton(string text, int x, int y, int width, ButtonTone tone, Action click)
        {
            OpaqueButton button = new OpaqueButton();
            button.Text = text;
            button.Tone = tone;
            button.SetBounds(x, y, width, 28);
            button.Font = KavrosTheme.Ui(9F, tone == ButtonTone.Primary);
            button.Click += delegate { click(); };
            Controls.Add(button);
        }

        private void DrawConnection(Graphics g, PaintedLabel label)
        {
            using (Brush brush = new SolidBrush(KavrosTheme.Panel)) g.FillRectangle(brush, label.ClientRectangle);
            string text = label.Text ?? "";
            PillTone tone = text.StartsWith("GAME DETECTED", StringComparison.Ordinal) ? PillTone.Ok : text.StartsWith("Waiting", StringComparison.Ordinal) ? PillTone.Warn : PillTone.Bad;
            string pill = tone == PillTone.Ok ? "GAME DETECTED" : tone == PillTone.Warn ? "Waiting" : "Not found";
            int width = KavrosTheme.DrawPill(g, 0, label.Height / 2, pill, tone, KavrosTheme.Panel);
            string rest = text.StartsWith("GAME DETECTED: ", StringComparison.Ordinal) ? text.Substring(15) : text;
            TextRenderer.DrawText(g, rest, KavrosTheme.Ui(8.5F, false), new Rectangle(width + 8, 0, Math.Max(1, label.Width - width - 8), label.Height), KavrosTheme.TextSoft, KavrosTheme.Panel, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private void ArrangeSettings()
        {
            PictureBox crest = null;
            Stream stream = typeof(KavrosTheme).Assembly.GetManifestResourceStream("Kavros.Branding.kavros-crest.png");
            if (stream != null)
            {
                crest = new PictureBox();
                crest.Name = "KavrosCrest";
                crest.AccessibleName = "Kavros";
                crest.TabStop = false;
                crest.Bounds = new Rectangle(16, 9, 65, 65);
                crest.SizeMode = PictureBoxSizeMode.Zoom;
                Bitmap bitmap;
                using (stream)
                using (Image image = Image.FromStream(stream)) bitmap = new Bitmap(image);
                crest.Image = bitmap;
                crest.Disposed += delegate { bitmap.Dispose(); };
            }
            Label footer = new Label();
            footer.Name = "BuildCredit";
            footer.Text = "Made by Zenity  ·  " + AppInfo.DisplayVersion;
            footer.Dock = DockStyle.Bottom;
            footer.Height = 28;
            footer.TextAlign = ContentAlignment.MiddleCenter;
            footer.ForeColor = KavrosTheme.TextFaint;
            footer.BackColor = KavrosTheme.Bg;
            footer.Font = KavrosTheme.Ui(8.5F, false);
            Size original = ClientSize;
            MainSurface body = new MainSurface();
            body.Name = "SettingsBody";
            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.AutoScrollMinSize = new Size(0, original.Height);
            body.Cards.Add(new MainSurface.Card { Label = "WATCH", Bounds = new Rectangle(16, 96, 648, 140) });
            body.Cards.Add(new MainSurface.Card { Label = "ALERTS", Bounds = new Rectangle(16, 248, 648, 390) });
            body.Cards.Add(new MainSurface.Card { Label = "CHANNELS", Bounds = new Rectangle(16, 652, 648, 340) });
            Control[] controls = new Control[Controls.Count];
            Controls.CopyTo(controls, 0);
            Controls.Clear();
            body.Controls.AddRange(controls);
            if (crest != null) body.Controls.Add(crest);
            foreach (Control control in body.Controls)
            {
                if (control is CheckBox || control is Label || control is PaintedLabel)
                {
                    if (control.Top >= 96) control.BackColor = KavrosTheme.Panel;
                }
            }
            Controls.Add(body);
            Controls.Add(footer);
            ClientSize = new Size(original.Width, original.Height + footer.Height);
        }

        private void Save()
        {
            if (loading) return;
            settings.Paused = pause.Checked;
            settings.Volume = volume.Value;
            sound.Volume = settings.Volume;
            settings.ClientTitle = clientTitle;
            settings.IdleEnabled = idleOn.Checked;
            if (settings.SkillingMode) settings.SkillingIdleSeconds = (int)idleSeconds.Value;
            else settings.IdleSeconds = (int)idleSeconds.Value;
            settings.HpEnabled = hpOn.Checked;
            settings.HpThreshold = (int)hpThreshold.Value;
            settings.HpPercent = hpPercent.Checked;
            settings.PrayerEnabled = prayerOn.Checked;
            settings.PrayerThreshold = (int)prayerThreshold.Value;
            settings.PrayerPercent = prayerPercent.Checked;
            settings.AfkEnabled = afkOn.Checked;
            settings.Grandmaster = grandmaster.Checked;
            settings.TreePerk = treePerk.Checked;
            afkDuration.Text = settings.AfkSeconds / 60 + " min timer";
            afkLead.Maximum = settings.AfkSeconds - 5;
            settings.AfkLeadSeconds = (int)afkLead.Value;
            settings.SoundMaster = soundMaster.Checked;
            settings.NotifyMaster = notifyMaster.Checked;
            settings.IdleSound = idleSound.Checked;
            settings.IdleNotify = idleNotify.Checked;
            settings.HpSound = hpSound.Checked;
            settings.HpNotify = hpNotify.Checked;
            settings.PrayerSound = prayerSound.Checked;
            settings.PrayerNotify = prayerNotify.Checked;
            settings.AfkSound = afkSound.Checked;
            settings.AfkNotify = afkNotify.Checked;
            settings.PotionEnabled = potionOn.Checked;
            settings.PotionSeconds = (int)potionSeconds.Value;
            settings.PotionSound = potionSound.Checked;
            settings.PotionNotify = potionNotify.Checked;
            settings.CooldownSeconds = (int)cooldown.Value;
            settings.BannerSeconds = (int)banner.Value;
            settings.CheckForUpdates = updates.Checked;
            settings.SaveDebug = saveDebug.Checked;
            settings.Clamp();
            try { SettingsText.Save(settingsPath, settings); } catch (IOException) { }
            panel.SetPaused(settings.Paused);
        }

        // Combat idle and skilling idle keep their own "After N seconds"; the
        // one field shows whichever mode is on.
        private void SwitchIdleMode()
        {
            if (settings.SkillingMode) settings.SkillingIdleSeconds = (int)idleSeconds.Value;
            else settings.IdleSeconds = (int)idleSeconds.Value;
            settings.SkillingMode = skillingOn.Checked;
            loading = true;
            try { idleSeconds.SetValue(Clamp(settings.ActiveIdleSeconds, idleSeconds), false); }
            finally { loading = false; }
            activity = new SceneActivity();
            Save();
        }

        private void SetStatus(string text) { if (!status.IsDisposed) status.Text = text; }

        private uint lastInputStamp;
        private bool sawGameForeground;

        private bool PlayerInput()
        {
            bool foreground = selected != IntPtr.Zero && WindowLocator.IsForeground(selected);
            NativeMethods.LastInput info = new NativeMethods.LastInput();
            info.Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.LastInput));
            if (!NativeMethods.GetLastInputInfo(ref info))
            {
                sawGameForeground = foreground;
                return false;
            }
            bool fresh = info.Time != lastInputStamp;
            uint age = NativeMethods.GetTickCount() - info.Time;
            // GetLastInputInfo is system-wide. Count a new stamp only while the
            // chosen client has stayed in front, so typing in another app does not.
            bool count = foreground && sawGameForeground && fresh && age < 800;
            lastInputStamp = info.Time;
            sawGameForeground = foreground;
            return count;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0231)
            {
                windowDragging = true;
                if (timer.Enabled) timer.Stop();
            }
            if (message.Msg == 0x0232)
            {
                windowDragging = false;
                if (started && !timer.Enabled) timer.Start();
            }
            base.WndProc(ref message);
        }

        private string lastLoggedReason = "";

        private void WriteDebug(bool pixels, bool moving)
        {
            if (!settings.SaveDebug) return;
            bool reasonChanged = activity.Reason != lastLoggedReason;
            bool due = (DateTime.UtcNow - lastDebugSave).TotalSeconds >= 2;
            if (!reasonChanged && !due) return;
            if (due) lastDebugSave = DateTime.UtcNow;
            lastLoggedReason = activity.Reason;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                HudReading reading = grab.Reading == null ? new HudReading() : grab.Reading;
                string line = DateTime.Now.ToString("HH:mm:ss")
                    + " route=" + grab.Route
                    + " hp=" + (reading.Hp == null ? "none" : reading.Hp.Value.ToString())
                    + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString())
                    + " prayer=" + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString())
                    + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString())
                    + " buffs=" + (reading.Buffs == null ? 0 : reading.Buffs.Count).ToString()
                    + " buffText=" + (reading.BuffTrace ?? "")
                    + " lifeBar=" + reading.LifeBar.X + "," + reading.LifeBar.Y + " " + reading.LifeBar.Width + "x" + reading.LifeBar.Height
                    + " prayerBar=" + reading.PrayerBar.X + "," + reading.PrayerBar.Y + " " + reading.PrayerBar.Width + "x" + reading.PrayerBar.Height
                    + " lifeText=" + (reading.LifeTrace ?? "")
                    + " prayerText=" + (reading.PrayerTrace ?? "")
                    + " pixels=" + pixels
                    + " activity=" + activity.Reason
                    + " skill=" + (grab.SkillTrace ?? "")
                    + " icons=" + (buffClock.Log ?? "")
                    + " chat=" + (ChatReader.Trace ?? "")
                    + " pots=" + (chatPots.Log ?? "")
                    + " metrics=" + (reading.MetricsFound ? ("xp=" + reading.MetricsXp.ToString()) : ("lost " + (reading.MetricsNote ?? "not found")))
                    + " idleReset=" + (moving ? "yes" : "no")
                    + "\r\n";
                string logPath = Path.Combine(dir, "debug-log.txt");
                File.AppendAllText(logPath, line);
                FileInfo info = new FileInfo(logPath);
                if (info.Exists && info.Length > 200000)
                {
                    string tail = File.ReadAllText(logPath);
                    if (tail.Length > 80000) File.WriteAllText(logPath, tail.Substring(tail.Length - 80000));
                }
                if (due) File.AppendAllText(Path.Combine(dir, "startup.txt"), "captureMs=" + grab.LastTickMs.ToString() + " shownMs=" + StartupClock.ShownMs.ToString() + "\r\n");
            }
            catch (Exception) { }
        }

        private static void SaveAnnotated(string dir, ImageBuffer image, HudReading reading)
        {
            using (Bitmap bitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] raw = new byte[Math.Abs(stride) * bitmap.Height];
                    for (int y = 0; y < image.Height; y++)
                        for (int x = 0; x < image.Width; x++)
                        {
                            int color = image.Argb(x, y);
                            int i = y * stride + x * 4;
                            raw[i] = (byte)(color & 255);
                            raw[i + 1] = (byte)((color >> 8) & 255);
                            raw[i + 2] = (byte)((color >> 16) & 255);
                            raw[i + 3] = 255;
                        }
                    System.Runtime.InteropServices.Marshal.Copy(raw, 0, data.Scan0, raw.Length);
                }
                finally { bitmap.UnlockBits(data); }
                using (Graphics g = Graphics.FromImage(bitmap))
                using (Pen life = new Pen(Color.OrangeRed, 2))
                using (Pen prayer = new Pen(Color.MediumPurple, 2))
                using (Pen buff = new Pen(Color.LimeGreen, 2))
                {
                    g.DrawRectangle(life, reading.LifeBar.X, reading.LifeBar.Y, reading.LifeBar.Width, reading.LifeBar.Height);
                    g.DrawRectangle(life, reading.LifeText.X, reading.LifeText.Y, reading.LifeText.Width, reading.LifeText.Height);
                    g.DrawRectangle(prayer, reading.PrayerBar.X, reading.PrayerBar.Y, reading.PrayerBar.Width, reading.PrayerBar.Height);
                    g.DrawRectangle(prayer, reading.PrayerText.X, reading.PrayerText.Y, reading.PrayerText.Width, reading.PrayerText.Height);
                    if (reading.BuffRects != null)
                        for (int i = 0; i < reading.BuffRects.Count; i++)
                            g.DrawRectangle(buff, reading.BuffRects[i].X, reading.BuffRects[i].Y, reading.BuffRects[i].Width, reading.BuffRects[i].Height);
                }
                bitmap.Save(Path.Combine(dir, "debug-capture.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private void ShowSettings()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            BringToFront();
            Activate();
        }

        private void ShowPanel()
        {
            FollowPanel();
        }

        private bool panelTop;
        private bool panelTopKnown;
        private DateTime raiseUntil = DateTime.MinValue;

        // Keep the panel above the game without taking focus. Topmost is set
        // only while the game (or the panel or Settings) is in front, and for a
        // few seconds after an explicit launch, then the panel goes back under
        // whatever application the player switched to. Only a change is sent.
        private void KeepPanelOnTop(bool exists, bool minimized, bool foreground)
        {
            if (!panel.IsHandleCreated || !panel.Visible) { panelTopKnown = false; return; }
            IntPtr front = NativeMethods.GetForegroundWindow();
            // Settings in front does not count: the panel must not cover it.
            bool own = front != IntPtr.Zero && front == panel.Handle;
            bool want = PanelDock.WantTopMost(exists, minimized, foreground, own, DateTime.UtcNow < raiseUntil);
            if (!PanelDock.ZOrderChanged(panelTopKnown, panelTop, want)) return;
            const uint flags = 0x0001 | 0x0002 | 0x0010; // NOSIZE | NOMOVE | NOACTIVATE
            if (want) NativeMethods.SetWindowPos(panel.Handle, new IntPtr(-1), 0, 0, 0, 0, flags);
            else
            {
                NativeMethods.SetWindowPos(panel.Handle, new IntPtr(-2), 0, 0, 0, 0, flags);
                // NOTOPMOST lands above every normal window; tuck the panel under
                // the application that is now in front.
                if (front != IntPtr.Zero && !own) NativeMethods.SetWindowPos(panel.Handle, front, 0, 0, 0, 0, flags);
            }
            panelTop = want;
            panelTopKnown = true;
        }

        private void FollowPanel()
        {
            bool exists = selected != IntPtr.Zero && NativeMethods.IsWindow(selected);
            IntPtr owner=exists ? selected : IntPtr.Zero;
            if (owner!=panelOwner && (exists || panel.IsHandleCreated))
            {
                try { NativeMethods.SetOwner(panel.Handle, owner); panelOwner=owner; }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }
            bool minimized = exists && NativeMethods.IsIconic(selected);
            bool foreground = exists && WindowLocator.IsForeground(selected);
            bool show = PanelDock.Visible(true, exists, minimized, foreground, panel.ContainsFocus, Visible && ContainsFocus);
            if (!show)
            {
                if (panel.Visible) panel.Hide();
                return;
            }
            Rectangle game = exists ? NativeMethods.ClientBounds(selected) : Screen.PrimaryScreen.WorkingArea;
            Rectangle desk = Screen.FromRectangle(game).WorkingArea;
            int x, y;
            PanelDock.Place(game.X, game.Y, game.Width, game.Height, panel.Width, panel.Height, settings.PanelOffsetX, settings.PanelOffsetY, desk.X, desk.Y, desk.Width, desk.Height, out x, out y);
            if (panel.IsDragging)
            {
                if (!panel.Visible) panel.Show();
                KeepPanelOnTop(exists, minimized, foreground);
                return;
            }
            Rectangle now = game;
            if (!haveDockedGame || dockedGame != now)
            {
                haveDockedGame = true;
                dockedGame = now;
                if (panel.Left != x || panel.Top != y) panel.Location = new Point(x, y);
            }
            if (!panel.Visible) { panel.Show(); panelTopKnown = false; }
            KeepPanelOnTop(exists, minimized, foreground);
        }

        private void TryPickClient()
        {
            if ((NativeMethods.GetAsyncKeyState(1) & 0x8000) == 0) return;
            NativeMethods.PointStruct cursor;
            if (!NativeMethods.GetCursorPos(out cursor)) return;
            IntPtr hit = NativeMethods.WindowFromPoint(cursor);
            IntPtr root = NativeMethods.GetAncestor(hit, 2);
            if (root == IntPtr.Zero) root = hit;
            RefreshWindows();
            for (int i = 0; i < found.Count; i++)
            {
                if (WindowLocator.Score(found[i]) < 100) continue;
                if (found[i].Handle != hit && found[i].Handle != root) continue;
                picking = false;
                selected = found[i].Handle;
                clientTitle = found[i].Title;
                settings.ClientTitle = clientTitle;
                loading = true;
                windows.SelectedItem = found[i];
                loading = false;
                connection.Text = "GAME DETECTED: " + found[i].Title;
                connection.Invalidate();
                Save();
                SetStatus("Watching " + found[i].Title + ".");
                return;
            }
        }

        private string ClientCaption()
        {
            if (selected == IntPtr.Zero) return "Choose a client";
            int index = 0;
            int seen = 0;
            for (int i = 0; i < found.Count; i++)
            {
                if (WindowLocator.Score(found[i]) < 100) continue;
                seen++;
                if (found[i].Handle == selected) index = seen;
            }
            GameWindow current = null;
            for (int i = 0; i < found.Count; i++) if (found[i].Handle == selected) current = found[i];
            if (current == null) return clientTitle;
            return WindowLocator.ClientLabel(current, Math.Max(0, index - 1));
        }

        private void Bootstrap()
        {
            WardenSettings loaded = SettingsText.Load(settingsPath);
            try
            {
                if (!IsDisposed) BeginInvoke(new Action(delegate { ApplyLoaded(loaded); }));
            }
            catch (Exception) { }
        }

        private void ApplyLoaded(WardenSettings loaded)
        {
            settings = loaded ?? new WardenSettings();
            clientTitle = settings.ClientTitle ?? "";
            sound.Volume = settings.Volume;
            loading=true;
            try { ApplySettingsToControls(); } finally { loading=false; }
            panel.SetPaused(settings.Paused);
            PlacePanel();
            nextUpdateCheck = DateTime.UtcNow.AddHours(3);
            if (settings.CheckForUpdates) AppUpdates.Check(this, false, SetStatus);
            CompanionStatus.Write("running", settings.Paused);
            DiscoverClients();
            panel.SetNote("Waiting for Kavros");
        }

        private void PlacePanel()
        {
            int x = settings.PanelX;
            int y = settings.PanelY;
            if (x == int.MinValue)
            {
                Rectangle work = Screen.PrimaryScreen == null ? new Rectangle(0, 0, 1920, 1080) : Screen.PrimaryScreen.WorkingArea;
                x = work.Right - panel.Width - 24;
                y = work.Top + 72;
            }
            try
            {
                Screen[] screens = Screen.AllScreens;
                int[] sx = new int[screens.Length];
                int[] sy = new int[screens.Length];
                int[] sw = new int[screens.Length];
                int[] sh = new int[screens.Length];
                for (int i = 0; i < screens.Length; i++)
                {
                    sx[i] = screens[i].WorkingArea.X;
                    sy[i] = screens[i].WorkingArea.Y;
                    sw[i] = screens[i].WorkingArea.Width;
                    sh[i] = screens[i].WorkingArea.Height;
                }
                ScreenClamp.Place(ref x, ref y, panel.Width, panel.Height, sx, sy, sw, sh);
            }
            catch (Exception) { }
            panel.Location = new Point(x, y);
        }

        internal void BringUp()
        {
            if(Visible){ShowSettings();return;}
            if(selected!=IntPtr.Zero && NativeMethods.IsIconic(selected)){ShowSettings();return;}
            // An explicit launch (Companion, the shortcut or a second start)
            // lifts the panel over everything for a few seconds without taking
            // keyboard focus from the game. After that it stays topmost while
            // the game is in front.
            raiseUntil = DateTime.UtcNow.AddSeconds(8);
            panelTopKnown = false;
            ShowPanel();
            if (panel.IsHandleCreated && !panel.Visible) { panel.Show(); KeepPanelOnTop(selected != IntPtr.Zero, false, false); }
        }

        private void Exit()
        {
            if (closing) return;
            closing = true;
            try { Save(); } catch (Exception) { }
            try { CompanionStatus.Write("stopped", settings.Paused); } catch (Exception) { }
            try { timer.Stop(); timer.Dispose(); } catch (Exception) { }
            try { grab.Stop(); } catch (Exception) { }
            try { tray.Visible = false; } catch (Exception) { }
            try { Application.Exit(); } catch (Exception) { }
            Environment.Exit(0);
        }

        private void RefreshWindows()
        {
            DiscoverClients();
        }

        private void ApplyWindows(List<GameWindow> list)
        {
            found = list == null ? new List<GameWindow>() : list;
            loading = true;
            windows.Items.Clear();
            int n = 0;
            for (int i = 0; i < found.Count; i++)
            {
                if (WindowLocator.Score(found[i]) < 100) continue;
                found[i].Label = WindowLocator.ClientLabel(found[i], n);
                n++;
                windows.Items.Add(found[i]);
            }
            GameWindow choice = WindowLocator.Choose(found, selected, clientTitle);
            if (choice != null)
            {
                selected = choice.Handle;
                windows.SelectedItem = choice;
            }
            loading = false;
            if (choice == null) connection.Text = "Waiting for Kavros";
            else connection.Text = "GAME DETECTED: " + choice.Title;
            connection.Invalidate();
            if(choice!=null && !everAttached){everAttached=true;BringUp();}
        }

        private void AttachSelected()
        {
            GameWindow window = windows.SelectedItem as GameWindow;
            selected = window == null ? IntPtr.Zero : window.Handle;
            if (window != null) { clientTitle = window.Title; settings.ClientTitle = clientTitle; if (!loading) Save(); }
            connection.Text = window == null ? "Waiting for Kavros" : "GAME DETECTED: " + window.Title;
            connection.Invalidate();
        }

        private void Tick()
        {
            if (selected != potsClient)
            {
                potsClient = selected;
                chatPots.Attach();
            }
            if (picking) TryPickClient();
            if (!windowDragging && (selected == IntPtr.Zero || !NativeMethods.IsWindow(selected)) && (DateTime.UtcNow - lastClientSearch).TotalSeconds > 2)
                DiscoverClients();
            if (settings.CheckForUpdates && DateTime.UtcNow >= nextUpdateCheck)
            {
                nextUpdateCheck = DateTime.UtcNow.AddHours(3);
                AppUpdates.Check(this, false, SetStatus);
            }
            bool minimized = selected != IntPtr.Zero && NativeMethods.IsIconic(selected);
            timer.Interval = minimized || selected == IntPtr.Zero ? 1000 : CapturePlan.SampleMilliseconds;
            grab.PanelBounds = panel.Visible ? panel.Bounds : Rectangle.Empty;
            if(observedClient!=selected)
            {
                observedClient=selected; hpGate=new ReadGate(); prayerGate=new ReadGate();
                activity=new SceneActivity(); engine=new WardenEngine(); lastHud=new HudReading();
                potionDisplay=new PotionDisplay(); lastFrame=DateTime.MinValue;
            }
            grab.Tick(selected, settings, !settings.Paused && !panel.IsDragging && !windowDragging && !minimized);
            if (!windowDragging) FollowPanel();
            bool pixels = !settings.Paused && (DateTime.UtcNow-grab.FrameTime).TotalSeconds<=3 && grab.Reading != null && (grab.Reading.StripFound || grab.Reading.BuffBarFound || grab.Reading.Hp != null || grab.Reading.Prayer != null);
            if (grab.Reading != null && pixels) lastHud = grab.Reading;
            bool moving = activity.Tick(null, lastHud, grab.Input, grab.XpActivity, grab.PackActivity);
            WriteDebug(pixels, moving);
            DateTime sampleTime = DateTime.UtcNow;
            bool newCapture=grab.FrameTime!=lastFrame;
            bool freshFrame=pixels && newCapture;
            if(newCapture)lastFrame=grab.FrameTime;
            if (freshFrame)
            {
                hpGate.Push(lastHud.Hp, lastHud.HpMax, sampleTime);
                prayerGate.Push(lastHud.Prayer, lastHud.PrayerMax, sampleTime);
            }
            else
            {
                hpGate.Push(null, null, sampleTime);
                prayerGate.Push(null, null, sampleTime);
            }
            int? hp = hpGate.Value;
            int? hpMax = hpGate.Max;
            int? prayer = prayerGate.Value;
            int? prayerMax = prayerGate.Max;
            bool hpComparable = hp != null && WardenSettings.Comparable(settings.HpPercent, lastHud.HpFromOrb, hpMax != null);
            bool prayerComparable = prayer != null && WardenSettings.Comparable(settings.PrayerPercent, lastHud.PrayerFromOrb, prayerMax != null);
            WatchSample sample = new WatchSample();
            sample.Time = DateTime.UtcNow;
            sample.GameAttached = selected != IntPtr.Zero && NativeMethods.IsWindow(selected);
            sample.PixelsFresh = pixels;
            sample.CharacterMoving = moving;
            sample.SkillProgress = grab.XpActivity || grab.PackActivity;
            sample.InputActivity = grab.Input;
            shownHp = hp;
            shownHpMax = hpMax;
            shownPrayer = prayer;
            shownPrayerMax = prayerMax;
            // Potion alerts come from the chat box. The buff bar does not raise them.
            sample.Hp = freshFrame && lastHud.HpMax.HasValue && !hpGate.Stale && hpComparable ? hp : (int?)null;
            sample.HpMax = hpMax;
            sample.Prayer = freshFrame && lastHud.PrayerMax.HasValue && !prayerGate.Stale && prayerComparable ? prayer : (int?)null;
            sample.PrayerMax = prayerMax;
            sample.PotionSeconds = null;
            sample.PotionClear = false;
            List<ChatNotice> potionNotices = freshFrame ? chatPots.Observe(lastHud.ChatHits) : new List<ChatNotice>();
            List<AlertEvent> events = engine.Tick(sample, settings);
            for (int n = 0; n < potionNotices.Count; n++)
            {
                if (!settings.PotionEnabled) break;
                AlertEvent extra = new AlertEvent();
                extra.Kind = "potion";
                extra.Title = potionNotices[n].Title;
                extra.Detail = potionNotices[n].Detail;
                extra.Sound = settings.AllowsSound("potion");
                extra.Notify = settings.AllowsNotify("potion");
                events.Add(extra);
            }
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Notify)
                {
                    popup.Present(events[i], settings.BannerSeconds);
                    toast.Show(events[i].Title, events[i].Detail);
                }
                if (events[i].Sound) sound.Play(events[i].Kind);
            }
            UpdatePanel(pixels);
            CompanionStatus.Write("running", settings.Paused);
        }

        private void UpdatePanel(bool pixels)
        {
            panel.SetGame(selected != IntPtr.Zero && NativeMethods.IsWindow(selected), ClientCaption());
            panel.SetNote(grab.Note);
            if (settings.Paused)
            {
                panel.Idle.Pill = "Paused";
                panel.Idle.Tone = PillTone.Warn;
                panel.Idle.Value = "Watching off";
            }
            else
            {
                bool skillPill = engine.Skilling;
                int still = (int)engine.StillFor.TotalSeconds;
                panel.Idle.Pill = engine.IdleLatched ? "Idle" : skillPill ? "Skilling" : (still > 0 ? "Still" : "Moving");
                panel.Idle.Tone = engine.IdleLatched ? PillTone.Bad : PillTone.Ok;
                panel.Idle.Value = !pixels ? "No view" : still.ToString() + (skillPill ? "s since XP" : "s still");
            }
            panel.Hp.Pill = engine.HpLatched ? "Low HP" : "Hitpoints";
            panel.Hp.Tone = engine.HpLatched ? PillTone.Bad : !pixels || lastHud.Hp == null ? PillTone.Example : PillTone.Ok;
            panel.Hp.Value = !pixels ? "No view" : hpGate.Stale ? Stale(shownHp) : shownHp == null ? "Unread" : Format(shownHp, shownHpMax, lastHud.HpFromOrb);
            panel.Prayer.Pill = engine.PrayerLatched ? "Low prayer" : "Prayer";
            panel.Prayer.Tone = engine.PrayerLatched ? PillTone.Warn : !pixels || lastHud.Prayer == null ? PillTone.Example : PillTone.Ok;
            panel.Prayer.Value = !pixels ? "No view" : prayerGate.Stale ? Stale(shownPrayer) : shownPrayer == null ? "Unread" : Format(shownPrayer, shownPrayerMax, lastHud.PrayerFromOrb);
            string roster = chatPots.Detail ?? "";
            bool potionLow = roster.IndexOf("running low") >= 0 || roster.IndexOf("expired") >= 0;
            panel.Potion.Pill = potionLow ? "Potion" : "Potions";
            panel.Potion.Tone = potionLow ? PillTone.Warn : roster.Length == 0 ? PillTone.Example : PillTone.Ok;
            panel.Potion.Value = roster.Length > 0 && !settings.Paused
                ? roster
                : (!pixels ? "No view" : "No potion timer");
            if (lastHud.MetricsFound) metricsAbsentSince = DateTime.MinValue;
            else if (metricsAbsentSince == DateTime.MinValue) metricsAbsentSince = DateTime.UtcNow;
            bool metricsHint = !lastHud.MetricsFound && metricsAbsentSince != DateTime.MinValue
                && (DateTime.UtcNow - metricsAbsentSince).TotalSeconds >= 5;
            panel.SetMetricsHint(metricsHint);
            int left = Math.Max(0, (int)engine.AfkRemaining.TotalSeconds);
            panel.Afk.Pill = engine.AfkLatched ? "AFK soon" : "AFK timer";
            panel.Afk.Tone = engine.AfkLatched ? PillTone.Bad : PillTone.Ok;
            panel.Afk.Value = settings.Paused ? "Paused" : selected==IntPtr.Zero ? "Waiting for client" : (left / 60).ToString() + ":" + (left % 60).ToString("00") + " left";
            panel.Idle.Commit();
            panel.Hp.Commit();
            panel.Prayer.Commit();
            panel.Potion.Commit();
            panel.Afk.Commit();
        }

        private static string Stale(int? value)
        {
            return value == null ? "?" : value.Value.ToString() + " ?";
        }

        private static string Format(int? current, int? max, bool orb)
        {
            if (current == null) return "Unread";
            if (orb) return current.Value.ToString() + "% orb";
            if (max != null) return current.Value.ToString() + " / " + max.Value.ToString();
            return current.Value.ToString();
        }

        private void Pin(int which)
        {
            if (selected == IntPtr.Zero || NativeMethods.IsIconic(selected))
            {
                SetStatus("Restore the Kavros window, then pin the number.");
                return;
            }
            Rectangle client = NativeMethods.ClientBounds(selected);
            string caption = which == 2 ? "DRAG THE BUFF BAR TIMERS" : "DRAG THE HITPOINT OR PRAYER NUMBER";
            Bitmap frozen = null;
            try
            {
                if (client.Width >= 80 && client.Height >= 80 && client.Width <= 4096 && client.Height <= 4096)
                {
                    frozen = new Bitmap(client.Width, client.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics graphics = Graphics.FromImage(frozen))
                        graphics.CopyFromScreen(client.Location, Point.Empty, client.Size, CopyPixelOperation.SourceCopy);
                }
            }
            catch (Exception) { if (frozen != null) frozen.Dispose(); frozen = null; }
            timer.Stop();
            try
            {
            using (RegionPicker picker = new RegionPicker(client, frozen, caption))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                Rectangle picked = picker.Selection;
                if (picked.Width < 8) return;
                double x = picked.X / (double)client.Width;
                double y = picked.Y / (double)client.Height;
                double w = picked.Width / (double)client.Width;
                double h = picked.Height / (double)client.Height;
                string done = "Hitpoint area pinned.";
                if (which == 0) { settings.HpPinX = x; settings.HpPinY = y; settings.HpPinW = w; settings.HpPinH = h; }
                else if (which == 1) { settings.PrayerPinX = x; settings.PrayerPinY = y; settings.PrayerPinW = w; settings.PrayerPinH = h; done = "Prayer area pinned."; }
                else { settings.BuffPinX = x; settings.BuffPinY = y; settings.BuffPinW = w; settings.BuffPinH = h; done = "Buff bar pinned."; }
                Save();
                SetStatus(done);
            }
            }
            finally
            {
                if (frozen != null) frozen.Dispose();
                if (!timer.Enabled) timer.Start();
            }
        }

        private void DrawWindowItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selectedItem = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color fill = selectedItem ? KavrosTheme.BgRaise : KavrosTheme.Input;
            Color ink = selectedItem ? KavrosTheme.Gold : KavrosTheme.Text;
            using (Brush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (selectedItem)
            {
                using (Pen pen = new Pen(KavrosTheme.Gold)) e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
            string text = windows.Items[e.Index].ToString();
            TextRenderer.DrawText(e.Graphics, text, KavrosTheme.Ui(9F, false), new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), ink, fill, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        }

        private static AlertEvent SampleAlert()
        {
            AlertEvent alert = new AlertEvent();
            alert.Kind = "hp";
            alert.Title = "Hitpoints are low";
            alert.Detail = "This is a test notification. It stays above other windows.";
            return alert;
        }

        private static Icon LoadIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "branding", "afk-warden.ico");
            if (File.Exists(path)) return new Icon(path);
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(KavrosTheme.Bg);
                using (Pen pen = new Pen(KavrosTheme.Gold, 2)) g.DrawEllipse(pen, 5, 5, 21, 21);
            }
            IntPtr handle = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
    }

    internal sealed class RegionPicker : Form
    {
        private Point origin;
        private Rectangle selection;
        private bool drag;
        internal Rectangle Selection;
        private readonly string caption;
        private readonly Bitmap frozen;

        internal RegionPicker(Rectangle client, Bitmap frozen, string caption)
        {
            this.caption = caption;
            this.frozen = frozen;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = client;
            BackColor = KavrosTheme.BgDeep;
            Cursor = Cursors.Cross;
            TopMost = true;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
            MouseDown += delegate(object sender, MouseEventArgs e)
            {
                drag = true;
                origin = e.Location;
                ReplaceSelection(new Rectangle(e.Location, Size.Empty));
            };
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!drag) return;
                ReplaceSelection(Rectangle.FromLTRB(Math.Min(origin.X, e.X), Math.Min(origin.Y, e.Y), Math.Max(origin.X, e.X), Math.Max(origin.Y, e.Y)));
            };
            MouseUp += delegate { drag = false; Selection = selection; DialogResult = DialogResult.OK; Close(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        private void ReplaceSelection(Rectangle next)
        {
            Rectangle previous = selection;
            selection = next;
            Invalidate(Rectangle.Inflate(previous, 4, 4));
            Invalidate(Rectangle.Inflate(selection, 4, 4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (frozen != null) e.Graphics.DrawImage(frozen, ClientRectangle);
            else using (Brush fill = new SolidBrush(KavrosTheme.BgDeep)) e.Graphics.FillRectangle(fill, ClientRectangle);
            using (Brush bar = new SolidBrush(Color.FromArgb(210, KavrosTheme.BgDeep))) e.Graphics.FillRectangle(bar, 0, 0, Width, 36);
            using (Brush gold = new SolidBrush(KavrosTheme.Gold)) e.Graphics.FillRectangle(gold, 0, 0, Width, 2);
            TextRenderer.DrawText(e.Graphics, caption, KavrosTheme.Display(8F), new Rectangle(12, 8, Width - 24, 24), KavrosTheme.Gold, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (selection.Width > 0 && selection.Height > 0)
            {
                using (Brush wash = new SolidBrush(Color.FromArgb(48, KavrosTheme.Gold))) e.Graphics.FillRectangle(wash, selection);
                using (Pen pen = new Pen(KavrosTheme.Gold, 2)) e.Graphics.DrawRectangle(pen, selection);
            }
        }
    }

    internal static class StartupClock
    {
        internal static readonly DateTime Began = DateTime.UtcNow;
        internal static int ShownMs;
        internal static void MarkShown()
        {
            ShownMs = (int)(DateTime.UtcNow - Began).TotalMilliseconds;
            if (ShownMs < 0) ShownMs = 0;
        }
    }

    internal static class CrashLog
    {
        internal static void Install()
        {
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args) { Write(args.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Exception ex = args.ExceptionObject as Exception;
                Write(ex ?? new Exception("Unhandled error"));
            };
        }

        internal static void Write(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex + "\r\n\r\n");
            }
            catch (Exception) { }
            try { MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch (Exception) { }
        }
    }
}
