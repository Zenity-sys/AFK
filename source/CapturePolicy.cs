using System;

namespace KavrosAfkWarden
{
    public sealed class DockTarget
    {
        public long Handle;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public bool InFront;
    }

    // Sit inside the tracked client at a saved offset. The panel follows that
    // window only. Another client in front of the same spot hides it.
    public static class PanelDock
    {
        public static void Place(int gameX, int gameY, int gameW, int gameH, int panelW, int panelH, int offsetX, int offsetY, int deskX, int deskY, int deskW, int deskH, out int x, out int y)
        {
            int areaX = gameX, areaY = gameY, areaW = gameW, areaH = gameH;
            int overlapW = Math.Min(gameX + gameW, deskX + deskW) - Math.Max(gameX, deskX);
            int overlapH = Math.Min(gameY + gameH, deskY + deskH) - Math.Max(gameY, deskY);
            if (overlapW >= panelW && overlapH >= panelH)
            {
                areaX = Math.Max(gameX, deskX);
                areaY = Math.Max(gameY, deskY);
                areaW = overlapW;
                areaH = overlapH;
            }
            else
            {
                areaX = deskX;
                areaY = deskY;
                areaW = deskW;
                areaH = deskH;
            }
            x = gameX + offsetX;
            y = gameY + offsetY;
            if (x < areaX) x = areaX;
            if (y < areaY) y = areaY;
            if (x > areaX + areaW - panelW) x = areaX + areaW - panelW;
            if (y > areaY + areaH - panelH) y = areaY + areaH - panelH;
        }

        public static bool Overlaps(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
        {
            if (aw <= 0 || ah <= 0 || bw <= 0 || bh <= 0) return false;
            return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
        }

        // Another Kavros client that is above the tracked window and crosses the
        // panel's rectangle. A client beside it, or a window behind it, does not.
        public static bool OtherClientCovers(long bound, int panelX, int panelY, int panelW, int panelH, System.Collections.Generic.List<DockTarget> others)
        {
            if (bound == 0 || others == null) return false;
            for (int i = 0; i < others.Count; i++)
            {
                DockTarget other = others[i];
                if (other == null || other.Handle == 0 || other.Handle == bound || !other.InFront) continue;
                if (Overlaps(panelX, panelY, panelW, panelH, other.X, other.Y, other.Width, other.Height)) return true;
            }
            return false;
        }

        public static long OwnerOf(long bound)
        {
            return bound > 0 ? bound : 0;
        }

        public static string PanelHeading(string label)
        {
            if (label == null) return "";
            string text = label.Trim();
            if (text.Length == 0 || text == "Choose a client" || text == "Client closed") return "";
            return text;
        }

        public static bool Visible(bool enabled, bool gameExists, bool minimized, bool gameForeground, bool panelFocused, bool settingsFocused)
        {
            return Visible(enabled, gameExists, minimized, gameForeground, panelFocused, settingsFocused, false);
        }

        public static bool Visible(bool enabled, bool gameExists, bool minimized, bool gameForeground, bool panelFocused, bool settingsFocused, bool coveredByOtherClient)
        {
            // Owned by the tracked client. Another application may cover it by
            // z-order. A minimized client, or another Kavros client in front of
            // this spot, hides the panel. gameForeground stays in the signature
            // so callers stay explicit.
            if (!enabled || minimized || coveredByOtherClient) return false;
            if (!gameExists && !panelFocused && !settingsFocused) return true;
            return true;
        }

        // The z-order call is sent only when the wanted state changes. Repeating
        // SetWindowPos every tick is enough to flash the desktop.
        public static bool ZOrderChanged(bool known, bool currentTop, bool wantTop)
        {
            return !known || currentTop != wantTop;
        }

        // Topmost only while the player is looking at the game (or at the panel
        // or Settings themselves), and briefly after an explicit launch so a
        // Companion start cannot leave it behind a client that is about to be
        // found. When another application is in front the panel drops back
        // under it, so it never floats over the browser or Discord.
        public static bool WantTopMost(bool gameExists, bool minimized, bool gameForeground, bool ownForeground, bool launchGrace)
        {
            if (minimized) return false;
            if (launchGrace) return true;
            if (ownForeground) return true;
            return gameExists && gameForeground;
        }
    }

    // How a frame is obtained. Focus is intentionally not an input: an
    // unobstructed background client is readable.
    public enum CaptureRoute
    {
        Missing,
        Minimized,
        Covered,
        Screen,
        WindowSurface
    }

    public static class CapturePolicy
    {
        public static CaptureRoute Choose(bool attached, bool minimized, bool onScreen, bool occluded, bool surfaceAvailable)
        {
            if (!attached) return CaptureRoute.Missing;
            // A minimized game is not composed. Do not keep serving the last frame.
            if (minimized) return CaptureRoute.Minimized;
            // The window surface is valid while another app is focused and while
            // other windows cover the client. Prefer it whenever the OS offers it.
            if (surfaceAvailable) return CaptureRoute.WindowSurface;
            // Whatever is on the monitor. This does
            // not require the game to be the foreground window.
            if (onScreen && !occluded) return CaptureRoute.Screen;
            return CaptureRoute.Covered;
        }

        public static bool PixelsAvailable(CaptureRoute route)
        {
            return route == CaptureRoute.Screen || route == CaptureRoute.WindowSurface;
        }
    }

    // Nine sample points across the client, so a
    // client that is only partly covered can still be read from the screen.
    public static class VisibilitySamples
    {
        public static void Fill(int left, int top, int width, int height, int[] xs, int[] ys)
        {
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = y * 3 + x;
                    xs[i] = left + (2 * x + 1) * width / 6;
                    ys[i] = top + (2 * y + 1) * height / 6;
                }
        }

        public static bool Any(bool[] belongsToGame)
        {
            for (int i = 0; i < belongsToGame.Length; i++) if (belongsToGame[i]) return true;
            return false;
        }

        public static bool MostlyClear(bool[] belongsToGame)
        {
            if (belongsToGame.Length < 9 || !belongsToGame[4]) return false;
            int visible = 0;
            for (int i = 0; i < belongsToGame.Length; i++) if (belongsToGame[i]) visible++;
            return visible >= 7;
        }
    }

    // Client origin inside a window bitmap, in physical pixels. The scale factor
    // is not applied: 150% and 200% stay in physical pixels. The capture loop
    // does not build that bitmap with PrintWindow.
    public static class WindowCrop
    {
        public static void Client(int windowX, int windowY, int windowW, int windowH, int clientX, int clientY, int clientW, int clientH, out int ox, out int oy, out int w, out int h)
        {
            ox = clientX - windowX;
            oy = clientY - windowY;
            if (ox < 0) ox = 0;
            if (oy < 0) oy = 0;
            w = clientW;
            h = clientH;
            if (ox + w > windowW) w = windowW - ox;
            if (oy + h > windowH) h = windowH - oy;
            if (w < 0) w = 0;
            if (h < 0) h = 0;
        }
    }

    // Steady state copies the locked slots twice a second. A full client
    // search is only for startup, a lost lock, and a slow refresh.
    public static class CapturePlan
    {
        public const int SampleMilliseconds = 500;
        // Chat is checked several times a second. OCR runs when the crop changed,
        // and twice more in the following second so a scroll can settle.
        public const int ChatMilliseconds = 400;
        public const int FullIntervalSeconds = 30;
        // Buff timers, the XP counter and the backpack change slowly. They are
        // copied on every other sample, about once a second.
        public const int SlowSlotEvery = 2;

        public static bool WantFull(bool locked, DateTime now, DateTime nextFull)
        {
            return now >= nextFull;
        }

        // Seconds until the next whole-client copy. A complete read waits the
        // slow refresh (shorter while no buff bar is locked). A failed read
        // backs off 3, 6, 12, 24 then 30 seconds, so a hidden HUD, an empty
        // prayer bar or a loading screen does not copy the whole client every
        // three seconds.
        // A minimized client has no picture. A covered client is not copied either:
        // PrintWindow with PW_RENDERFULLCONTENT flashes the whole desktop black.
        public static bool ShouldRead(bool minimized, bool paused)
        {
            return !minimized && !paused;
        }

        // An unfocused client that is still visible is a screen copy. A covered
        // or minimized client is not copied. focused is unused on purpose.
        public static bool ContinueReading(bool minimized, bool focused, bool covered)
        {
            return !minimized && !covered;
        }

        // Never pull pixels out of the game window. That path was PrintWindow.
        public static bool PreferWindowPixels(bool regionOnScreen)
        {
            return false;
        }

        public static bool UsePrintWindow { get { return false; } }

        // Borderless Graphics Capture needs a Win11 consent call that this
        // unpackaged .NET 4.8 build cannot confirm without risking the yellow
        // border. Covered windows are left unread instead.
        public static bool OfferWindowSurface { get { return true; } }

        public static bool AcceptFrame(ImageBuffer image)
        {
            return image != null && !ImageBuffer.MostlyBlack(image);
        }

        public static int NextFullSeconds(bool lifeAndPrayerRead, bool buffLocked, int failedInARow)
        {
            if (lifeAndPrayerRead) return buffLocked ? FullIntervalSeconds : 15;
            int wait = 3;
            for (int i = 1; i < failedInARow && wait < FullIntervalSeconds; i++) wait *= 2;
            return Math.Min(wait, FullIntervalSeconds);
        }

        // Copy two HUD slots in one screen grab when they sit on the same row
        // and the band between them is small. One grab instead of two halves
        // the screen reads.
        public static bool ShareGrab(IntRect a, IntRect b)
        {
            if (a.Width < 4 || b.Width < 4) return false;
            int left = Math.Min(a.X, b.X), top = Math.Min(a.Y, b.Y);
            int right = Math.Max(a.Right, b.Right), bottom = Math.Max(a.Bottom, b.Bottom);
            long union = (long)(right - left) * (bottom - top);
            long parts = (long)a.Width * a.Height + (long)b.Width * b.Height;
            return union <= parts * 3 && bottom - top <= Math.Max(a.Height, b.Height) * 2;
        }
    }

    // The RuneMetrics tip sits on its own lines above the buttons until XP is
    // found. What's new, when open, stays inside the panel with padding below it.
    public static class PanelHint
    {
        public const string Line1 = "Keep RuneMetrics open with precise values.";
        public const string Line2 = "Background is fine; not minimized.";
        public const int NotesBody = 168;
        public const int EdgePad = 16;

        public static bool ShowTip(bool metricsXpFound)
        {
            return !metricsXpFound;
        }

        public static int AfkBottom(int lineHeight)
        {
            if (lineHeight < 16) lineHeight = 16;
            int rowH = Math.Max(28, lineHeight + 8);
            int sacH = lineHeight * 2 + 10;
            int sacY = 66 + (rowH + 4) * 4;
            int afkY = sacY + sacH + 4;
            return afkY + rowH;
        }

        public static void Foot(int lineHeight, int lineWidth, bool showTip, bool showNotes, out int panelW, out int hintY, out int hintH, out int buttonY, out int notesY, out int notesH, out int panelH)
        {
            if (lineHeight < 16) lineHeight = 16;
            if (lineWidth < 280) lineWidth = 280;
            panelW = Math.Max(400, lineWidth + 32);
            int afkBottom = AfkBottom(lineHeight);
            if (showTip)
            {
                hintY = afkBottom + 8;
                hintH = lineHeight * 2 + 12;
                buttonY = hintY + hintH + 16;
            }
            else
            {
                hintY = afkBottom;
                hintH = 0;
                buttonY = afkBottom + 8;
            }
            int buttonBottom = buttonY + 28;
            if (showNotes)
            {
                notesY = buttonBottom + 12;
                notesH = NotesBody;
                panelH = notesY + notesH + EdgePad;
            }
            else
            {
                notesY = buttonBottom;
                notesH = 0;
                panelH = buttonBottom + EdgePad;
            }
        }
    }
}
