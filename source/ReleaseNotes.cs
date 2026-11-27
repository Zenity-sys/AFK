using System.Text;

namespace KavrosAfkWarden
{
    public sealed class ReleaseNote
    {
        public string Version = "";
        public string Body = "";
    }

    // Hand-written notes for players. Newest first. Shown once after an update.
    public static class ReleaseNotes
    {
        private static readonly ReleaseNote[] notes = new ReleaseNote[]
        {
            Note("v0.2.40", "Launch with --no-tray to run without a system tray icon."),
            Note("v0.2.39", "A new sacrifice still counts after the chat scrolls and the line sits where an older one was. The total has to match the last counted total plus the gain."),
            Note("v0.2.38", "The AFK timer resets only when you left-click the game. Moving the camera, or using another mouse button, does not reset it. The debug log keeps a much longer history and writes down each sacrifice line."),
            Note("v0.2.37", "What's new opens on the panel once after an update, and you can open it again from Settings. The sacrifice row shows points per hour since the last Reset."),
            Note("v0.2.36", "A sacrifice is counted once, from where the line sits in chat. A misread total waits until the same line is read again, so it is not counted a second time."),
            Note("v0.2.35", "Sacrifice points come from the gain written in the chat line. A wrong digit in the running total no longer adds millions of points."),
            Note("v0.2.34", "Each sacrifice is counted once. A last-digit misread does not count it again. The row shows this session's points and how many sacrifices."),
            Note("v0.2.33", "Garbled sacrifice lines still count. The RuneMetrics reminder stays on the panel."),
            Note("v0.2.32", "The panel counts sacrifice points from chat. It shows the session total, how many sacrifices, and Reset."),
            Note("v0.2.31", "Chat is read again within about two seconds, including the newest line."),
            Note("v0.2.30", "A potion alert fires when a new line appears, then waits five minutes. Hitpoint and prayer bars stay on the real bars."),
            Note("v0.2.29", "Settings headings have room to draw in full.")
        };

        public static int Count { get { return notes.Length; } }

        public static string VersionAt(int index)
        {
            if (index < 0 || index >= notes.Length) return "";
            return notes[index].Version;
        }

        public static string Text()
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < notes.Length; i++)
            {
                if (i > 0) text.Append("\r\n\r\n");
                text.Append(notes[i].Version).Append("\r\n").Append(notes[i].Body);
            }
            return text.ToString();
        }

        public static bool Unseen(string seen, string current)
        {
            if (seen == null) seen = "";
            if (current == null) current = "";
            return seen != current;
        }

        private static ReleaseNote Note(string version, string body)
        {
            ReleaseNote note = new ReleaseNote();
            note.Version = version;
            note.Body = body;
            return note;
        }
    }
}
