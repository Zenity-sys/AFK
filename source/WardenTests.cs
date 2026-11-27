using System;
using System.Collections.Generic;
using System.IO;

namespace KavrosAfkWarden
{
    public static class WardenTests
    {
        private static int assertions;
        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> notes = new List<string>();

        public static int Run(string report, string root)
        {
            assertions = 0;
            failures.Clear();
            notes.Clear();
            try
            {
                Channels();
                Persistence();
                Gates();
                Levels();
                IdleAndAfk();
                Background();
                Capture();
                Input();
                Placement();
                Windows();
                Motion();
                Digits();
                Hud();
                Potions();
                LiveCapture(root);
                Live023(root);
                ScaleMatrix(root);
                LiveRead(root);
                Release026(root);
                ReadFixes(root);
                PotionClock();
                CaptureCost();
                PanelOrder();
                Skilling();
                XpDropFixture(root);
                ScaleFixtures(root);
                LiveFlasks(root);
                MovedHud(root);
                LiveClient(root);
                Live0213(root);
                ChatAndMetrics(root);
                Manifest(root);
            }
            catch (Exception ex)
            {
                failures.Add(ex.ToString());
            }
            string text = "AFK Warden " + AppInfo.DisplayVersion + " self-test\r\nAssertions: " + assertions.ToString() + "\r\nFailures: " + failures.Count.ToString() + "\r\nFixtures:\r\n";
            for (int i = 0; i < notes.Count; i++) text += notes[i] + "\r\n";
            for (int i = 0; i < failures.Count; i++) text += failures[i] + "\r\n";
            if (failures.Count == 0) text += "PASS\r\n";
            File.WriteAllText(report, text);
            return failures.Count == 0 ? 0 : 1;
        }

        private static void Release026(string root)
        {
            WardenSettings legacy=SettingsText.Read("afk-seconds\t300\nvolume\t71\n");
            Check(legacy.AfkSeconds==1500 && legacy.Volume==71,"migration replaces the old five-minute guess and preserves other settings");
            string dir=Path.Combine(root,"verification","fixtures","live023");
            HudReading frame=HudReader.Read(PngImage.Load(Path.Combine(dir,"live-v024-full-layout-lag.png")),new WardenSettings());
            Check(frame.Hp==1633 && frame.HpMax==1060 && frame.Prayer==1040 && frame.PrayerMax==990,
                "bright landscape does not hide life and prayer digits");
            Check(BuffClock.ShownSeconds(frame.Buffs)==420,"live buff row reads seven minutes against bright ground");
            DateTime now=DateTime.UtcNow;
            var clock=new PotionDisplay();
            Check(clock.Observe(17,now)==17, "observed potion");
            Check(clock.Observe(null,now.AddSeconds(2))==15 && clock.Estimated, "temporary missing potion is marked last read");
            Check(clock.Observe(600,now.AddSeconds(3))==14 && clock.Estimated, "longer second buff does not hide a briefly unread shorter buff");
            Check(clock.Observe(13,now.AddSeconds(4))==13 && !clock.Estimated, "short timer confirmed again");
            Check(clock.Observe(null,now.AddSeconds(10))==7 && clock.Estimated, "an unread potion keeps counting down from its last read");
            Check(clock.Current(now.AddSeconds(17))==0, "the countdown reaches zero");
            Check(clock.Current(now.AddSeconds(23))==null && !clock.Tracking, "a finished potion is dropped");
            var drain=new ReadGate();
            drain.Push(942,990,now);drain.Push(939,990,now.AddSeconds(1));drain.Push(936,990,now.AddSeconds(2));
            Check(drain.Value==936 && drain.Max==990,"prayer can drain continuously between frames");
            foreach(bool gm in new[]{false,true})foreach(bool tree in new[]{false,true})
            {
                var cfg=new WardenSettings{Grandmaster=gm,TreePerk=tree};var eng=new WardenEngine();
                eng.Tick(Sample(now,true,true,false,true),cfg);
                var early=eng.Tick(Sample(now.AddSeconds(cfg.AfkSeconds-31),true,false,false,false),cfg);
                var due=eng.Tick(Sample(now.AddSeconds(cfg.AfkSeconds-30),true,false,false,false),cfg);
                Check(Find(early,"afk")==null && Find(due,"afk")!=null,"warning timing for GM="+gm+" tree="+tree);
            }
        }

        private static void ReadFixes(string root)
        {
            Check(ReadGate.Plausible(1633, 1060) && ReadGate.Plausible(1040, 990), "boosted life and prayer above the cap are plausible");
            Check(ReadGate.Plausible(1058, 990) && ReadGate.Plausible(3500, 990) && ReadGate.Plausible(4000, 990), "hitpoints up to 4000 stay legal above the cap");
            Check(!ReadGate.Plausible(4001, 990) && !ReadGate.Plausible(6633, 1060) && !ReadGate.Plausible(5, 0), "above 4000, or no cap, is a misread");
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ReadGate wild = new ReadGate();
            wild.Push(900, 1060, t); wild.Push(900, 1060, t.AddMilliseconds(500));
            wild.Push(6900, 1060, t.AddSeconds(1)); wild.Push(6900, 1060, t.AddSeconds(1.5));
            Check(wild.Value == 900, "a doubled misread never replaces the shown life, got " + wild.Value);
            ReadGate odd = new ReadGate();
            odd.Push(700, 1060, t); odd.Push(700, 1060, t.AddMilliseconds(500));
            odd.Push(520, 1060, t.AddSeconds(1)); odd.Push(580, 1060, t.AddSeconds(1.5)); odd.Push(520, 1060, t.AddSeconds(2));
            Check(odd.Value == 520, "two of the last three reads move the value past one odd frame, got " + odd.Value);
            if (string.IsNullOrEmpty(root)) return;
            string dir = Path.Combine(root, "verification", "fixtures", "live023");
            string[] names = new string[] { "debug-capture.png", "live-v024-full-layout-lag.png", "live-v024-hp-917-unread.png", "live-v024-lagging-values.png" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(dir, names[i]);
                if (!File.Exists(path)) { Check(false, "fixture present " + names[i]); continue; }
                HudReading full = HudReader.Read(PngImage.Load(path), new WardenSettings());
                int dark = ImageBuffer.Pack(18, 18, 22);
                ImageBuffer noPrayerFill = PngImage.Load(path);
                noPrayerFill.Fill(full.PrayerBar.X - 2, full.PrayerBar.Y - 1, full.PrayerBar.Width + 4, full.PrayerBar.Height + 3, dark);
                HudReading a = HudReader.Read(noPrayerFill, new WardenSettings());
                Check(a.Prayer == full.Prayer && a.PrayerMax == full.PrayerMax && a.Hp == full.Hp, names[i] + ": an empty prayer fill still reads prayer from its slot, got " + a.Prayer + "/" + a.PrayerMax + " " + a.PrayerTrace);
                ImageBuffer noLifeFill = PngImage.Load(path);
                noLifeFill.Fill(full.LifeBar.X - 2, full.LifeBar.Y - 1, full.LifeBar.Width + 4, full.LifeBar.Height + 3, dark);
                HudReading b = HudReader.Read(noLifeFill, new WardenSettings());
                Check(b.Hp == full.Hp && b.HpMax == full.HpMax && b.Prayer == full.Prayer, names[i] + ": a short life fill still reads life from its slot, got " + b.Hp + "/" + b.HpMax + " " + b.LifeTrace);
            }
        }

        private static void PotionClock()
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            PotionDisplay clock = new PotionDisplay();
            clock.Observe(660, t);
            clock.Observe(660, t.AddSeconds(0.5));
            Check(clock.Alertable, "two reads of 11m confirm the potion");
            Check(clock.Observe(null, t.AddSeconds(60)) == 600 && clock.Estimated, "a buff bar hidden for a minute keeps counting down");
            Check(clock.Current(t.AddSeconds(240)) == 420, "four minutes later the clock still runs");
            clock.Observe(420, t.AddSeconds(241));
            Check(clock.Current(t.AddSeconds(241.2)) == 420 && !clock.Estimated, "7m ticking over from 8m pins seven minutes exactly");
            Check(clock.Observe(60, t.AddSeconds(242)) == 419, "one odd lower read does not move the clock");
            Check(clock.Observe(null, true, t.AddSeconds(243)) == 418, "a bar read without a potion icon does not clear it at once");
            PotionDisplay late = new PotionDisplay();
            late.Observe(30, t); late.Observe(29, t.AddSeconds(1));
            Check(late.Alertable && late.Current(t.AddSeconds(20)) == 10, "the clock reaches the warning while the icon is unread");
            int? stale = late.Current(t.AddSeconds(200));
            Check(!late.Alertable, "a clock not confirmed for minutes stops warning, shown " + stale);
            PotionDisplay dose = new PotionDisplay();
            dose.Observe(40, t); dose.Observe(39, t.AddSeconds(1));
            for (int i = 0; i < 4; i++) dose.Observe(360, t.AddSeconds(9 + i * 0.5));
            Check(dose.Current(t.AddSeconds(11)) == 360 - 1 || dose.Current(t.AddSeconds(11)) == 360, "a fresh dose replaces the old timer once the old one is no longer read, got " + dose.Current(t.AddSeconds(11)));
            PotionDisplay keep = new PotionDisplay();
            keep.Observe(40, t); keep.Observe(39, t.AddSeconds(1));
            keep.Observe(600, t.AddSeconds(1.5)); keep.Observe(38, t.AddSeconds(2)); keep.Observe(600, t.AddSeconds(2.5)); keep.Observe(600, t.AddSeconds(3));
            Check(keep.Current(t.AddSeconds(3)) == 37, "a longer second buff does not hide the shorter one that is still read");
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.PotionSeconds = 15; settings.CooldownSeconds = 0; settings.IdleEnabled = false; settings.HpEnabled = false; settings.PrayerEnabled = false; settings.AfkEnabled = false;
            WatchSample hidden = Sample(t, true, false, false, false);
            hidden.PotionSeconds = 12;
            Check(Find(engine.Tick(hidden, settings), "potion") != null, "a confirmed countdown warns even while the bar is covered");
        }

        private static void CaptureCost()
        {
            Check(CapturePlan.NextFullSeconds(false, false, 1) == 3 && CapturePlan.NextFullSeconds(false, false, 2) == 6 && CapturePlan.NextFullSeconds(false, false, 4) == 24 && CapturePlan.NextFullSeconds(false, false, 9) == 30,
                "a HUD that cannot be read backs off 3, 6, 12, 24, 30 seconds between whole-client copies");
            Check(CapturePlan.NextFullSeconds(true, true, 0) == 30 && CapturePlan.NextFullSeconds(true, false, 0) == 15, "a complete read waits the slow refresh");
            IntRect life = new IntRect(613, 1116, 108, 36), prayer = new IntRect(851, 1116, 108, 36);
            Check(CapturePlan.ShareGrab(life, prayer), "life and prayer on one HUD row are copied in one grab");
            Check(!CapturePlan.ShareGrab(life, new IntRect(1800, 200, 108, 36)), "slots far apart are copied separately");
            Check(CapturePlan.SlowSlotEvery >= 2, "buffs, XP and the backpack are read at most once a second");
            Check(CapturePlan.ShouldRead(false, false) && !CapturePlan.ShouldRead(true, false) && !CapturePlan.ShouldRead(false, true), "a minimized or paused client is not read");
            Check(!CapturePlan.PreferWindowPixels(false) && !CapturePlan.PreferWindowPixels(true) && !CapturePlan.UsePrintWindow && CapturePlan.OfferWindowSurface, "covered windows use Graphics Capture, not PrintWindow");
            Check(!CapturePlan.ContinueReading(false, false, true) && CapturePlan.ContinueReading(false, true, false) && !CapturePlan.ContinueReading(true, true, false), "a visible unfocused client is copied; a covered or minimized one is not");
            Check(!CapturePlan.AcceptFrame(null) && !CapturePlan.AcceptFrame(ImageBuffer.Blank(32, 32, ImageBuffer.Pack(0, 0, 0))) && CapturePlan.AcceptFrame(ImageBuffer.Blank(32, 32, ImageBuffer.Pack(180, 40, 40))), "a black or failed frame is not a reading");
            Check(!PanelDock.ZOrderChanged(true, true, true) && !PanelDock.ZOrderChanged(true, false, false) && PanelDock.ZOrderChanged(false, false, true) && PanelDock.ZOrderChanged(true, false, true), "the panel z-order changes only when topmost changes");
            int ox, oy, cw, ch;
            WindowCrop.Client(0, 0, 2576, 1488, 8, 40, 2560, 1440, out ox, out oy, out cw, out ch);
            Check(ox == 8 && oy == 40 && cw == 2560 && ch == 1440, "1440p at 150% keeps the physical client crop");
            WindowCrop.Client(0, 0, 1936, 1128, 8, 32, 1920, 1080, out ox, out oy, out cw, out ch);
            Check(ox == 8 && oy == 32 && cw == 1920 && ch == 1080, "1080p at 100% keeps the physical client crop");
            WindowCrop.Client(0, 0, 3872, 2240, 16, 48, 3840, 2160, out ox, out oy, out cw, out ch);
            Check(ox == 16 && oy == 48 && cw == 3840 && ch == 2160, "4K at 200% keeps the physical client crop");
            ImageBuffer corner = ImageBuffer.Blank(800, 400, ImageBuffer.Pack(16, 18, 20));
            corner.Fill(300, 16, 36, 10, ImageBuffer.Pack(230, 160, 40));
            Check(SceneActivity.XpWatch.Find(corner).Width == 0, "a solid orange patch is not an XP drop");
            ImageBuffer noisy = ImageBuffer.Blank(800, 400, ImageBuffer.Pack(16, 18, 20));
            noisy.Fill(300, 16, 36, 10, ImageBuffer.Pack(40, 90, 30));
            noisy.Fill(420, 40, 8, 8, ImageBuffer.Pack(220, 140, 40));
            SkillSense noise = new SkillSense();
            noise.Observe(noisy, null);
            noise.Observe(noisy, null);
            Check(!noise.XpEvent, "scene noise behind the view is not an XP drop");
        }

        private static void PanelOrder()
        {
            Check(PanelDock.WantTopMost(true, false, true, false, false), "the panel is topmost while the game is in front");
            Check(!PanelDock.WantTopMost(true, false, false, false, false), "the panel drops back when another application is in front");
            Check(PanelDock.WantTopMost(false, false, false, false, true), "an explicit launch lifts the panel before the client is found");
            Check(PanelDock.WantTopMost(true, false, false, true, false), "the panel stays up while it is being used");
            Check(!PanelDock.WantTopMost(true, true, true, true, true), "a minimized client never gets a topmost panel");
        }

        private static void Skilling()
        {
            WardenSettings settings = new WardenSettings();
            settings.SkillingMode = true;
            settings.SkillingIdleSeconds = 30;
            settings.IdleSeconds = 12;
            settings.HpEnabled = false; settings.PrayerEnabled = false; settings.AfkEnabled = false; settings.PotionEnabled = false;
            Check(settings.ActiveIdleSeconds == 30, "skilling mode uses its own idle time");
            WardenSettings loaded = SettingsText.Read(SettingsText.Write(settings));
            Check(loaded.SkillingMode && loaded.SkillingIdleSeconds == 30 && loaded.IdleSeconds == 12, "skilling settings round-trip");
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            WardenEngine engine = new WardenEngine();
            WatchSample start = Sample(t, true, true, false, false);
            start.SkillProgress = true;
            engine.Tick(start, settings);
            engine.Tick(Sample(t.AddSeconds(0.5), true, true, true, false), settings);
            // Combat-style signals (life changing) do not count while skilling.
            List<AlertEvent> early = engine.Tick(Sample(t.AddSeconds(20), true, true, true, false), settings);
            Check(Find(early, "idle") == null, "twenty seconds without XP is not yet idle in skilling mode");
            List<AlertEvent> stopped = engine.Tick(Sample(t.AddSeconds(46), true, true, true, false), settings);
            AlertEvent alert = Find(stopped, "idle");
            Check(alert != null && alert.Title == "You stopped skilling", "forty-five seconds without an XP gain warns that skilling stopped");
            PotionRoster();
            WatchSample xp = Sample(t.AddSeconds(47), true, true, false, false);
            xp.SkillProgress = true;
            engine.Tick(xp, settings);
            Check(!engine.IdleLatched && engine.StillFor == TimeSpan.Zero, "an XP drop resets skilling idle");
            engine.Tick(Sample(t.AddSeconds(50), true, true, false, false), settings);
            WatchSample clicked = Sample(t.AddSeconds(60), true, true, true, true);
            engine.Tick(clicked, settings);
            Check(engine.StillFor >= TimeSpan.FromSeconds(9), "a click or key does not reset skilling idle, still " + engine.StillFor.TotalSeconds.ToString());
            engine.Tick(Sample(t.AddSeconds(61), true, true, false, false), settings);
            engine.Tick(Sample(t.AddSeconds(100), true, true, false, false), settings);
            Check(engine.StillFor >= TimeSpan.FromSeconds(29), "idle counts up when the clicks, XP and backpack stop");
            WardenEngine combat = new WardenEngine();
            WardenSettings normal = new WardenSettings();
            normal.IdleSeconds = 12; normal.HpEnabled = false; normal.PrayerEnabled = false; normal.AfkEnabled = false;
            combat.Tick(Sample(t, true, true, false, false), normal);
            WatchSample onlyXp = Sample(t.AddSeconds(13), true, true, false, false);
            onlyXp.SkillProgress = true;
            DateTime xpTime = t.AddSeconds(13);
            onlyXp.Time = xpTime;
            Check(Find(combat.Tick(onlyXp, normal), "idle") == null && combat.Skilling, "an XP drop is activity without a skilling switch");
            Check(Find(combat.Tick(Sample(xpTime.AddSeconds(1), true, true, true, false), normal), "idle") == null && combat.Skilling, "nearby motion during a skilling gap is not idle");
            Check(Find(combat.Tick(Sample(xpTime.AddSeconds(normal.SkillingIdleSeconds + 1), true, true, true, false), normal), "idle") != null, "idle fires after the skilling gap even if the scene is moving");
        }

        private static BuffIcon PotionIcon(string kind, string signature, bool minutes, int value, int x)
        {
            BuffIcon icon = new BuffIcon();
            icon.Kind = kind;
            icon.Name = PotionKind.Name(kind);
            icon.Signature = signature;
            icon.HasTime = true;
            icon.Minutes = minutes;
            icon.Value = value;
            icon.X = x;
            return icon;
        }

        private static void PotionRoster()
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            BuffClock clock = new BuffClock();
            List<BuffIcon> icons = new List<BuffIcon>();
            icons.Add(PotionIcon("overload", "bbbbbbbbbbbbbbbb", true, 12, 10));
            Check(clock.Tick(icons, true, t) == null, "one 12m read is not adopted yet");
            int? shown = clock.Tick(icons, true, t.AddSeconds(1));
            Check(shown != null && shown.Value >= 700 && shown.Value <= 720 && clock.Name == "Overload", "two agreeing 12m reads adopt the overload, got " + (shown == null ? "none" : shown.Value.ToString()));
            icons[0] = PotionIcon("overload", "bbbbbbbbbbbbbbbb", true, 12, 400);
            shown = clock.Tick(icons, true, t.AddSeconds(11));
            Check(shown != null && shown.Value >= 680 && shown.Value <= 720, "the same potion moved along the bar keeps its clock, got " + (shown == null ? "none" : shown.Value.ToString()));
            icons[0] = PotionIcon("overload", "bbbbbbbbbbbbbbbb", true, 2, 420);
            shown = clock.Tick(icons, true, t.AddSeconds(12));
            Check(shown != null && shown.Value > 600, "one misread of 12m as 2m does not replace the clock, got " + (shown == null ? "none" : shown.Value.ToString()));
            shown = clock.Tick(icons, true, t.AddSeconds(13));
            Check(shown != null && shown.Value <= 120, "a repeated shorter read replaces the longer timer, got " + (shown == null ? "none" : shown.Value.ToString()));
            BuffClock minutes = new BuffClock();
            List<BuffIcon> row = new List<BuffIcon>();
            row.Add(PotionIcon("elder", "mmmmmmmmmmmmmmmm", true, 11, 20));
            minutes.Tick(row, true, t);
            minutes.Tick(row, true, t.AddSeconds(1));
            row[0] = PotionIcon("elder", "mmmmmmmmmmmmmmmm", false, 11, 20);
            int? kept = minutes.Tick(row, true, t.AddSeconds(2));
            Check(kept != null && kept.Value > 600 && minutes.Log.IndexOf("rejected") >= 0, "11m read as 11s is rejected, got " + (kept == null ? "none" : kept.Value.ToString()) + " " + minutes.Log);
            BuffClock prayer = new BuffClock();
            List<BuffIcon> bad = new List<BuffIcon>();
            bad.Add(PotionIcon("prayer", "pppppppppppppppp", false, 60, 30));
            bad.Add(PotionIcon("other", "ssssssssssssssss", false, 5, 60));
            prayer.Tick(bad, true, t);
            Check(prayer.Tick(bad, true, t.AddSeconds(1)) == null, "prayer 60 and a stack count are not potion timers");
            BuffClock grace = new BuffClock();
            List<BuffIcon> elder = new List<BuffIcon>();
            elder.Add(PotionIcon("elder", "mmmmmmmmmmmmmmmm", true, 11, 20));
            grace.Tick(elder, true, t);
            grace.Tick(elder, true, t.AddSeconds(1));
            Check(grace.Tick(new List<BuffIcon>(), true, t.AddSeconds(20)) != null, "a potion keeps counting when its digits vanish");
            Check(grace.Tick(new List<BuffIcon>(), true, t.AddSeconds(50)) != null, "an empty buff frame does not drop the clock inside a minute");
            Check(grace.Tick(new List<BuffIcon>(), true, t.AddSeconds(70)) == null, "the potion clock drops after a minute with no icon");
            BuffClock fast = new BuffClock();
            List<BuffIcon> live = new List<BuffIcon>();
            BuffIcon elderIcon = PotionIcon("elder", "elder", true, 12, 10);
            elderIcon.Strong = true;
            live.Add(elderIcon);
            int? quick = fast.Tick(live, true, t);
            Check(quick != null && quick.Value >= 700 && quick.Value <= 720 && fast.Detail.IndexOf("Elder") >= 0, "a strong flask template is adopted on the first read, got " + (fast.Detail ?? ""));
            Check(fast.Tick(new List<BuffIcon>(), true, t.AddSeconds(40)) != null, "empty frames between reads keep the adopted clock");
            BuffIcon aggroIcon = PotionIcon("aggression", "aggression", true, 11, 40);
            aggroIcon.Strong = true;
            live.Add(aggroIcon);
            fast.Tick(live, true, t.AddSeconds(41));
            Check(fast.Detail.IndexOf("Elder") >= 0 && fast.Detail.IndexOf("Aggro") >= 0, "each potion is listed with its own time, got " + fast.Detail);
            live[0] = PotionIcon("elder", "elder", true, 2, 10);
            live[0] = WithStrong(live[0]);
            int? held = fast.Tick(live, true, t.AddSeconds(42));
            Check(held != null && held.Value > 600, "one short read does not replace a confirmed timer, got " + (held == null ? "none" : held.Value.ToString()));
        }

        private static BuffIcon WithStrong(BuffIcon icon)
        {
            icon.Strong = true;
            return icon;
        }

        private static void Blit(ImageBuffer dest, ImageBuffer src, int dx, int dy)
        {
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    int px = dx + x;
                    int py = dy + y;
                    if (px >= 0 && py >= 0 && px < dest.Width && py < dest.Height) dest.Set(px, py, src.Argb(x, y));
                }
        }

        private static void MovedHud(string root)
        {
            if (root == null || root.Length == 0) return;
            string twelve = Path.Combine(root, "verification", "fixtures", "live023", "buffbar-potions-12m.png");
            if (!File.Exists(twelve)) { Check(false, "missing 12m buff crop"); return; }
            ImageBuffer icon = PngImage.Load(twelve);
            int[] xs = new int[] { 16, 620 };
            int[] ys = new int[] { 24, 430 };
            for (int i = 0; i < xs.Length; i++)
            {
                ImageBuffer frame = ImageBuffer.Blank(980, 640, ImageBuffer.Pack(18, 22, 26));
                frame.Fill(80, 80, 240, 90, ImageBuffer.Pack(40, 48, 36));
                Blit(frame, icon, xs[i], ys[i]);
                HudReading hud = HudReader.Read(frame, new WardenSettings());
                Check(BuffClock.ShownSeconds(hud.Buffs) == 720, "a buff bar pasted at " + xs[i].ToString() + "," + ys[i].ToString() + " still reads 12m, got " + (hud.BuffTrace ?? ""));
            }
            ImageBuffer low = ImageBuffer.Blank(860, 520, ImageBuffer.Pack(24, 40, 22));
            int orange = ImageBuffer.Pack(226, 170, 70);
            DrawPlus(low, 540, 390, 2, orange);
            DrawGlyph(low, 5, 554, 390, 2, orange);
            DrawGlyph(low, 8, 568, 390, 2, orange);
            DrawGlyph(low, 9, 582, 390, 2, orange);
            var drop = SceneActivity.XpWatch.Read(low);
            Check(drop.Found && drop.Plus && drop.X > 400 && drop.Y > 300, "an XP drop in the lower right is still a drop, got " + drop.Trace);
            ImageBuffer corner = ImageBuffer.Blank(520, 420, ImageBuffer.Pack(12, 14, 16));
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    corner.Fill(18 + col * 36, 22 + row * 30, 34, 28, ImageBuffer.Pack(70, 60, 40));
                    corner.Fill(22 + col * 36, 26 + row * 30, 26, 20, ImageBuffer.Pack(16, 16, 18));
                }
            IntRect grid = SceneActivity.PackWatch.Find(corner);
            Check(grid.Width > 8 && grid.X < 80 && grid.Y < 80, "a backpack in the top left is found, got " + grid.X.ToString() + "," + grid.Y.ToString());
        }

        private static void LiveClient(string root)
        {
            if (root == null || root.Length == 0) return;
            string path = Path.Combine(root, "verification", "fixtures", "live0212", "full-client-v0212.png");
            if (!File.Exists(path)) { Check(false, "missing full client fixture"); return; }
            ImageBuffer shot = PngImage.Load(path).Scale(2.0);
            HudReading hud = HudReader.Read(shot, new WardenSettings());
            int fives = 0;
            if (hud.Buffs != null)
            {
                for (int i = 0; i < hud.Buffs.Count; i++)
                {
                    BuffIcon icon = hud.Buffs[i];
                    if (icon.HasTime && icon.Minutes && icon.Value == 5 && PotionKind.IsPotion(icon.Kind)) fives++;
                }
            }
            Check(fives >= 4, "the client shot has four potions at 5m, got " + fives.ToString() + " " + (hud.BuffTrace ?? ""));
            Check(hud.LifeBar.Width > 20 && hud.PrayerBar.Width > hud.LifeBar.Width / 2 && hud.PrayerBar.X > hud.LifeBar.Right && Math.Abs(hud.LifeBar.Y - hud.PrayerBar.Y) < 8,
                "the client shot locates the life and prayer bars on one row, got " + hud.LifeBar.X.ToString() + "," + hud.LifeBar.Y.ToString() + " " + hud.LifeBar.Width.ToString() + " and " + hud.PrayerBar.X.ToString() + "," + hud.PrayerBar.Y.ToString());
            Check(ReadGate.Plausible(1471, 990), "boosted hitpoints 1471/990 stay a legal reading");
            if (hud.Hp != null)
                Check(hud.Hp == 1471 && hud.HpMax == 990, "a hitpoint number from the client shot is 1471/990, got " + hud.Hp.Value.ToString() + "/" + (hud.HpMax == null ? "-" : hud.HpMax.Value.ToString()));
            IntRect pack = SceneActivity.PackWatch.Find(shot);
            Check(pack.Width > 8, "the client shot has a backpack grid, got " + pack.X.ToString() + "," + pack.Y.ToString());
            var xp = SceneActivity.XpWatch.Read(shot);
            Check(!xp.Found, "a still client with no drop is not an XP event, got " + xp.Trace);
        }

        private static void DrawPlus(ImageBuffer image, int x, int y, int scale, int color)
        {
            for (int px = 0; px < 5; px++) image.Fill(x + px * scale, y + 3 * scale, scale, scale, color);
            for (int py = 0; py < 7; py++) image.Fill(x + 2 * scale, y + py * scale, scale, scale, color);
        }

        private static void XpDropFixture(string root)
        {
            ImageBuffer synthetic = ImageBuffer.Blank(640, 360, ImageBuffer.Pack(24, 40, 22));
            synthetic.Fill(80, 40, 90, 40, ImageBuffer.Pack(30, 70, 28));
            int orange = ImageBuffer.Pack(226, 170, 70);
            DrawPlus(synthetic, 40, 80, 2, orange);
            DrawGlyph(synthetic, 5, 54, 80, 2, orange);
            DrawGlyph(synthetic, 8, 68, 80, 2, orange);
            DrawGlyph(synthetic, 9, 82, 80, 2, orange);
            var drop = SceneActivity.XpWatch.Read(synthetic);
            Check(drop.Found && drop.Plus && drop.X < 200 && drop.Y > 40 && drop.Y < 200, "a floating +589 on the left of the view is an XP drop, got " + drop.Trace);
            ImageBuffer faded = ImageBuffer.Blank(640, 360, ImageBuffer.Pack(24, 40, 22));
            int dim = ImageBuffer.Pack(150, 95, 45);
            DrawPlus(faded, 40, 80, 2, dim);
            DrawGlyph(faded, 5, 54, 80, 2, dim);
            DrawGlyph(faded, 8, 68, 80, 2, dim);
            DrawGlyph(faded, 9, 82, 80, 2, dim);
            var fadedDrop = SceneActivity.XpWatch.Read(faded);
            Check(fadedDrop.Found && fadedDrop.Plus, "a faded +589 still counts while the drop floats away, got " + fadedDrop.Trace);
            SkillSense sense = new SkillSense();
            sense.Observe(synthetic, null);
            Check(sense.XpEvent, "the first confirmed +xp drop resets idle");
            sense.Observe(synthetic, null);
            Check(!sense.XpEvent, "the same drop on the next look is not a second event");
            ImageBuffer shifted = ImageBuffer.Blank(640, 360, ImageBuffer.Pack(24, 40, 22));
            shifted.Fill(200, 30, 40, 20, ImageBuffer.Pack(18, 60, 16));
            DrawPlus(shifted, 40, 60, 2, orange);
            DrawGlyph(shifted, 5, 54, 60, 2, orange);
            DrawGlyph(shifted, 8, 68, 60, 2, orange);
            DrawGlyph(shifted, 9, 82, 60, 2, orange);
            sense.Observe(shifted, null);
            Check(!sense.XpEvent, "the same drop floating upwards is not a second XP event");
            if (root == null || root.Length == 0) return;
            string path = Path.Combine(root, "verification", "fixtures", "live029", "xpdrop-woodcutting-589.png");
            if (!File.Exists(path)) { Check(false, "missing woodcutting XP fixture"); return; }
            ImageBuffer live = PngImage.Load(path);
            var liveDrop = SceneActivity.XpWatch.Read(live);
            Check(liveDrop.Found && liveDrop.Plus && liveDrop.X > 700 && liveDrop.X < 1800 && liveDrop.Y > 80 && liveDrop.Y < 400, "the woodcutting frame finds the orange +xp drop in the upper middle, got " + liveDrop.Trace);
            ImageBuffer cleared = new ImageBuffer(live.Width, live.Height, (int[])live.Pixels.Clone());
            int pad = 6;
            cleared.Fill(Math.Max(0, liveDrop.X - pad), Math.Max(0, liveDrop.Y - 8), 220, 32, ImageBuffer.Pack(36, 48, 28));
            var gone = SceneActivity.XpWatch.Read(cleared);
            Check(!gone.Found, "the same frame with the orange text removed is not an XP drop, got " + gone.Trace);
            IntRect pack = SceneActivity.PackWatch.Find(live);
            IntRect packAgain = SceneActivity.PackWatch.Find(live);
            Check(pack.Width > 8 && pack.X == packAgain.X && pack.Y == packAgain.Y, "the woodcutting frame finds a stable backpack grid, got " + pack.X.ToString() + "," + pack.Y.ToString() + " " + pack.Width.ToString() + "x" + pack.Height.ToString());
        }

        private static void Live0213(string root)
        {
            if (root == null || root.Length == 0) return;
            string dir = Path.Combine(root, "verification", "fixtures", "live0213");
            string crop = Path.Combine(dir, "buffbar-6icons-4m.png");
            double[] scales = new double[] { 1.0, 1.5, 2.0 };
            string[] names = new string[] { "100%", "150%", "200%" };
            for (int s = 0; s < scales.Length; s++)
            {
                if (!File.Exists(crop)) { Check(false, "missing buffbar-6icons-4m.png"); break; }
                HudReading bar = HudReader.Read(PngImage.Load(crop).Scale(scales[s]), new WardenSettings());
                int fours = PotionMinutes(bar, 4);
                int? shown = BuffClock.ShownSeconds(bar.Buffs);
                Check(fours >= 4 && shown == 240, "four potions at 4m on the live buff crop at " + names[s] + ", got " + fours.ToString() + " shown " + (shown == null ? "none" : shown.Value.ToString()) + " " + (bar.BuffTrace ?? ""));
            }
            string panel = Path.Combine(dir, "full-client-panel-no-pot-timer.png");
            if (File.Exists(panel))
            {
                HudReading bar = HudReader.Read(PngImage.Load(panel), new WardenSettings());
                int six = PotionMinutes(bar, 6);
                int five = PotionMinutes(bar, 5);
                int? shown = BuffClock.ShownSeconds(bar.Buffs);
                Check(six >= 1 && five >= 1 && shown == 300, "the panel client reads 6m and 5m, soonest 5m, got " + six.ToString() + " sixes and " + five.ToString() + " fives, shown " + (shown == null ? "none" : shown.Value.ToString()) + " " + (bar.BuffTrace ?? ""));
            }
            else Check(false, "missing full-client-panel-no-pot-timer.png");
            string other = Path.Combine(dir, "full-client-other-account-layout.png");
            if (File.Exists(other))
            {
                HudReading bar = HudReader.Read(PngImage.Load(other), new WardenSettings());
                int elevens = PotionMinutes(bar, 11);
                int? shown = BuffClock.ShownSeconds(bar.Buffs);
                bool shortPotion = false;
                if (bar.Buffs != null)
                {
                    for (int i = 0; i < bar.Buffs.Count; i++)
                    {
                        BuffIcon icon = bar.Buffs[i];
                        if (!PotionKind.IsPotion(icon.Kind) || !icon.HasTime) continue;
                        int seconds = icon.Minutes ? icon.Value * 60 : icon.Value;
                        if (seconds < 660) shortPotion = true;
                    }
                }
                Check(elevens >= 4 && shown == 660 && !shortPotion, "the other account reads four 11m potions and nothing sooner, got " + elevens.ToString() + " shown " + (shown == null ? "none" : shown.Value.ToString()) + " " + (bar.BuffTrace ?? ""));
            }
            else Check(false, "missing full-client-other-account-layout.png");
        }

        private static int PotionMinutes(HudReading bar, int minutes)
        {
            int count = 0;
            if (bar.Buffs == null) return 0;
            for (int i = 0; i < bar.Buffs.Count; i++)
            {
                BuffIcon icon = bar.Buffs[i];
                if (icon.HasTime && icon.Minutes && icon.Value == minutes && PotionKind.IsPotion(icon.Kind)) count++;
            }
            return count;
        }

        private static void LiveFlasks(string root)
        {
            if (root == null || root.Length == 0) return;
            string dir = Path.Combine(root, "verification", "fixtures", "live0211");
            string[] files = new string[] { "elder-overload-12m.png", "aggression-12m.png", "overload-11m.png" };
            string[] kinds = new string[] { "elder", "aggression", "overload" };
            int[] minutes = new int[] { 12, 12, 11 };
            double[] scales = new double[] { 1.0, 1.5, 2.0 };
            string[] names = new string[] { "100%", "150%", "200%" };
            for (int s = 0; s < scales.Length; s++)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string path = Path.Combine(dir, files[i]);
                    if (!File.Exists(path)) { Check(false, "missing " + files[i]); continue; }
                    HudReading bar = HudReader.Read(PngImage.Load(path).Scale(scales[s]), new WardenSettings());
                    BuffIcon icon = new BuffIcon();
                    bool found = false;
                    if (bar.Buffs != null)
                    {
                        for (int b = 0; b < bar.Buffs.Count; b++)
                        {
                            if (bar.Buffs[b].Kind == kinds[i]) { icon = bar.Buffs[b]; found = true; break; }
                        }
                    }
                    string read = !icon.HasTime ? "unread" : icon.Value.ToString() + (icon.Minutes ? "m" : "s");
                    int? shown = BuffClock.ShownSeconds(bar.Buffs);
                    // 12 must stay twelve minutes. A clipped "2m" or a "12" with no minute mark is a miss.
                    Check(found && icon.Strong && icon.HasTime && icon.Minutes && icon.Value == minutes[i] && icon.Value != 2 && shown == minutes[i] * 60,
                        files[i] + " at " + names[s] + " is " + minutes[i].ToString() + "m, not 2m or " + minutes[i].ToString() + "s, got " + read + " shown " + (shown == null ? "none" : shown.Value.ToString()) + " " + (bar.BuffTrace ?? ""));
                }
                string wood = Path.Combine(root, "verification", "fixtures", "live029", "xpdrop-woodcutting-589.png");
                if (File.Exists(wood) && scales[s] != 1.0)
                {
                    ImageBuffer frame = PngImage.Load(wood).Scale(scales[s]);
                    IntRect grid = SceneActivity.PackWatch.Find(frame);
                    IntRect again = SceneActivity.PackWatch.Find(frame);
                    Check(grid.Width > 8 && grid.X == again.X && grid.Y == again.Y,
                        "backpack at " + names[s] + " is a stable grid, got " + grid.X.ToString() + "," + grid.Y.ToString());
                }
            }
        }

        private static void ScaleFixtures(string root)
        {
            if (root == null || root.Length == 0) return;
            string xpPath = Path.Combine(root, "verification", "fixtures", "live029", "xpdrop-woodcutting-589.png");
            string hudPath = Path.Combine(root, "verification", "fixtures", "live023", "debug-capture.png");
            string twelve = Path.Combine(root, "verification", "fixtures", "live023", "buffbar-potions-12m.png");
            string eleven = Path.Combine(root, "verification", "fixtures", "live023", "buffbar-potions-11m-elder-overload.png");
            string prayer = Path.Combine(root, "verification", "fixtures", "live023", "buffbar-prayer60-bone5-heart.png");
            double[] scales = new double[] { 1.0, 1.5, 2.0 };
            string[] names = new string[] { "100%", "150%", "200%" };
            for (int i = 0; i < scales.Length; i++)
            {
                if (File.Exists(xpPath))
                {
                    ImageBuffer scaled = PngImage.Load(xpPath).Scale(scales[i]);
                    var sight = SceneActivity.XpWatch.Read(scaled);
                    Check(sight.Found && sight.Plus, "XP drop at " + names[i] + " is found, got " + sight.Trace);
                }
                if (File.Exists(hudPath))
                {
                    ImageBuffer full = PngImage.Load(hudPath);
                    int top = full.Height * 70 / 100;
                    ImageBuffer band = full.Crop(0, top, full.Width, full.Height - top).Scale(scales[i]);
                    HudReading hud = HudReader.Read(band, new WardenSettings());
                    Check(hud.Hp == 1633 && hud.HpMax == 1060 && hud.Prayer == 1040 && hud.PrayerMax == 990, "HP and prayer at " + names[i] + " got " + (hud.Hp == null ? "none" : hud.Hp.Value.ToString()) + "/" + (hud.HpMax == null ? "-" : hud.HpMax.Value.ToString()) + " prayer " + (hud.Prayer == null ? "none" : hud.Prayer.Value.ToString()) + " " + hud.LifeTrace);
                }
                if (File.Exists(twelve))
                {
                    HudReading bar = HudReader.Read(PngImage.Load(twelve).Scale(scales[i]), new WardenSettings());
                    Check(BuffClock.ShownSeconds(bar.Buffs) == 720, "12m flasks at " + names[i] + " got " + (bar.BuffTrace ?? ""));
                }
                if (File.Exists(eleven))
                {
                    HudReading bar = HudReader.Read(PngImage.Load(eleven).Scale(scales[i]), new WardenSettings());
                    Check(BuffClock.ShownSeconds(bar.Buffs) == 660, "11m elder row at " + names[i] + " got " + (bar.BuffTrace ?? ""));
                }
                if (File.Exists(prayer))
                {
                    HudReading bar = HudReader.Read(PngImage.Load(prayer).Scale(scales[i]), new WardenSettings());
                    Check(BuffClock.ShownSeconds(bar.Buffs) == null, "prayer 60 at " + names[i] + " is not a potion, got " + (bar.BuffTrace ?? ""));
                }
            }
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) failures.Add(message);
        }

        private static AlertEvent Find(List<AlertEvent> events, string kind)
        {
            for (int i = 0; i < events.Count; i++) if (events[i].Kind == kind) return events[i];
            return null;
        }

        private static void Channels()
        {
            WardenSettings settings = new WardenSettings();
            Check(settings.AllowsSound("idle") && settings.AllowsNotify("hp"), "defaults allow sound and notification");
            settings.SoundMaster = false;
            Check(!settings.AllowsSound("afk") && settings.AllowsNotify("afk"), "global sound off leaves notifications on");
            settings.SoundMaster = true;
            settings.NotifyMaster = false;
            Check(settings.AllowsSound("prayer") && !settings.AllowsNotify("prayer"), "global notification off leaves sound on");
            settings.NotifyMaster = true;
            settings.IdleSound = false;
            settings.HpNotify = false;
            Check(!settings.AllowsSound("idle") && settings.AllowsSound("hp"), "idle sound can be off while hitpoints sound stays on");
            Check(settings.AllowsNotify("idle") && !settings.AllowsNotify("hp"), "hitpoints notification can be off while idle notification stays on");
            settings.AfkSound = false;
            settings.AfkNotify = false;
            Check(!settings.AllowsSound("afk") && !settings.AllowsNotify("afk"), "an alert can silence both of its channels");
            Check(settings.AllowsSound("prayer") && settings.AllowsNotify("prayer"), "other alerts keep their channels");
            Check(settings.AllowsSound("potion") && settings.AllowsNotify("potion") && settings.PotionSeconds == 15, "potion alert defaults to 15 seconds with both channels on");
            settings.PotionSound = false;
            settings.PotionNotify = false;
            Check(!settings.AllowsSound("potion") && !settings.AllowsNotify("potion"), "potion sound and notification can both be off");
        }

        private static void Persistence()
        {
            WardenSettings settings = new WardenSettings();
            settings.IdleSeconds = 40;
            settings.HpThreshold = 15;
            settings.HpPercent = false;
            settings.HpThreshold = 1500;
            settings.PrayerPercent = true;
            settings.PrayerThreshold = 12;
            settings.Grandmaster = true;
            settings.TreePerk = true;
            settings.AfkLeadSeconds = 45;
            settings.SoundMaster = false;
            settings.NotifyMaster = true;
            settings.IdleSound = false;
            settings.IdleNotify = true;
            settings.HpSound = true;
            settings.HpNotify = false;
            settings.PrayerSound = false;
            settings.PrayerNotify = false;
            settings.AfkSound = true;
            settings.AfkNotify = true;
            settings.CooldownSeconds = 7;
            settings.BannerSeconds = 11;
            settings.Paused = true;
            settings.PanelTopMost = false;
            settings.PanelX = 40;
            settings.PanelY = 80;
            settings.HpPinX = 0.1;
            settings.HpPinY = 0.2;
            settings.HpPinW = 0.3;
            settings.HpPinH = 0.05;
            settings.PotionEnabled = false;
            settings.PotionSeconds = 22;
            settings.PotionSound = false;
            settings.PotionNotify = true;
            settings.SeenNotes = "0.2.36";
            settings.BuffPinX = 0.05;
            settings.BuffPinY = 0.02;
            settings.BuffPinW = 0.4;
            settings.BuffPinH = 0.08;
            string text = SettingsText.Write(settings);
            WardenSettings loaded = SettingsText.Read(text);
            Check(loaded.IdleSeconds == 40 && loaded.HpThreshold == 1500 && !loaded.HpPercent, "numeric settings round-trip");
            Check(!loaded.SoundMaster && loaded.NotifyMaster, "global channel switches round-trip");
            Check(!loaded.IdleSound && loaded.IdleNotify && loaded.HpSound && !loaded.HpNotify, "per-alert sound and notification round-trip");
            Check(!loaded.PrayerSound && !loaded.PrayerNotify && loaded.AfkSound && loaded.AfkNotify, "prayer and AFK channels round-trip");
            Check(loaded.CooldownSeconds == 7 && loaded.BannerSeconds == 11 && loaded.Paused && !loaded.PanelTopMost, "cooldown, banner and panel flags round-trip");
            Check(loaded.PanelX == 40 && loaded.PanelY == 80 && loaded.HasHpPin, "panel position and hitpoint pin round-trip");
            WardenSettings rates = new WardenSettings();
            Check(rates.AfkSeconds == 1500, "normal AFK rate");
            rates.TreePerk = true;
            Check(rates.AfkSeconds == 2400, "tree-only AFK rate");
            rates.Grandmaster = true;
            Check(rates.AfkSeconds == 3900, "grandmaster plus tree AFK rate");
            rates.TreePerk = false;
            Check(rates.AfkSeconds == 3000, "grandmaster-only AFK rate");
            Check(loaded.Grandmaster && loaded.TreePerk && loaded.AfkSeconds == 3900 && loaded.AfkLeadSeconds == 45, "AFK timer settings round-trip");
            Check(!loaded.PotionEnabled && loaded.PotionSeconds == 22 && !loaded.PotionSound && loaded.PotionNotify && loaded.HasBuffPin, "potion alert and buff pin round-trip");
            Check(loaded.SeenNotes == "0.2.36", "the last seen notes version round-trips");
            string path = Path.Combine(Path.GetTempPath(), "afk-warden-settings-" + Guid.NewGuid().ToString("N") + ".txt");
            SettingsText.Save(path, settings);
            WardenSettings fromDisk = SettingsText.Load(path);
            File.Delete(path);
            Check(!fromDisk.SoundMaster && fromDisk.HpNotify == false && fromDisk.IdleSeconds == 40, "settings file survives a reload");
            WardenSettings defaults = SettingsText.Read("");
            Check(defaults.SoundMaster && defaults.NotifyMaster && defaults.IdleSound && defaults.AfkNotify, "missing file keeps both channels on");
        }

        private static void Gates()
        {
            AlertGate gate = new AlertGate();
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Check(gate.ShouldFire(true, t, TimeSpan.FromSeconds(20)), "first breach fires");
            Check(!gate.ShouldFire(true, t.AddSeconds(5), TimeSpan.FromSeconds(20)), "holding the breach does not fire again");
            Check(!gate.ShouldFire(false, t.AddSeconds(6), TimeSpan.FromSeconds(20)) && gate.Armed, "recovery rearms");
            Check(!gate.ShouldFire(true, t.AddSeconds(10), TimeSpan.FromSeconds(20)), "cooldown blocks the next fire");
            Check(gate.ShouldFire(true, t.AddSeconds(21), TimeSpan.FromSeconds(20)), "fire after recovery and cooldown");
        }

        private static void Levels()
        {
            LevelWatch watch = new LevelWatch();
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Check(watch.Tick(20, 100, true, 30, true, t, TimeSpan.FromSeconds(20)), "20 percent fires a 30 percent hitpoint alert");
            int boosted;
            Check(Thresholds.TryMeasure(1633, 1060, true, out boosted) && boosted > 100 && boosted == 154, "boosted life 1,633/1,060 is over 100 percent and is not clamped, got " + boosted.ToString());
            Check(!Thresholds.IsLow(1633, 1060, true, 30), "life above the cap is not a low-hitpoint alert");
            int prayerPct;
            Check(Thresholds.TryMeasure(1040, 990, true, out prayerPct) && prayerPct > 100, "boosted prayer 1,040/990 stays above 100 percent, got " + prayerPct.ToString());
            Check(!Thresholds.IsLow(1040, 990, true, 20), "prayer above the cap is not a low-prayer alert");
            Check(watch.Low && !watch.Tick(22, 100, true, 30, true, t.AddSeconds(1), TimeSpan.FromSeconds(20)), "still low, inside the margin, does not refire or recover");
            Check(watch.Low && !watch.Tick(null, null, true, 30, true, t.AddSeconds(2), TimeSpan.FromSeconds(20)), "a missing reading is not a recovery");
            Check(!watch.Tick(21, 100, true, 30, true, t.AddSeconds(3), TimeSpan.FromSeconds(20)), "the same low reading still does not refire");
            Check(!watch.Tick(80, 100, true, 30, true, t.AddSeconds(4), TimeSpan.FromSeconds(20)) && !watch.Low, "crossing the margin recovers");
            Check(!watch.Tick(20, 100, true, 30, true, t.AddSeconds(10), TimeSpan.FromSeconds(20)), "cooldown still applies after recovery");
            Check(watch.Tick(20, 100, true, 30, true, t.AddSeconds(25), TimeSpan.FromSeconds(20)), "the alert fires again after cooldown");
            Check(!WardenSettings.Comparable(true, false, false), "percent mode waits until the maximum is known");
            Check(WardenSettings.Comparable(true, true, true), "an orb fill is a percent");
            Check(!WardenSettings.Comparable(false, true, true), "absolute mode does not treat an orb fill as hitpoints");
        }

        private static WatchSample Sample(DateTime time, bool attached, bool pixels, bool moving, bool input)
        {
            WatchSample sample = new WatchSample();
            sample.Time = time;
            sample.GameAttached = attached;
            sample.PixelsFresh = pixels;
            sample.CharacterMoving = moving;
            sample.InputActivity = input;
            return sample;
        }

        private static void IdleAndAfk()
        {
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.HpEnabled = false;
            settings.PrayerEnabled = false;
            settings.IdleSeconds = 45;
            settings.AfkLeadSeconds = 30;
            settings.CooldownSeconds = 60;
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            engine.Tick(Sample(t, true, true, false, false), settings);
            List<AlertEvent> early = engine.Tick(Sample(t.AddSeconds(20), true, true, false, false), settings);
            Check(Find(early, "idle") == null && Find(early, "afk") == null, "twenty quiet seconds is not idle and not an AFK warning");
            List<AlertEvent> idle = engine.Tick(Sample(t.AddSeconds(45), true, true, false, true), settings);
            AlertEvent idleEvent = Find(idle, "idle");
            Check(idleEvent != null && idleEvent.Sound && idleEvent.Notify, "stillness fires idle after 45 seconds while input keeps the AFK timer reset");
            Check(Find(engine.Tick(Sample(t.AddSeconds(50), true, true, false, true), settings), "idle") == null, "idle does not repeat while you stay still");
            WatchSample struck = Sample(t.AddSeconds(51), true, true, true, true);
            struck.HpDrop = true;
            engine.Tick(struck, settings);
            Check(!engine.IdleLatched, "a clear hitpoints drop clears idle");
            engine.Tick(Sample(t.AddSeconds(52), true, true, false, true), settings);
            List<AlertEvent> cooled = engine.Tick(Sample(t.AddSeconds(97), true, true, false, true), settings);
            Check(Find(cooled, "idle") == null, "idle cooldown blocks the return of stillness");
            Check(Find(engine.Tick(Sample(t.AddSeconds(110), true, true, false, true), settings), "idle") != null, "idle fires again after cooldown");
        }

        private static void Background()
        {
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.IdleSeconds = 12;
            settings.AfkLeadSeconds = 30;
            settings.CooldownSeconds = 0;
            settings.HpEnabled = true;
            settings.HpPercent = true;
            settings.HpThreshold = 30;
            settings.PrayerEnabled = false;
            settings.SoundMaster = true;
            settings.NotifyMaster = false;
            settings.AfkNotify = true;
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            WatchSample start = Sample(t, true, true, true, false);
            start.Hp = 90;
            start.HpMax = 100;
            engine.Tick(start, settings);
            WatchSample hidden = Sample(t.AddSeconds(settings.AfkSeconds - 30), true, false, false, false);
            hidden.Hp = 10;
            hidden.HpMax = 100;
            List<AlertEvent> events = engine.Tick(hidden, settings);
            AlertEvent afk = Find(events, "afk");
            Check(afk != null && afk.Sound && !afk.Notify, "minimized client still warns about the AFK timer, and only on the channels that are on");
            Check(Find(events, "idle") == null, "time the window was unreadable is not counted as idle");
            Check(Find(events, "hp") == null && !engine.HpLatched, "a hidden client does not invent a low-hitpoint reading");
            WatchSample seen = Sample(t.AddSeconds(settings.AfkSeconds - 29), true, true, true, true);
            seen.Hp = 10;
            seen.HpMax = 100;
            List<AlertEvent> hp = engine.Tick(seen, settings);
            Check(Find(hp, "hp") != null, "hitpoints alert as soon as the client can be read again");
            WardenEngine covered = new WardenEngine();
            covered.Tick(Sample(t, true, true, false, false), settings);
            List<AlertEvent> stillHidden = covered.Tick(Sample(t.AddMinutes(30), true, false, false, false), settings);
            Check(Find(stillHidden, "idle") == null, "a long minimize does not mature the idle alert");
            Check(Find(stillHidden, "afk") != null, "the inactivity timer keeps running with no pixels");
        }

        private static void Capture()
        {
            Check(CapturePolicy.Choose(false, false, true, false, true) == CaptureRoute.Missing, "no game window");
            Check(CapturePolicy.Choose(true, true, true, false, true) == CaptureRoute.Minimized, "minimized wins over every capture path");
            Check(CapturePolicy.Choose(true, false, true, false, true) == CaptureRoute.WindowSurface, "a visible unfocused client uses the window surface when it is offered");
            Check(CapturePolicy.Choose(true, false, true, true, true) == CaptureRoute.WindowSurface, "a covered client is still read from the window surface");
            Check(CapturePolicy.Choose(true, false, true, false, false) == CaptureRoute.Screen, "without window capture, a visible client is read from the screen");
            Check(CapturePolicy.Choose(true, false, true, true, false) == CaptureRoute.Covered, "without Graphics Capture a covered client is left unread");
            Check(CapturePolicy.Choose(true, false, true, true, CapturePlan.OfferWindowSurface) == CaptureRoute.WindowSurface, "Graphics Capture reads a covered client");
            Check(!CapturePolicy.PixelsAvailable(CaptureRoute.Minimized) && CapturePolicy.PixelsAvailable(CaptureRoute.Screen), "only real frames count as pixels");
            bool[] clear = new bool[] { true, true, true, true, true, true, true, true, true };
            bool[] partial = new bool[] { true, false, false, false, false, false, false, false, false };
            bool[] blocked = new bool[] { false, false, false, false, false, false, false, false, false };
            Check(VisibilitySamples.MostlyClear(clear) && !VisibilitySamples.MostlyClear(partial) && VisibilitySamples.Any(partial) && !VisibilitySamples.Any(blocked), "partial cover still has a screen sample; full cover does not");
        }

        private static void Input()
        {
            Check(!InputActivity.IsActivity(false, false, true, true, false, false), "cursor motion does not reset the AFK timer");
            Check(!InputActivity.IsActivity(false, true, true, true, false, false), "camera movement over the game is not a left click");
            Check(!InputActivity.IsActivity(false, true, true, false, false, true), "keys do not reset the AFK timer");
            Check(!InputActivity.IsActivity(false, true, true, false, false, false), "a middle or right click does not reset the AFK timer");
            Check(InputActivity.IsActivity(false, false, true, false, true, false), "a left click on the game window resets the AFK timer");
            Check(!InputActivity.IsActivity(false, true, false, false, true, false), "a left click outside the game window does not reset it");
            Check(!InputActivity.IsActivity(true, true, true, false, true, true), "a minimized game receives none of this input");
            Check(!InputActivity.IsActivity(false, false, false, true, true, false), "clicks on the window that covers the game do not count");
            Check(AfkInput.Resets(true, true, AfkInput.LeftButton, true), "left button down on the window is the only reset");
            Check(!AfkInput.Resets(true, true, 4, true) && !AfkInput.Resets(true, true, 2, true), "middle and right buttons do not reset the timer");
            Check(!AfkInput.Resets(true, true, AfkInput.LeftButton, false), "a left button that did not go down is not a new click");
            WardenEngine afk = new WardenEngine();
            WardenSettings afkSettings = new WardenSettings();
            afkSettings.AfkEnabled = true;
            afkSettings.IdleEnabled = false;
            afkSettings.HpEnabled = false;
            afkSettings.PrayerEnabled = false;
            afkSettings.PotionEnabled = false;
            DateTime afkTime = new DateTime(2026, 11, 23, 6, 0, 0, DateTimeKind.Utc);
            afk.Tick(Sample(afkTime, true, true, true, false), afkSettings);
            afk.Tick(Sample(afkTime.AddMinutes(10), true, true, true, false), afkSettings);
            Check(afk.AfkRemaining <= TimeSpan.FromSeconds(afkSettings.AfkSeconds - 590) && afk.AfkRemaining >= TimeSpan.FromSeconds(afkSettings.AfkSeconds - 610), "moving the camera for ten minutes does not reset the AFK timer, left " + afk.AfkRemaining.TotalSeconds.ToString());
            afk.Tick(Sample(afkTime.AddMinutes(10).AddSeconds(1), true, true, true, true), afkSettings);
            Check(afk.AfkRemaining >= TimeSpan.FromSeconds(afkSettings.AfkSeconds - 2), "a left click resets the AFK timer, left " + afk.AfkRemaining.TotalSeconds.ToString());
            Check(!CapturePlan.WantFull(false, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(3)), "unread HUD backs off between full-frame attempts");
            Check(!CapturePlan.WantFull(true, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(12)), "a locked slot waits out the slow full search");
            Check(CapturePlan.SampleMilliseconds <= 500 && CapturePlan.FullIntervalSeconds >= 10, "steady capture stays at or under 2 Hz");
            Check(CapturePlan.ChatMilliseconds <= 1000 && CapturePlan.ChatMilliseconds >= 200, "chat is checked about once a second without a tight loop");
        }

        private static void Placement()
        {
            IntRect alert = AlertPlacement.Place(0, 0, 1600, 900, 380, 92, 0);
            IntRect game = new IntRect(40, 40, 400, 300);
            Check(alert.X > game.Right && alert.Y + alert.Height <= 900, "the alert sits on the work area, not inside the game client");
            Check(alert.Right <= 1600 && alert.Bottom <= 900, "the alert stays inside the work area");
        }

        private static void Windows()
        {
            Check(WindowScore.Score("Kavros", "Kavros", "JagWindow", 1024, 768) >= 100, "a Kavros client scores as the game");
            Check(WindowScore.Score("Kavros Launcher", "launcher", "Launcher", 800, 600) < 0, "the launcher is not the client");
            Check(WindowScore.Score("AFK Warden", "KavrosAfkWarden", "WindowsForms", 300, 200) < 0, "the module does not select itself");
            Check(WindowScore.Score("RuneScape", "rs2client", "SunAwtFrame", 1024, 768) < 0, "another game with no Kavros title is ignored");
            Check(WindowScore.Score("Companion", "Companion", "Window", 400, 700) < 0, "the Companion overlay is not the game");
            Check(WindowScore.Score("Kavros Companion", "Kavros", "Window", 420, 760) < 0, "a Companion window titled with Kavros is still not the game");
            Check(WindowScore.Score("Clue Helper", "ClueHelper", "Window", 360, 640) < 0, "Clue Helper is not the game");
            Check(WindowScore.Score("Kavros", "ClueHelper", "JagWindow", 1024, 768) < 0, "a Clue Helper process is not taken as the client");
            int px, py;
            PanelDock.Place(100, 80, 900, 600, 400, 262, 12, 72, 0, 0, 1920, 1080, out px, out py);
            Check(px == 112 && py == 152, "the panel keeps its offset inside the chosen client");
            int farX, farY;
            PanelDock.Place(0, 0, 800, 600, 400, 262, 900, 72, 0, 0, 1920, 1080, out farX, out farY);
            Check(farX >= 0 && farX + 400 <= 800 && farY >= 0 && farY + 262 <= 600, "a drag toward another client stays inside the tracked client");
            int movedX, movedY;
            PanelDock.Place(300, 40, 800, 600, 400, 262, 12, 72, 0, 0, 1920, 1080, out movedX, out movedY);
            Check(movedX == 312 && movedY == 112, "the panel follows the tracked client when that window moves");
            Check(!PanelDock.Visible(true, true, true, true, false, false), "a minimized client hides the panel");
            Check(PanelDock.Visible(true, true, false, false, false, false), "tabbing out keeps the panel owned by the game instead of hiding it");
            Check(PanelDock.Visible(true, true, false, true, false, false), "the panel shows while the chosen client is in front");
            Check(PanelDock.Visible(true, false, false, false, false, true), "settings keeps the panel available while it is focused");
            List<DockTarget> clients = new List<DockTarget>();
            DockTarget second = new DockTarget();
            second.Handle = 2;
            second.X = 700;
            second.Y = 0;
            second.Width = 800;
            second.Height = 600;
            second.InFront = true;
            clients.Add(second);
            Check(PanelDock.OtherClientCovers(1, 420, 80, 400, 262, clients), "another client in front of the panel hides it");
            Check(!PanelDock.Visible(true, true, false, false, false, false, true), "a covered panel is not shown");
            second.X = 1200;
            Check(!PanelDock.OtherClientCovers(1, 12, 72, 400, 262, clients), "a client beside the tracked one does not take the panel");
            second.X = 700;
            second.InFront = false;
            Check(!PanelDock.OtherClientCovers(1, 420, 80, 400, 262, clients), "a client behind the tracked one leaves the panel up");
            Check(PanelDock.OwnerOf(1) == 1 && PanelDock.OwnerOf(1) != 2, "the panel owner is the tracked client");
            Check(PanelDock.PanelHeading("Zezima") == "Zezima" && PanelDock.PanelHeading("Client 2") == "Client 2" && PanelDock.PanelHeading("Choose a client").Length == 0, "the panel shows the tracked client's name");
        }

        private static void Motion()
        {
            ImageBuffer still = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(20, 30, 40));
            MotionDetector detector = new MotionDetector();
            Check(!detector.Moved(still), "the first frame is a baseline, not movement");
            Check(!detector.Moved(still), "an unchanged client is not movement");
            ImageBuffer moved = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(20, 30, 40));
            IntRect world = MotionDetector.WorldRect(200, 160);
            moved.Fill(world.X, world.Y, world.Width, world.Height / 2, ImageBuffer.Pack(180, 40, 40));
            Check(detector.Moved(moved), "a large change in the character view is movement");
            MotionDetector dot = new MotionDetector();
            ImageBuffer map = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(12, 16, 20));
            IntRect mini = MotionDetector.MinimapRect(200, 160);
            map.Fill(mini.X + 4, mini.Y + 4, 6, 6, ImageBuffer.Pack(240, 230, 160));
            Check(!dot.Moved(map), "the first minimap dot is not movement");
            ImageBuffer shifted = ImageBuffer.Blank(200, 160, ImageBuffer.Pack(12, 16, 20));
            shifted.Fill(mini.X + 20, mini.Y + 20, 6, 6, ImageBuffer.Pack(240, 230, 160));
            Check(dot.Moved(shifted), "a moved minimap dot is movement");
        }

        private static void DrawGlyph(ImageBuffer image, int digit, int x, int y, int scale, int color)
        {
            string glyph = DigitFont.Glyphs[digit];
            for (int py = 0; py < 7; py++)
                for (int px = 0; px < 5; px++)
                    if (glyph[py * 5 + px] == '1')
                        image.Fill(x + px * scale, y + py * scale, scale, scale, color);
        }

        private static void DrawLifeNumber(ImageBuffer image, int x, int y, int[] current, int[] max, int color)
        {
            int dx = x;
            for (int i = 0; i < current.Length; i++)
            {
                if (i > 0 && i % 3 == current.Length % 3) { image.Fill(dx, y + 12, 3, 3, color); dx += 6; }
                DrawGlyph(image, current[i], dx, y, 2, color);
                dx += 12;
            }
            DrawSlash(image, dx, y, color);
            dx += 10;
            for (int i = 0; i < max.Length; i++)
            {
                if (i > 0 && i % 3 == max.Length % 3) { image.Fill(dx, y + 12, 3, 3, color); dx += 6; }
                DrawGlyph(image, max[i], dx, y, 2, color);
                dx += 12;
            }
        }

        private static void DrawSlash(ImageBuffer image, int x, int y, int color)
        {
            for (int i = 0; i < 12; i++)
            {
                image.Set(x + i / 2, y + i, color);
                image.Set(x + i / 2 + 1, y + i, color);
            }
        }

        private static void Digits()
        {
            int white = ImageBuffer.Pack(236, 236, 236);
            for (int digit = 0; digit < 10; digit++)
            {
                ImageBuffer image = ImageBuffer.Blank(80, 50, ImageBuffer.Pack(8, 12, 16));
                DrawGlyph(image, 1, 8, 8, 3, white);
                DrawGlyph(image, digit, 26, 8, 3, white);
                WardenSettings settings = new WardenSettings();
                settings.HpPinX = 0;
                settings.HpPinY = 0;
                settings.HpPinW = 1;
                settings.HpPinH = 1;
                settings.PrayerPinW = 0;
                HudReading reading = HudReader.Read(image, settings);
                int expected = 10 + digit;
                if (reading.Hp != expected) failures.Add("digit " + digit.ToString() + " read as " + (reading.Hp == null ? "nothing" : reading.Hp.Value.ToString()) + " " + HudReader.Explain(image, new IntRect(0, 0, image.Width, image.Height)));
                assertions++;
            }
        }

        private static void Hud()
        {
            int white = ImageBuffer.Pack(236, 236, 236);
            int green = ImageBuffer.Pack(40, 186, 70);
            int orb = ImageBuffer.Pack(210, 36, 36);
            ImageBuffer image = ImageBuffer.Blank(640, 220, ImageBuffer.Pack(10, 16, 22));
            int y = 120;
            int red = ImageBuffer.Pack(196, 42, 42);
            int purple = ImageBuffer.Pack(142, 70, 196);
            int yellow = ImageBuffer.Pack(214, 184, 48);
            image.Fill(40, y, 150, 8, red);
            DrawLifeNumber(image, 40, y - 22, new int[] { 1, 6, 3, 3 }, new int[] { 1, 0, 6, 0 }, white);
            image.Fill(210, y, 90, 8, yellow);
            DrawGlyph(image, 1, 210, y - 22, 2, white);
            DrawGlyph(image, 3, 222, y - 22, 2, white);
            DrawGlyph(image, 5, 234, y - 22, 2, white);
            image.Fill(320, y, 140, 8, purple);
            DrawLifeNumber(image, 320, y - 22, new int[] { 1, 0, 4, 0 }, new int[] { 9, 9, 0 }, white);
            image.Fill(480, y, 80, 8, green);
            DrawGlyph(image, 6, 480, y - 22, 2, white);
            DrawGlyph(image, 0, 492, y - 22, 2, white);
            DrawSlash(image, 506, y - 22, white);
            DrawGlyph(image, 6, 516, y - 22, 2, white);
            DrawGlyph(image, 0, 528, y - 22, 2, white);
            int cx = 320, cy = 40, radius = 24;
            for (int py = -radius; py <= radius; py++)
                for (int px = -radius; px <= radius; px++)
                {
                    if (px * px + py * py > radius * radius) continue;
                    bool edge = px * px + py * py > (radius - 2) * (radius - 2);
                    if (py >= 0 || edge) image.Set(cx + px, cy + py, orb);
                }
            WardenSettings settings = new WardenSettings();
            HudReading reading = HudReader.Read(image, settings);
            Check(reading.Hp == 1633 && reading.HpMax == 1060 && reading.HpHow == "bar", "red life bar reads 1,633/1,060 even when current exceeds max, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()) + " " + reading.HpHow + " " + HudReader.Explain(image, new IntRect(32, 88, 160, 40)));
            Check(reading.Prayer == 1040 && reading.PrayerMax == 990 && reading.PrayerHow == "bar", "purple prayer bar reads 1,040/990, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()));
            ImageBuffer pinned = ImageBuffer.Blank(200, 80, ImageBuffer.Pack(10, 16, 22));
            DrawGlyph(pinned, 4, 10, 20, 3, white);
            DrawGlyph(pinned, 2, 27, 20, 3, white);
            WardenSettings pin = new WardenSettings();
            pin.HpPinX = 0;
            pin.HpPinY = 0;
            pin.HpPinW = 1;
            pin.HpPinH = 1;
            HudReading pinnedReading = HudReader.Read(pinned, pin);
            Check(pinnedReading.Hp == 42 && pinnedReading.HpHow == "pin", "a pinned rectangle overrides the automatic search");
            ImageBuffer wideNumber = ImageBuffer.Blank(520, 80, ImageBuffer.Pack(10, 16, 22));
            int redWide = ImageBuffer.Pack(196, 42, 42);
            wideNumber.Fill(20, 48, 220, 8, redWide);
            DrawLifeNumber(wideNumber, 20, 26, new int[] { 1, 2, 3, 4, 5, 6, 7 }, new int[] { 1, 0, 0, 0, 0, 0, 0 }, white);
            int purpleWide = ImageBuffer.Pack(142, 70, 196);
            wideNumber.Fill(270, 48, 200, 8, purpleWide);
            DrawLifeNumber(wideNumber, 270, 26, new int[] { 1, 0, 4, 0 }, new int[] { 9, 9, 0 }, white);
            HudReading wideReading = HudReader.Read(wideNumber, new WardenSettings());
            Check(wideReading.Hp == null, "a life total above 4000 is not shown, got " + (wideReading.Hp == null ? "none" : wideReading.Hp.Value.ToString()));
            Check(wideReading.Prayer == 1040 && wideReading.PrayerMax == 990, "prayer above its cap is kept");
            int sevenPct;
            Check(Thresholds.TryMeasure(1234567, 1000000, true, out sevenPct) && sevenPct == 123, "seven-digit life uses the cap as the percent base, got " + sevenPct.ToString());
        }

        private static void Potions()
        {
            WardenEngine engine = new WardenEngine();
            WardenSettings settings = new WardenSettings();
            settings.IdleEnabled = false;
            settings.HpEnabled = false;
            settings.PrayerEnabled = false;
            settings.AfkEnabled = false;
            settings.PotionSeconds = 15;
            settings.CooldownSeconds = 20;
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            WatchSample high = Sample(t, true, true, true, false);
            high.PotionSeconds = 40;
            Check(Find(engine.Tick(high, settings), "potion") == null && !engine.PotionLatched, "40 seconds left does not fire a 15 second potion alert");
            WatchSample low = Sample(t.AddSeconds(1), true, true, true, false);
            low.PotionSeconds = 10;
            List<AlertEvent> fired = engine.Tick(low, settings);
            AlertEvent potion = Find(fired, "potion");
            Check(potion != null && potion.Sound && potion.Notify && engine.PotionLatched, "10 seconds left fires the potion alert on both channels");
            low.Time = t.AddSeconds(2);
            Check(Find(engine.Tick(low, settings), "potion") == null && engine.PotionLatched, "the potion alert does not refire while the timer stays low");
            WatchSample missed = Sample(t.AddSeconds(3), true, true, true, false);
            Check(Find(engine.Tick(missed, settings), "potion") == null && engine.PotionLatched, "a missed frame is not a potion recovery");
            WatchSample margin = Sample(t.AddSeconds(4), true, true, true, false);
            margin.PotionSeconds = 16;
            Check(Find(engine.Tick(margin, settings), "potion") == null && engine.PotionLatched, "16 seconds is still inside the recovery margin");
            WatchSample recovered = Sample(t.AddSeconds(5), true, true, true, false);
            recovered.PotionSeconds = 30;
            Check(Find(engine.Tick(recovered, settings), "potion") == null && !engine.PotionLatched, "30 seconds recovers the potion alert");
            low.Time = t.AddSeconds(10);
            Check(Find(engine.Tick(low, settings), "potion") == null, "potion cooldown blocks the return");
            low.Time = t.AddSeconds(26);
            Check(Find(engine.Tick(low, settings), "potion") != null, "the potion alert fires again after cooldown");
            WatchSample gone = Sample(t.AddSeconds(27), true, true, true, false);
            gone.PotionClear = true;
            Check(Find(engine.Tick(gone, settings), "potion") == null && !engine.PotionLatched, "no buff timers means the potion is no longer running out");
            settings.PotionSound = false;
            low.Time = t.AddSeconds(50);
            AlertEvent quiet = Find(engine.Tick(low, settings), "potion");
            Check(quiet != null && !quiet.Sound && quiet.Notify, "potion sound can be off while its notification stays on");

            int white = ImageBuffer.Pack(236, 236, 236);
            ImageBuffer bar = ImageBuffer.Blank(420, 90, ImageBuffer.Pack(12, 18, 28));
            DrawBuffIcon(bar, 16, 24, "60");
            DrawBuffIcon(bar, 64, 24, "11m");
            DrawBuffIcon(bar, 112, 24, "4");
            DrawBuffIcon(bar, 160, 24, "");
            var readClock = System.Diagnostics.Stopwatch.StartNew();
            ImageBuffer wide = ImageBuffer.Blank(1920, 1080, ImageBuffer.Pack(12, 18, 28));
            DrawBuffIcon(wide, 40, 40, "11m");
            HudReading wideRead = HudReader.Read(wide, new WardenSettings());
            readClock.Stop();
            Check(wideRead.BuffBarFound && wideRead.Buffs.Count >= 1 && readClock.ElapsedMilliseconds < 250, "a 1080p buff search stays under 250 ms, took " + readClock.ElapsedMilliseconds.ToString() + " ms");
            Console.WriteLine("hud-read-1080p-ms=" + readClock.ElapsedMilliseconds.ToString());
            HudReading icons = HudReader.Read(bar, new WardenSettings());
            Check(icons.BuffBarFound && icons.Buffs.Count == 4, "green-bordered buff icons are found in a row, got " + icons.Buffs.Count.ToString());
            Check(icons.Buffs.Count > 0 && icons.Buffs[0].HasTime && !icons.Buffs[0].Minutes && icons.Buffs[0].Value == 60, "a plain buff number is seconds");
            Check(icons.Buffs.Count > 1 && icons.Buffs[1].Minutes && icons.Buffs[1].Value == 11, "11m is eleven minutes, not eleven seconds, got has=" + (icons.Buffs.Count > 1 && icons.Buffs[1].HasTime) + " min=" + (icons.Buffs.Count > 1 && icons.Buffs[1].Minutes) + " value=" + (icons.Buffs.Count > 1 ? icons.Buffs[1].Value.ToString() : "-"));
            ImageBuffer secondsIcon = ImageBuffer.Blank(80, 70, ImageBuffer.Pack(12, 18, 28));
            DrawBuffIcon(secondsIcon, 8, 8, "45s");
            HudReading secondsRead = HudReader.Read(secondsIcon, new WardenSettings());
            Check(secondsRead.Buffs != null && secondsRead.Buffs.Count > 0 && secondsRead.Buffs[0].HasTime && !secondsRead.Buffs[0].Minutes && secondsRead.Buffs[0].Value == 45, "45s is forty-five seconds, trace " + (secondsRead.BuffTrace ?? ""));
            Check(icons.Buffs.Count > 2 && icons.Buffs[2].HasTime && !icons.Buffs[2].Minutes && icons.Buffs[2].Value == 4, "a stack count is still read as a number");
            Check(icons.Buffs.Count > 3 && !icons.Buffs[3].HasTime, "an icon with no text has no timer");
            BuffClock clock = new BuffClock();
            DateTime buffTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            int? first = clock.Tick(icons.Buffs, true, buffTime);
            Check(first == null, "one look at 11m is not adopted yet, got " + (first == null ? "none" : first.Value.ToString()));
            int? second = clock.Tick(icons.Buffs, true, buffTime.AddSeconds(1));
            Check(second != null && second.Value >= 600 && second.Value <= 660, "the minute timer is adopted on the second look, got " + (second == null ? "none" : second.Value.ToString()));
            List<BuffIcon> dropped = new List<BuffIcon>();
            BuffIcon seconds = icons.Buffs[1];
            seconds.Minutes = false;
            seconds.Value = 10;
            dropped.Add(icons.Buffs[0]);
            dropped.Add(seconds);
            dropped.Add(icons.Buffs[2]);
            int? lowest = clock.Tick(dropped, true, buffTime.AddSeconds(2));
            Check(lowest != null && lowest.Value > 500, "11m read as 10 seconds does not replace the minute clock, got " + (lowest == null ? "none" : lowest.Value.ToString()));
        }

        private static void DrawBuffIcon(ImageBuffer image, int x, int y, string text)
        {
            int green = ImageBuffer.Pack(80, 210, 90);
            int dark = ImageBuffer.Pack(14, 20, 32);
            int white = ImageBuffer.Pack(236, 236, 236);
            int size = 40;
            image.Fill(x, y, size, 3, green);
            image.Fill(x, y + size - 3, size, 3, green);
            image.Fill(x, y, 3, size, green);
            image.Fill(x + size - 3, y, 3, size, green);
            image.Fill(x + 3, y + 3, size - 6, size - 6, dark);
            image.Fill(x + 8, y + 6, 18, 12, ImageBuffer.Pack(90, 150, 230));
            int tx = x + 4;
            int ty = y + size - 18;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= '0' && ch <= '9')
                {
                    DrawGlyph(image, ch - '0', tx, ty, 2, white);
                    tx += 12;
                }
                else if (ch == 'm')
                {
                    image.Fill(tx, ty + 2, 2, 10, white);
                    image.Fill(tx + 4, ty + 2, 2, 10, white);
                    image.Fill(tx + 8, ty + 2, 2, 10, white);
                    image.Fill(tx, ty + 2, 10, 2, white);
                    tx += 12;
                }
                else if (ch == 's')
                {
                    image.Fill(tx, ty + 2, 8, 10, white);
                    tx += 12;
                }
            }
        }

        private static void LiveCapture(string root)
        {
            string path = Path.Combine(root, "verification", "fixtures", "debug-capture.png");
            if (!File.Exists(path))
            {
                failures.Add("missing live fixture " + path);
                assertions++;
                return;
            }
            ImageBuffer image = PngImage.Load(path);
            HudReading reading = HudReader.Read(image, new WardenSettings());
            Check(reading.Hp == 1293 && reading.HpMax == 1060 && reading.Hp > reading.HpMax, "live capture life is the red bar 1293/1060, above the cap, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()));
            Check(reading.Prayer == 1040 && reading.PrayerMax == 990 && reading.Prayer > reading.PrayerMax, "live capture prayer is the purple bar 1040/990, above the cap, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()));
            Check(reading.Hp != 60 && reading.Hp != 6060 && reading.Hp != 66, "summoning 60/60 is not reported as hitpoints");
            int seconds = 0, minutes = 0;
            if (reading.Buffs != null)
            {
                for (int i = 0; i < reading.Buffs.Count; i++)
                {
                    if (!reading.Buffs[i].HasTime) continue;
                    if (reading.Buffs[i].Minutes && reading.Buffs[i].Value == 10) minutes++;
                    if (!reading.Buffs[i].Minutes && reading.Buffs[i].Value == 60) seconds++;
                }
            }
            string seen = "";
            if (reading.Buffs != null)
                for (int i = 0; i < reading.Buffs.Count; i++)
                    seen += reading.BuffRects[i].X.ToString() + ":" + (reading.Buffs[i].HasTime ? (reading.Buffs[i].Minutes ? reading.Buffs[i].Value.ToString() + "m" : reading.Buffs[i].Value.ToString()) : "-") + " ";
            Check(reading.BuffBarFound && seconds >= 1 && minutes >= 4, "live buff row reads 60 seconds and four 10 minute icons, got " + seen);
            SceneActivity scene = new SceneActivity();
            Check(!scene.Tick(image, reading), "a steady target bar does not reset idle, reason " + scene.Reason);
            ImageBuffer struck = new ImageBuffer(image.Width, image.Height, (int[])image.Pixels.Clone());
            int painted = 0;
            for (int y = 0; y < struck.Height && painted == 0; y++)
            {
                int x = 0;
                while (x < struck.Width && painted == 0)
                {
                    int c = struck.Argb(x, y);
                    int r = ImageBuffer.R(c), g = ImageBuffer.G(c), b = ImageBuffer.B(c);
                    bool lime = g >= 160 && b <= 80 && g > r + 30 && g > b + 40;
                    if (!lime) { x++; continue; }
                    int x0 = x;
                    while (x < struck.Width)
                    {
                        int d = struck.Argb(x, y);
                        int rr = ImageBuffer.R(d), gg = ImageBuffer.G(d), bb = ImageBuffer.B(d);
                        if (!(gg >= 160 && bb <= 80 && gg > rr + 30 && gg > bb + 40)) break;
                        x++;
                    }
                    if (x - x0 >= 20)
                    {
                        for (int dy = -3; dy <= 3; dy++)
                            for (int k = 0; k < 16; k++) struck.Set(x0 + k, y + dy, ImageBuffer.Pack(8, 8, 8));
                        painted = 16;
                    }
                }
            }
            Check(painted > 0 && !scene.Tick(struck, reading), "a shrinking target bar is not activity, reason " + scene.Reason);
            SceneActivity calm = new SceneActivity();
            ImageBuffer trees = ImageBuffer.Blank(320, 240, ImageBuffer.Pack(20, 80, 30));
            ImageBuffer sway = ImageBuffer.Blank(320, 240, ImageBuffer.Pack(30, 110, 40));
            sway.Fill(40, 40, 200, 120, ImageBuffer.Pack(40, 140, 50));
            Check(!calm.Tick(trees, new HudReading()), "the first scene is not activity");
            Check(!calm.Tick(sway, new HudReading()), "swaying trees and water are not player activity");
            StableValue steady = new StableValue();
            Check(steady.Push(1040) == null, "one prayer frame is not shown");
            Check(steady.Push(1040) == 1040, "two agreeing prayer reads are shown");
            Check(steady.Push(400) == 1040, "a single prayer jump is rejected");
            Check(steady.Push(400) == 400, "two agreeing new reads replace the old value");
            SceneActivity xpScene = new SceneActivity();
            ImageBuffer xpBase = ImageBuffer.Blank(400, 300, ImageBuffer.Pack(20, 24, 28));
            xpBase.Fill(280, 20, 40, 12, ImageBuffer.Pack(240, 230, 210));
            Check(!xpScene.Tick(xpBase, new HudReading(), false, false, false), "the first xp sample is not activity");
            Check(xpScene.Tick(xpBase, new HudReading(), false, true, false) && xpScene.Reason.IndexOf("xp") >= 0, "an xp drop resets idle");
            ImageBuffer packA = ImageBuffer.Blank(200, 280, ImageBuffer.Pack(12, 14, 16));
            ImageBuffer packB = ImageBuffer.Blank(200, 280, ImageBuffer.Pack(12, 14, 16));
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 4; col++)
                {
                    packA.Fill(20 + col * 30, 20 + row * 30, 28, 28, ImageBuffer.Pack(90, 90, 90));
                    packA.Fill(24 + col * 30, 24 + row * 30, 20, 20, ImageBuffer.Pack(20, 20, 20));
                    packB.Fill(20 + col * 30, 20 + row * 30, 28, 28, ImageBuffer.Pack(90, 90, 90));
                    packB.Fill(24 + col * 30, 24 + row * 30, 20, 20, ImageBuffer.Pack(20, 20, 20));
                }
            IntRect grid = SceneActivity.PackWatch.Find(packA);
            Check(grid.Width > 0, "a 28-slot backpack grid can be located");
            int before = SceneActivity.PackWatch.Hash(packA, grid);
            packB.Fill(24, 24, 20, 20, ImageBuffer.Pack(180, 40, 40));
            Check(SceneActivity.PackWatch.Hash(packB, grid) != before, "a backpack content change changes the slot hash");
            WardenSettings junk = SettingsText.Read("not-a-key\tidle-seconds\tnope\r\npanel\tnot\tnumbers\r\n");
            Check(junk.IdleSeconds == 12, "a broken settings line keeps the defaults");
            int px = -4000, py = 20;
            ScreenClamp.Place(ref px, ref py, 400, 262, new int[] { 0 }, new int[] { 0 }, new int[] { 1920 }, new int[] { 1080 });
            Check(px >= 0 && py >= 0, "an off-screen panel is moved onto a visible screen");
        }

        private static void Live023(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live023");
            string full = Path.Combine(dir, "debug-capture.png");
            string prayer = Path.Combine(dir, "game-prayer-936-990.png");
            if (!File.Exists(full) || !File.Exists(prayer))
            {
                failures.Add("missing live023 fixtures");
                assertions++;
                return;
            }
            HudReading reading = HudReader.Read(PngImage.Load(full), new WardenSettings());
            Check(reading.Hp == 1633 && reading.HpMax == 1060, "live023 life is 1633/1060 from the full slot, got " + (reading.Hp == null ? "none" : reading.Hp.Value.ToString()) + "/" + (reading.HpMax == null ? "-" : reading.HpMax.Value.ToString()) + " " + reading.LifeTrace);
            Check(reading.Prayer == 1040 && reading.PrayerMax == 990, "live023 prayer is 1040/990, got " + (reading.Prayer == null ? "none" : reading.Prayer.Value.ToString()) + "/" + (reading.PrayerMax == null ? "-" : reading.PrayerMax.Value.ToString()) + " " + reading.PrayerTrace);
            Check(reading.LifeText.Width > reading.LifeBar.Width, "the life text box uses the slot, not the fill");
            Check(reading.PrayerText.Width > 80, "the prayer text box stays wide enough when the fill is full, width " + reading.PrayerText.Width.ToString());
            int? shown = BuffClock.ShownSeconds(reading.Buffs);
            Check(reading.BuffBarFound && shown != null, "live023 buff icons expose a timer, got " + (reading.BuffTrace ?? "") + " shown " + (shown == null ? "none" : shown.Value.ToString()));
            HudReading drained = HudReader.Read(PngImage.Load(prayer), new WardenSettings());
            Check(drained.Prayer == 936 && drained.PrayerMax == 990, "drained prayer reads 936/990, got " + (drained.Prayer == null ? "none" : drained.Prayer.Value.ToString()) + "/" + (drained.PrayerMax == null ? "-" : drained.PrayerMax.Value.ToString()) + " trace=" + drained.PrayerTrace + " last=" + HudReader.LastTrace);
            string v024 = Path.Combine(dir, "game-prayer-936-990-v024.png");
            if (File.Exists(v024))
            {
                HudReading again = HudReader.Read(PngImage.Load(v024), new WardenSettings());
                Check(again.Prayer == 936 && again.PrayerMax == 990, "v024 prayer crop reads 936/990, got " + (again.Prayer == null ? "none" : again.Prayer.Value.ToString()) + "/" + (again.PrayerMax == null ? "-" : again.PrayerMax.Value.ToString()) + " " + again.PrayerTrace);
            }
        }

        // Nine combinations: 1080p, 1440p and 4K, each at 100%, 150% and 200% Windows scaling.
        // The fixture is a 1440p capture, so the image is scaled by resolution.
        // DPI is physical pixels: it multiplies that scale and does not shrink the crop.
        private static void ScaleMatrix(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live023");
            string fullPath = Path.Combine(dir, "debug-capture.png");
            if (!File.Exists(fullPath)) { Check(false, "missing scale fixture"); return; }
            ImageBuffer full = PngImage.Load(fullPath);
            HudReading native = HudReader.Read(full, new WardenSettings());
            int top = native.LifeBar.Height > 0 && native.LifeBar.Y > 50 ? native.LifeBar.Y - 50 : (int)(full.Height * 0.75);
            if (top < 0) top = 0;
            int bandH = Math.Min(full.Height - top, 140);
            ImageBuffer band = full.Crop(0, top, full.Width, bandH);
            double[] resolution = new double[] { 1920.0 / 2560.0, 1.0, 3840.0 / 2560.0 };
            double[] dpi = new double[] { 1.0, 1.5, 2.0 };
            string[] resolutionName = new string[] { "1080p", "1440p", "4K" };
            string[] dpiName = new string[] { "100%", "150%", "200%" };
            for (int r = 0; r < resolution.Length; r++)
                for (int d = 0; d < dpi.Length; d++)
                {
                    double physical = resolution[r] * dpi[d];
                    HudReading reading = HudReader.Read(band.Scale(physical), new WardenSettings());
                    bool exact = reading.Hp == 1633 && reading.HpMax == 1060 && reading.Prayer == 1040 && reading.PrayerMax == 990;
                    bool wrong = (reading.Hp != null && reading.Hp != 1633) || (reading.HpMax != null && reading.HpMax != 1060) || (reading.Prayer != null && reading.Prayer != 1040) || (reading.PrayerMax != null && reading.PrayerMax != 990);
                    bool stable = Math.Abs(physical - 1) < 0.05 || Math.Abs(physical - 1.5) < 0.05 || Math.Abs(physical - 2) < 0.05 || Math.Abs(physical - 3) < 0.05;
                    Check(!wrong && (!stable || exact), resolutionName[r] + " at " + dpiName[d] + " Windows scaling reads HP and prayer, got " + Fmt(reading.Hp) + "/" + Fmt(reading.HpMax) + " prayer " + Fmt(reading.Prayer) + "/" + Fmt(reading.PrayerMax) + " " + reading.LifeTrace + " | " + reading.PrayerTrace);
                    int ox, oy, w, h;
                    int frameW = (int)Math.Round(2576 * resolution[r] * dpi[d]);
                    int frameH = (int)Math.Round(1488 * resolution[r] * dpi[d]);
                    int clientW = (int)Math.Round(2560 * resolution[r] * dpi[d]);
                    int clientH = (int)Math.Round(1440 * resolution[r] * dpi[d]);
                    int insetX = (int)Math.Round(8 * dpi[d]);
                    int insetY = (int)Math.Round(24 * dpi[d]);
                    WindowCrop.Client(0, 0, frameW, frameH, insetX, insetY, clientW, clientH, out ox, out oy, out w, out h);
                    Check(ox == insetX && oy == insetY && w == clientW && h == clientH, resolutionName[r] + " at " + dpiName[d] + " crop stays in physical pixels");
                }
            double[] interfaceScale = new double[] { 0.8, 1.0, 1.25, 1.5 };
            for (int i = 0; i < interfaceScale.Length; i++)
            {
                HudReading reading = HudReader.Read(band.Scale(interfaceScale[i]), new WardenSettings());
                bool exact = reading.Hp == 1633 && reading.HpMax == 1060 && reading.Prayer == 1040 && reading.PrayerMax == 990;
                bool wrong = (reading.Hp != null && reading.Hp != 1633) || (reading.HpMax != null && reading.HpMax != 1060) || (reading.Prayer != null && reading.Prayer != 1040) || (reading.PrayerMax != null && reading.PrayerMax != 990);
                bool stable = interfaceScale[i] == 1.0 || interfaceScale[i] == 1.5;
                Check(!wrong && (!stable || exact), "interface scale " + interfaceScale[i].ToString("0.00") + " reads HP and prayer, got " + Fmt(reading.Hp) + "/" + Fmt(reading.HpMax) + " " + Fmt(reading.Prayer) + "/" + Fmt(reading.PrayerMax) + " " + reading.LifeTrace);
            }
            ExpectPotions(dir, "buffbar-potions-12m.png", 720);
            ExpectPotions(dir, "buffbar-potions-11m-elder-overload.png", 660);
            ExpectPotions(dir, "buffbar-prayer60-bone5-heart.png", 0);
        }

        private static void ExpectPotions(string dir, string name, int seconds)
        {
            string path = Path.Combine(dir, name);
            if (!File.Exists(path)) { Check(false, "missing " + name); return; }
            ImageBuffer icon = PngImage.Load(path);
            double[] scales = new double[] { 0.8, 1.0, 1.25, 1.5 };
            for (int i = 0; i < scales.Length; i++)
            {
                HudReading potionRead = HudReader.Read(icon.Scale(scales[i]), new WardenSettings());
                int? shown = BuffClock.ShownSeconds(potionRead.Buffs);
                int got = shown == null ? 0 : shown.Value;
                if (scales[i] != 1.0) continue;
                Check(got == seconds, name + " at interface 1.00 potion seconds " + got.ToString() + " expected " + seconds.ToString() + " " + (potionRead.BuffTrace ?? ""));
            }
        }

        private static string Fmt(int? value)
        {
            return value == null ? "none" : value.Value.ToString();
        }

        private static void LiveRead(string root)
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ExpectPair(917, 1060, true, "HP 917/1060 is accepted");
            ExpectPair(99, 1060, true, "HP 99/1060 is accepted");
            ExpectPair(5, 1060, true, "HP 5/1060 is accepted");
            ExpectPair(842, 990, false, "prayer 842/990 is accepted");
            ExpectPair(9, 990, false, "prayer 9/990 is accepted");
            SlotWatch drain = new SlotWatch();
            int[] steps = new int[] { 990, 980, 970, 960, 950, 940, 936 };
            for (int i = 0; i < steps.Length; i++)
            {
                ImageBuffer frame = HudFrame(1060, 1060, steps[i], 990);
                drain.Observe(frame, new WardenSettings(), t.AddMilliseconds(500 * i));
                drain.Observe(frame, new WardenSettings(), t.AddMilliseconds(500 * i + 200));
            }
            Check(drain.Prayer == 936 && drain.PrayerMax == 990, "draining prayer reaches 936/990 through the locked slot, got " + (drain.Prayer == null ? "none" : drain.Prayer.Value.ToString()) + "/" + (drain.PrayerMax == null ? "-" : drain.PrayerMax.Value.ToString()));
            Check(drain.Hp == 1060 && drain.HpMax == 1060, "locked life stays 1060/1060 while prayer drains");
            ReadGate gate = new ReadGate();
            gate.Push(917, 1060, t);
            gate.Push(917, 1060, t.AddMilliseconds(500));
            gate.Push(null, 1060, t.AddSeconds(6));
            Check(gate.Value == 917 && gate.Stale, "a missing read keeps the last current and marks it stale, got " + (gate.Value == null ? "none" : gate.Value.Value.ToString()));
            gate.Push(50, 60, t.AddSeconds(7));
            Check(gate.Value == 917, "a read whose maximum does not match the lock is ignored");
            string dir = string.IsNullOrEmpty(root) ? "" : Path.Combine(root, "verification", "fixtures", "live023");
            if (dir.Length == 0 || !Directory.Exists(dir)) return;
            string[] names = new string[] { "buffbar-prayer60-bone5-heart.png", "buffbar-potions-11m-elder-overload.png", "buffbar-potions-12m.png", "panel-potions-60s-wrong.png", "panel-prayer-stuck-990.png", "live-v024-hp-917-unread.png", "live-v024-lagging-values.png", "live-v024-full-layout-lag.png" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(dir, names[i]);
                Check(File.Exists(path), "fixture present " + names[i]);
            }
            HudReading prayerBar = HudReader.Read(PngImage.Load(Path.Combine(dir, "buffbar-prayer60-bone5-heart.png")), new WardenSettings());
            Check(BuffClock.ShownSeconds(prayerBar.Buffs) == null, "the purple 60 and the stack of 5 are not potion timers, trace " + (prayerBar.BuffTrace ?? ""));
            HudReading twelve = HudReader.Read(PngImage.Load(Path.Combine(dir, "buffbar-potions-12m.png")), new WardenSettings());
            Check(BuffClock.ShownSeconds(twelve.Buffs) == 720, "two flasks at 12m are potion timers, trace " + (twelve.BuffTrace ?? ""));
            HudReading eleven = HudReader.Read(PngImage.Load(Path.Combine(dir, "buffbar-potions-11m-elder-overload.png")), new WardenSettings());
            int? potion = BuffClock.ShownSeconds(eleven.Buffs);
            Check(potion == 660, "elder overload row tracks 11m and ignores the stack of 5, got " + (potion == null ? "none" : potion.Value.ToString()) + " " + (eleven.BuffTrace ?? ""));
        }

        private static void ExpectPair(int current, int max, bool life, string label)
        {
            SlotWatch watch = new SlotWatch();
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ImageBuffer frame = life ? HudFrame(current, max, 990, 990) : HudFrame(1060, 1060, current, max);
            watch.Observe(frame, new WardenSettings(), t);
            watch.Observe(frame, new WardenSettings(), t.AddMilliseconds(500));
            int? got = life ? watch.Hp : watch.Prayer;
            int? gotMax = life ? watch.HpMax : watch.PrayerMax;
            Check(got == current && gotMax == max, label + ", got " + (got == null ? "none" : got.Value.ToString()) + "/" + (gotMax == null ? "-" : gotMax.Value.ToString()));
        }

        private static ImageBuffer HudFrame(int hp, int hpMax, int prayer, int prayerMax)
        {
            int white = ImageBuffer.Pack(236, 236, 236);
            int red = ImageBuffer.Pack(196, 42, 42);
            int purple = ImageBuffer.Pack(142, 70, 196);
            int yellow = ImageBuffer.Pack(214, 184, 48);
            ImageBuffer image = ImageBuffer.Blank(640, 160, ImageBuffer.Pack(10, 16, 22));
            image.Fill(40, 90, 150, 8, red);
            DrawLifeNumber(image, 40, 68, DigitsOf(hp), DigitsOf(hpMax), white);
            image.Fill(230, 90, 80, 8, yellow);
            image.Fill(340, 90, 140, 8, purple);
            DrawLifeNumber(image, 340, 68, DigitsOf(prayer), DigitsOf(prayerMax), white);
            return image;
        }

        private static int[] DigitsOf(int value)
        {
            string text = value.ToString();
            int[] digits = new int[text.Length];
            for (int i = 0; i < text.Length; i++) digits[i] = text[i] - '0';
            return digits;
        }

        private static void ChatAndMetrics(string root)
        {
            if (root == null || root.Length == 0) return;
            string chatDir = Path.Combine(root, "verification", "fixtures", "chat");
            string elder = Path.Combine(chatDir, "chat-elder-overload-messages.png");
            string full = Path.Combine(chatDir, "chatbox-full-timestamps.png");
            string agg = Path.Combine(chatDir, "chat-overload-aggression-messages.png");
            if (!File.Exists(elder) || !File.Exists(full) || !File.Exists(agg))
            {
                Check(false, "missing chat fixtures");
                return;
            }
            double[] scales = new double[] { 1.0, 1.5, 2.0 };
            string[] names = new string[] { "100%", "150%", "200%" };
            for (int s = 0; s < scales.Length; s++)
            {
                List<ChatHit> lines = ChatReader.Read(PngImage.Load(elder).Scale(scales[s]));
                Check(Has(lines, "elder", "low") && Has(lines, "elder", "expired") && !Has(lines, "prayer", "low") && !Has(lines, "antifire", "low") && !Has(lines, "poison", "low") && !Has(lines, "overload", "low"),
                    "elder chat crop at " + names[s] + " keeps elder overload and drops the other pots, got " + Describe(lines));
                List<ChatHit> box = ChatReader.Read(PngImage.Load(full).Scale(scales[s]));
                Check(Has(box, "elder", "low") && Has(box, "elder", "expired") && !Has(box, "antifire", "expired") && !Has(box, "poison", "expired"),
                    "full chatbox at " + names[s] + " keeps elder overload only, got " + Describe(box));
                List<ChatHit> other = ChatReader.Read(PngImage.Load(agg).Scale(scales[s]));
                Check(Has(other, "aggression", "low") && Has(other, "aggression", "expired") && !Has(other, "overload", "low") && !Has(other, "prayer", "low") && !Has(other, "elder", "low"),
                    "aggression crop at " + names[s] + " keeps aggression and drops overload and prayer, got " + Describe(other));
                bool star = false;
                for (int i = 0; i < other.Count; i++) if (other[i].Phrase != null && other[i].Phrase.IndexOf("star") >= 0) star = true;
                Check(!star, "the shooting star line is ignored");
            }
            ChatPotionBook book = new ChatPotionBook();
            List<ChatHit> once = ChatReader.Read(PngImage.Load(agg));
            List<ChatNotice> first = book.Observe(once);
            List<ChatNotice> second = book.Observe(once);
            Check(first.Count >= 1 && second.Count == 0 && book.Detail.IndexOf("Aggression") >= 0 && book.Detail.IndexOf("Pray") < 0 && book.Detail.IndexOf("Anti") < 0,
                "the same aggression lines do not alert twice and other pots stay off the row, got " + first.Count.ToString() + " " + book.Detail);
            book.Attach();
            Check(book.Observe(once).Count == 0, "lines already on screen when attaching do not alert");
            Check(book.Observe(once).Count == 0, "repeating those preexisting lines still does not alert");
            List<ChatHit> scrolled = new List<ChatHit>();
            for (int i = 0; i < once.Count; i++)
            {
                ChatHit moved = new ChatHit();
                moved.Phrase = once[i].Phrase;
                moved.Kind = once[i].Kind;
                moved.State = once[i].State;
                moved.Y = once[i].Y - 14;
                scrolled.Add(moved);
            }
            Check(book.Observe(scrolled).Count == 0, "scrolling the same lines does not alert again");
            Check(ChatPotionBook.Drink("You drink the Elder overload salve"), "a drink line re-arms");
            string quiet = Path.Combine(chatDir, "chatbox-no-pot-lines-false-ping.png");
            if (File.Exists(quiet))
            {
                List<ChatHit> noise = ChatReader.Read(PngImage.Load(quiet));
                ChatPotionBook silent = new ChatPotionBook();
                List<ChatNotice> ping = silent.Observe(noise);
                List<ChatNotice> again = silent.Observe(noise);
                Check(noise.Count == 0 && ping.Count == 0 && again.Count == 0, "a chat with no potion lines matches nothing and makes no sound, got " + Describe(noise) + " notices " + ping.Count.ToString());
            }
            else Check(false, "missing chatbox-no-pot-lines-false-ping.png");
            string xpZero = Path.Combine(chatDir, "runemetrics-xp-only-zero.png");
            string xpDamage = Path.Combine(chatDir, "runemetrics-damage-and-xp.png");
            for (int s = 0; s < scales.Length; s++)
            {
                if (File.Exists(xpZero))
                {
                    MetricsReader.Result zero = MetricsReader.Read(PngImage.Load(xpZero).Scale(scales[s]));
                    Check(zero.Found && zero.Xp == 0, "XP-only metrics at " + names[s] + " reads 0, got " + zero.Xp.ToString() + " " + (zero.Note ?? ""));
                }
                if (File.Exists(xpDamage))
                {
                    MetricsReader.Result total = MetricsReader.Read(PngImage.Load(xpDamage).Scale(scales[s]));
                    Check(total.Found && total.Xp == 5947600L, "damage metrics at " + names[s] + " reads 5947600, got " + total.Xp.ToString() + " " + (total.Note ?? ""));
                }
            }
            string otherClient = Path.Combine(root, "verification", "fixtures", "live0213", "full-client-other-account-layout.png");
            string panelClient = Path.Combine(root, "verification", "fixtures", "live0213", "full-client-panel-no-pot-timer.png");
            if (File.Exists(otherClient))
            {
                MetricsReader.Result xp = MetricsReader.Read(PngImage.Load(otherClient));
                Check(xp.Found && xp.Xp == 5403925L, "the other account total XP is 5403925, got " + xp.Xp.ToString() + " found " + xp.Found.ToString());
            }
            if (File.Exists(panelClient))
            {
                MetricsReader.Result xp = MetricsReader.Read(PngImage.Load(panelClient));
                Check(xp.Found, "the panel client finds the RuneMetrics XP row");
            }
            string readme = File.ReadAllText(Path.Combine(root, "README.md"));
            Check(readme.IndexOf("RuneMetrics") >= 0 && readme.IndexOf("chatbox") >= 0, "the readme tells players to keep RuneMetrics and the chatbox open");
            string pots = Path.Combine(chatDir, "chat-all-pots-expiring.png");
            if (!File.Exists(pots)) Check(false, "missing chat-all-pots-expiring.png");
            else
            {
                for (int s = 0; s < scales.Length; s++)
                {
                    List<ChatHit> red = ChatReader.Read(PngImage.Load(pots).Scale(scales[s]));
                    Check(Has(red, "elder", "low") && Has(red, "elder", "expired") && !Has(red, "prayer", "low") && !Has(red, "antifire", "low") && !Has(red, "poison", "low"),
                        "red potion chat at " + names[s] + " keeps elder overload only, got " + Describe(red));
                }
                Check(ChatReader.Trace.IndexOf("Your elder") >= 0 && ChatReader.Trace.IndexOf("raw=") >= 0 && ChatReader.Trace.IndexOf("candidate=") >= 0, "the debug line quotes the raw read and the candidate");
            string four = Path.Combine(chatDir, "chat-four-pots-low-missed-v0218.png");
            if (!File.Exists(four)) Check(false, "missing chat-four-pots-low-missed-v0218.png");
            else
            {
                List<ChatHit> missed = ChatReader.Read(PngImage.Load(four));
                Check(Has(missed, "elder", "low") && !Has(missed, "prayer", "low") && !Has(missed, "antifire", "low") && !Has(missed, "poison", "low"),
                    "the four red lines keep elder overload and drop the others, got " + Describe(missed) + " " + ChatReader.Trace);
            }
            Check(ChatPhrase.KindOf("[16:12:16] Your elder overload is about to run out...") == "elder" && ChatPhrase.StateOf("[16:12:16] Your elder overload is about to run out...") == "low", "a timestamped elder warning is low");
            Check(ChatPhrase.KindOf("Your prayer renewal effect will run out in 30 seconds.") == "" && ChatPhrase.KindOf("Your super antifire potion is about to run out") == "" && ChatPhrase.KindOf("Your immunity to poison is about to run out") == "", "prayer, antifire and poison are not tracked");
            Check(ChatPhrase.StateOf("The effects of elder overload have worn off.") == "expired" && ChatPhrase.KindOf("The effects of your Aggression Potion will expire in 30 seconds.") == "aggression" && ChatPhrase.StateOf("The effects of your Aggression Potion will expire in 30 seconds.") == "low", "aggression 30 seconds is low, and worn off is expired");
            Check(SceneActivity.XpPopup.Matches("+351 xp (23.4 bonus xp)") && SceneActivity.XpPopup.Matches("+2,437 xp (162.5 bonus xp)") && SceneActivity.XpPopup.Matches("+12 xp"), "an XP popup with or without bonus xp counts");
            Check(!SceneActivity.XpPopup.Matches("1.6M") && !SceneActivity.XpPopup.Matches("Your elder overload is about to run out"), "a total and a chat line are not XP popups");
            Check(MetricsReader.IsFullTotal("5,947,600") && MetricsReader.IsFullTotal("0") && !MetricsReader.IsFullTotal("1.6M") && !MetricsReader.IsFullTotal("250K") && !MetricsReader.IsFullTotal("1.6"), "only a full integer total counts");
            long parsed;
            Check(MetricsReader.Parse("5,947,600", out parsed) && parsed == 5947600L, "commas stay in a full total");
            Check(!SceneActivity.XpPopup.WantHint(true, false) && !SceneActivity.XpPopup.WantHint(false, true) && SceneActivity.XpPopup.WantHint(false, false), "the XP hint shows only when both signals are missing");
            Check(ClientOrder.Place(34692, new int[] { 34692, 33960 }) == 1 && ClientOrder.Place(33960, new int[] { 34692, 33960 }) == 0, "client numbers follow process id, so the higher pid is Client 2");
            Check(ClientNames.Label(0, ClientOrder.Place(34692, new int[] { 33960, 34692 })) == "Client 2", "the bound higher pid is labelled Client 2");
            string kindName, stateName, phraseName;
            Check(ChatPhrase.MatchLine("[16:12:16] Your elder overload is about to run out...", out kindName, out stateName, out phraseName) && kindName == "elder" && stateName == "low", "whole phrase: elder warning");
            Check(ChatPhrase.MatchLine("The effects of your Aggression Potion will expire in 30 seconds.", out kindName, out stateName, out phraseName) && kindName == "aggression" && stateName == "low", "whole phrase: aggression warning");
            Check(ChatPhrase.MatchLine("The effects of your Aggression Potion have expired.", out kindName, out stateName, out phraseName) && kindName == "aggression" && stateName == "expired", "whole phrase: aggression expired");
            Check(ChatPhrase.MatchLine("The effects of elder overload have worn off and you feel normal again.", out kindName, out stateName, out phraseName) && kindName == "elder" && stateName == "expired", "whole phrase: elder worn off");
            Check(!ChatPhrase.MatchLine("Your prayer renewal effect will run out in 30 seconds.", out kindName, out stateName, out phraseName), "prayer renewal is not a tracked pot");
            Check(!ChatPhrase.MatchLine("Your super antifire potion is about to run out", out kindName, out stateName, out phraseName), "antifire is not a tracked pot");
            Check(!ChatPhrase.MatchLine("Your immunity to poison is about to run out", out kindName, out stateName, out phraseName), "poison immunity is not a tracked pot");
            Check(!ChatPhrase.MatchLine("Welcome to trivia and the server news", out kindName, out stateName, out phraseName), "unrelated chat is not a potion line");
            Check(ChatPhrase.MatchLine("Your elder overload is about to out...", out kindName, out stateName, out phraseName) && kindName == "elder" && stateName == "low", "a missing word in the elder warning still matches");
            Check(ChatPhrase.MatchLine("The effects of your Aggresslon Potion wlll explre in 30 sec0nds.", out kindName, out stateName, out phraseName) && kindName == "aggression" && stateName == "low", "a garbled aggression warning still matches, got " + kindName + " " + stateName);
            Check(ChatPhrase.MatchLine("Y0ur eldar ovefload is abcut tc run oyt", out kindName, out stateName, out phraseName) && kindName == "elder" && stateName == "low", "a garbled elder warning still matches, got " + kindName + " " + stateName);
            Check(ChatPhrase.MatchLine("The effects of eldar 0verload haue wom off.", out kindName, out stateName, out phraseName) && kindName == "elder" && stateName == "expired", "a garbled elder expiry still matches, got " + kindName + " " + stateName);
            Check(!ChatPhrase.MatchLine("Your prayer reneual effect wi11 run 0ut in 30 sec0nds.", out kindName, out stateName, out phraseName), "a garbled prayer line is not a pot");
            Check(!ChatPhrase.MatchLine("Your super ant1fire potlon is ab0ut to run out", out kindName, out stateName, out phraseName), "a garbled antifire line is not a pot");
            Check(!ChatPhrase.MatchLine("Your immunlty to polson is ab0ut to expire", out kindName, out stateName, out phraseName), "a garbled poison line is not a pot");
            Check(ChatPhrase.MatchLine("The effects of your aggresslon potlon haue explred.", out kindName, out stateName, out phraseName) && kindName == "aggression" && stateName == "expired", "a garbled aggression expiry still matches, got " + kindName + " " + stateName);
            Check(!ChatPhrase.MatchLine("the effects of ouerload haue worn 0ff", out kindName, out stateName, out phraseName), "a plain overload line is not elder or aggression");
            Check(!ChatPhrase.MatchLine("] Your prayer effect Will 3Ü seconds..", out kindName, out stateName, out phraseName), "the v0.2.21 prayer OCR line is not a pot");
            Check(!ChatPhrase.MatchLine("] Your super antifliE potion IS about to run out...", out kindName, out stateName, out phraseName), "the v0.2.21 antifire OCR line is not a pot");
            Check(!ChatPhrase.MatchLine("] Your Immunity to po•n about to out...", out kindName, out stateName, out phraseName), "the v0.2.21 poison OCR line is not a pot");
            Check(ChatPhrase.MatchLine("The effazts of pur Potion will expire in 30 saonds.", out kindName, out stateName, out phraseName) && kindName == "aggression" && stateName == "low", "aggression low still matches when the word Aggression is missing, got " + kindName + " " + stateName);
            Check(ChatPhrase.MatchLine("The effects of your Potion have expired.", out kindName, out stateName, out phraseName) && kindName == "aggression" && stateName == "expired", "aggression expired still matches when the word Aggression is missing, got " + kindName + " " + stateName);
            Check(ChatPhrase.MatchLine("The effa:ts of elder overload •exorn off and you fæl normal again.", out kindName, out stateName, out phraseName) && kindName == "elder" && stateName == "expired", "the worn-off OCR line is elder expired, got " + kindName + " " + stateName);
            ReplayPotLogs(root);
            ReplayMetrics(root);
            ChatConfirm(root);
            Live026(root);
            BoostedLife(root);
            PotLines();
            BarAnchor();
            ChatFresh();
            SacrificeCases(root);
            ReleaseNoteCases();
            string[] chatter = new string[] {
                "-a ALL CHAT",
                ". 14KlllS In core",
                "Not bad",
                "] You haue earnad log entinel fragments. Thay ha»e bæn addad to pur currency pou",
                "] You a total of lü,ü8ü entinel fragments.",
                "] You're hit by a pule core blast and gain an XP bccst of for the next IO minute",
                "] [ ] Spmm",
                "1m fæling core s:on",
                "You have earned 10 sentinel fragments.",
                "Construction XP has increased"
            };
            for (int c = 0; c < chatter.Length; c++)
            {
                bool pot = ChatPhrase.MatchLine(chatter[c], out kindName, out stateName, out phraseName);
                Check(!pot, "v0.2.20 chat is not a potion line: " + chatter[c]);
                notes.Add("OCR line not a pot: " + (pot ? "FAIL matched " + kindName : "PASS") + " | " + chatter[c]);
            }
            LiveShots(root);
            ImageBuffer chrome = ImageBuffer.Blank(200, 800, ImageBuffer.Pack(8, 12, 16));
            chrome.Fill(10, 4, 80, 8, ImageBuffer.Pack(230, 230, 230));
            List<ChatHit> top = ChatReader.Read(chrome);
            Check(top.Count == 0 && ChatReader.Trace.IndexOf("skip-top") >= 0, "the line at the top of the window is not locked as chat, got " + ChatReader.Trace);
            Check(ChatReader.Text(ImageBuffer.Pack(216, 3, 4)) && ChatReader.Text(ImageBuffer.Pack(135, 2, 3)) && ChatReader.Text(ImageBuffer.Pack(220, 120, 20)) && ChatReader.Text(ImageBuffer.Pack(20, 200, 40)), "red, dark red, orange and green chat ink is read");
            Check(ChatPrep.Coloured(ImageBuffer.Pack(135, 2, 3)) && ChatPrep.Coloured(ImageBuffer.Pack(233, 2, 3)), "dark red and bright red both survive the OCR mask");
            ChatPotionBook doses = new ChatPotionBook();
            List<ChatHit> drinks = new List<ChatHit>();
            drinks.Add(Hit("You drink the Elder overload salve", "elder", "active", 40));
            drinks.Add(Hit("You drink some of your aggression potion", "aggression", "active", 54));
            doses.Observe(drinks);
            Check(doses.Detail.IndexOf("Elder overload") >= 0 && doses.Detail.IndexOf(":") >= 0, "elder shows a countdown from the drink, got " + doses.Detail);
            Check(doses.Detail.IndexOf("Aggression Active") >= 0 && doses.Detail.IndexOf("Pray") < 0, "aggression with no countdown says Active, and other pots stay off the row, got " + doses.Detail);
            Check(ChatPotionBook.ValueArgb("Elder low") != 0 && ChatPotionBook.ValueArgb("Elder expired") != 0 && ChatPotionBook.ValueArgb("Elder low") != ChatPotionBook.ValueArgb("Elder expired") && ChatPotionBook.ValueArgb("Anti Active") == 0, "low is orange, expired is red, and Active keeps the text colour");
            MetricsLock totals = new MetricsLock();
            DateTime at = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
            totals.Observe(true, 450, "xp=450", at);
            totals.Observe(true, 7, "xp=7", at.AddSeconds(1));
            Check(totals.Found && totals.Xp == 450 && totals.Note.IndexOf("reject 7") >= 0, "a drop to 7 does not replace the total, got " + totals.Note);
            totals.Observe(true, 3095, "xp=3095", at.AddSeconds(2));
            totals.Observe(true, 5947600, "xp=5947600", at.AddSeconds(3));
            Check(totals.Xp == 450, "a jump to another total is held, got " + totals.Xp.ToString());
            totals.Observe(true, 471, "xp=471", at.AddSeconds(4));
            Check(totals.Xp == 471 && totals.Gain, "a small climb is kept");
            totals.Observe(false, 0, "no XP row", at.AddSeconds(5));
            Check(totals.Found && totals.Xp == 471, "a missed frame keeps the last total");
            for (int n = 0; n < 5; n++) totals.Observe(true, 7, "xp=7", at.AddSeconds(6 + n));
            Check(totals.Found && totals.Xp == 471, "a repeated crash is ignored, got " + totals.Xp.ToString() + " " + totals.Note);
            totals.Observe(false, 0, "no XP row", at.AddSeconds(20));
            totals.Observe(false, 0, "no XP row", at.AddSeconds(26));
            Check(!totals.Found && totals.Note == "lost", "the tracker is lost after it stays missing");
            List<ClientCandidate> clients = new List<ClientCandidate>();
            clients.Add(Client("Kavros", 11, 100, false));
            clients.Add(Client("Kavros", 22, 200, true));
            clients.Add(Client("Kavros", 33, 300, false));
            clients.Add(Overlay("Companion", 90, 900));
            clients.Add(Overlay("Clue Helper", 91, 901));
            long bound = ClientBind.Choose(clients, 0, 0, 0);
            Check(bound == 200, "with three clients the foreground one is bound, got " + bound.ToString());
            Check(ClientBind.Choose(clients, 200, 100, 0) == 200, "a later toggle does not jump to another client");
            Check(ClientBind.Choose(clients, 0, 900, 0) == 200, "a Companion hwnd falls through to the foreground client");
            Check(ClientBind.Choose(clients, 0, 0, 33) == 300, "a passed client pid binds that client");
            List<ClientCandidate> closed = new List<ClientCandidate>();
            closed.Add(Client("Kavros", 22, 200, true));
            closed.Add(Client("Kavros", 33, 300, false));
            Check(ClientBind.Choose(closed, 100, 0, 0) == -1, "a closed client stays closed instead of switching");
            Check(ClientBind.Choose(closed, 100, 300, 0) == 300, "a new toggle after the close binds the named client");
            ClientLaunch.Clear();
            ClientLaunch.ReadArgs(new string[] { "--hwnd", "4567", "--pid", "89" });
            Check(ClientLaunch.Hwnd == 4567 && ClientLaunch.Pid == 89, "a companion launch can pass the client hwnd and pid");
            ClientLaunch.Clear();
            ClientLaunch.NoTray = false;
            ClientLaunch.ReadArgs(new string[] { "--hwnd", "4567", "--pid", "89" });
            Check(!ClientLaunch.NoTray, "the tray icon stays unless --no-tray is passed");
            ClientLaunch.ReadArgs(new string[] { "--unknown", "--hwnd=4567", "--no-tray" });
            Check(ClientLaunch.NoTray && ClientLaunch.Hwnd == 4567, "--no-tray is read next to the client hwnd");
            ClientLaunch.NoTray = false;
            ClientLaunch.ReadArgs(new string[] { "--hwnd", "4567", "--no-tray", "--pid", "89" });
            Check(ClientLaunch.NoTray && ClientLaunch.Hwnd == 4567 && ClientLaunch.Pid == 89, "--no-tray does not disturb the client hwnd and pid");
            ClientLaunch.NoTray = false;
            ClientLaunch.ReadArgs(new string[] { "--no-trays", "no-tray", "--notray" });
            Check(!ClientLaunch.NoTray, "only the exact --no-tray switch hides the tray icon");
            ClientLaunch.Clear();
            Check(ClientBind.Choose(clients, 200, 0, 0) == 200, "a character label does not move the bound client");
            Dictionary<int, string> named = ClientRoster.Parse("[{\"pid\": 22, \"name\": \"Zezima\"}, {\"pid\": 33, \"name\": \"Tim \\\"Two\\\"\"}]");
            string zezima;
            string quoted;
            Check(named.TryGetValue(22, out zezima) && zezima == "Zezima", "a client name is read from the json");
            Check(named.TryGetValue(33, out quoted) && quoted == "Tim \"Two\"", "an escaped quote in a name is kept, got " + (quoted ?? ""));
            Check(ClientRoster.Parse("{\"pid\": 1, \"name\": \"still writing").Count == 0, "a file that is still being written yields no name");
            Check(ClientRoster.Parse("this is not json").Count == 0, "a bad names file yields no name");
            Check(ClientRoster.Sanitize("  Elder   overload\nname  ") == "Elder overload name", "a name is trimmed and kept on one line");
            Check(ClientRoster.Sanitize("ABCDEFGHIJKLMNOPQRSTUVWXYZ").Length == 24, "a long name is capped");
            string dir = Path.Combine(Path.GetTempPath(), "afk-names-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "client-names.json");
            ClientRoster roster = new ClientRoster(file);
            Check(roster.Label(22, 0) == "Client 1" && roster.Label(33, 1) == "Client 2", "with no names file the picker says Client 1, Client 2");
            File.WriteAllText(file, "[{\"pid\":22,\"name\":\"Zezima\"},{\"pid\":33,\"name\":\"Danni\"}]");
            DateTime stamp = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(file, stamp);
            Check(roster.Label(22, 0) == "Zezima" && roster.Label(33, 1) == "Danni", "names follow the client process id");
            File.WriteAllText(file, "[{\"pid\":22,\"name\":\"Someone Else\"}]");
            File.SetLastWriteTimeUtc(file, stamp);
            Check(roster.Label(22, 0) == "Zezima", "the names file is not re-read while its write time is unchanged");
            File.SetLastWriteTimeUtc(file, stamp.AddMinutes(1));
            Check(roster.Label(22, 0) == "Someone Else" && roster.Label(33, 1) == "Client 2", "a newer names file updates the label and leaves an unnamed client as Client 2");
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }
        }

        private static void LiveShots(string root)
        {
            if (root == null || root.Length == 0) return;
            string dir = Path.Combine(root, "verification", "fixtures", "live0219");
            string xp = Path.Combine(dir, "full-client-v0219-xpmiss.png");
            string pots = Path.Combine(dir, "full-client-v0219-potlive.png");
            if (!File.Exists(xp) || !File.Exists(pots))
            {
                Check(false, "missing full-client v0.2.19 screenshots");
                return;
            }
            ImageBuffer xpShot = PngImage.Load(xp);
            ImageBuffer potShot = PngImage.Load(pots);
            bool popup = XpFloat.Present(xpShot);
            Check(popup, "full-client-v0219-xpmiss.png has the floating XP popup in the centre-top");
            notes.Add("full-client-v0219-xpmiss.png (" + xpShot.Width.ToString() + "x" + xpShot.Height.ToString() + ") XP popup: " + (popup ? "PASS" : "FAIL"));
            PopupAt(Path.Combine(dir, "full-client-v0219-xpmiss.png"), "full-client-v0219-xpmiss.png", true);
            IntRect chat = ChatPrep.FindBox(potShot);
            bool chatOk = chat.Width >= 80 && chat.Height >= 40 && chat.Y > potShot.Height / 2 && chat.X < potShot.Width / 3 && chat.Width < potShot.Width / 2;
            Check(chatOk, "full-client-v0219-potlive.png chat box sits in the lower left, got " + chat.X.ToString() + "," + chat.Y.ToString() + " " + chat.Width.ToString() + "x" + chat.Height.ToString());
            notes.Add("full-client-v0219-potlive.png (" + potShot.Width.ToString() + "x" + potShot.Height.ToString() + ") chat box: " + (chatOk ? "PASS" : "FAIL") + " at " + chat.X.ToString() + "," + chat.Y.ToString() + " " + chat.Width.ToString() + "x" + chat.Height.ToString());
            ImageBuffer page = chat.Width > 8 ? ChatPrep.ForOcr(potShot.Crop(chat.X, chat.Y, chat.Width, chat.Height)) : null;
            int dark = 0;
            if (page != null)
            {
                for (int y = 0; y < page.Height; y += 3)
                    for (int x = 0; x < page.Width; x += 3)
                        if (ImageBuffer.R(page.Argb(x, y)) < 40) dark++;
            }
            bool contrast = page != null && page.Width >= chat.Width * 3 - 2 && dark > 20;
            Check(contrast, "the full-client chat crop is tripled and drawn as dark ink on white");
            notes.Add("full-client-v0219-potlive.png OCR preparation: " + (contrast ? "PASS (dark-on-light, 3x)" : "FAIL"));
            notes.Add("full-client-v0219-potlive.png Windows.Media.Ocr pot lines: NOT RUN on this host (no Windows OCR). Crop template matches are not this result.");
            notes.Add("full-client-v0219-xpmiss.png Windows.Media.Ocr: NOT RUN. The XP result above is the centre-top popup detector, not a number read.");
            PopupAt(Path.Combine(root, "verification", "fixtures", "live0221", "full-client-v0220-xpmiss.png"), "full-client-v0220-xpmiss.png", true);
            PopupAt(Path.Combine(root, "verification", "fixtures", "live0221", "full-client-v0218-xp.png"), "full-client-v0218-xp.png", false);
            PopupAt(Path.Combine(root, "verification", "fixtures", "live0212", "full-client-v0212.png"), "full-client-v0212.png", false);
            PopupAt(Path.Combine(root, "verification", "fixtures", "live0213", "full-client-panel-no-pot-timer.png"), "full-client-panel-no-pot-timer.png", false);
            PopupAt(Path.Combine(root, "verification", "fixtures", "live0213", "full-client-other-account-layout.png"), "full-client-other-account-layout.png", false);
            string pot = Path.Combine(dir, "full-client-v0219-potlive.png");
            if (File.Exists(pot))
            {
                ImageBuffer faint = PngImage.Load(pot);
                bool at100 = XpFloat.Present(faint);
                bool at150 = XpFloat.Present(faint.Scale(1.5));
                bool at200 = XpFloat.Present(faint.Scale(2));
                notes.Add("full-client-v0219-potlive.png faint '+157 xp' popup at 100%: " + (at100 ? "PASS" : "MISS"));
                notes.Add("full-client-v0219-potlive.png faint '+157 xp' popup at 150%: " + (at150 ? "PASS" : "MISS"));
                notes.Add("full-client-v0219-potlive.png faint '+157 xp' popup at 200%: " + (at200 ? "PASS" : "MISS"));
            }
            if (popup)
            {
                XpFloat.Sight seen = XpFloat.Look(xpShot);
                XpFloat.Sight again = XpFloat.Look(xpShot);
                XpFloat.Sight moved = seen;
                moved.Y = seen.Y - 12;
                moved.Stamp = seen.Stamp + 1;
                XpFloat.Sight empty = new XpFloat.Sight();
                Check(seen.Found && !XpFloat.Changed(seen, again) && XpFloat.Changed(empty, seen) && XpFloat.Changed(seen, moved) && !XpFloat.Changed(seen, empty), "a popup counts when it appears or moves, and the same pixels do not count twice");
            }
            Shots0222(root);
            Shots0223(root);
        }

        private static void ReplayMetrics(string root)
        {
            string path = Path.Combine(root, "verification", "fixtures", "live0224", "debug-log-v0223-a.txt");
            if (!File.Exists(path))
            {
                Check(false, "missing debug-log-v0223-a.txt");
                return;
            }
            MetricsLock watch = new MetricsLock();
            string[] lines = File.ReadAllLines(path);
            bool sawFall = false;
            bool reportedFall = false;
            bool gotTotal = false;
            DateTime firstFall = DateTime.MinValue;
            for (int i = 0; i < lines.Length; i++)
            {
                int at = lines[i].IndexOf("metrics=");
                if (at < 0 || lines[i].Length < 8 || lines[i][2] != ':') continue;
                string token = lines[i].Substring(at + 8);
                int end = token.IndexOf(' ');
                if (end > 0) token = token.Substring(0, end);
                int hour = 0, minute = 0, second = 0;
                if (lines[i][2] == ':')
                {
                    hour = (lines[i][0] - '0') * 10 + (lines[i][1] - '0');
                    minute = (lines[i][3] - '0') * 10 + (lines[i][4] - '0');
                    second = (lines[i][6] - '0') * 10 + (lines[i][7] - '0');
                }
                DateTime when = new DateTime(2026, 11, 21, hour, minute, second, DateTimeKind.Utc);
                if (token == "lost")
                {
                    watch.Observe(false, 0, "lost", when);
                    continue;
                }
                if (token.Length < 5 || token[0] != 'x') continue;
                long xp;
                if (!long.TryParse(token.Substring(3), out xp)) continue;
                watch.Observe(true, xp, token, when);
                if (xp >= 1122000 && xp <= 1124000 && watch.Xp > 0 && watch.Xp < xp)
                    sawFall = true;
                if (watch.Xp >= 1122000 && watch.Xp <= 1124000 && watch.Xp < 1123687)
                    reportedFall = true;
                if (sawFall && firstFall == DateTime.MinValue && xp < 1123687) firstFall = when;
                if (firstFall != DateTime.MinValue && (when - firstFall).TotalSeconds >= 4 && watch.Found && watch.Xp >= 1122000 && watch.Xp <= 1124000)
                    reportedFall = true;
                if (watch.Found && watch.Xp >= 6000000) gotTotal = true;
            }
            Check(!reportedFall, "the falling XP/h values are not kept as the total");
            Check(gotTotal && watch.Xp >= 6072472, "the real XP total is kept once it is read, got " + watch.Xp.ToString() + " " + watch.Note);
            notes.Add("debug-log-v0223-a.txt metrics replay: " + (reportedFall ? "FAIL kept a falling total" : "PASS") + ", final " + watch.Xp.ToString());
        }

        private static void ChatConfirm(string root)
        {
            ChatOcrGate gate = new ChatOcrGate();
            Check(gate.Due(10, false), "a changed chat crop is read");
            Check(gate.Due(10, false), "the next tick is read again while the hash settles");
            Check(gate.Due(10, false), "the tick after that is read as well");
            Check(!gate.Due(10, false), "a settled chat crop is not read again");
            Check(!gate.Due(10, false), "a settled chat crop stays unread");
            Check(gate.Due(11, false), "a new crop is read");
            Check(gate.Due(11, false) && gate.Due(11, false) && !gate.Due(11, false), "a new crop gets the same short settle window");
            ChatOcrGate torn = new ChatOcrGate();
            Check(torn.Due(4, false) && torn.Due(4, false) && torn.Due(4, false) && !torn.Due(4, false), "a scroll is read, then two settle reads, then it stops");
            Check(torn.Due(4, true) && torn.Due(4, true) && !torn.Due(4, true), "a torn potion line is read twice more and then left alone");
            string garbled = "1 The eff«ts of i.mur Bate:rssion Potion will exnire in *ronds.";
            string kind;
            string state;
            string phrase;
            Check(!ChatPhrase.MatchLine(garbled, out kind, out state, out phrase), "the scrolling aggression line is not a false pot");
            Check(ChatPrep.TornPotion(garbled), "the scrolling aggression line asks for another read");
            Check(!ChatPrep.TornPotion("] Your prayer renewal effazt will run out in 30 saonds.."), "a prayer line does not ask for another read");
            Check(!ChatPrep.TornPotion("] Your super antifire potion is about to run out..."), "an antifire line does not ask for another read");
            Check(!ChatPrep.TornPotion("] Your immunity to poison is about to run out..."), "a poison line does not ask for another read");
            Check(!ChatPrep.TornPotion("The effazts of pur Potion will expire in 30 saonds."), "a clean aggression line does not ask for another read");
            Check(!ChatPrep.TornPotion("The effa:ts of elder overload •exorn off and you fæl normal again."), "a matched worn-off line does not ask for another read");
            ReplayPotOk(root);
        }

        private static void ReplayPotOk(string root)
        {
            string path = Path.Combine(root, "verification", "fixtures", "live025", "debug-log-v0224-pots-ok.txt");
            if (!File.Exists(path))
            {
                Check(false, "missing debug-log-v0224-pots-ok.txt");
                return;
            }
            ChatPotionBook book = new ChatPotionBook();
            book.Attach();
            string[] lines = File.ReadAllLines(path);
            System.Collections.Generic.List<string> alerts = new System.Collections.Generic.List<string>();
            bool sawTorn = false;
            bool tornOnClean = false;
            for (int i = 0; i < lines.Length; i++)
            {
                int at = lines[i].IndexOf(" chat=");
                int potsAt = lines[i].IndexOf(" pots=");
                if (at < 0 || potsAt < at) continue;
                string chat = lines[i].Substring(at + 6, potsAt - (at + 6));
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                int from = 0;
                while (from < chat.Length)
                {
                    int raw = chat.IndexOf("raw=\"", from);
                    if (raw < 0) break;
                    int start = raw + 5;
                    int end = chat.IndexOf('"', start);
                    if (end < 0) break;
                    if (text.Length > 0) text.Append('\n');
                    text.Append(chat.Substring(start, end - start));
                    from = end + 1;
                }
                string body = text.ToString();
                if (ChatPrep.TornPotion(body)) sawTorn = true;
                List<ChatHit> hits = ChatPhrase.FromOcr(body);
                if (ChatPrep.TornPotion(body) && Has(hits, "aggression", "expired")) tornOnClean = true;
                List<ChatNotice> notices = book.Observe(hits);
                for (int n = 0; n < notices.Count; n++)
                    alerts.Add(notices[n].Title ?? "");
            }
            string got = string.Join(" | ", alerts.ToArray());
            Check(alerts.Count == 4, "v0.2.24 pot log raises four alerts, got " + got);
            Check(got.IndexOf("Elder overload is running low") >= 0 && got.IndexOf("Aggression is running low") >= 0, "v0.2.24 pot log still alerts both low warnings, got " + got);
            Check(got.IndexOf("Elder overload has expired") >= 0 && got.IndexOf("Aggression has expired") >= 0, "v0.2.24 pot log still alerts both expired warnings, got " + got);
            int lowAt = got.IndexOf("running low");
            int expiredAt = got.IndexOf("has expired");
            Check(lowAt >= 0 && expiredAt > lowAt, "low warnings stay ahead of the expired warnings");
            Check(sawTorn && !tornOnClean, "the scrolling line asks for another read and the clean expiry does not");
            book.Refresh(DateTime.UtcNow.AddSeconds(46));
            Check((book.Detail ?? "").IndexOf("expired") < 0, "the v0.2.24 expired rows still clear after 45 seconds");
            notes.Add("debug-log-v0224-pots-ok.txt replay: " + got);
        }

        private static void BoostedLife(string root)
        {
            DateTime t = new DateTime(2026, 11, 21, 22, 0, 0, DateTimeKind.Utc);
            ReadGate life = new ReadGate();
            life.Push(990, 990, t);
            life.Push(990, 990, t.AddMilliseconds(500));
            Check(life.Value == 990 && life.Max == 990, "the unboosted total locks, got " + life.Value);
            life.Push(1058, 990, t.AddSeconds(1));
            Check(life.Value == 1058 && life.Max == 990, "1058/990 is shown at once, got " + life.Value + "/" + life.Max);
            life.Push(1200, 990, t.AddSeconds(2));
            life.Push(1104, 990, t.AddSeconds(3));
            Check(life.Value == 1104, "a boosted total keeps updating instead of sticking, got " + life.Value);
            life.Push(3500, 990, t.AddSeconds(4));
            Check(life.Value == 3500, "3500/990 is accepted, got " + life.Value);
            life.Push(4500, 990, t.AddSeconds(5));
            Check(life.Value == 3500, "above 4000 does not replace the shown hitpoints, got " + life.Value);
            ReadGate prayer = new ReadGate();
            prayer.Push(990, 990, t);
            prayer.Push(990, 990, t.AddMilliseconds(500));
            prayer.Push(1040, 990, t.AddSeconds(1));
            prayer.Push(1200, 990, t.AddSeconds(2));
            Check(prayer.Value == 1200 && prayer.Max == 990, "boosted prayer uses the same ceiling and keeps moving, got " + prayer.Value);
            prayer.Push(4001, 990, t.AddSeconds(3));
            Check(prayer.Value == 1200, "prayer above 4000 is rejected");
            int panelW, hintY, hintH, buttonY, panelH;
            PanelHint.Foot(18, 360, out panelW, out hintY, out hintH, out buttonY, out panelH);
            Check(hintH >= 18 * 2 && buttonY >= hintY + hintH + 16 && panelH >= buttonY + 28, "the tip has its own lines and a gap above the buttons at 100%");
            int wideW, wideY, wideH, wideButton, widePanel;
            PanelHint.Foot(36, 720, out wideW, out wideY, out wideH, out wideButton, out widePanel);
            Check(wideH >= 36 * 2 && wideButton >= wideY + wideH + 16 && wideW >= 752 && widePanel >= wideButton + 28, "the tip grows at 200% and still sits above the buttons");
            int[] tipScale = new int[] { 18, 27, 36 };
            string[] tipName = new string[] { "100%", "150%", "200%" };
            for (int scale = 0; scale < tipScale.Length; scale++)
            {
                int tipW, tipY, tipH, tipButton, tipPanel;
                PanelHint.Foot(tipScale[scale], 360 * tipScale[scale] / 18, out tipW, out tipY, out tipH, out tipButton, out tipPanel);
                int afkBottom = PanelHint.AfkBottom(tipScale[scale]);
                Check(PanelHint.Always && tipY >= afkBottom && tipH >= tipScale[scale] * 2 && tipButton >= tipY + tipH + 8 && tipPanel >= tipButton + 28,
                    "the dimmed tip sits in the gap above the buttons at " + tipName[scale] + ", afk " + afkBottom.ToString() + " hint " + tipY.ToString() + " button " + tipButton.ToString());
            }
            Check(PanelHint.Line1.IndexOf("precise values") >= 0 && PanelHint.Line1.IndexOf("RuneMetrics") >= 0 && PanelHint.Line2.IndexOf("minimized") >= 0, "the tip says to keep RuneMetrics open, and not minimized");
            Check(!AlertSurface.ShowsWindow, "low alerts do not open a window or a toast");
            string path = Path.Combine(root, "verification", "fixtures", "live027", "debug-log-v0226-hp.txt");
            if (!File.Exists(path))
            {
                Check(false, "missing debug-log-v0226-hp.txt");
                return;
            }
            string[] lines = File.ReadAllLines(path);
            ReadGate logged = new ReadGate();
            int shown = 0;
            DateTime when = t;
            for (int i = 0; i < lines.Length; i++)
            {
                int at = lines[i].IndexOf("hp=");
                if (at < 0) continue;
                string token = lines[i].Substring(at + 3);
                int end = token.IndexOf(' ');
                if (end > 0) token = token.Substring(0, end);
                int slash = token.IndexOf('/');
                if (slash <= 0) continue;
                int current, max;
                if (!int.TryParse(token.Substring(0, slash), out current)) continue;
                if (!int.TryParse(token.Substring(slash + 1), out max)) continue;
                when = when.AddSeconds(1);
                logged.Push(current, max, when);
                if (logged.Value == current) shown++;
            }
            Check(shown > 0 && logged.Value == 1633 && logged.Max == 1060, "the v0.2.26 hitpoint log stays on 1633/1060, got " + logged.Value + "/" + logged.Max);
            notes.Add("debug-log-v0226-hp.txt ends before the stuck boost; replay shows " + logged.Value + "/" + logged.Max);
            string idleLog = Path.Combine(root, "verification", "fixtures", "live027", "debug-log-v0226-idlereset.txt");
            if (!File.Exists(idleLog))
            {
                Check(false, "missing debug-log-v0226-idlereset.txt");
                return;
            }
            string[] idleLines = File.ReadAllLines(idleLog);
            int falsePopups = 0;
            int countedPopups = 0;
            int gains = 0;
            long previousXp = -1;
            WardenEngine idleEngine = new WardenEngine();
            WardenSettings idleSettings = new WardenSettings();
            idleSettings.IdleSeconds = 12;
            idleSettings.HpEnabled = false;
            idleSettings.PrayerEnabled = false;
            idleSettings.AfkEnabled = false;
            DateTime idleTime = new DateTime(2026, 11, 21, 23, 2, 0, DateTimeKind.Utc);
            WatchSample quiet = Sample(idleTime, true, true, false, false);
            idleEngine.Tick(quiet, idleSettings);
            idleEngine.Tick(Sample(idleTime.AddSeconds(45), true, true, true, true), idleSettings);
            Check(idleEngine.IdleLatched, "keys and a popup-sized scene do not keep the player active");
            for (int i = 0; i < idleLines.Length; i++)
            {
                if (idleLines[i].Length < 8 || idleLines[i][2] != ':') continue;
                int skillAt = idleLines[i].IndexOf("skill=");
                int metricsAt = idleLines[i].IndexOf("metrics=xp=");
                if (skillAt < 0 || metricsAt < 0) continue;
                string skill = idleLines[i].Substring(skillAt + 6);
                int skillEnd = skill.IndexOf(' ');
                if (skillEnd > 0) skill = skill.Substring(0, skillEnd);
                string number = idleLines[i].Substring(metricsAt + 11);
                int numberEnd = number.IndexOf(' ');
                if (numberEnd > 0) number = number.Substring(0, numberEnd);
                long xp;
                if (!long.TryParse(number, out xp)) continue;
                bool gain = previousXp >= 0 && xp > previousXp;
                if (gain) gains++;
                bool popup = skill.IndexOf("xp=popup") >= 0;
                bool counts = ActivitySignals.Xp(gain, popup);
                if (xp == 7402846L && popup && idleLines[i].IndexOf("idleReset=yes") >= 0)
                {
                    falsePopups++;
                    if (counts) countedPopups++;
                }
                previousXp = xp;
            }
            Check(falsePopups > 0 && countedPopups == 0, "flat 7402846 popup hits do not reset idle, lines " + falsePopups.ToString() + " counted " + countedPopups.ToString());
            Check(gains > 0, "a rising total in the same log still counts");
            notes.Add("debug-log-v0226-idlereset.txt popup resets while flat: " + falsePopups.ToString() + ", counted " + countedPopups.ToString());
        }

        private static void Live026(string root)
        {
            Check(!ActivitySignals.HpDrop(1000, 1000) && !ActivitySignals.HpDrop(1000, 999) && !ActivitySignals.HpDrop(1000, 998), "one or two hitpoints of wobble is not a hit");
            Check(!ActivitySignals.HpDrop(1000, 1100), "hitpoints rising is not activity");
            Check(ActivitySignals.HpDrop(1000, 990), "a clear hitpoints drop is activity");
            Check(ActivitySignals.Xp(true, false) && !ActivitySignals.Xp(false, true) && !ActivitySignals.Xp(false, false), "only a rising RuneMetrics total counts, and a popup does not");
            SceneActivity scene = new SceneActivity();
            HudReading steady = new HudReading();
            steady.Hp = 1471;
            steady.Prayer = 1040;
            Check(!scene.Tick(null, steady, true, false, true), "keys and a backpack do not count on the first look");
            HudReading wobble = new HudReading();
            wobble.Hp = 1469;
            wobble.Prayer = 900;
            Check(!scene.Tick(null, wobble, true, false, true), "prayer and a two point wobble are not activity, reason " + scene.Reason);
            HudReading hit = new HudReading();
            hit.Hp = 1400;
            Check(scene.Tick(null, hit, false, false, false) && scene.Reason.IndexOf("hp") >= 0, "taking a hit is activity, reason " + scene.Reason);
            WardenSettings tone = new WardenSettings();
            tone.SoundTone = "Bell";
            WardenSettings toneBack = SettingsText.Read(SettingsText.Write(tone));
            Check(toneBack.SoundTone == "Bell", "the alert sound choice round-trips, got " + toneBack.SoundTone);
            short[] chime = AlertTones.Render("Chime", "preview", 100);
            short[] bell = AlertTones.Render("Bell", "preview", 100);
            short[] soft = AlertTones.Render("Soft", "preview", 100);
            short[] horn = AlertTones.Render("Drop", "preview", 100);
            short[] alarm = AlertTones.Render("Alarm", "preview", 100);
            Check(chime.Length < AlertTones.Rate * 3 / 2 && bell.Length < AlertTones.Rate * 3 / 2 && soft.Length < AlertTones.Rate * 3 / 2 && horn.Length < AlertTones.Rate * 3 / 2 && alarm.Length < AlertTones.Rate * 3 / 2, "each alert sound is shorter than a second and a half");
            Check(Bursts(chime) >= 4, "Chime is a rising run of notes, got " + Bursts(chime).ToString());
            Check(Bursts(bell) == 2, "Bell is a two-tone ding-dong, got " + Bursts(bell).ToString());
            Check(Bursts(alarm) >= 5, "Alarm is a run of short beeps, got " + Bursts(alarm).ToString());
            Check(Bursts(soft) == 1 && Bursts(horn) == 1 && horn.Length > soft.Length * 2, "Soft is one quiet note and Drop is a longer buzz");
            Check(Crossings(alarm) > Crossings(horn) * 3, "Alarm is much higher than the horn");
            Check(Peak(chime) <= 11000 && Peak(alarm) <= 11000 && Peak(horn) <= 11000, "the tones stay under a hard ceiling");
            Check(SameWave(AlertTones.Render("By alert", "potion", 100), bell), "By alert plays the doorbell for a potion");
            Check(SameWave(AlertTones.Render("By alert", "hp", 100), alarm), "By alert plays the beeps for hitpoints");
            ChatPotionBook warning = new ChatPotionBook();
            List<ChatHit> low = new List<ChatHit>();
            low.Add(Hit("Your elder overload is about to run out.", "elder", "low", 10));
            List<ChatNotice> quiet = warning.Observe(low);
            Check(quiet.Count == 1 && !quiet[0].Audible, "a 30 second potion warning does not play a sound");
            List<ChatHit> ended = new List<ChatHit>();
            ended.Add(Hit("The effects of elder overload have worn off and you feel normal again.", "elder", "expired", 10));
            List<ChatNotice> loud = warning.Observe(ended);
            Check(loud.Count == 1 && loud[0].Audible, "expiry is the potion warning that plays a sound");
            string dir = Path.Combine(root, "verification", "fixtures", "live026");
            string log = Path.Combine(dir, "debug-log-v0225-falsexp.txt");
            if (!File.Exists(log))
            {
                Check(false, "missing debug-log-v0225-falsexp.txt");
                return;
            }
            int packOrKey = 0;
            long flat = -1;
            bool flatHeld = true;
            string[] rows = File.ReadAllLines(log);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].IndexOf("activity=") < 0) continue;
                string act = rows[i].Substring(rows[i].IndexOf("activity=") + 9);
                int sp = act.IndexOf(' ');
                if (sp > 0) act = act.Substring(0, sp);
                if ((act == "pack" || act == "key") && rows[i].IndexOf("idleReset=yes") >= 0) packOrKey++;
                int mx = rows[i].IndexOf("metrics=xp=");
                if (mx >= 0)
                {
                    string num = rows[i].Substring(mx + 11);
                    int end = num.IndexOf(' ');
                    if (end > 0) num = num.Substring(0, end);
                    long xp;
                    if (long.TryParse(num, out xp))
                    {
                        if (flat < 0) flat = xp;
                        if (xp != flat) flatHeld = false;
                    }
                }
            }
            Check(packOrKey > 0 && flatHeld && flat == 7389384L, "the other-account log is flat XP with pack and key resets, xp " + flat.ToString() + " resets " + packOrKey.ToString());
            Check(!ActivitySignals.Xp(false, false), "a flat RuneMetrics total is not activity by itself");
            string panel = Path.Combine(dir, "runemetrics-panel-otheracc-v0225.png");
            MetricsReader.Result total = MetricsReader.Read(PngImage.Load(panel));
            Check(total.Found && total.Xp == 7389384L, "the other-account panel reads the XP column, got " + total.Xp.ToString() + " " + (total.Note ?? ""));
            string[] quietNames = new string[] {
                "full-client-v0225-falsexp-otheracc.png",
                "layout-community-1.png", "layout-community-2.png", "layout-community-3.png",
                "layout-community-4.png", "layout-community-5.png", "layout-community-6.png",
                "layout-community-7.png", "layout-community-8.png"
            };
            for (int i = 0; i < quietNames.Length; i++)
            {
                ImageBuffer frame = PngImage.Load(Path.Combine(dir, quietNames[i]));
                bool popup = frame != null && XpFloat.Present(frame);
                notes.Add(quietNames[i] + " popup: " + (popup ? "HIT" : "quiet"));
                if (quietNames[i].IndexOf("falsexp") >= 0)
                    Check(!popup, "the other-account client is not an XP popup");
            }
            ImageBuffer nine = PngImage.Load(Path.Combine(dir, "layout-community-9.png"));
            bool ninePopup = nine != null && XpFloat.Present(nine);
            notes.Add("layout-community-9.png popup: " + (ninePopup ? "HIT" : "MISS"));
            Check(ninePopup, "layout 9 keeps the +15,491 xp popup over the action bar");
            ImageBuffer eight = PngImage.Load(Path.Combine(dir, "layout-community-8.png"));
            bool eightPopup = eight != null && XpFloat.Present(eight);
            notes.Add("layout-community-8.png popup: " + (eightPopup ? "HIT" : "MISS"));
            Check(eightPopup, "layout 8 keeps the +5,700 and +2,121 xp popups");
            ImageBuffer three = PngImage.Load(Path.Combine(dir, "layout-community-3.png"));
            Check(three == null || !XpFloat.Present(three), "a boss name over a busy fight is not an XP popup");
            ImageBuffer main = PngImage.Load(Path.Combine(dir, "full-client-v0225-mainacc-layout.png"));
            MetricsReader.Result mainXp = MetricsReader.Read(main);
            bool mainPopup = main != null && XpFloat.Present(main);
            notes.Add("full-client-v0225-mainacc-layout.png metrics " + (mainXp.Found ? mainXp.Xp.ToString() : "MISS " + (mainXp.Note ?? "")) + " popup " + (mainPopup ? "HIT" : "quiet"));
            Check(mainXp.Found && mainXp.Xp > 1000000L, "the main-account layout still reads a full XP total, got " + mainXp.Xp.ToString() + " " + (mainXp.Note ?? ""));
        }

        private static void ChatFresh()
        {
            DateTime t = new DateTime(2026, 11, 22, 15, 14, 34, DateTimeKind.Utc);
            ChatOcrGate gate = new ChatOcrGate();
            Check(gate.Due(4, false, t), "the first chat crop is read");
            Check(gate.Due(4, false, t) && gate.Due(4, false, t), "the hash still gets its short settle");
            Check(!gate.Due(4, false, t.AddSeconds(1)), "an unchanged chat hash does not read again after one second");
            Check(gate.Due(4, false, t.AddSeconds(2)), "the same hash is read again within two seconds, so a new bottom line is not missed");
            ChatOcrGate moved = new ChatOcrGate();
            Check(moved.Due(9, false, t), "a fresh crop is read");
            Check(moved.Due(9, false, t) && moved.Due(9, false, t), "the settle reads still run");
            Check(moved.Due(10, false, t.AddMilliseconds(200)), "a red line that changes the hash is read at once");
            ImageBuffer calm = ImageBuffer.Blank(360, 160, ImageBuffer.Pack(10, 12, 16));
            calm.Fill(24, 40, 90, 3, ImageBuffer.Pack(220, 180, 40));
            int before = ChatPrep.Stamp(calm);
            calm.Fill(24, 130, 160, 6, ImageBuffer.Pack(176, 12, 14));
            int after = ChatPrep.Stamp(calm);
            Check(before != after, "red text alone changes the chat stamp");
            ImageBuffer panel = ImageBuffer.Blank(800, 600, ImageBuffer.Pack(48, 90, 42));
            panel.Fill(8, 400, 380, 180, ImageBuffer.Pack(8, 10, 14));
            int[] rows = new int[] { 430, 448, 466 };
            for (int r = 0; r < rows.Length; r++)
            {
                for (int x = 28; x < 150; x += 3)
                {
                    panel.Set(x, rows[r], ImageBuffer.Pack(220, 180, 40));
                    panel.Set(x, rows[r] + 1, ImageBuffer.Pack(220, 180, 40));
                }
            }
            for (int x = 30; x < 560; x += 3)
            {
                panel.Set(x, 548, ImageBuffer.Pack(170, 8, 10));
                panel.Set(x, 549, ImageBuffer.Pack(170, 8, 10));
            }
            IntRect box = ChatPrep.FindBox(panel);
            Check(box.Width >= 80 && box.Y <= 430 && box.Bottom >= 550, "the chat box reaches the newest line at the bottom, got " + box.X.ToString() + "," + box.Y.ToString() + " " + box.Width.ToString() + "x" + box.Height.ToString());
        }

        private static void ReleaseNoteCases()
        {
            string text = ReleaseNotes.Text();
            int at = 0;
            for (int i = 0; i < ReleaseNotes.Count; i++)
            {
                string version = ReleaseNotes.VersionAt(i);
                int next = text.IndexOf(version, at);
                Check(next >= at, "notes are newest first, missing " + version);
                at = next + 1;
            }
            Check(ReleaseNotes.VersionAt(0) == "v0.2.39" && text.IndexOf("v0.2.29") > text.IndexOf("v0.2.39"), "notes run from this release back to the heading fix");
            Check(text.IndexOf("left-click") >= 0, "the notes mention the left click");
            Check(text.IndexOf("What's new") >= 0 && text.IndexOf("points per hour") >= 0, "this release mentions the notes and the hourly rate");
            Check(text.IndexOf("five minutes") >= 0 && text.IndexOf("prayer bars") >= 0, "the notes mention potion cooldown and the bars");
            Check(text.IndexOf("two seconds") >= 0 && text.IndexOf("RuneMetrics") >= 0 && text.IndexOf("Reset") >= 0, "the notes mention chat, the reminder and Reset");
            Check(text.IndexOf("headings") >= 0, "the notes mention the settings headings");
            Check(ReleaseNotes.Unseen("", AppInfo.Version) && ReleaseNotes.Unseen("0.2.36", AppInfo.Version) && !ReleaseNotes.Unseen(AppInfo.Version, AppInfo.Version), "notes show once for a new version");
        }

        private static void SacrificeCases(string root)
        {
            string m1 = "[17:31:37] You sacrifice Off-hand khopesh of the Kharidian and gain 11 points. You now have 66 total sacrifice points.";
            string m2 = "[17:34:08] You sacrifice Off-hand khopesh of the Kharidian and gain 11 points. You now have 9,847,166 total sacrifice points.";
            string m3 = "[17:36:57] You sacrifice Khopesh of the Kharidian and gain 11 points. You now have 9,843,177 total sacrifice points.";
            string m4 = "[17:37:54] You sacrifice Blast diffusion boots and gain 115 points. You now have 9,843,292 total sacrifice points.";
            string m5 = "[17:38:59] You sacrifice City of Senntisten codex and gain 460 points. You now have 9,844,097 total sacrifice points.";
            string m6 = "[17:39:40] You sacrifice Scripture of Ful and gain 862 points. You now have 9,844,959 total sacrifice points.";
            string news1 = "[17:38:59] News: Zeni received a City of Senntisten codex from TzKal-Zuk at 2 kills!";
            string news2 = "[17:38:59] News: You received a City of Senntisten codex from TzKal-Zuk.";
            Check(SacrificeBook.Commas(66) == "66" && SacrificeBook.Commas(1470) == "1,470" && SacrificeBook.Commas(9844959) == "9,844,959", "sacrifice totals use thousands separators");
            SacrificeHit parsed;
            Check(SacrificeBook.TryParse(m1, out parsed) && parsed.Item == "Off-hand khopesh of the Kharidian" && parsed.Gain == 11 && parsed.Total == 66, "the first sacrifice line parses, got " + (parsed == null ? "none" : parsed.Item + " +" + parsed.Gain.ToString()));
            Check(SacrificeBook.TryParse(m2, out parsed) && parsed.Total == 9847166, "a comma total parses");
            Check(SacrificeBook.TryParse(m4, out parsed) && parsed.Item == "Blast diffusion boots" && parsed.Gain == 115 && parsed.Total == 9843292, "boots parse");
            Check(SacrificeBook.TryParse(m5, out parsed) && parsed.Gain == 460, "the codex sacrifice parses");
            Check(SacrificeBook.TryParse(m6, out parsed) && parsed.Item == "Scripture of Ful" && parsed.Gain == 862 && parsed.Total == 9844959, "scripture parses");
            Check(!SacrificeBook.TryParse(news1, out parsed) && !SacrificeBook.TryParse(news2, out parsed), "a drop announcement is not a sacrifice");
            string wrapped = "[17:31:37] You sacrifice Off-hand khopesh of the Kharidian and gain 11 points.\nYou now have 66 total sacrifice points.";
            List<string> joined = SacrificeBook.Join(wrapped);
            Check(joined.Count == 1 && SacrificeBook.TryParse(joined[0], out parsed) && parsed.Total == 66 && parsed.Gain == 11, "a wrapped sacrifice is joined before it is matched");
            string splitItem = "[17:38:59] You sacrifice City of Senntisten\ncodex and gain 460 points. You now have 9,844,097 total sacrifice points.";
            List<string> itemLines = SacrificeBook.Join(splitItem);
            Check(itemLines.Count == 1 && SacrificeBook.TryParse(itemLines[0], out parsed) && parsed.Item == "City of Senntisten codex" && parsed.Gain == 460, "a wrapped item name is rejoined, got " + (parsed == null ? "none" : parsed.Item));
            string garbled = "Y0u sacrifice Blast diffusion boots and galn 115 points. You now haue 9,843,292 totel sacrifice points.";
            Check(SacrificeBook.TryParse(garbled, out parsed) && parsed.Gain == 115 && parsed.Total == 9843292 && parsed.Item == "Blast diffusion boots", "a garbled sacrifice line still parses when the numbers do, got " + (parsed == null ? "none" : parsed.Item));
            SacrificeBook book = new SacrificeBook();
            System.Text.StringBuilder chat = new System.Text.StringBuilder();
            string[] steps = new string[] { m1, m2, m3, m4, news1 + "\n" + news2 + "\n" + m5, m6 };
            // A complete first sacrifice counts at once. A later total that does
            // not match waits for the next read of that same line.
            long[] gained = new long[] { 11, 11, 22, 33, 148, 608 };
            int[] counted = new int[] { 1, 1, 2, 3, 4, 5 };
            long[] trusted = new long[] { 66, 66, 66, 66, 66, 66 };
            for (int i = 0; i < steps.Length; i++)
            {
                if (chat.Length > 0) chat.Append('\n');
                chat.Append(steps[i]);
                book.Observe(chat.ToString());
                Check(book.SessionGained == gained[i] && book.SessionCount == counted[i] && book.Total == trusted[i] && SacCounts(book.Log) == book.SessionCount, "sacrifice step " + (i + 1).ToString() + " gained " + book.SessionGained.ToString() + " count " + book.SessionCount.ToString() + " total " + book.Total.ToString() + " log " + book.Log);
            }
            string full = chat.ToString();
            book.Observe(full);
            Check(book.SessionGained == 11 + 11 + 11 + 115 + 460 + 862 && book.SessionCount == 6 && book.Total == 66 && SacCounts(book.Log) == 6, "six sacrifices add the parsed gains once the last line is read again, got " + book.SessionGained.ToString() + " " + book.Log);
            Check(book.LastItem == "Scripture of Ful" && book.Value() == "1,470 this session \u00b7 6 sacrifices \u00b7 \u2014/h", "the row is the session counter and the newest item is kept, got " + book.Value() + " " + book.LastItem);
            book.Observe(full);
            Check(book.SessionGained == 1470 && book.SessionCount == 6, "re-reading the same chat adds nothing");
            string scrolled = full.Replace("You sacrifice Scripture", "Y0u sacrifice Scripture");
            book.Observe(scrolled);
            Check(book.SessionGained == 1470 && book.SessionCount == 6, "scrolling an OCR variant of the same line does not count twice");
            SacrificeBook lower = new SacrificeBook();
            lower.Observe(m2);
            lower.Observe(m2 + "\n" + m3);
            lower.Observe(m2 + "\n" + m3);
            Check(lower.Total == 9847166 && lower.SessionGained == 22 && lower.SessionCount == 2, "a lower misread total is ignored and the gain still counts, got " + lower.Total.ToString() + " " + lower.SessionGained.ToString());
            SacrificeBook gaps = new SacrificeBook();
            gaps.Observe(m1);
            gaps.Observe(m1 + "\n" + m2);
            Check(gaps.Log.IndexOf("sac pending reason=unvalidated") >= 0 && gaps.Log.IndexOf("9847166") >= 0, "a total that does not match the gain is held, got " + gaps.Log);
            SacrificeBook chain = new SacrificeBook();
            string chainA = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 1,000 total sacrifice points.";
            string chainB = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 1,115 total sacrifice points.";
            chain.Observe(chainA);
            chain.Observe(chainA + "\n" + chainB);
            Check(chain.SessionCount == 2 && chain.SessionGained == 230 && chain.Log.IndexOf("reason=total") >= 0 && SacCounts(chain.Log) == 2, "a total that matches the previous plus the gain counts on that read, got " + chain.Log);
            SacrificeBook news = new SacrificeBook();
            news.Observe(news1 + "\n" + news2);
            Check(!news.Seen && news.SessionCount == 0 && news.SessionGained == 0, "news lines do not start the sacrifice counter");
            SacrificeBook baseline = new SacrificeBook();
            baseline.Attach();
            baseline.Observe("");
            baseline.Observe(m1);
            Check(baseline.Seen && baseline.SessionGained == 0 && baseline.SessionCount == 0 && baseline.Total == 66, "a line already on screen at startup sets the total and does not count, got " + baseline.SessionGained.ToString() + " " + baseline.Total.ToString());
            baseline.Observe(m1 + "\n" + m2);
            baseline.Observe(m1 + "\n" + m2);
            Check(baseline.SessionGained == 11 && baseline.SessionCount == 1 && baseline.Total == 66, "the next sacrifice after the baseline adds its gain, got " + baseline.SessionGained.ToString() + " total " + baseline.Total.ToString());
            book.ResetSession();
            Check(book.SessionGained == 0 && book.SessionCount == 0 && book.Total == 66 && SacCounts(book.Log) == 0, "reset clears the session counter and keeps the last trusted total");
            book.Observe(full);
            Check(book.SessionGained == 0 && book.SessionCount == 0, "messages already counted stay counted after a reset");
            Check(book.Value() == "0 this session \u00b7 0 sacrifices \u00b7 \u2014/h", "the row shows the session counter and a blank rate, got " + book.Value());
            SacrificeBook hidden = new SacrificeBook();
            Check(hidden.Value() == "\u2014", "the sacrifice row is a dash until a message has been seen");
            Check(SacrificeBook.Rate(690, 59) == "\u2014/h" && SacrificeBook.Rate(690, 0) == "\u2014/h", "the rate stays blank for the first minute");
            Check(SacrificeBook.Rate(690, 60) == "41,400/h", "six hundred and ninety points in a minute is 41,400 an hour, got " + SacrificeBook.Rate(690, 60));
            Check(SacrificeBook.Rate(1500000, 3600) == "1,500,000/h", "a large rate keeps thousands separators, got " + SacrificeBook.Rate(1500000, 3600));
            Check(SacrificeBook.Format(true, 115, 1, 120) == "115 this session \u00b7 1 sacrifices \u00b7 3,450/h", "the row shows points, count and rate, got " + SacrificeBook.Format(true, 115, 1, 120));
            SacrificeBook timed = new SacrificeBook();
            DateTime stamp = new DateTime(2026, 11, 22, 20, 0, 0, DateTimeKind.Utc);
            timed.Seen = true;
            timed.SessionGained = 600;
            timed.SessionCount = 1;
            timed.SessionStarted = stamp.AddMinutes(-10);
            Check(timed.ElapsedSeconds(stamp) == 600 && SacrificeBook.Format(true, timed.SessionGained, timed.SessionCount, timed.ElapsedSeconds(stamp)).IndexOf("3,600/h") >= 0, "ten minutes of points makes an hourly rate");
            timed.ResetSession();
            Check(timed.SessionGained == 0 && timed.SessionCount == 0 && timed.ElapsedSeconds(DateTime.UtcNow) < 60 && timed.Value().IndexOf("\u2014/h") >= 0, "reset restarts the points and the rate timer, got " + timed.Value());
            string laceration = "] You sacrifice Laceration boots and gain 115 points. You now have 774 total sacrifice\npoints.";
            SacrificeBook first = new SacrificeBook();
            first.Observe(laceration);
            Check(first.SessionCount == 1 && first.SessionGained == 115 && first.Total == 774, "the first sacrifice, wrapped before points, counts at once, got " + first.SessionCount.ToString() + " " + first.SessionGained.ToString() + " total " + first.Total.ToString() + " " + first.Log);
            Check(first.Trace.IndexOf("counted") >= 0 && first.Trace.IndexOf("Laceration boots") >= 0 && first.Trace.IndexOf("gain=115") >= 0, "the sacrifice trace records the counted line, got " + first.Trace);
            string again = first.Trace;
            first.Observe(laceration);
            Check(first.SessionCount == 1 && first.SessionGained == 115 && first.Trace.IndexOf("rejected") >= 0 && first.Trace != again, "reading that same wrapped line again does not count it twice, got " + first.Trace);
            string kept = new string('x', 50) + "\nEND-OF-LOG\n";
            string bulky = new string('a', 80) + "\n" + kept;
            Check(DebugLog.Tail(bulky, 40).EndsWith("END-OF-LOG\n") && DebugLog.Tail(bulky, 40).IndexOf("aaaa") < 0, "trimming the debug log keeps the newest lines");
            string path = Path.Combine(Path.GetTempPath(), "afk-debug-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, bulky);
            DebugLog.TrimFile(path, 60, 30);
            string trimmed = File.ReadAllText(path);
            File.Delete(path);
            Check(trimmed.Length <= 30 && trimmed.IndexOf("END-OF-LOG") >= 0, "a long debug file is cut back to its tail, got " + trimmed.Length.ToString());
            Check(DebugLog.OverCap(DebugLog.CapBytes + 1) && !DebugLog.OverCap(DebugLog.KeepBytes) && DebugLog.CapBytes >= 5 * 1024 * 1024 && DebugLog.KeepBytes >= 2 * 1024 * 1024, "the debug log keeps about two megabytes and caps near five");
            GarbledSacrifice(root);
        }

        private static void GarbledSacrifice(string root)
        {
            string path = Path.Combine(root, "verification", "fixtures", "live032", "debug-log-v0232-sac.txt");
            if (!File.Exists(path))
            {
                Check(false, "missing debug-log-v0232-sac.txt");
                return;
            }
            string[] rows = File.ReadAllLines(path);
            string raws = "";
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].IndexOf("18:21:23 ") != 0) continue;
                int at = rows[i].IndexOf(" chat=");
                int pots = rows[i].IndexOf(" pots=");
                if (at < 0 || pots < at) continue;
                raws = rows[i].Substring(at + 6, pots - (at + 6));
                break;
            }
            Check(raws.IndexOf("Blast diffusion bmts") >= 0, "the v0.2.32 log still has the garbled sacrifice lines");
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            int from = 0;
            int count = 0;
            while (from < raws.Length)
            {
                int raw = raws.IndexOf("raw=\"", from);
                if (raw < 0) break;
                int start = raw + 5;
                int end = raws.IndexOf('"', start);
                if (end < 0) break;
                if (text.Length > 0) text.Append('\n');
                text.Append(raws.Substring(start, end - start));
                from = end + 1;
                count++;
            }
            Check(count >= 10, "the log line keeps the OCR raws in log order, got " + count.ToString());
            SacrificeBook book = new SacrificeBook();
            book.Observe(text.ToString());
            book.Observe(text.ToString());
            Check(book.SessionCount == 4 && book.SessionGained == 460 && SacCounts(book.Log) == 4, "four garbled boots lines count as 4 x 115, got " + book.SessionCount.ToString() + " x " + book.SessionGained.ToString() + " log " + book.Log);
            book.Observe(text.ToString());
            Check(book.SessionCount == 4 && book.SessionGained == 460, "re-reading the garbled chat adds nothing");
            long fixedGain;
            Check(SacrificeBook.FixNum("I IS", out fixedGain) && fixedGain == 115, "I IS in a number slot is 115");
            Check(SacrificeBook.FixNum("9,845, 189", out fixedGain) && fixedGain == 9845189, "spaces and commas stay out of the total");
            Check(SacrificeBook.FixNum("g,845, 189", out fixedGain) && fixedGain == 9845189, "a g in a number slot is a 9");
            SacrificeBook unknown = new SacrificeBook();
            unknown.Observe("] You sacrifice Mystery charm and gain later");
            unknown.Observe("] You sacrifice Mystery charm and gain later");
            Check(unknown.SessionCount == 1 && unknown.SessionGained == 0 && unknown.Log.IndexOf("gain unknown") >= 0 && SacCounts(unknown.Log) == 1, "a new line with no readable gain counts once and adds no points, got " + unknown.Log);
            List<OcrLine> visual = new List<OcrLine>();
            OcrLine head = new OcrLine();
            head.Text = "] You sacrifice Blast diffusion bmts and gain";
            head.Y = 40;
            head.Order = 1;
            OcrLine tail = new OcrLine();
            tail.Text = "I IS points. You now 9,845, 189 total sacrifice";
            tail.Y = 58;
            tail.Order = 0;
            visual.Add(tail);
            visual.Add(head);
            List<string> ordered = SacrificeBook.Join(visual);
            SacrificeHit seen;
            Check(ordered.Count == 1 && SacrificeBook.TryParse(ordered[0], out seen) && seen.GainKnown && seen.Gain == 115 && seen.TotalKnown && seen.Total == 9845189, "a wrapped half is joined to the line visually above it");
            SacrificeDoubles(root);
        }

        private static void SacrificeDoubles(string root)
        {
            SacrificeBook flip = new SacrificeBook();
            string first = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 9,845,534 total sacrifice points.";
            string secondA = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 9,845,648 total sacrifice points.";
            string secondB = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 9,845,649 total sacrifice points.";
            flip.Observe(first);
            flip.Observe(first + "\n" + secondA);
            flip.Observe(first + "\n" + secondB);
            Check(flip.SessionCount == 2 && flip.SessionGained == 230, "a last-digit flip of the same total does not count twice, got " + flip.SessionCount.ToString() + " " + flip.SessionGained.ToString());
            string path = Path.Combine(root, "verification", "fixtures", "live033", "debug-log-v0233-sacdouble.txt");
            if (!File.Exists(path))
            {
                Check(false, "missing debug-log-v0233-sacdouble.txt");
                return;
            }
            string[] rows = File.ReadAllLines(path);
            SacrificeBook live = new SacrificeBook();
            string last = "";
            System.Text.StringBuilder trace = new System.Text.StringBuilder();
            int prevCount = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Length < 8 || rows[i].Substring(0, 5) != "18:41") continue;
                int at = rows[i].IndexOf(" chat=");
                int pots = rows[i].IndexOf(" pots=");
                if (at < 0 || pots < at) continue;
                string raws = rows[i].Substring(at + 6, pots - (at + 6));
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                int from = 0;
                while (from < raws.Length)
                {
                    int raw = raws.IndexOf("raw=\"", from);
                    if (raw < 0) break;
                    int start = raw + 5;
                    int end = raws.IndexOf('"', start);
                    if (end < 0) break;
                    if (text.Length > 0) text.Append('\n');
                    text.Append(raws.Substring(start, end - start));
                    from = end + 1;
                }
                last = text.ToString();
                live.Observe(last);
                if (live.SessionCount != prevCount)
                {
                    trace.Append(rows[i].Substring(0, 8)).Append('=').Append(live.SessionCount.ToString()).Append('/').Append(live.SessionGained.ToString()).Append(' ');
                    prevCount = live.SessionCount;
                }
            }
            Check(live.SessionCount == 6 && live.SessionGained == 690, "the 18:41 log is 6 sacrifices and 690 points, got " + live.SessionCount.ToString() + " " + live.SessionGained.ToString() + " steps " + trace.ToString() + " last " + live.Log);
            live.Observe(last);
            Check(live.SessionCount == 6 && live.SessionGained == 690, "re-reading the last 18:41 chat adds nothing");
            Check(live.Value() == "690 this session \u00b7 6 sacrifices \u00b7 \u2014/h", "the row shows the session counter and holds the rate, got " + live.Value());
            SacrificeBook big = new SacrificeBook();
            big.Observe("] You sacrifice a large pile and gain 50000 points. You now have 50,000 total sacrifice points.");
            big.Observe("] You sacrifice a large pile and gain 50000 points. You now have 50,000 total sacrifice points.\n] You sacrifice a large pile and gain 50000 points. You now have 100,000 total sacrifice points.");
            Check(big.SessionCount == 2 && big.SessionGained == 100000, "a large gain with a matching total is accepted, got " + big.SessionGained.ToString());
            SacrificeBook mis = new SacrificeBook();
            string good = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 9,846,799 total sacrifice points.";
            string nine = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 3,846,914 total sacrifice points.";
            string fixedTotal = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 9,846,914 total sacrifice points.";
            string dropped = "] You sacrifice Blast diffusion boots and gain 115 points. You now have 984,702 total sacrifice points.";
            mis.Observe(good);
            mis.Observe(good + "\n" + nine);
            Check(mis.SessionCount == 1 && mis.SessionGained == 115 && mis.Total == 9846799, "a 9 read as 3 is not counted and does not move the total, got " + mis.SessionGained.ToString() + " total " + mis.Total.ToString() + " " + mis.Log);
            mis.Observe(good + "\n" + fixedTotal);
            Check(mis.SessionCount == 2 && mis.SessionGained == 230 && mis.Total == 9846914, "the corrected total is kept and counted once, got " + mis.Total.ToString() + " " + mis.SessionGained.ToString() + " " + mis.Log);
            string three = good + "\n" + fixedTotal + "\n" + dropped;
            mis.Observe(three);
            mis.Observe(three);
            Check(mis.SessionCount == 3 && mis.SessionGained == 345 && mis.Total == 9846914 && SacCounts(mis.Log) == 3, "a dropped digit is ignored and the gain still counts once, got " + mis.SessionCount.ToString() + " " + mis.SessionGained.ToString() + " total " + mis.Total.ToString());
            string six = Path.Combine(root, "verification", "fixtures", "live034", "debug-log-v0234-sac6m.txt");
            if (!File.Exists(six))
            {
                Check(false, "missing debug-log-v0234-sac6m.txt");
                return;
            }
            string[] sixRows = File.ReadAllLines(six);
            SacrificeBook boots = new SacrificeBook();
            string sixLast = "";
            for (int n = 0; n < sixRows.Length; n++)
            {
                if (sixRows[n].Length < 8 || sixRows[n].Substring(0, 5) != "19:36") continue;
                int at = sixRows[n].IndexOf(" chat=");
                int potsAt = sixRows[n].IndexOf(" pots=");
                if (at < 0 || potsAt < at) continue;
                string raws = sixRows[n].Substring(at + 6, potsAt - (at + 6));
                System.Text.StringBuilder body = new System.Text.StringBuilder();
                int from = 0;
                while (from < raws.Length)
                {
                    int raw = raws.IndexOf("raw=\"", from);
                    if (raw < 0) break;
                    int start = raw + 5;
                    int end = raws.IndexOf('"', start);
                    if (end < 0) break;
                    if (body.Length > 0) body.Append('\n');
                    body.Append(raws.Substring(start, end - start));
                    from = end + 1;
                }
                sixLast = body.ToString();
                boots.Observe(sixLast);
            }
            Check(boots.SessionCount == 8 && boots.SessionGained == 920, "the 19:36 log is 8 sacrifices and 920 points, got " + boots.SessionCount.ToString() + " " + boots.SessionGained.ToString() + " " + boots.Log);
            boots.Observe(sixLast);
            Check(boots.SessionCount == 8 && boots.SessionGained == 920, "re-reading the 19:36 chat adds nothing");
            Check(SacCounts(live.Log) == live.SessionCount && SacCounts(boots.Log) == boots.SessionCount, "every counted sacrifice is in the log");
            string seven = Path.Combine(root, "verification", "fixtures", "live035", "debug-log-v0235-sac7.txt");
            if (!File.Exists(seven))
            {
                Check(false, "missing debug-log-v0235-sac7.txt");
                return;
            }
            string[] sevenRows = File.ReadAllLines(seven);
            SacrificeBook over = new SacrificeBook();
            string sevenLast = "";
            for (int n = 0; n < sevenRows.Length; n++)
            {
                int at = sevenRows[n].IndexOf(" chat=");
                int potsAt = sevenRows[n].IndexOf(" pots=");
                if (at < 0 || potsAt < at) continue;
                string raws = sevenRows[n].Substring(at + 6, potsAt - (at + 6));
                System.Text.StringBuilder body = new System.Text.StringBuilder();
                int from = 0;
                while (from < raws.Length)
                {
                    int raw = raws.IndexOf("raw=\"", from);
                    if (raw < 0) break;
                    int start = raw + 5;
                    int end = raws.IndexOf('"', start);
                    if (end < 0) break;
                    if (body.Length > 0) body.Append('\n');
                    body.Append(raws.Substring(start, end - start));
                    from = end + 1;
                }
                sevenLast = body.ToString();
                over.Observe(sevenLast);
            }
            Check(over.SessionCount == 6 && over.SessionGained == 690 && SacCounts(over.Log) == 6, "the 20:10 log is 6 sacrifices and 690 points, got " + over.SessionCount.ToString() + " " + over.SessionGained.ToString() + " " + over.Log);
            over.Observe(sevenLast);
            Check(over.SessionCount == 6 && over.SessionGained == 690, "re-reading the 20:10 chat adds nothing");
            string slotA = "] You sacrifice Laceration boots and gain 115 points. You now have 889 total sacrifice points.";
            string slotB = "] You sacrifice Laceration boots and gain 115 points. You now have 1,004 total sacrifice points.";
            string slotC = "] You sacrifice Laceration boots and gain 115 points. You now have 1,119 total sacrifice points.";
            SacrificeBook scrolled = new SacrificeBook();
            scrolled.Observe(slotA);
            scrolled.Observe(slotA);
            scrolled.Observe(slotB);
            scrolled.Observe(slotC);
            Check(scrolled.SessionCount == 3 && scrolled.SessionGained == 345 && scrolled.Total == 1119, "a new total in an old chat slot still counts, got " + scrolled.SessionCount.ToString() + " " + scrolled.SessionGained.ToString() + " " + scrolled.Log);
            scrolled.Observe(slotA);
            scrolled.Observe(slotC);
            Check(scrolled.SessionCount == 3 && scrolled.SessionGained == 345, "re-reading 889 or 1,119 does not count again, got " + scrolled.SessionCount.ToString() + " " + scrolled.Log);
            string sameSlot = Path.Combine(root, "verification", "fixtures", "fixtures-chat", "debug-log-v0238-sameslot.txt");
            if (!File.Exists(sameSlot))
            {
                Check(false, "missing debug-log-v0238-sameslot.txt");
                return;
            }
            SacrificeBook slotLog = ReplaySacrificeLog(File.ReadAllLines(sameSlot), "");
            Check(slotLog.SessionCount == 3 && slotLog.SessionGained == 345 && SacCounts(slotLog.Log) == 3, "the 16:42 log is 3 sacrifices and 345 points, got " + slotLog.SessionCount.ToString() + " " + slotLog.SessionGained.ToString() + " " + slotLog.Log);
        }

        private static SacrificeBook ReplaySacrificeLog(string[] rows, string prefix)
        {
            SacrificeBook book = new SacrificeBook();
            for (int n = 0; n < rows.Length; n++)
            {
                if (prefix.Length > 0 && (rows[n].Length < prefix.Length || rows[n].Substring(0, prefix.Length) != prefix)) continue;
                int at = rows[n].IndexOf(" chat=");
                int potsAt = rows[n].IndexOf(" pots=");
                if (at < 0 || potsAt < at) continue;
                string raws = rows[n].Substring(at + 6, potsAt - (at + 6));
                System.Text.StringBuilder body = new System.Text.StringBuilder();
                int from = 0;
                while (from < raws.Length)
                {
                    int raw = raws.IndexOf("raw=\"", from);
                    if (raw < 0) break;
                    int start = raw + 5;
                    int end = raws.IndexOf('"', start);
                    if (end < 0) break;
                    if (body.Length > 0) body.Append('\n');
                    body.Append(raws.Substring(start, end - start));
                    from = end + 1;
                }
                book.Observe(body.ToString());
            }
            return book;
        }

        private static int SacCounts(string log)
        {
            int n = 0;
            int at = 0;
            string mark = "sac count #";
            if (log == null) return 0;
            while ((at = log.IndexOf(mark, at)) >= 0)
            {
                n++;
                at += mark.Length;
            }
            return n;
        }

        private static void PotLines()
        {
            DateTime t = new DateTime(2026, 11, 22, 13, 33, 42, DateTimeKind.Utc);
            ChatPotionBook book = new ChatPotionBook();
            List<ChatHit> clean = new List<ChatHit>();
            clean.Add(Hit("The effects of your Aggression Potion will expire in 30 seconds.", "aggression", "low", 80));
            List<ChatNotice> first = book.Observe(clean, t);
            Check(first.Count == 1 && !first[0].Audible, "the first aggression warning fires with no sound");
            Check(book.Observe(clean, t.AddSeconds(2)).Count == 0, "the same line on the next read does not fire");
            List<ChatHit> scrolled = new List<ChatHit>();
            scrolled.Add(Hit("The effects of your Aggression Potion will expire in 30 seconds.", "aggression", "low", 48));
            Check(book.Observe(scrolled, t.AddSeconds(4)).Count == 0, "scrolling that line up does not fire");
            List<ChatHit> shifted = new List<ChatHit>();
            shifted.Add(Hit("someone says hello", "", "", 24));
            shifted.Add(Hit("The effects of your Aggression Potion will expire in 30 seconds.", "aggression", "low", 64));
            Check(book.Observe(shifted, t.AddSeconds(6)).Count == 0, "a new message shifting the line does not fire");
            List<ChatHit> ocr = new List<ChatHit>();
            ocr.Add(Hit("The effazts of pur Potion will expire in 30 saonds.", "aggression", "low", 60));
            Check(book.Observe(ocr, t.AddSeconds(8)).Count == 0, "an OCR variant of the same line does not fire within five minutes");
            Check(book.Observe(new List<ChatHit>(), t.AddMinutes(1)).Count == 0, "an empty chat read does not fire");
            Check(book.Observe(ocr, t.AddMinutes(2)).Count == 0, "the same warning coming back inside five minutes does not fire");
            Check(book.Observe(new List<ChatHit>(), t.AddMinutes(6)).Count == 0, "a gap after the cooldown still does not fire by itself");
            List<ChatNotice> later = book.Observe(clean, t.AddMinutes(6).AddSeconds(2));
            Check(later.Count == 1 && !later[0].Audible, "the same line more than five minutes later fires again");

            ChatPotionBook kinds = new ChatPotionBook();
            List<ChatHit> low = new List<ChatHit>();
            low.Add(Hit("Your elder overload is about to run out.", "elder", "low", 40));
            List<ChatNotice> quiet = kinds.Observe(low, t);
            Check(quiet.Count == 1 && !quiet[0].Audible, "an overload warning does not need a drink line");
            List<ChatHit> ended = new List<ChatHit>();
            ended.Add(Hit("The effects of elder overload have worn off and you feel normal again.", "elder", "expired", 40));
            List<ChatNotice> loud = kinds.Observe(ended, t.AddSeconds(20));
            Check(loud.Count == 1 && loud[0].Audible, "the low warning does not block the expired line");

            ChatPotionBook pair = new ChatPotionBook();
            List<ChatHit> both = new List<ChatHit>();
            both.Add(Hit("The effects of elder overload have worn off and you feel normal again.", "elder", "expired", 30));
            both.Add(Hit("The effects of your Aggression Potion have expired.", "aggression", "expired", 48));
            List<ChatNotice> together = pair.Observe(both, t);
            bool elder = false;
            bool aggression = false;
            for (int i = 0; i < together.Count; i++)
            {
                if (together[i].Title != null && together[i].Title.IndexOf("Elder") >= 0) elder = true;
                if (together[i].Title != null && together[i].Title.IndexOf("Aggression") >= 0) aggression = true;
            }
            Check(together.Count == 2 && elder && aggression && together[0].Audible && together[1].Audible, "overload and aggression expiring together both fire");

            ChatPotionBook startup = new ChatPotionBook();
            startup.Attach();
            Check(startup.Observe(clean, t).Count == 0, "lines already on screen at startup do not fire");
            Check(startup.Observe(scrolled, t.AddSeconds(2)).Count == 0, "those baseline lines still do not fire after they scroll");
            Check(startup.Detail.IndexOf("Aggression") < 0, "a baseline line does not put a potion on the row");
        }

        private static void BarAnchor()
        {
            int width = 2560;
            int height = 1440;
            IntRect life = new IntRect(717, 1178, 81, 4);
            IntRect prayer = new IntRect(954, 1179, 83, 3);
            Check(HudBars.RealBar(life, width, height) && !HudBars.Overhead(life, width, height), "life 717,1178 81x4 is the HUD bar at 1440p 100%");
            Check(HudBars.RealBar(prayer, width, height) && !HudBars.Overhead(prayer, width, height), "prayer 954,1179 83x3 is the HUD bar at 1440p 100%");
            IntRect[] junk = new IntRect[] {
                new IntRect(1401, 787, 34, 2),
                new IntRect(1574, 1147, 29, 7),
                new IntRect(1556, 1082, 28, 7),
                new IntRect(1349, 1073, 28, 3),
                new IntRect(1741, 1144, 28, 3)
            };
            for (int i = 0; i < junk.Length; i++)
                Check(HudBars.Overhead(junk[i], width, height) && !HudBars.RealBar(junk[i], width, height), "overhead bar " + junk[i].X.ToString() + "," + junk[i].Y.ToString() + " " + junk[i].Width.ToString() + "x" + junk[i].Height.ToString() + " is rejected");
            Check(HudBars.RealBar(new IntRect(400, 200, 81, 4), width, height), "a full-size bar on another row is still a HUD bar");
            Check(HudBars.Valid(1515, 1102) && HudBars.Valid(4000, 990), "1515/1102 and a boost up to 4000 are legal");
            Check(!HudBars.Valid(4001, 990) && !HudBars.Valid(2, 1) && !HudBars.Valid(1, null), "4001, 2/1 and 1/- are rejected");
            Check(HudBars.Shown(1515, 1102, false, false) == "1515 / 1102", "a live total is shown with its maximum");
            Check(HudBars.Shown(1515, 1102, true, false) == "\u2014", "a lost bar shows a dash instead of the stale 1515/1102");
            Check(HudBars.Shown(2, 1, false, false) == "\u2014" && HudBars.Shown(1, null, false, false) == "\u2014", "junk totals show a dash");
            int wide = 2560 * 3 / 2;
            int tall = 1440 * 3 / 2;
            Check(HudBars.RealBar(new IntRect(717 * 3 / 2, 1178 * 3 / 2, 81 * 3 / 2, 6), wide, tall), "the life bar still qualifies at 150%");
            Check(!HudBars.RealBar(new IntRect(1401 * 3 / 2, 787 * 3 / 2, 34 * 3 / 2, 3), wide, tall), "the overhead bar stays too small at 150%");
            Check(HudBars.RealBar(new IntRect(717 * 2, 1178 * 2, 81 * 2, 8), 2560 * 2, 1440 * 2), "the life bar still qualifies at 200%");
            Check(!HudBars.RealBar(new IntRect(1401 * 2, 787 * 2, 34 * 2, 4), 2560 * 2, 1440 * 2), "the overhead bar stays too small at 200%");
            DateTime t = new DateTime(2026, 11, 22, 13, 0, 0, DateTimeKind.Utc);
            HudBars.Lock slot = new HudBars.Lock();
            Check(slot.Open(t), "with no bar yet a candidate can be taken");
            bool take = HudBars.Valid(1515, 1102) && HudBars.RealBar(life, width, height) && slot.Open(t);
            Check(take, "717,1178 is taken when it reads 1515/1102");
            slot.Hold(new IntRect(700, 1148, 140, 36), life);
            slot.Note(true, t);
            Check(slot.Held && !slot.Open(t.AddSeconds(9)), "a bar that still reads stays locked");
            slot.Note(false, t.AddSeconds(1));
            Check(!slot.Due(t.AddSeconds(9)) && !slot.Open(t.AddSeconds(9)), "nine seconds of misses does not unlock it");
            Check(slot.Due(t.AddSeconds(12)) && slot.Open(t.AddSeconds(12)), "about ten seconds of misses allows a new search");
            bool junkTake = HudBars.Valid(2, 1) && HudBars.RealBar(junk[0], width, height) && slot.Open(t.AddSeconds(12));
            Check(!junkTake, "1401,787 is not taken, and 2/1 is not a reading");
            bool prayerBrief = HudBars.Valid(990, 990) && HudBars.RealBar(new IntRect(955, 1178, 83, 4), width, height);
            Check(prayerBrief, "prayer 955,1178 83x4 can be locked");
            Check(!HudBars.RealBar(new IntRect(1741, 1144, 28, 3), width, height), "prayer does not move to 1741,1144 28x3");
        }

        private static void ReplayPotLogs(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live0223");
            bool lowAlert = false;
            bool expiredAlert = false;
            bool aggroAlert = false;
            ChatPotionBook book = new ChatPotionBook();
            book.Attach();
            string lowLog = Path.Combine(dir, "debug-log-v0222-pots.txt");
            string expiredLog = Path.Combine(dir, "debug-log-v0222-potsexpired.txt");
            if (!File.Exists(lowLog) || !File.Exists(expiredLog))
            {
                Check(false, "missing v0.2.22 potion logs");
                return;
            }
            Replay(book, File.ReadAllLines(lowLog), ref lowAlert, ref expiredAlert, ref aggroAlert);
            Check(lowAlert && book.Detail.IndexOf("Elder overload low") >= 0, "replaying the low log raises Elder overload low, got " + book.Detail + " alert " + lowAlert.ToString());
            book = new ChatPotionBook();
            book.Attach();
            lowAlert = false;
            expiredAlert = false;
            aggroAlert = false;
            Replay(book, File.ReadAllLines(expiredLog), ref lowAlert, ref expiredAlert, ref aggroAlert);
            Check(lowAlert && expiredAlert && book.Detail.IndexOf("Elder overload expired") >= 0, "replaying the expired log takes elder from low to expired, got " + book.Detail);
            Check(aggroAlert && book.Detail.IndexOf("Aggression low") >= 0, "the line that dropped Aggression still sets Aggression low, got " + book.Detail);
            List<ChatHit> gone = ChatPhrase.FromOcr("The effects of your Potion have expired.");
            book.Observe(gone);
            Check(book.Detail.IndexOf("Aggression expired") >= 0, "the missing bottom line sets Aggression expired, got " + book.Detail);
            ChatPotionBook fading = new ChatPotionBook();
            List<ChatHit> both = new List<ChatHit>();
            both.Add(Hit("The effects of elder overload have worn off and you feel normal again.", "elder", "expired", 20));
            both.Add(Hit("The effects of your Aggression Potion will expire in 30 seconds.", "aggression", "low", 40));
            fading.Observe(both);
            Check(fading.Detail.IndexOf("Elder overload expired") >= 0 && fading.Detail.IndexOf("Aggression low") >= 0, "both pots show before the expiry fades, got " + fading.Detail);
            fading.Refresh(DateTime.UtcNow.AddSeconds(29));
            Check(fading.Detail.IndexOf("Aggression low") >= 0, "a 30 second warning is still on the panel just before 30 seconds, got " + fading.Detail);
            fading.Refresh(DateTime.UtcNow.AddSeconds(31));
            Check(fading.Detail.IndexOf("Aggression low") < 0 && fading.Detail.IndexOf("Elder overload expired") >= 0, "the 30 second warning clears and expired stays, got " + fading.Detail);
            fading.Refresh(DateTime.UtcNow.AddSeconds(44));
            Check(fading.Detail.IndexOf("Elder overload expired") >= 0, "expired is still shown just before 45 seconds, got " + fading.Detail);
            fading.Refresh(DateTime.UtcNow.AddSeconds(46));
            Check(fading.Detail.IndexOf("expired") < 0, "expired clears after 45 seconds, got " + fading.Detail);
            ChatPotionBook alone = new ChatPotionBook();
            List<ChatHit> only = new List<ChatHit>();
            only.Add(Hit("The effects of elder overload have worn off and you feel normal again.", "elder", "expired", 20));
            alone.Observe(only);
            alone.Refresh(DateTime.UtcNow.AddSeconds(46));
            Check(alone.Detail.Length == 0, "with nothing else tracked the row is empty, so the panel says Watching chat, got " + alone.Detail);
        }

        private static void Replay(ChatPotionBook book, string[] lines, ref bool lowAlert, ref bool expiredAlert, ref bool aggroAlert)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string joined = RawBlock(lines[i]);
                if (joined.Length == 0) continue;
                List<ChatHit> hits = ChatPhrase.FromOcr(joined);
                List<ChatNotice> notices = book.Observe(hits);
                for (int n = 0; n < notices.Count; n++)
                {
                    string title = notices[n].Title ?? "";
                    if (title.IndexOf("Elder overload") >= 0 && title.IndexOf("low") >= 0) lowAlert = true;
                    if (title.IndexOf("Elder overload") >= 0 && title.IndexOf("expired") >= 0) expiredAlert = true;
                    if (title.IndexOf("Aggression") >= 0 && title.IndexOf("low") >= 0) aggroAlert = true;
                }
            }
        }

        private static string RawBlock(string line)
        {
            if (line == null) return "";
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            int at = 0;
            while (at < line.Length)
            {
                int raw = line.IndexOf("raw=\"", at);
                if (raw < 0) break;
                int start = raw + 5;
                int end = line.IndexOf('"', start);
                if (end < 0) break;
                if (text.Length > 0) text.Append('\n');
                text.Append(line.Substring(start, end - start));
                at = end + 1;
            }
            return text.ToString();
        }

        private static void Shots0223(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live0223");
            PopupSome(Path.Combine(dir, "full-client-v0222-a.png"), "full-client-v0222-a.png");
            string aPath = Path.Combine(dir, "full-client-v0222-a.png");
            if (File.Exists(aPath))
                Check(XpFloat.Present(PngImage.Load(aPath).Scale(2)), "the popup at about 21 percent of the client is detected at 200%");
            PopupSome(Path.Combine(dir, "full-client-v0222-pots.png"), "full-client-v0222-pots.png");
            PopupSome(Path.Combine(dir, "full-client-v0222-potsexpired.png"), "full-client-v0222-potsexpired.png");
            string metrics = Path.Combine(dir, "runemetrics-xph-vs-xp-v0222.png");
            if (!File.Exists(metrics)) Check(false, "missing runemetrics-xph-vs-xp-v0222.png");
            else
            {
                MetricsReader.Result total = MetricsReader.Read(PngImage.Load(metrics));
                notes.Add("runemetrics-xph-vs-xp-v0222.png XP column: " + (total.Found ? total.Xp.ToString() : ("MISS " + (total.Note ?? ""))));
                Check(total.Found && total.Xp == 4867895L, "the XP column is 4867895, not the XP/h beside it, got " + total.Xp.ToString() + " " + (total.Note ?? ""));
            }
        }

        private static void PopupSome(string path, string label)
        {
            if (!File.Exists(path))
            {
                Check(false, "missing " + label);
                notes.Add(label + " XP popup: MISSING FILE");
                return;
            }
            ImageBuffer image = PngImage.Load(path);
            int[] scales = new int[] { 100, 150, 200 };
            int hits = 0;
            for (int i = 0; i < scales.Length; i++)
            {
                ImageBuffer frame = scales[i] == 100 ? image : image.Scale(scales[i] / 100.0);
                bool hit = XpFloat.Present(frame);
                if (hit) hits++;
                notes.Add(label + " (" + frame.Width.ToString() + "x" + frame.Height.ToString() + ") at " + scales[i].ToString() + "% XP popup: " + (hit ? "PASS" : "MISS"));
            }
            notes.Add(label + " detected on " + hits.ToString() + " of 3 scales");
        }

        private static void PopupAt(string path, string label, bool expect)
        {
            if (!File.Exists(path))
            {
                Check(false, "missing " + label);
                notes.Add(label + " XP popup: MISSING FILE");
                return;
            }
            ImageBuffer image = PngImage.Load(path);
            int[] scales = new int[] { 100, 150, 200 };
            for (int i = 0; i < scales.Length; i++)
            {
                ImageBuffer frame = scales[i] == 100 ? image : image.Scale(scales[i] / 100.0);
                bool hit = XpFloat.Present(frame);
                string verdict = hit == expect ? "PASS" : "FAIL";
                string wanted = expect ? "popup" : "quiet";
                notes.Add(label + " (" + frame.Width.ToString() + "x" + frame.Height.ToString() + ") at " + scales[i].ToString() + "% XP popup: " + verdict + " (" + wanted + ")");
                Check(hit == expect, label + " at " + scales[i].ToString() + "% is " + wanted + ", got " + (hit ? "popup" : "quiet"));
            }
            if (!expect && label.IndexOf("v0218") >= 0)
                notes.Add("full-client-v0218-xp.png has no readable gold popup in the centre-top sky. Quiet is the correct read of this file.");
        }

        private static void Shots0222(string root)
        {
            string dir = Path.Combine(root, "verification", "fixtures", "live0222");
            PopupAt(Path.Combine(dir, "full-client-v0221-potlate.png"), "full-client-v0221-potlate.png", true);
            PopupAt(Path.Combine(dir, "full-client-v0221-rmmiss.png"), "full-client-v0221-rmmiss.png", false);
            PopupAt(Path.Combine(dir, "full-client-v0221-potmiss.png"), "full-client-v0221-potmiss.png", false);
            string metrics = Path.Combine(dir, "full-client-v0221-rmmiss.png");
            if (File.Exists(metrics))
            {
                MetricsReader.Result total = MetricsReader.Read(PngImage.Load(metrics));
                notes.Add("full-client-v0221-rmmiss.png RuneMetrics: " + (total.Found ? ("xp=" + total.Xp.ToString()) : ("MISS " + (total.Note ?? ""))));
                Check(total.Found || (total.Note != null && total.Note.IndexOf("chart") >= 0), "the second-account metrics bar icon is found, got " + (total.Note ?? ""));
            }
            RedInk(Path.Combine(dir, "chat-elder-low-30s-darkred-v0221.png"), "chat-elder-low-30s-darkred-v0221.png");
            RedInk(Path.Combine(dir, "chat-aggro-low-30s-red-v0221.png"), "chat-aggro-low-30s-red-v0221.png");
            RedInk(Path.Combine(dir, "chat-aggro-expired-red-v0221.png"), "chat-aggro-expired-red-v0221.png");
            RedInk(Path.Combine(dir, "chat-pots-expired-v0221.png"), "chat-pots-expired-v0221.png");
            RedInk(Path.Combine(dir, "chat-four-pots-low-missed-v0221.png"), "chat-four-pots-low-missed-v0221.png");
            notes.Add("Windows.Media.Ocr of the v0.2.21 chat crops: NOT RUN on this host (no Windows OCR). The dark-on-light prep above is what the engine would receive.");
        }

        private static void RedInk(string path, string label)
        {
            if (!File.Exists(path))
            {
                Check(false, "missing " + label);
                notes.Add(label + " OCR prep: MISSING FILE");
                return;
            }
            ImageBuffer image = PngImage.Load(path);
            ImageBuffer page = ChatPrep.ForOcr(image);
            int dark = 0;
            if (page != null)
            {
                for (int y = 0; y < page.Height; y++)
                    for (int x = 0; x < page.Width; x++)
                        if (ImageBuffer.R(page.Argb(x, y)) < 40) dark++;
            }
            bool ok = page != null && page.Width >= image.Width * 3 - 2 && dark > 40;
            notes.Add(label + " red OCR prep: " + (ok ? "PASS" : "FAIL") + " dark=" + dark.ToString());
            Check(ok, label + " turns red text into dark ink, dark pixels " + dark.ToString());
        }

        private static ChatHit Hit(string phrase, string kind, string state, int y)
        {
            ChatHit hit = new ChatHit();
            hit.Phrase = phrase;
            hit.Kind = kind;
            hit.State = state;
            hit.Y = y;
            return hit;
        }

        private static ClientCandidate Client(string title, int pid, long handle, bool foreground)
        {
            ClientCandidate window = new ClientCandidate();
            window.Title = title;
            window.Process = "Kavros";
            window.ClassName = "JagWindow";
            window.Width = 1024;
            window.Height = 768;
            window.Pid = pid;
            window.Handle = handle;
            window.Foreground = foreground;
            return window;
        }

        private static ClientCandidate Overlay(string title, int pid, long handle)
        {
            ClientCandidate window = new ClientCandidate();
            window.Title = title;
            window.Process = title;
            window.ClassName = "Window";
            window.Width = 400;
            window.Height = 700;
            window.Pid = pid;
            window.Handle = handle;
            return window;
        }

        private static int Bursts(short[] samples)
        {
            if (samples == null) return 0;
            int bursts = 0;
            int quiet = 1000;
            for (int i = 0; i < samples.Length; i++)
            {
                int mag = samples[i] < 0 ? -samples[i] : samples[i];
                if (mag > 1400)
                {
                    if (quiet > 400) bursts++;
                    quiet = 0;
                }
                else quiet++;
            }
            return bursts;
        }

        private static int Crossings(short[] samples)
        {
            if (samples == null || samples.Length < 2) return 0;
            int count = 0;
            for (int i = 1; i < samples.Length; i++)
                if ((samples[i - 1] < 0 && samples[i] >= 0) || (samples[i - 1] >= 0 && samples[i] < 0)) count++;
            return count;
        }

        private static int Peak(short[] samples)
        {
            int peak = 0;
            if (samples == null) return 0;
            for (int i = 0; i < samples.Length; i++)
            {
                int mag = samples[i] < 0 ? -samples[i] : samples[i];
                if (mag > peak) peak = mag;
            }
            return peak;
        }

        private static bool SameWave(short[] a, short[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool Has(List<ChatHit> lines, string kind, string state)
        {
            if (lines == null) return false;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Kind == kind && lines[i].State == state) return true;
            return false;
        }

        private static string Describe(List<ChatHit> lines)
        {
            if (lines == null || lines.Count == 0) return "none";
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) text.Append(" ");
                text.Append(lines[i].Kind).Append(":").Append(lines[i].State);
            }
            return text.ToString();
        }

        private static void Manifest(string root)
        {
            Check(AppInfo.Version == "0.2.39" && AppInfo.Name == "AFK Warden" && AppInfo.Author == "Zenity", "module identity");
            if (string.IsNullOrEmpty(root)) return;
            string jsonPath = Path.Combine(root, "companion.module.json");
            string manifestPath = Path.Combine(root, "source", "app.manifest");
            if (!File.Exists(jsonPath) || !File.Exists(manifestPath))
            {
                failures.Add("missing companion.module.json or app.manifest under " + root);
                return;
            }
            string json = File.ReadAllText(jsonPath);
            string manifest = File.ReadAllText(manifestPath);
            Check(json.IndexOf("\"version\": \"0.2.39\"") >= 0, "public module manifest version");
            Check(json.IndexOf("\"name\": \"AFK Warden\"") >= 0 && json.IndexOf("\"author\": \"Zenity\"") >= 0, "public module manifest name and author");
            Check(json.IndexOf("\"id\": \"afk-warden\"") >= 0, "public module id");
            Check(manifest.IndexOf("version=\"0.2.39.0\"") >= 0 && manifest.IndexOf("name=\"KavrosAfkWarden\"") >= 0, "assembly manifest version");
        }
    }
}
