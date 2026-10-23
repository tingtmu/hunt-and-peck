# HuntAndPeck 1.9

Simple vimium/vimperator style navigation for Windows applications based on the UI Automation framework. In essence, it works the same as screen readers or accessibility programs but with the goal of making any Windows program faster to use.

It works for any Windows program that exposes UI Automation (Win32, WinForms, WPF, WinUI, browsers).

This is the 1.9 build (a fork of zsims/hunt-and-peck by Zachary Sims): https://github.com/tingtmu/hunt-and-peck

# Download

https://github.com/tingtmu/hunt-and-peck/releases/latest

Download `HuntAndPeck-1.9.0.zip` (portable, no installer), unzip it anywhere and run `hap.exe`. To update an
existing install, unzip over the old folder. `SHA256SUMS.txt` is there to verify the download.

# Configuration

Right-click (or left-click) the tray icon and select `Options`:

- **Hotkeys**: click the box for the window or taskbar hotkey and press the new combination (Ctrl, Alt or
  Win plus a key, or F1-F24 on their own). `Reset` restores the default (`Alt + ;` and `Ctrl + ;`). If
  another app already uses the combination, the window says so and the old hotkey stays active.
- **Letters**: the letters used for hint labels, 2-26 different letters A-Z (default `SADFJKLEWCMPGH`).
- **Font size**: hint label size, 6-72 (default 14).
- **Hint style**: hint label colours: classic yellow (default), follow the Windows light/dark theme, or high contrast.

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
3. An overlay window will be displayed, type any of the hint characters you see. What you type shows in a
   small box at the bottom of the screen, in light or dark colours following the Windows app theme.
   Hints that no longer match disappear; a letter that matches nothing is ignored (the box shakes).
    - Modifiers held while typing the last letter choose a mouse action instead (e.g. type `d` then
      `Ctrl + D`); they may stay held:

      | Last letter with | Action |
      |---|---|
      | (nothing) | the element's UI Automation action (a click if that fails) |
      | `Shift` | left click, for an element that ignores its UI Automation action |
      | `Ctrl` | right-click (context menu) |
      | `Ctrl + Shift` | double-click (e.g. open a file) |
    - Menus: when a hint opens a menu or dropdown (a right-click menu, a submenu), its hints appear on their
      own and the menu stays open. Letters pick an item, `Esc` hides the hints, and any other key (arrows,
      `Enter`) goes to the menu as usual.

Alternatively, Hunt and Peck can be launched via the command-line or AutoHotKey by specifying `/hint`:
```
hap.exe /hint
```

Or in bars mode (as `Ctrl + ;`) with
```
hap.exe /tray
```

# Supported Elements
Elements with a UI Automation action (Invoke, Toggle, Select, Expand/Collapse) or an editable value get
hints. If an element's action fails (e.g. some Chrome toolbar buttons), HuntAndPeck clicks it with the mouse
instead. List, tree and table items in Qt apps (e.g. LINE's chat list), whose UI Automation action does
nothing, are always clicked with the mouse.
