# Changelog

## 0.2.41

- What's new stays inside the panel, with room under the box. The RuneMetrics reminder hides once your XP total is found.

## 0.2.40

- Launch with --no-tray to run without a system tray icon.

## 0.2.39

- A new sacrifice still counts when the chat scrolls and the line sits where an older sacrifice was. The total has to match the last counted total plus the gain.

## 0.2.38

- The AFK timer resets only when you left-click the game. Moving the camera does not reset it. The debug log keeps a much longer history and writes down each sacrifice line.

## 0.2.37

- What's new opens on the panel once after an update. Settings can open it again. The sacrifice row shows points per hour since the last Reset, and waits a minute before the first rate.

## 0.2.36

- A sacrifice line counts once, from its place in the chat. A total that does not match the last trusted total plus the gain is held until the same line is read again, so one misread total cannot add an extra sacrifice. The log lists every sacrifice the panel counted.

## 0.2.35

- Sacrifice points come from the gain written in the chat line. A total is kept only when it agrees with that gain, so a misread digit cannot add millions. The row still shows the session points and how many sacrifices.

## 0.2.34

- A sacrifice is counted from a real rise in the running total. A last-digit OCR flip, or a line with no readable gain, does not count again. The row shows only the session points and how many sacrifices.

## 0.2.33

- Sacrifice lines are ordered by where they sit on screen, and a garbled gain such as "I IS" still counts. A missing total does not drop the sacrifice.
- The RuneMetrics tip stays on the panel, dimmed, in the space above Pause and Exit.

## 0.2.32

- The panel counts sacrifice points from chat. Each new sacrifice message adds its gain once, shows how many sacrifices this session, and shows the latest game total. There is no sound. Reset clears the session counter.

## 0.2.31

- Chat is read again within two seconds even when the picture hash has not changed, and the crop follows the chat panel down to the newest line. A reused read is logged with its age.

## 0.2.30

- A potion line alerts when it is new since the previous chat read and that potion's warning is outside a five-minute cooldown. The same line scrolling up, or an OCR variant of it, does not alert again. Lines already on screen at startup are the baseline. A 30 second warning is still panel only. Expiry still plays a sound.
- The hitpoint and prayer bars stay where a valid total was read. A smaller overhead bar is not taken in their place. A lost bar shows a dash instead of the last number or a junk total. Boosted totals up to 4000 still count.

## 0.2.29

- Settings headings have enough room to draw in full, and long labels fit or wrap instead of being cut off at the edge.

## 0.2.28

- The alert tones are easy to tell apart: Chime climbs, Bell is a ding-dong, Alarm is fast beeps, Soft is one low note, and Drop is a longer low buzz. By alert still picks one of those for each alert. Test sound plays the one you chose.
- Closing RuneMetrics drops the saved spot. Opening it again is found within a few seconds.
- The RuneMetrics tip sits on the panel, above Resume and Exit. Settings is one row per alert, then Sound, then App. Idle is 45 seconds.

## 0.2.27

- Idle follows a rising RuneMetrics total, or a clear drop in hitpoints. An on-screen XP popup does not reset idle. Potion lines are unchanged.
- Low hitpoints, prayer, idle and potion alerts no longer open a window or a toast. Sound and the panel status remain. Nothing the module shows takes focus from the game.
- Boosted hitpoints and prayer can read up to 4000, including while they sit above the cap. A total above 4000 is ignored. The boosted number keeps updating instead of sticking.
- The RuneMetrics tip is larger, brighter, and on its own lines above the buttons. It says to keep RuneMetrics open with precise values.

## 0.2.26

- Idle follows an XP total that is going up, an XP popup, or a clear drop in hitpoints. The backpack and key presses do not count, and neither does prayer or a heal. A flat RuneMetrics total leaves only popups and hitpoint drops.
- A 30 second potion warning stays on the panel and is silent. It clears after 30 seconds. The sound plays when the potion expires, and that row still clears after 45 seconds.
- Settings has a choice of alert sounds, with a button to hear the one you picked. The client can be in the background, but not minimized.

## 0.2.25

- After the chat crop changes, it is read again on the next two looks even if the colour hash matches, so a line that appears while the chat is scrolling is not left until the picture moves. A torn potion line asks for that same short repeat, once. An unchanged crop is still not sent to OCR.

## 0.2.24

- RuneMetrics reads the column headed XP and leaves XP/h alone. A total that starts falling is dropped within a few seconds so the panel is found again.
- Chat is checked several times a second, and OCR runs only when the box changed. The crop follows the dark chat background down to the input bar, so a new line at the bottom is included.

## 0.2.23

- The chat crop reaches the newest line, down to the input bar, and the OCR page has room under that line so it is read within a second or two.
- A potion line that was not on screen at the first read alerts once. Low and expired are separate, so one does not swallow the other. Elder overload still goes Low, then Expired.
- Aggression is recognised when Windows OCR drops the word itself: "The effects of your Potion will expire in 30 seconds" is Low, and "have expired" is Expired.
- RuneMetrics reads the column headed XP. A layout that puts XP/h first no longer reports the rate as the total.
- The XP popup search covers the top 40 percent of the client, so a drop that sits a little below the compass still counts.
- An Expired potion row clears itself after about 45 seconds. The row then says Watching chat again, unless another tracked pot is still Low or Active.

## 0.2.22

- The potion row tracks elder overload and aggression only, under those full names. Prayer renewal, antifire and poison are left alone. Low stays orange and expired stays red.
- Red and dark-red chat is kept when the line is prepared for Windows OCR, so those warnings are not dropped. A fresh line is read about once a second, and a missing or misspelled word can still match.
- The XP popup search includes the top edge of the client, where the drop sits beside the compass.
- The RuneMetrics total looks for the red-green-blue bar icon, including the wider icon on a 1440p client. The hint says to enable precise values.
- The pin buttons are gone. Hitpoints, prayer and the buff bar are found on their own.

## 0.2.21

- Any XP popup in the centre of the sky counts as activity, including while it fades in, fades out or floats upward. The watcher looks at a wide centre-top band every 200 ms and compares the gold, orange and white text with the previous frame. The number is only read when it can be, and a popup that is already on screen is not wiped by a later search that misses the digits.
- Potion lines from Windows OCR match when the words are close to the known sentence after the usual mix-ups (u/v, a/e, rn/m, 0/o, 1/l/i). News, trivia and other chat still do not. The chat crop is tripled before it is read.

## 0.2.20

- Chat lines are read with Windows OCR from the chat box, doubled and turned into dark text on a light page. The raw text of each line is logged. If the English language pack is missing, the potion row says Chat reader unavailable.
- The '+N xp' popup is checked several times a second in the centre of the sky. Seeing the popup resets idle. The number does not have to be read correctly.

## 0.2.19

- An on-screen XP popup, such as "+351 xp" or "+2,437 xp (162.5 bonus xp)", resets idle. A RuneMetrics total is only a backup, and only when it is a full number. 1.6M and 250K are ignored. The hint appears only when neither signal is there.
- Potion lines are matched on the row of pixels that actually holds the glyphs, and the debug log shows the raw read beside the nearest phrase. The four red warnings each keep their own pot.
- Client 1, Client 2 and Client 3 follow process id order, in the picker and on the panel. Each client writes its own debug log. Settings has a Debug button that opens that folder. With no potion seen yet, the row says Watching chat.

## 0.2.18

- The floating panel stays on the client it is tracking. It follows that window, and it hides when that client is minimized or when another Kavros client is in front of the same spot. The panel shows that client's name.

## 0.2.17

- Coloured chat lines match. Red, orange and green game messages, including the six potion warnings on a red chat, map to the right pot and state. The strip along the top of the window is not treated as chat. The debug log includes the text read from each line.
- An empty capture frame keeps the last hitpoints, prayer and other readings. Minimized is shown only when the client really is minimized.
- The RuneMetrics total stays put. A jump or a drop is ignored until the same new total is read again several times.
- AFK Warden binds to the Kavros client under the Companion overlay that toggled it, including a client hwnd or pid passed in by Companion. It leaves the Companion overlay and Clue Helper windows alone, stays on that client, and says the client is closed instead of moving to another one.
- The client picker shows the character name Companion writes for that client's process. With no name, the clients are labelled Client 1, Client 2 and Client 3. The name is a label only.
- Each potion shows Active, with a countdown only where the duration is known, Low in orange, or Expired in red. The short names stay.

## 0.2.16

- Chat potion lines have to match the whole phrase. Ordinary chat, news and trivia no longer raise a potion alert. Lines already on screen when you attach are ignored, and each new line pings at most once. An expired potion stays quiet until a drink or a warning re-arms it.
- The debug log lists each chat line, the phrase it matched, the score, and whether it alerted, was already seen, was already on screen, or did not match. It also records the RuneMetrics total and why a read was missed.
- "Keep RuneMetrics XP open" stays hidden until the tracker has been missing for 5 seconds.
- RuneMetrics is found from the XP / XP/h / ETA header row, including a panel that is all zeros and one that also shows Damage. The potion line uses short labels so it fits the panel.

## 0.2.15

- Potion alerts are read from the chat box. Elder overload, overload, aggression, prayer renewal, antifire and antipoison each keep their own state: active, running low, or expired. A line already on screen does not alert again when the chat scrolls. The buff bar no longer raises potion alerts.
- Idle follows the RuneMetrics total XP. Keep that window open. An increase resets idle for skilling and for combat, along with hitpoints changes and clicks or keys.
- A covered or unfocused client is read with Windows Graphics Capture. A minimized client shows Minimized and pauses the reads. PrintWindow is not used.

## 0.2.14

- Live potion timers are read from the buff icons themselves. A full-size bar of four flasks at 4 minutes, a client with 6m, 5m, 6m and 6m, and another account's four flasks at 11 minutes all keep those times. Prayer and a stack count stay off the potion clock.
- Each confirmed XP drop resets idle. The same number still floating in the same spot for about a second is not a second drop. The backpack grid, once found, stays put when a later frame misses it.

## 0.2.13

- The buff bar is found by its row of timers, wherever it sits. A still client shot scaled back up reads four potions at 5 minutes. Pasting the same buff bar in another corner still reads.
- An orange plus in the scenery is not an XP drop. A drop needs the xp letters, and the search follows it instead of locking onto the trees. The same drop is still found after it is moved.
- The backpack grid is searched across the whole client and stays put once it repeats. Windows scale stays on 100, 150 or 200 after two agreeing reads.
- Boosted hitpoints such as 1471/990 stay legal.

## 0.2.12

- A potion is recognised from the flask itself: Elder is the dark teal flask, Aggro the purple flask, and Overload the green flask with a dark rim. The first clean 1–60 minute or second read is kept. A later read can correct it, and one odd reading does not replace a timer that is already counting. An empty buff frame leaves the clocks alone for a minute. The panel lists each potion with the time it has left.
- The backpack is the lower-right grid of slot borders, including when the window cuts off the bottom rows, at 100%, 150% and 200%.
- Orange XP drops stay visible while they float and fade. The hitpoint and prayer bars keep the last place they were found. Idle counts clicks, keys, XP drops and backpack changes, and the seconds keep moving on the panel.

## 0.2.11

- Buff times, hitpoints and prayer are matched against glyph stamps cut from the live captures, at 100%, 150% and 200%. A thin "1m" is not kept when the rest of the timer is still readable.
- The orange "+N xp" drop is found from a plus stamp confirmed by the xp letters. A backpack change counts only when the slots form a real grid.
- Enlarging a capture by 1.5 or 2 samples one source pixel, so a 12 stays a 12. Hitpoints 1633/1060, prayer 1040/990, the 12 minute flasks and the 11 minute Elder row read at 100%, 150% and 200%.

## 0.2.10

- Each potion keeps its own clock, matched by how the icon looks rather than where it sits, because buffs move. A reading is kept only after it agrees twice. A shorter reading replaces a longer one only when it repeats. A missed timer keeps counting for about 30 seconds. Prayer, stack counts, and a red ring with no flask or crystal are ignored. The panel shows the soonest confirmed potion by name.
- Woodcutting XP is the orange "+N xp" text above the player, not a hash of the scenery. Scene noise no longer holds the Skilling pill at 0 seconds. A backpack change is a slot filling or emptying.

## 0.2.9

- The whole screen no longer flashes black. The periodic capture never calls PrintWindow. A visible client is a small screen copy, focused or not. A covered client is left unread, and hitpoints, prayer and idle stay on the last good values. A black frame cannot raise an alert. The panel's topmost state changes only when that state changes.

## 0.2.8

- The game is read while it is behind other windows. A visible slot is still a small screen copy. A covered slot is taken from the window itself, and a black result falls back to the screen.
- Potion times keep minutes and seconds apart. The buff strip widens when the icons move, and one missed read does not drop a timer that is already counting down.
- XP drops and backpack changes count as skilling without a mode switch. Idle waits through the skilling gap, including while other motion is on screen, then warns when skilling stops.

## 0.2.7

- Life and prayer stay readable when the coloured fill is too short to find. Implausible values are dropped, and the panel moves when two of three reads agree.
- Potion timers keep counting down between reads. A missed frame does not jump the timer.
- Whole-client copies back off when the HUD cannot be read. Life and prayer share one grab, and slow slots are copied once a second.
- The panel stays above the game while the game is in front, without taking focus.
- Skilling idle uses XP drops, backpack changes, and clicks.

## 0.2.6

- AFK teleport uses the Kavros rates. Normal is 25 minutes. Grandmaster adds 25 minutes and the tree perk adds 15. The two switches are independent and saved. Old 300-second settings migrate to the normal rate. The warning lead is still adjustable.
- Capture stops while paused. Missing HUD locks retry on a delay, stable layouts use small screen regions, and full searches skip pixels that cannot contain a bar. The capture worker reuses its pixel buffers.
- The HP and prayer readers isolate the number line above each bar and retain manually pinned regions. Prayer can update while draining continuously. The bright-background screenshot reads 1,633 / 1,060 HP and 1,040 / 990 prayer.
- Potion recognition groups adjacent buff icons and reads white timer text against bright scenery. A brief missed timer shows its last reading for up to four seconds. An estimated value cannot trigger an alert.
- Opening Warden raises the game-owned overlay. Opening it again signals the running copy. Settings remain accessible when the game is minimized.

## 0.2.5

- The window opens on the first click. The panel shows Starting immediately. Client search, settings, the update check, and the first full capture run in the background. A second launch brings the existing window forward, or replaces it if it is stuck. Closing exits the process.
- Capture follows the small locked slots (life, prayer, buff strip, XP) about twice a second. A full client search runs at startup, when a lock is lost, and about every 12 seconds, off the UI thread. Debug images are saved only when that option is on, at most every 30 seconds, as a smaller JPEG.
- Life and prayer accept a short current value such as 917/1,060 or 9/990. The panel updates when two of the last three reads agree, and it does not show the maximum as the current value. A read with no new sample for five seconds is marked stale.
- Idle ignores mouse movement. Clicks and keys count only while the game is in front. An XP drop or a change in the backpack also counts. The reason is written to the debug log when that log is on.
- Potion time comes from potion icons (flasks, overload, antifire-style), not from a prayer buff or a stack count.

## 0.2.4

- Life and prayer numbers are read from the full bar slot, not the shrinking coloured fill. A good slot is kept, so a short fill on a later frame does not drop the number.
- A read with an unrecognised glyph is discarded. When the maximum has three or more digits, a shorter current value is discarded too, so a clipped `9/990` cannot replace `936/990`. Prayer is still shown only after three consistent reads.
- Buff icons are searched anywhere, including a detached bar, and their borders and timers tolerate a smaller icon. Minute timers and plain numbers of 10 or more show immediately. Each icon's timer text is written to the debug log.
- Idle resets only for input while the game window is in front, a change in life, an adrenaline increase, a target bar that actually changes width, or a hitsplat. Prayer drain, swaying trees, and other players do not count. The reason is written to the debug log. Pausing watching shows Paused on the panel.

## 0.2.3

- The window appears before the client list and the update check. Those run off the UI thread. Capture pauses while a window is dragged, and Settings uses the window manager for the drag.
- Life numbers are read again when the heart icon sits in the box. Prayer is shown only after three consistent reads, so a single bad frame does not jump the value.
- Idle ignores swaying trees, water and other people. It resets on input to the foreground client, a change in life, prayer or adrenaline, a target bar, a hitsplat, or a compact XP counter.
- The panel header is one bar. Settings and the collapse button sit on that bar, and the gold line runs the full width.
- Each read is appended to `%LocalAppData%\KavrosAfkWarden\debug-log.txt`. A full frame with the detected rectangles is saved as `debug-capture.png` in the same folder.

## 0.2.2

- Life is the red bar and prayer is the purple bar, wherever the HUD strip sits. Yellow adrenaline and green summoning are not life or prayer. Current may be higher than the cap, with thousands separators and no fixed upper limit.
- Buff timers are read from green-bordered icons anywhere on the client. `10m` is minutes. A plain number such as `60` is seconds.
- Combat and skilling count as activity. A target health bar, hitsplat or HUD value change, an XP or inventory change, or motion around the player resets the idle timer.
- Settings number boxes, labels, the AFK sound switch, and the client list use the navy and gold theme. The watch pill says GAME DETECTED. Header buttons are flat. Pinning freezes one capture while the rectangle is dragged.

## 0.2.1

- The installer splash says Kavros AFK Warden.
- The panel stays with the chosen client when you tab out and hides only while that client is minimized. Dragging the header uses the window manager.
- Capture no longer flushes the desktop compositor or scans the whole client on every tick. Windows Graphics Capture is not started unless its yellow border can be turned off, so the game has no capture border.
- Settings can save a debug capture of the frame and the detected regions under %LocalAppData%\KavrosAfkWarden.

## 0.2.0

- Life and prayer are read from the movable HUD strip: the red bar is life points and the purple bar is prayer, including comma separators and a current value above the maximum.
- Buff icons with a green border are read anywhere. `11m` is eleven minutes. A plain number alerts only after that icon has shown minutes or counted down, so a stack count does not warn.
- The status panel follows the chosen Kavros window and hides when that window is minimized or another application is in front. A green GAME DETECTED pill names the client. One client is watched, chosen from the list or by clicking it.
- Settings number boxes are text fields. A volume slider covers alert sounds. Settings stays open when another application is focused. Update checks repeat every three hours while startup checks are on.

## 0.1.0

- Idle, low hitpoints, low prayer, potion running out, and AFK-teleport warnings.
- Potion running out watches the buff-bar timers and warns below 15 seconds, with its own sound and notification switches. Pin buff bar if the timers are not along the top.
- Each alert has its own on/off switch, threshold, sound switch, and notification switch. Global sound and notification switches sit on top of those.
- Settings are saved under `%LocalAppData%\KavrosAfkWarden\settings.txt`.
- Watching continues while Kavros is visible but not focused, and while it is covered, using the window surface when Windows can provide it.
- Alerts are a sound (optional) and an always-on-top popup on the desktop, plus a Windows toast when the shell accepts one. They are not drawn inside the game.
