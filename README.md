# AFK Warden

AFK Warden is a Companion module for Kavros. It watches the game window and warns you when you have gone idle, when hitpoints or prayer run low, and shortly before the inactivity timer would send you to AFK. Version 0.2.41 is a Windows .NET Framework 4.8 application.

Keep the RuneMetrics window open and turn on precise values, so the total is a full number. The game client can sit in the background, but it must not be minimized. Keep the chatbox visible, with game messages on, so the elder overload and aggression lines are read from chat.

The public module record is [`companion.module.json`](companion.module.json). The Companion panel can read it from this repository's `main` branch. While the program is running it also writes `%LocalAppData%\Kavros\Companion\modules\afk-warden.json` with `Running · v0.2.41`.

The window uses the shared Companion theme: `source/KavrosTheme.cs`, `OpaqueButton`, `KavrosCheckBox`, the Cinzel and Outfit fonts, the crest, the navy panels, the gold primary button, the dark secondary buttons, and the status pills.

## Install

Requires Windows 10 or 11 and .NET Framework 4.8.

A release tag `v0.2.41` (the first three parts of `source/app.manifest`) builds with `source/build.ps1`, packs with Velopack 1.2.161, and publishes:

- `KavrosAfkWarden-win-Setup.exe`
- `KavrosAfkWarden-win-Portable.zip`
- `KavrosAfkWarden-<version>-full.nupkg`
- `releases.win.json`

Close the previous copy with Exit. Run Setup, or extract the whole portable zip into a new folder and run `KavrosAfkWarden.exe` beside its `assets` folder. Do not run a bare copy of the exe with no `assets` folder.

Settings has **Check for updates at startup** and **Check for updates**. Both talk only to this repository's GitHub Releases. A folder copy that was not installed with Setup, and is not the portable package, cannot apply an update by itself.

## What you see

The small panel is owned by the Kavros window you picked. It moves with that window and hides only when that window is minimized. Other applications cover it by normal window order. It is not kept above other applications. Drag the header to change where it sits on the client. **Settings** and **−** are in the header. **−** collapses the panel to the header; **+** opens it again. **Pause** is the gold button. **Exit** is the dark button. A green **GAME DETECTED** pill names the client. **Pick client** waits for you to click the Kavros window to watch. Alert popups stay on top so they can be seen while the game is in the background. Settings stays open until you close it.

**Settings** is the large window: crest, Cinzel "AFK WARDEN" title, and three cards (Watch, Alerts, Channels). Closing that window returns to the panel.

An alert is a sound and a change on the panel. No extra window or Windows toast is opened, so the game keeps focus.

## Settings

Saved in `%LocalAppData%\KavrosAfkWarden\settings.txt`. They come back the next time you open the module.

| Setting | Default | What it does |
| --- | --- | --- |
| Idle | On, 45 seconds | Warns when the RuneMetrics total has not risen, and hitpoints have not dropped, for 45 seconds. |
| Low hitpoints | On, 30 percent | Warns below this percent, or below an absolute number if Percent is off. |
| Low prayer | On, 20 percent | Same, for prayer. |
| Potion alerts | On, 15 seconds | Elder overload and aggression from chat. Low is panel-only. Expiry plays a sound. |
| AFK teleport | On, 25 minutes, 30-second warning | Warns this many seconds before the inactivity timer expires. Grandmaster and Tree perk add time. |
| Play sounds | On | Master switch. Each alert has its own Sound box. Both must be on. |
| Tone | By alert | Chime, Bell, Soft, Drop, or Alarm. Test sound plays the one you picked. |
| Volume | 100 percent | Scales every alert sound and Test sound. |
| Cooldown | 20 seconds | After an alert fires, it will not fire again until the condition has cleared and this long has passed. |
| Pause watching | Off | Freezes every timer and does not alert. |

Test sound always runs, so you can check the device. Live alerts obey the sound switches.

Pin hitpoints or Pin prayer if the automatic search is looking at the wrong number. Drag a rectangle around that readout. Pin buff bar does the same for the row of potion timers. Clear pins returns to the automatic search.

## How the game is read

Kavros does not give this program a hitpoint value, a prayer value, or an inactivity countdown. The rule is: read pixels from the window, do not read process memory, and do not send mouse or keyboard input into the game.

Capture uses `Graphics.CopyFromScreen` on the client, focused or not, as long as the client is actually on a monitor. A full search runs about every four seconds. Between searches only the locked hitpoint, prayer, buff, and a small motion crop are copied. Windows Graphics Capture is not started unless its yellow system border can be turned off. On the Windows 10 contract this build uses, that switch is missing, so the border never appears and the screen path is used. A minimized window has no frame. A window covered by another application may not be readable, because those screen pixels are no longer the game.

A minimized Kavros window is not composed. Neither the screen nor the window surface has a frame. Hitpoints, prayer, and idle cannot be read until you restore it (it can stay behind other windows). The inactivity timer does not need those pixels, so it keeps running and can still notify you. The panel says so while the window is minimized.

Two similar frames are required before a number is trusted. A single bad frame does not alert, and a missing frame is not treated as a recovery.

**Hitpoints and prayer.** The HUD strip can sit anywhere. Life points are the number over the red bar, prayer is the number over the purple bar. The text is white, often `1,633/1,060`, and the current value may be higher than the maximum. The yellow adrenaline bar and the green summoning bar are not hitpoints or prayer. Pin hitpoints or Pin prayer if the strip is read wrong.

**Potions.** Buffs are the row of green-bordered icons. `60` is 60 seconds. `11m` is 11 minutes and does not warn until that icon changes to a seconds count in the last minute. A number that never counts down, such as a stack of `4`, does not warn. Pin buff bar if the row is missed.

**Potions.** The buff bar is the row of small timers along the top of the client. The shortest one is watched. It warns when that time drops below the threshold (15 seconds unless you change it). A plain number is seconds, so `65` is 65 seconds and `15` is 15. `1:05`, or `1m 05s` when the letters sit between the two numbers, is one minute and five seconds. A timer written only as `1m` or `15s` is skipped, so the letter is not read as part of the count and a minute is not turned into one second. The search does not look at the hitpoint and prayer numbers along the bottom. If the buff bar is somewhere else, or the text is too small to separate, use Pin buff bar and drag around the timers. No timers means nothing is running out. A missed frame does not clear an alert that already fired.

**Idle.** The character view is the centre of the client, outside the minimap, the top ribbon, and the bottom bar. Movement is either a clear change in that view or the minimap player dot moving. A skilling animation counts as movement. Moving the mouse does not. Time the window cannot be seen is not counted as stillness.

**Potion clock.** Once a potion timer has been read twice, Warden keeps counting it down between reads, so a blinking icon, bright scenery behind the digits, or an interface over the buff bar does not lose it. The panel shows `left (est.)` while the value comes from the clock. A minute value such as `11m` is pinned to the exact time when it ticks over to `10m`. A reading that disagrees with the clock has to repeat before it replaces it, and a timer that has not been read for two minutes no longer triggers the alert.

**AFK teleport.** The countdown starts when Warden attaches to the client and resets on detected game clicks or keys. The normal timeout is 25 minutes. Enable Grandmaster for 50 minutes, the tree perk for 40 minutes, or both for 65 minutes. A client already idle before Warden opens can teleport sooner than the displayed estimate.

## Rebuild

On Windows, with .NET Framework 4.8:

```
powershell -ExecutionPolicy Bypass -File source\build.ps1
KavrosAfkWarden.exe --self-test results.txt
```

The headless test checks the alert gates, the per-alert sound and notification switches, the settings file, idle versus the AFK clock, minimized and covered capture decisions, digit and bar reading, and that `companion.module.json` matches `source/app.manifest`. It does not open a window and it does not capture the desktop.

Tag `v0.2.41` to publish a release. The tag must match the first three parts of the version in `source/app.manifest`. While **Check for updates at startup** is on, an installed copy also checks GitHub Releases every three hours. A folder copy that was not installed with Setup cannot update itself.

## What was not verified

This copy was built and logic-tested on Linux. The Windows compiler, Velopack pack, the live WinForms theme, and a running Kavros client were not executed here. The exact AFK-area delay on Kavros was not published in the sources checked, and the HUD anchors can move if the interface scale changes. Pin the number if the automatic search misses it.
