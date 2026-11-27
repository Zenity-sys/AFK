using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
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
            ClientLaunch.ReadArgs(args);
            ClientLaunch.ReadEnvironment();
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
                try { ClientLaunch.WriteRequest(); } catch (Exception) { }
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
        internal bool ColorParts;
        internal bool Dim;

        internal StatusRow()
        {
            Renderer = Draw;
            Height = 28;
            BackColor = KavrosTheme.Bg;
        }

        private string paintedPill, paintedValue;
        private PillTone paintedTone;
        private bool paintedDim;

        // Repaint only when the text or tone changed. The panel sits over the
        // game, so an unchanged row is not redrawn twice a second.
        internal void Commit()
        {
            if (Pill == paintedPill && Value == paintedValue && Tone == paintedTone && Dim == paintedDim) return;
            Invalidate();
        }

        private void Draw(Graphics g, PaintedLabel label)
        {
            paintedPill = Pill;
            paintedValue = Value;
            paintedTone = Tone;
            paintedDim = Dim;
            using (Brush brush = new SolidBrush(KavrosTheme.Bg)) g.FillRectangle(brush, ClientRectangle);
            int width = KavrosTheme.DrawPill(g, 0, Height / 2, Pill, Tone, KavrosTheme.Bg);
            Color ink = Dim ? KavrosTheme.TextSoft : KavrosTheme.Text;
            string[] lines = (Value ?? "").Split('\n');
            if (lines.Length > 1)
            {
                int lineH = Math.Max(1, Height / lines.Length);
                Font lineFont = KavrosTheme.Ui(9F, false);
                for (int i = 0; i < lines.Length; i++)
                    TextRenderer.DrawText(g, lines[i], lineFont, new Rectangle(width + 10, i * lineH, Math.Max(1, Width - width - 12), lineH), ink, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                return;
            }
            if (!ColorParts)
            {
                TextRenderer.DrawText(g, Value, KavrosTheme.Ui(9F, false), new Rectangle(width + 10, 0, Math.Max(1, Width - width - 12), Height), ink, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                return;
            }
            string[] parts = (Value ?? "").Split(new string[] { " · " }, StringSplitOptions.None);
            int x = width + 10;
            Font font = KavrosTheme.Ui(9F, false);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    Size gap = TextRenderer.MeasureText(g, " · ", font, new Size(80, Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(g, " · ", font, new Rectangle(x, 0, gap.Width + 2, Height), KavrosTheme.TextFaint, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    x += gap.Width;
                }
                int argb = ChatPotionBook.ValueArgb(parts[i]);
                Color color = argb == 0 ? KavrosTheme.Text : Color.FromArgb(argb);
                Size size = TextRenderer.MeasureText(g, parts[i], font, new Size(400, Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                int room = Math.Max(1, Width - x);
                TextRenderer.DrawText(g, parts[i], font, new Rectangle(x, 0, Math.Min(room, size.Width + 2), Height), color, KavrosTheme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                x += size.Width;
            }
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
        internal readonly StatusRow Sacrifice = new StatusRow();
        internal readonly StatusRow Afk = new StatusRow();
        private readonly OpaqueButton sacrificeReset = new OpaqueButton();
        private bool sacrificeResetWanted;
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
        internal event Action SacrificeReset;
        internal event Action NotesDismissed;
        private Panel notesHost;
        private TextBox notesBox;
        private bool notesWanted;
        private const int NotesHeight = 168;

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
            ClientSize = new Size(400, 294);
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
            Potion.ColorParts = true;
            Place(Sacrifice, 194);
            Sacrifice.Dim = true;
            sacrificeReset.Text = "Reset";
            sacrificeReset.Tone = ButtonTone.Ghost;
            sacrificeReset.Surface = KavrosTheme.Bg;
            sacrificeReset.Visible = false;
            sacrificeReset.Click += delegate { if (SacrificeReset != null) SacrificeReset(); };
            Controls.Add(sacrificeReset);
            Place(Afk, 242);
            metricsHint.Text = PanelHint.Line1 + "\n" + PanelHint.Line2;
            metricsHint.ForeColor = KavrosTheme.Text;
            metricsHint.BackColor = KavrosTheme.Bg;
            metricsHint.Renderer = DrawHint;
            Controls.Add(metricsHint);
            pause.Text = "Pause";
            pause.Tone = ButtonTone.Primary;
            pause.Click += delegate { if (PauseRequested != null) PauseRequested(); };
            Controls.Add(pause);
            exit.Text = "Exit";
            exit.Tone = ButtonTone.Secondary;
            exit.Click += delegate { if (ExitRequested != null) ExitRequested(); };
            Controls.Add(exit);
            LayoutFoot();
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
        }

        internal bool IsDragging { get { return dragging; } }
        internal void SetPaused(bool paused) { pause.Text = paused ? "Resume" : "Pause"; }
        internal void SetNote(string text) { note.Text = text; }
        private bool hintWanted = true;
        internal void ShowSacrificeReset(bool show)
        {
            sacrificeResetWanted = show;
            sacrificeReset.Visible = show && !collapsed;
        }
        internal void SetMetricsHint(bool show)
        {
            hintWanted = PanelHint.Always || show;
            metricsHint.Visible = hintWanted && !collapsed;
        }
        private bool gameFound;
        private string gameName = "";
        internal void SetGame(bool found, string name)
        {
            if (gameFound == found && gameName == (name ?? "")) return;
            gameFound = found;
            gameName = name ?? "";
            note.Invalidate();
            title.Invalidate();
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
            Idle.Visible = Hp.Visible = Prayer.Visible = Potion.Visible = Sacrifice.Visible = Afk.Visible = !collapsed;
            sacrificeReset.Visible = !collapsed && sacrificeResetWanted;
            pause.Visible = exit.Visible = !collapsed;
            ApplyNotes();
        }

        internal void ShowNotes(string text)
        {
            EnsureNotes();
            notesBox.Text = text ?? "";
            notesWanted = true;
            ApplyNotes();
        }

        internal void HideNotes()
        {
            notesWanted = false;
            ApplyNotes();
        }

        private void EnsureNotes()
        {
            if (notesHost != null) return;
            notesHost = new Panel();
            notesHost.BackColor = KavrosTheme.BgRaise;
            notesHost.Visible = false;
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 28;
            header.BackColor = KavrosTheme.BgRaise;
            Label title = new Label();
            title.Text = "What's new";
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Padding = new Padding(8, 0, 0, 0);
            title.ForeColor = KavrosTheme.Gold;
            title.BackColor = KavrosTheme.BgRaise;
            title.Font = KavrosTheme.Ui(9F, true);
            OpaqueButton close = new OpaqueButton();
            close.Text = "Got it";
            close.Dock = DockStyle.Right;
            close.Width = 72;
            close.Tone = ButtonTone.Secondary;
            close.TabStop = false;
            close.Click += delegate
            {
                HideNotes();
                if (NotesDismissed != null) NotesDismissed();
            };
            header.Controls.Add(title);
            header.Controls.Add(close);
            notesBox = new TextBox();
            notesBox.Multiline = true;
            notesBox.ReadOnly = true;
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.Dock = DockStyle.Fill;
            notesBox.BorderStyle = BorderStyle.None;
            notesBox.TabStop = false;
            notesBox.Font = KavrosTheme.Ui(8.5F, false);
            notesBox.BackColor = KavrosTheme.Bg;
            notesBox.ForeColor = KavrosTheme.Text;
            notesHost.Controls.Add(notesBox);
            notesHost.Controls.Add(header);
            Controls.Add(notesHost);
        }

        private void ApplyNotes()
        {
            bool show = notesWanted && !collapsed && notesHost != null;
            if (notesHost != null) notesHost.Visible = show;
            int width = ClientSize.Width < 80 ? 400 : ClientSize.Width;
            int height = collapsed ? 32 : openHeight + (show ? NotesHeight : 0);
            if (ClientSize.Width != width || ClientSize.Height != height)
                ClientSize = new Size(width, height);
            if (!show || notesHost == null) return;
            notesHost.SetBounds(8, openHeight, Math.Max(40, width - 16), NotesHeight);
            notesHost.BringToFront();
        }

        private int openHeight = 360;
        private bool layingOut;

        private void LayoutFoot()
        {
            if (layingOut) return;
            layingOut = true;
            try
            {
                Font font = KavrosTheme.Ui(12F, true);
                Size first = TextRenderer.MeasureText(PanelHint.Line1, font);
                Size second = TextRenderer.MeasureText(PanelHint.Line2, font);
                int lineH = Math.Max(first.Height, second.Height);
                int lineW = Math.Max(first.Width, second.Width);
                int panelW, hintY, hintH, buttonY, panelH;
                PanelHint.Foot(lineH, lineW, out panelW, out hintY, out hintH, out buttonY, out panelH);
                Font rowFont = KavrosTheme.Ui(9F, false);
                int rowLine = Math.Max(16, TextRenderer.MeasureText("Ag", rowFont).Height);
                int rowH = Math.Max(28, rowLine + 8);
                int sacH = rowLine * 2 + 10;
                int idleY = 66;
                int hpY = idleY + rowH + 4;
                int prayerY = hpY + rowH + 4;
                int potionY = prayerY + rowH + 4;
                int sacY = potionY + rowH + 4;
                int afkY = sacY + sacH + 4;
                int stackHint = afkY + rowH + 8;
                if (stackHint > hintY) hintY = stackHint;
                buttonY = hintY + hintH + 16;
                panelH = buttonY + 28 + 16;
                int resetW = Math.Max(64, TextRenderer.MeasureText("Reset", rowFont).Width + 18);
                string sample = "1,470 this session · 6 sacrifices · 12,345/h";
                int sampleW = TextRenderer.MeasureText(sample, rowFont).Width;
                int pillW = TextRenderer.MeasureText("Sacrifice", rowFont).Width + 48;
                panelW = Math.Max(panelW, pillW + sampleW + resetW + 36);
                int textW = panelW - 24;
                note.SetBounds(12, 40, textW, 22);
                Idle.SetBounds(12, idleY, textW, rowH);
                Hp.SetBounds(12, hpY, textW, rowH);
                Prayer.SetBounds(12, prayerY, textW, rowH);
                Potion.SetBounds(12, potionY, textW, rowH);
                Sacrifice.SetBounds(12, sacY, Math.Max(40, textW - resetW - 8), sacH);
                sacrificeReset.SetBounds(12 + textW - resetW, sacY + Math.Max(0, (sacH - rowH) / 2), resetW, rowH);
                Afk.SetBounds(12, afkY, textW, rowH);
                metricsHint.SetBounds(12, hintY, textW, hintH);
                int buttonW = Math.Max(120, (textW - 8) / 2);
                pause.SetBounds(12, buttonY, buttonW, 28);
                exit.SetBounds(12 + buttonW + 8, buttonY, buttonW, 28);
                openHeight = panelH;
                ClientSize = new Size(panelW, collapsed ? 32 : openHeight);
                LayoutHeader();
                ApplyNotes();
            }
            finally { layingOut = false; }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            LayoutFoot();
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
            string heading = PanelDock.PanelHeading(gameName);
            if (heading.Length > 0)
                TextRenderer.DrawText(g, heading, KavrosTheme.Ui(8.5F, false), new Rectangle(128, 0, Math.Max(1, label.Width - 250), label.Height), KavrosTheme.Ice, KavrosTheme.BgRaise, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
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
            Font font = KavrosTheme.Ui(12F, true);
            int lineH = Math.Max(16, label.Height / 2);
            TextRenderer.DrawText(g, PanelHint.Line1, font, new Rectangle(0, 4, label.Width, lineH), KavrosTheme.TextSoft, KavrosTheme.Bg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, PanelHint.Line2, font, new Rectangle(0, 4 + lineH, label.Width, lineH), KavrosTheme.TextSoft, KavrosTheme.Bg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
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
        private KavrosCheckBox hpOn = new KavrosCheckBox();
        private KavrosCheckBox prayerOn = new KavrosCheckBox();
        private KavrosCheckBox afkOn = new KavrosCheckBox();
        private KavrosCheckBox hpPercent = new KavrosCheckBox();
        private KavrosCheckBox prayerPercent = new KavrosCheckBox();
        private KavrosCheckBox soundMaster = new KavrosCheckBox();
        private KavrosCheckBox idleSound = new KavrosCheckBox();
        private KavrosCheckBox hpSound = new KavrosCheckBox();
        private KavrosCheckBox prayerSound = new KavrosCheckBox();
        private KavrosCheckBox afkSound = new KavrosCheckBox();
        private KavrosCheckBox potionOn = new KavrosCheckBox();
        private KavrosCheckBox potionSound = new KavrosCheckBox();
        private KavrosCheckBox updates = new KavrosCheckBox();
        private KavrosCheckBox saveDebug = new KavrosCheckBox();
        private readonly NumberField hpThreshold = new NumberField();
        private readonly NumberField prayerThreshold = new NumberField();
        private KavrosCheckBox grandmaster, treePerk;
        private Label afkDuration;
        private readonly NumberField afkLead = new NumberField();
        private readonly NumberField potionSeconds = new NumberField();
        private readonly NumberField cooldown = new NumberField();
        private readonly VolumeSlider volume = new VolumeSlider();
        private readonly ComboBox soundChoice = new ComboBox();
        private readonly Label status = new Label();
        private readonly Label chatHint = new Label();
        private readonly Label idleFixed = new Label();
        private Label potionUnit;
        private Label afkUnit;
        private Label toneCaption;
        private Label volumeCaption;
        private Label coolCaption;
        private Label coolUnit;
        private OpaqueButton refreshButton;
        private OpaqueButton pickButton;
        private OpaqueButton testSoundButton;
        private OpaqueButton updateButton;
        private OpaqueButton debugButton;
        private OpaqueButton whatsNewButton;
        private Panel releaseNotes;
        private MainSurface settingsBody;
        private readonly WatchPanel panel = new WatchPanel();
        private readonly AlertSound sound = new AlertSound();
        private readonly FrameGrab grab = new FrameGrab();
        private SceneActivity activity = new SceneActivity();
        private DateTime lastDebugSave = DateTime.MinValue;
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
        private int? activityHp;
        private ReadGate prayerGate = new ReadGate();
        private readonly BuffClock buffClock = new BuffClock();
        private readonly ChatPotionBook chatPots = new ChatPotionBook();
        private readonly SacrificeBook sacrifice = new SacrificeBook();
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
        private bool clientClosed;
        private DateTime nextUpdateCheck = DateTime.UtcNow.AddHours(3);
        private HudReading lastHud = new HudReading();

        internal WardenForm()
        {
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden", "settings.txt");
            settings = new WardenSettings();
            clientTitle = "";
            sound.Volume = settings.Volume;
            sound.Tone = settings.SoundTone;
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
            panel.SacrificeReset += delegate { sacrifice.ResetSession(); };
            panel.NotesDismissed += delegate
            {
                settings.SeenNotes = AppInfo.Version;
                Save();
            };
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
            if (!ClientLaunch.NoTray)
            {
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
            }
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
            FormClosed += delegate { closing = true; try { timer.Stop(); } catch (Exception) { } try { Save(); } catch (Exception) { } try { CompanionStatus.Write("stopped", settings.Paused); } catch (Exception) { } try { grab.Stop(); } catch (Exception) { } try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch (Exception) { } try { panel.Dispose(); } catch (Exception) { } };
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
            refreshButton = AddButton("Refresh", 520, 128, 128, ButtonTone.Secondary, delegate { RefreshWindows(); });
            connection.SetBounds(28, 164, 620, 28);
            connection.Renderer = DrawConnection;
            connection.Text = "Waiting for Kavros";
            Controls.Add(connection);
            pause = Check("Pause watching", 28, 200, 160, settings.Paused);
            pickButton = AddButton("Pick client", 210, 196, 120, ButtonTone.Secondary, delegate { picking = true; SetStatus("Click the Kavros window to watch."); });

            idleOn = Check("Idle", 0, 0, 220, settings.IdleEnabled);
            idleFixed.Text = "45 seconds";
            idleFixed.AutoSize = false;
            idleFixed.ForeColor = KavrosTheme.TextFaint;
            idleFixed.BackColor = KavrosTheme.Panel;
            idleFixed.Font = KavrosTheme.Ui(8.5F, false);
            Controls.Add(idleFixed);
            idleSound = Check("Sound", 0, 0, 90, settings.IdleSound);

            hpOn = Check("Low hitpoints", 0, 0, 220, settings.HpEnabled);
            Number(hpThreshold, 0, 0, 1, settings.HpPercent ? 999 : 9999999, settings.HpThreshold);
            hpPercent = Check("Percent", 0, 0, 110, settings.HpPercent);
            hpSound = Check("Sound", 0, 0, 90, settings.HpSound);

            prayerOn = Check("Low prayer", 0, 0, 220, settings.PrayerEnabled);
            Number(prayerThreshold, 0, 0, 1, settings.PrayerPercent ? 999 : 9999999, settings.PrayerThreshold);
            prayerPercent = Check("Percent", 0, 0, 110, settings.PrayerPercent);
            prayerSound = Check("Sound", 0, 0, 90, settings.PrayerSound);

            potionOn = Check("Potion alerts", 0, 0, 220, settings.PotionEnabled);
            Number(potionSeconds, 0, 0, 1, 600, settings.PotionSeconds);
            potionUnit = Caption("sec", 0, 0);
            potionSound = Check("Sound", 0, 0, 90, settings.PotionSound);
            chatHint.Text = "Keep the chatbox visible, with game messages on.";
            chatHint.AutoSize = false;
            chatHint.ForeColor = KavrosTheme.TextFaint;
            chatHint.BackColor = KavrosTheme.Panel;
            chatHint.Font = KavrosTheme.Ui(8F, false);
            Controls.Add(chatHint);

            afkOn = Check("AFK teleport warning", 0, 0, 220, settings.AfkEnabled);
            Number(afkLead, 0, 0, 5, Math.Max(5, settings.AfkSeconds - 5), settings.AfkLeadSeconds);
            afkUnit = Caption("sec", 0, 0);
            afkSound = Check("Sound", 0, 0, 90, settings.AfkSound);
            afkDuration = Caption(settings.AfkSeconds / 60 + " min timer", 0, 0);
            grandmaster = Check("Grandmaster (+25 min)", 0, 0, 210, settings.Grandmaster);
            treePerk = Check("Tree perk (+15 min)", 0, 0, 200, settings.TreePerk);

            soundMaster = Check("Play sounds", 0, 0, 180, settings.SoundMaster);
            toneCaption = Caption("Tone", 0, 0);
            soundChoice.DropDownStyle = ComboBoxStyle.DropDownList;
            string[] tones = AlertTones.Names();
            for (int i = 0; i < tones.Length; i++) soundChoice.Items.Add(tones[i]);
            soundChoice.SelectedIndex = AlertTones.Index(settings.SoundTone);
            KavrosTheme.StyleCombo(soundChoice);
            soundChoice.SelectedIndexChanged += delegate { if (!loading) Save(); };
            Controls.Add(soundChoice);
            testSoundButton = AddButton("Test sound", 0, 0, 140, ButtonTone.Primary, delegate
            {
                sound.Volume = volume.Value;
                sound.Tone = soundChoice.SelectedItem == null ? "By alert" : soundChoice.SelectedItem.ToString();
                sound.Play(sound.Tone == "By alert" ? "hp" : "preview");
            });
            volumeCaption = Caption("Volume", 0, 0);
            volume.SetValue(settings.Volume);
            volume.ValueChanged += delegate { if (!loading) { sound.Volume = volume.Value; Save(); } };
            Controls.Add(volume);
            coolCaption = Caption("Cooldown", 0, 0);
            Number(cooldown, 0, 0, 0, 600, settings.CooldownSeconds);
            coolUnit = Caption("sec", 0, 0);

            updates = Check("Check for updates at startup", 0, 0, 320, settings.CheckForUpdates);
            saveDebug = Check("Save debug capture", 0, 0, 240, settings.SaveDebug);
            updateButton = AddButton("Check for updates", 0, 0, 170, ButtonTone.Secondary, delegate { AppUpdates.Check(this, true, SetStatus); });
            debugButton = AddButton("Debug", 0, 0, 120, ButtonTone.Secondary, delegate { OpenDebugFolder(); });
            whatsNewButton = AddButton("What's new", 0, 0, 140, ButtonTone.Secondary, delegate { ShowReleaseNotes(); });
            status.ForeColor = KavrosTheme.TextFaint;
            status.BackColor = KavrosTheme.Panel;
            status.Font = KavrosTheme.Ui(8F, false);
            status.Text = "";
            Controls.Add(status);

            hpPercent.CheckedChanged += delegate { if (!loading) hpThreshold.Maximum = hpPercent.Checked ? 999 : 9999999; };
            prayerPercent.CheckedChanged += delegate { if (!loading) prayerThreshold.Maximum = prayerPercent.Checked ? 999 : 9999999; };
            Wire(grandmaster, treePerk, pause, idleOn, hpOn, prayerOn, afkOn, potionOn, hpPercent, prayerPercent, soundMaster, idleSound, hpSound, prayerSound, afkSound, potionSound, updates, saveDebug);
            hpThreshold.ValueChanged += delegate { if (!loading) Save(); };
            prayerThreshold.ValueChanged += delegate { if (!loading) Save(); };
            afkLead.ValueChanged += delegate { if (!loading) Save(); };
            potionSeconds.ValueChanged += delegate { if (!loading) Save(); };
            cooldown.ValueChanged += delegate { if (!loading) Save(); };
        }

        private void ApplySettingsToControls()
        {
            pause.Checked = settings.Paused;
            idleOn.Checked = settings.IdleEnabled;
            hpOn.Checked = settings.HpEnabled;
            prayerOn.Checked = settings.PrayerEnabled;
            afkOn.Checked = settings.AfkEnabled;
            hpPercent.Checked = settings.HpPercent;
            prayerPercent.Checked = settings.PrayerPercent;
            soundMaster.Checked = settings.SoundMaster;
            idleSound.Checked = settings.IdleSound;
            hpSound.Checked = settings.HpSound;
            prayerSound.Checked = settings.PrayerSound;
            afkSound.Checked = settings.AfkSound;
            potionOn.Checked = settings.PotionEnabled;
            potionSound.Checked = settings.PotionSound;
            updates.Checked = settings.CheckForUpdates;
            saveDebug.Checked = settings.SaveDebug;
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
            volume.SetValue(settings.Volume);
            soundChoice.SelectedIndex = AlertTones.Index(settings.SoundTone);
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

        private OpaqueButton AddButton(string text, int x, int y, int width, ButtonTone tone, Action click)
        {
            OpaqueButton button = new OpaqueButton();
            button.Text = text;
            button.Tone = tone;
            button.SetBounds(x, y, width, 28);
            button.Font = KavrosTheme.Ui(9F, tone == ButtonTone.Primary);
            button.Click += delegate { click(); };
            Controls.Add(button);
            return button;
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
            settingsBody = body;
            Control[] controls = new Control[Controls.Count];
            Controls.CopyTo(controls, 0);
            Controls.Clear();
            body.Controls.AddRange(controls);
            if (crest != null) body.Controls.Add(crest);
            Controls.Add(body);
            Controls.Add(footer);
            PlaceSettings();
        }

        private int U(int value)
        {
            int dpi = DeviceDpi;
            if (dpi < 96) dpi = 96;
            return value * dpi / 96;
        }

        private int FitWidth(params Control[] controls)
        {
            int width = 0;
            for (int i = 0; i < controls.Length; i++)
            {
                Control control = controls[i];
                if (control == null) continue;
                int text = TextRenderer.MeasureText(control.Text ?? "", control.Font).Width;
                int need = text + U(40);
                if (need > width) width = need;
            }
            return width;
        }

        private void PlaceSettings()
        {
            if (settingsBody == null) return;
            int textH = TextRenderer.MeasureText("Ag", idleOn.Font).Height;
            int boxH = Math.Max(U(24), textH + U(8));
            int row = Math.Max(U(40), boxH + U(12));
            int left = U(28);
            int numW = U(72);
            int nameW = FitWidth(idleOn, hpOn, prayerOn, potionOn, afkOn, updates, saveDebug, soundMaster);
            int percentW = FitWidth(hpPercent);
            int soundW = FitWidth(idleSound);
            int mid = left + nameW + U(20);
            int right = mid + numW + U(12) + percentW + U(16);
            int width = right + soundW + U(36);
            int pair = left + U(26) + FitWidth(grandmaster) + U(16) + FitWidth(treePerk) + U(36);
            int longLine = left + FitWidth(updates) + U(48);
            if (pair > width) width = pair;
            if (longLine > width) width = longLine;
            int notesEnd = left + U(348) + U(150) + U(36);
            if (notesEnd > width) width = notesEnd;
            if (width < U(720)) width = U(720);
            int head = KavrosTheme.CardHeadingHeight() + Math.Max(8, KavrosTheme.CardHeadingHeight() / 5) + U(12);
            int y = U(300);
            int alertTop = y - head;
            idleOn.SetBounds(left, y, FitWidth(idleOn), boxH);
            idleFixed.SetBounds(mid, y + U(2), U(160), boxH);
            idleSound.SetBounds(right, y, soundW, boxH);
            y += row;
            hpOn.SetBounds(left, y, FitWidth(hpOn), boxH);
            hpThreshold.SetBounds(mid, y, numW, U(24));
            hpPercent.SetBounds(mid + numW + U(12), y, percentW, boxH);
            hpSound.SetBounds(right, y, soundW, boxH);
            y += row;
            prayerOn.SetBounds(left, y, FitWidth(prayerOn), boxH);
            prayerThreshold.SetBounds(mid, y, numW, U(24));
            prayerPercent.SetBounds(mid + numW + U(12), y, percentW, boxH);
            prayerSound.SetBounds(right, y, soundW, boxH);
            y += row;
            potionOn.SetBounds(left, y, FitWidth(potionOn), boxH);
            potionSeconds.SetBounds(mid, y, numW, U(24));
            potionUnit.Location = new Point(mid + numW + U(12), y + U(4));
            potionSound.SetBounds(right, y, soundW, boxH);
            y += row;
            int hintW = Math.Max(U(200), width - left - U(48));
            Size hintSize = TextRenderer.MeasureText(chatHint.Text, chatHint.Font, new Size(hintW, U(80)), TextFormatFlags.WordBreak);
            int hintH = Math.Max(U(20), hintSize.Height + U(6));
            chatHint.SetBounds(left + U(26), y, hintW, hintH);
            y += hintH + U(8);
            afkOn.SetBounds(left, y, FitWidth(afkOn), boxH);
            afkLead.SetBounds(mid, y, numW, U(24));
            afkUnit.Location = new Point(mid + numW + U(12), y + U(4));
            afkSound.SetBounds(right, y, soundW, boxH);
            y += row;
            int gmW = FitWidth(grandmaster);
            int treeW = FitWidth(treePerk);
            grandmaster.SetBounds(left + U(26), y, gmW, boxH);
            treePerk.SetBounds(left + U(26) + gmW + U(16), y, treeW, boxH);
            afkDuration.Location = new Point(right, y + U(4));
            int alertBottom = y + boxH + U(12);
            y = alertBottom + head;
            int soundTop = y - head;
            soundMaster.SetBounds(left, y, FitWidth(soundMaster), boxH);
            y += row;
            toneCaption.Location = new Point(left, y + U(4));
            soundChoice.SetBounds(left + U(70), y, U(200), U(28));
            testSoundButton.SetBounds(left + U(286), y, U(150), U(28));
            y += row + U(4);
            volumeCaption.Location = new Point(left, y + U(2));
            volume.SetBounds(left + U(70), y, U(280), U(22));
            y += row;
            coolCaption.Location = new Point(left, y + U(4));
            cooldown.SetBounds(left + U(100), y, numW, U(24));
            coolUnit.Location = new Point(left + U(100) + numW + U(8), y + U(4));
            int soundBottom = y + boxH + U(8);
            y = soundBottom + head;
            int appTop = y - head;
            updates.SetBounds(left, y, FitWidth(updates), boxH);
            y += row;
            saveDebug.SetBounds(left, y, FitWidth(saveDebug), boxH);
            y += row;
            updateButton.SetBounds(left, y, U(190), U(28));
            debugButton.SetBounds(left + U(206), y, U(130), U(28));
            whatsNewButton.SetBounds(left + U(348), y, U(150), U(28));
            y += U(40);
            status.SetBounds(left, y, Math.Max(U(200), width - left - U(36)), U(36));
            int content = y + U(48);
            int cardW = width - U(32);
            int watchTop = Math.Min(U(96), U(128) - head);
            if (watchTop < U(64)) watchTop = U(64);
            settingsBody.Cards.Clear();
            settingsBody.Cards.Add(new MainSurface.Card { Label = "WATCH", Bounds = new Rectangle(U(16), watchTop, cardW, U(240) - watchTop) });
            settingsBody.Cards.Add(new MainSurface.Card { Label = "ALERTS", Bounds = new Rectangle(U(16), alertTop, cardW, alertBottom - alertTop) });
            settingsBody.Cards.Add(new MainSurface.Card { Label = "SOUND", Bounds = new Rectangle(U(16), soundTop, cardW, soundBottom - soundTop) });
            settingsBody.Cards.Add(new MainSurface.Card { Label = "APP", Bounds = new Rectangle(U(16), appTop, cardW, content - appTop) });
            settingsBody.AutoScrollMinSize = new Size(0, content);
            windows.SetBounds(left, U(128), U(470), U(28));
            if (refreshButton != null) refreshButton.SetBounds(left + U(486), U(128), U(128), U(28));
            connection.SetBounds(left, U(164), cardW - U(24), U(28));
            pause.SetBounds(left, U(204), U(170), U(24));
            if (pickButton != null) pickButton.SetBounds(left + U(186), U(200), U(130), U(28));
            ClientSize = new Size(width, Math.Min(U(920), content + U(48)));
            settingsBody.Invalidate();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            PlaceSettings();
        }

        private void Save()
        {
            if (loading) return;
            settings.Paused = pause.Checked;
            settings.Volume = volume.Value;
            settings.SoundTone = soundChoice.SelectedItem == null ? "By alert" : AlertTones.Canonical(soundChoice.SelectedItem.ToString());
            sound.Volume = settings.Volume;
            sound.Tone = settings.SoundTone;
            settings.ClientTitle = clientTitle;
            settings.IdleEnabled = idleOn.Checked;
            settings.IdleSeconds = 45;
            settings.SkillingIdleSeconds = 45;
            settings.SkillingMode = false;
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
            settings.IdleSound = idleSound.Checked;
            settings.HpSound = hpSound.Checked;
            settings.PrayerSound = prayerSound.Checked;
            settings.AfkSound = afkSound.Checked;
            settings.PotionEnabled = potionOn.Checked;
            settings.PotionSeconds = (int)potionSeconds.Value;
            settings.PotionSound = potionSound.Checked;
            settings.CooldownSeconds = (int)cooldown.Value;
            settings.CheckForUpdates = updates.Checked;
            settings.SaveDebug = saveDebug.Checked;
            settings.Clamp();
            try { SettingsText.Save(settingsPath, settings); } catch (IOException) { }
            panel.SetPaused(settings.Paused);
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
        private string lastSacrificeTrace = "";

        private void AppendDebug(string dir, string text)
        {
            string logPath = Path.Combine(dir, DebugLogName());
            File.AppendAllText(logPath, text);
            DebugLog.TrimFile(logPath);
        }

        private void NoteSacrificeTrace()
        {
            if (!settings.SaveDebug) return;
            string trace = sacrifice.Trace ?? "";
            if (trace == lastSacrificeTrace) return;
            lastSacrificeTrace = trace;
            if (trace.Length == 0) return;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                string[] rows = trace.Replace("\r", "").Split('\n');
                System.Text.StringBuilder body = new System.Text.StringBuilder();
                string stamp = DateTime.Now.ToString("HH:mm:ss");
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i].Length == 0) continue;
                    body.Append(stamp).Append(' ').Append(rows[i]).Append("\r\n");
                }
                if (body.Length > 0) AppendDebug(dir, body.ToString());
            }
            catch (Exception) { }
        }

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
                    + " sac=" + (sacrifice.Log ?? "")
                    + " metrics=" + (reading.MetricsFound ? ("xp=" + reading.MetricsXp.ToString()) : ("lost " + (reading.MetricsNote ?? "not found")))
                    + " idleReset=" + (moving ? "yes" : "no")
                    + " " + panelDebug
                    + "\r\n";
                AppendDebug(dir, line);
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

        // The notes stay inside this window. Opening them does not activate
        // anything, so the game keeps focus.
        private void ShowReleaseNotes()
        {
            if (releaseNotes == null)
            {
                releaseNotes = new Panel();
                releaseNotes.BackColor = KavrosTheme.Bg;
                Panel header = new Panel();
                header.Dock = DockStyle.Top;
                header.Height = 36;
                header.BackColor = KavrosTheme.BgRaise;
                Label title = new Label();
                title.Text = "What's new";
                title.Dock = DockStyle.Fill;
                title.TextAlign = ContentAlignment.MiddleLeft;
                title.Padding = new Padding(10, 0, 0, 0);
                title.ForeColor = KavrosTheme.Gold;
                title.BackColor = KavrosTheme.BgRaise;
                title.Font = KavrosTheme.Ui(10F, true);
                OpaqueButton close = new OpaqueButton();
                close.Text = "Close";
                close.Dock = DockStyle.Right;
                close.Width = 88;
                close.Tone = ButtonTone.Secondary;
                close.Click += delegate { releaseNotes.Visible = false; };
                header.Controls.Add(title);
                header.Controls.Add(close);
                TextBox body = new TextBox();
                body.Multiline = true;
                body.ReadOnly = true;
                body.ScrollBars = ScrollBars.Vertical;
                body.Dock = DockStyle.Fill;
                body.BorderStyle = BorderStyle.None;
                body.TabStop = false;
                body.Font = KavrosTheme.Ui(9F, false);
                body.BackColor = KavrosTheme.Bg;
                body.ForeColor = KavrosTheme.Text;
                body.Text = ReleaseNotes.Text();
                releaseNotes.Controls.Add(body);
                releaseNotes.Controls.Add(header);
                Controls.Add(releaseNotes);
            }
            releaseNotes.Bounds = new Rectangle(16, 72, Math.Max(40, ClientSize.Width - 32), Math.Max(40, ClientSize.Height - 110));
            releaseNotes.Visible = true;
            releaseNotes.BringToFront();
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

        private string panelDebug = "bound=0 pid=0 owner=0 panel=hidden";
        private bool panelShownKnown;
        private bool panelWasShown;

        private void FollowPanel()
        {
            bool exists = selected != IntPtr.Zero && NativeMethods.IsWindow(selected);
            IntPtr owner = exists ? selected : IntPtr.Zero;
            if (owner != panelOwner && (exists || panel.IsHandleCreated))
            {
                try { NativeMethods.SetOwner(panel.Handle, owner); panelOwner = owner; }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }
            bool minimized = exists && NativeMethods.IsIconic(selected);
            bool foreground = exists && WindowLocator.IsForeground(selected);
            Rectangle game = exists ? NativeMethods.ClientBounds(selected) : (Screen.PrimaryScreen == null ? new Rectangle(0, 0, 1920, 1080) : Screen.PrimaryScreen.WorkingArea);
            Rectangle desk = Screen.FromRectangle(game).WorkingArea;
            int x, y;
            PanelDock.Place(game.X, game.Y, game.Width, game.Height, panel.Width, panel.Height, settings.PanelOffsetX, settings.PanelOffsetY, desk.X, desk.Y, desk.Width, desk.Height, out x, out y);
            if (exists && panel.IsDragging) ClampDraggedPanel(game, desk);
            bool covered = exists && !panel.IsDragging && OtherClientCovers(x, y, panel.Width, panel.Height);
            bool show = PanelDock.Visible(true, exists, minimized, foreground, panel.ContainsFocus, Visible && ContainsFocus, covered);
            NotePanel(exists, show, minimized, covered);
            if (!show)
            {
                if (panel.Visible) panel.Hide();
                return;
            }
            if (panel.IsDragging)
            {
                if (!panel.Visible) panel.Show();
                KeepPanelOnTop(exists, minimized, foreground);
                return;
            }
            if (panel.Left != x || panel.Top != y) panel.Location = new Point(x, y);
            if (!panel.Visible) { panel.Show(); panelTopKnown = false; }
            KeepPanelOnTop(exists, minimized, foreground);
        }

        private bool clamping;

        private void ClampDraggedPanel(Rectangle game, Rectangle desk)
        {
            if (clamping) return;
            int x, y;
            PanelDock.Place(game.X, game.Y, game.Width, game.Height, panel.Width, panel.Height, panel.Left - game.X, panel.Top - game.Y, desk.X, desk.Y, desk.Width, desk.Height, out x, out y);
            if (panel.Left == x && panel.Top == y) return;
            clamping = true;
            panel.Location = new Point(x, y);
            clamping = false;
        }

        // Windows above the tracked client. Another Kavros client that crosses
        // the panel hides it. The Companion overlay and Clue Helper do not count.
        private bool OtherClientCovers(int x, int y, int width, int height)
        {
            IntPtr cursor = selected;
            for (int n = 0; n < 24; n++)
            {
                cursor = NativeMethods.GetWindow(cursor, 3);
                if (cursor == IntPtr.Zero) return false;
                if (cursor == panel.Handle || cursor == Handle || cursor == selected) continue;
                if (!IsOtherClient(cursor)) continue;
                Rectangle bounds = NativeMethods.ClientBounds(cursor);
                if (PanelDock.Overlaps(x, y, width, height, bounds.X, bounds.Y, bounds.Width, bounds.Height)) return true;
            }
            return false;
        }

        private bool IsOtherClient(IntPtr window)
        {
            if (window == IntPtr.Zero || window == selected) return false;
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].Handle != window) continue;
                return WindowLocator.Score(found[i]) >= 100;
            }
            StringBuilder title = new StringBuilder(256);
            NativeMethods.GetWindowText(window, title, title.Capacity);
            Rectangle bounds = NativeMethods.ClientBounds(window);
            return WindowScore.Score(title.ToString(), "", "", bounds.Width, bounds.Height) >= 100;
        }

        private void NotePanel(bool exists, bool show, bool minimized, bool covered)
        {
            uint pid = 0;
            if (exists) NativeMethods.GetWindowThreadProcessId(selected, out pid);
            string state = show ? "shown" : "hidden";
            string why = !exists ? "no-client" : minimized ? "minimized" : covered ? "covered" : "anchored";
            panelDebug = "bound=" + (exists ? selected.ToInt64().ToString() : "0")
                + " pid=" + pid.ToString()
                + " owner=" + panelOwner.ToInt64().ToString()
                + " panel=" + state;
            if (panelShownKnown && panelWasShown == show) return;
            panelShownKnown = true;
            panelWasShown = show;
            AppendDebug(DateTime.Now.ToString("HH:mm:ss") + " panel visible=" + state + " reason=" + why + " " + panelDebug);
        }

        private void OpenDebugFolder()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception) { SetStatus("The debug log folder could not be opened."); }
        }

        private string DebugLogName()
        {
            uint pid = 0;
            if (selected != IntPtr.Zero && NativeMethods.IsWindow(selected))
                NativeMethods.GetWindowThreadProcessId(selected, out pid);
            if (pid == 0) return "debug-log.txt";
            return "debug-log-" + pid.ToString() + ".txt";
        }

        private void AppendDebug(string line)
        {
            if (settings == null || !settings.SaveDebug) return;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KavrosAfkWarden");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, DebugLogName()), line + "\r\n");
            }
            catch (Exception) { }
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
            GameWindow current = null;
            for (int i = 0; i < found.Count; i++) if (found[i].Handle == selected) current = found[i];
            if (current == null) return clientTitle;
            return WindowLocator.ClientLabel(current, StableIndex(current));
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
            sound.Tone = settings.SoundTone;
            loading=true;
            try { ApplySettingsToControls(); } finally { loading=false; }
            panel.SetPaused(settings.Paused);
            PlacePanel();
            nextUpdateCheck = DateTime.UtcNow.AddHours(3);
            if (settings.CheckForUpdates) AppUpdates.Check(this, false, SetStatus);
            if (ReleaseNotes.Unseen(settings.SeenNotes, AppInfo.Version)) panel.ShowNotes(ReleaseNotes.Text());
            else panel.HideNotes();
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
            try { ClientLaunch.ReadRequest(); } catch (Exception) { }
            raiseUntil = DateTime.UtcNow.AddSeconds(8);
            panelTopKnown = false;
            ShowPanel();
        }

        private void Exit()
        {
            if (closing) return;
            closing = true;
            try { Save(); } catch (Exception) { }
            try { CompanionStatus.Write("stopped", settings.Paused); } catch (Exception) { }
            try { timer.Stop(); timer.Dispose(); } catch (Exception) { }
            try { grab.Stop(); } catch (Exception) { }
            try { if (tray != null) tray.Visible = false; } catch (Exception) { }
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
            for (int i = 0; i < found.Count; i++)
            {
                if (WindowLocator.Score(found[i]) < 100) continue;
                found[i].Label = WindowLocator.ClientLabel(found[i], StableIndex(found[i]));
                windows.Items.Add(found[i]);
            }
            long pick = WindowLocator.ChooseHandle(found, selected, ClientLaunch.Hwnd, ClientLaunch.Pid);
            GameWindow choice = null;
            if (pick > 0)
            {
                for (int i = 0; i < found.Count; i++)
                    if (found[i].Handle.ToInt64() == pick) choice = found[i];
            }
            if (choice != null)
            {
                selected = choice.Handle;
                windows.SelectedItem = choice;
                clientClosed = false;
                clientTitle = choice.Title;
                bool requested = ClientLaunch.Hwnd > 0 && Listed(ClientLaunch.Hwnd);
                if (ClientLaunch.Hwnd == 0 || requested || pick == ClientLaunch.Hwnd) ClientLaunch.Clear();
            }
            loading = false;
            if (choice != null) connection.Text = "GAME DETECTED: " + (choice.Label != null && choice.Label.Length > 0 ? choice.Label : choice.Title);
            else if (pick < 0)
            {
                clientClosed = true;
                connection.Text = "Client closed";
            }
            else connection.Text = "Waiting for Kavros";
            connection.Invalidate();
            if(choice!=null && !everAttached){everAttached=true;BringUp();}
        }

        private int StableIndex(GameWindow window)
        {
            int count = 0;
            for (int i = 0; i < found.Count; i++)
                if (WindowLocator.Score(found[i]) >= 100 && found[i].Pid > 0) count++;
            int[] pids = new int[count];
            int n = 0;
            for (int i = 0; i < found.Count; i++)
                if (WindowLocator.Score(found[i]) >= 100 && found[i].Pid > 0) pids[n++] = found[i].Pid;
            return ClientOrder.Place(window == null ? 0 : window.Pid, pids);
        }

        private bool Listed(long handle)
        {
            for (int i = 0; i < found.Count; i++)
                if (found[i].Handle.ToInt64() == handle) return true;
            return false;
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
                sacrifice.Attach();
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
                observedClient=selected; hpGate=new ReadGate(); prayerGate=new ReadGate(); activityHp=null;
                activity=new SceneActivity(); engine=new WardenEngine(); lastHud=new HudReading();
                potionDisplay=new PotionDisplay(); lastFrame=DateTime.MinValue;
            }
            grab.Tick(selected, settings, !settings.Paused && !panel.IsDragging && !windowDragging && !minimized);
            if (!windowDragging) FollowPanel();
            bool pixels = !settings.Paused && (DateTime.UtcNow-grab.FrameTime).TotalSeconds<=3 && grab.Reading != null && (grab.Reading.StripFound || grab.Reading.BuffBarFound || grab.Reading.Hp != null || grab.Reading.Prayer != null);
            if (grab.Reading != null && pixels) lastHud = grab.Reading;
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
            bool hpDrop = ActivitySignals.HpDrop(activityHp, hp);
            if (hp != null) activityHp = hp;
            int? rawHp = lastHud == null ? (int?)null : lastHud.Hp;
            if (lastHud != null) lastHud.Hp = hp;
            activity.Tick(null, lastHud, false, grab.XpActivity, false);
            if (lastHud != null) lastHud.Hp = rawHp;
            bool moving = hpDrop || grab.XpActivity;
            WriteDebug(pixels, moving);
            WatchSample sample = new WatchSample();
            sample.Time = DateTime.UtcNow;
            sample.GameAttached = selected != IntPtr.Zero && NativeMethods.IsWindow(selected);
            sample.PixelsFresh = pixels;
            sample.CharacterMoving = moving;
            sample.SkillProgress = grab.XpActivity;
            sample.HpDrop = hpDrop;
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
            if (freshFrame) sacrifice.Observe(lastHud.ChatText);
            NoteSacrificeTrace();
            List<AlertEvent> events = engine.Tick(sample, settings);
            for (int n = 0; n < potionNotices.Count; n++)
            {
                if (!settings.PotionEnabled) break;
                AlertEvent extra = new AlertEvent();
                extra.Kind = "potion";
                extra.Title = potionNotices[n].Title;
                extra.Detail = potionNotices[n].Detail;
                extra.Sound = potionNotices[n].Audible && settings.AllowsSound("potion");
                extra.Notify = potionNotices[n].Audible && settings.AllowsNotify("potion");
                sound.Tone = settings.SoundTone;
                events.Add(extra);
            }
            sound.Tone = settings.SoundTone;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Sound) sound.Play(events[i].Kind);
            }
            UpdatePanel(pixels);
            CompanionStatus.Write("running", settings.Paused);
        }

        private void UpdatePanel(bool pixels)
        {
            bool alive = selected != IntPtr.Zero && NativeMethods.IsWindow(selected);
            bool closed = clientClosed || (selected != IntPtr.Zero && !NativeMethods.IsWindow(selected));
            bool minimized = alive && NativeMethods.IsIconic(selected);
            panel.SetGame(alive, closed ? "Client closed" : ClientCaption());
            panel.SetNote(closed ? "Client closed" : grab.Note);
            chatPots.Refresh();
            string roster = chatPots.Detail ?? "";
            bool potionExpired = roster.IndexOf("expired") >= 0;
            bool potionLow = roster.IndexOf("low") >= 0;
            if (settings.Paused)
            {
                panel.Idle.Pill = "Paused";
                panel.Idle.Tone = PillTone.Warn;
                panel.Idle.Value = "Watching off";
            }
            else if (closed)
            {
                panel.Idle.Pill = "Idle";
                panel.Idle.Tone = PillTone.Example;
                panel.Idle.Value = "Client closed";
            }
            else if (minimized)
            {
                panel.Idle.Pill = "Idle";
                panel.Idle.Tone = PillTone.Example;
                panel.Idle.Value = "Minimized";
            }
            else
            {
                bool skillPill = engine.Skilling;
                int still = (int)engine.StillFor.TotalSeconds;
                panel.Idle.Pill = engine.IdleLatched ? "Idle" : skillPill ? "Skilling" : (still > 0 ? "Still" : "Moving");
                panel.Idle.Tone = engine.IdleLatched ? PillTone.Bad : PillTone.Ok;
                panel.Idle.Value = !pixels && still == 0 ? "No view" : still.ToString() + (skillPill ? "s since XP" : "s still");
            }
            panel.Hp.Pill = engine.HpLatched ? "Low HP" : "Hitpoints";
            panel.Hp.Tone = engine.HpLatched ? PillTone.Bad : shownHp == null ? PillTone.Example : PillTone.Ok;
            panel.Hp.Value = closed ? "Client closed" : minimized ? "Minimized" : HpText(pixels);
            panel.Prayer.Pill = engine.PrayerLatched ? "Low prayer" : "Prayer";
            panel.Prayer.Tone = engine.PrayerLatched ? PillTone.Warn : shownPrayer == null ? PillTone.Example : PillTone.Ok;
            panel.Prayer.Value = closed ? "Client closed" : minimized ? "Minimized" : PrayerText(pixels);
            panel.Potion.Pill = potionExpired || potionLow ? "Potion" : "Potions";
            panel.Potion.Tone = potionExpired ? PillTone.Bad : potionLow ? PillTone.Warn : roster.Length == 0 ? PillTone.Example : PillTone.Ok;
            string waiting = (grab.ChatNote != null && grab.ChatNote.Length > 0) ? grab.ChatNote : "Watching chat";
            panel.Potion.Value = closed ? "Client closed" : roster.Length > 0 && !settings.Paused
                ? roster
                : (minimized ? "Minimized" : !pixels ? "No view" : waiting);
            panel.SetMetricsHint(true);
            int left = Math.Max(0, (int)engine.AfkRemaining.TotalSeconds);
            panel.Afk.Pill = engine.AfkLatched ? "AFK soon" : "AFK timer";
            panel.Afk.Tone = engine.AfkLatched ? PillTone.Bad : PillTone.Ok;
            bool clientGone = clientClosed || (selected != IntPtr.Zero && !NativeMethods.IsWindow(selected));
            panel.Afk.Value = settings.Paused ? "Paused" : clientGone ? "Client closed" : selected==IntPtr.Zero ? "Waiting for client" : (left / 60).ToString() + ":" + (left % 60).ToString("00") + " left";
            panel.Idle.Commit();
            panel.Hp.Commit();
            panel.Prayer.Commit();
            panel.Potion.Commit();
            panel.Sacrifice.Pill = "Sacrifice";
            panel.Sacrifice.Tone = PillTone.Example;
            panel.Sacrifice.Dim = true;
            panel.Sacrifice.Value = sacrifice.Value();
            panel.ShowSacrificeReset(sacrifice.Seen);
            panel.Sacrifice.Commit();
            panel.Afk.Commit();
        }

        private string HpText(bool pixels)
        {
            string shown = HudBars.Shown(shownHp, shownHpMax, hpGate.Stale, lastHud != null && lastHud.HpFromOrb);
            if (shown.Length > 0) return shown;
            return pixels ? "Unread" : "No view";
        }

        private string PrayerText(bool pixels)
        {
            string shown = HudBars.Shown(shownPrayer, shownPrayerMax, prayerGate.Stale, lastHud != null && lastHud.PrayerFromOrb);
            if (shown.Length > 0) return shown;
            return pixels ? "Unread" : "No view";
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
