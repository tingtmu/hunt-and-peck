# HuntAndPeck 1.8

Simple vimium/vimperator style navigation for Windows applications based on the UI Automation framework. In essence, it works the same as screen readers or accessibility programs but with the goal of making any Windows program faster to use.

It works for any Windows program (excluding Modern UI apps :))

This is the 1.8 build (a fork of zsims/hunt-and-peck by Zachary Sims): https://github.com/tingtmu/hunt-and-peck

# Download

https://github.com/tingtmu/hunt-and-peck/releases/latest

Download `HuntAndPeck-1.8.0.zip` (portable, no installer), unzip it anywhere and run `hap.exe`. To update an
existing install, unzip over the old folder. `SHA256SUMS.txt` is there to verify the download.

# Configuration

Right-click (or left-click) the tray icon and select `Options`:

- **Hotkeys**: click the box for the window or taskbar hotkey and press the new combination (Ctrl, Alt or
  Win plus a key, or F1-F24 on their own). `Reset` restores the default (`Alt + ;` and `Ctrl + ;`). If
  another app already uses the combination, the window says so and the old hotkey stays active.
- **Letters**: the letters used for hint labels, 2-26 different letters A-Z (default `SADFJKLEWCMPGH`).
- **Font size**: hint label size, 6-72 (default 14).

Changes apply when you press `OK`; `Cancel` (or Esc) discards them.

The tray menu's `Start with Windows` item starts HuntAndPeck at sign-in (an entry under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, which Task Manager's Startup apps page can also disable).

Where things live:

- Settings: `%LOCALAPPDATA%\HuntAndPeck\hap.exe_Url_<hash>\<version>\user.config` (standard .NET user
  settings; the hash depends on where `hap.exe` is). Settings carry over when you update HuntAndPeck in the
  same folder. If the file is damaged, HuntAndPeck renames it to `user.config.corrupt-<timestamp>`, starts with
  the defaults and shows a notification. Invalid values (e.g. edited by hand) are logged and replaced by their
  defaults.
- Log: `%LOCALAPPDATA%\HuntAndPeck\hap.log`.

# Screenshots

![ScreenShot](https://raw.github.com/zsims/hunt-n-peck/master/screenshots/explorer.png)
![ScreenShot](https://raw.github.com/zsims/hunt-n-peck/master/screenshots/visual-studio.png)

## To use

1. Launch the executable.
2. With any window focused, press `Alt + ;`
    - `Ctrl + ;` is bars mode: hints for the taskbar (also an auto-hidden one, which is shown while the
      overlay is open) and for edge-docked bars such as Zebar or YASB, on the monitor under the mouse. Pinned
      and running apps on the taskbar get hints too; selecting one acts like a click (switch to, minimize or
      launch the app).
3. An overlay window will be displayed, type any of the hint characters you see.

Alternatively, Hunt and Peck can be launched via the command-line or AutoHotKey by specifying `/hint`:
```
hap.exe /hint
```

Or in bars mode (as `Ctrl + ;`) with
```
hap.exe /tray
```

# Supported Elements
Only UI Automation elements with "Invoke" patterns are supported (and displayed).
