# Changelog

## 1.10.1 (2026-10-23)

No changes to the app itself.

### Development
- SDK-style project files (still .NET Framework 4.8.1). Building needs the ".NET SDK" component of VS 2022 Build Tools;
  `global.json` pins a 9.0 SDK.
- The version lives in `<Version>` in `src/Directory.Build.props`, which replaces `SolutionInfo.cs`.
- CI fails if the build writes files outside `bin\`/`obj\`, and builds the release zip on every push.
  Tests check hap.exe's title, version text (no commit suffix) and that it isn't 32-bit-preferred.

## 1.10.0 (2026-10-23)

### Mouse actions
- **Alt + last letter** hovers: the mouse moves to the element and stays there (tooltips, hover menus). Then the
  arrow keys, PgUp and PgDn scroll the pane under the mouse with the mouse wheel, whichever window has focus.
  Esc, any other key (including Ctrl/Shift + arrow) or moving the mouse stops scrolling; the key goes through as
  usual, so Alt+; still works.

### Options
- An "Overlay keys" section lists the keys that work with the hints shown (Shift / Ctrl / Ctrl+Shift / Alt
  with the last letter, Backspace, Esc, menu keys). The window has a fixed size and scrolls; OK and Cancel stay visible.

## 1.9.0 (2026-10-23)

### Mouse actions
- **Ctrl + last letter** right-clicks the element (context menus); **Ctrl+Shift + last letter** double-clicks it.
  Shift + last letter still left-clicks. Shift/Ctrl may stay held: HAP releases them for the click.
- **Menu hints:** when a hint opens a menu or dropdown (e.g. a Ctrl right-click, a submenu), its hints appear
  automatically without closing it. Letters pick, Esc hides the hints, and any other key (arrows, Enter) goes
  to the menu as usual.

### Overlay
- Hints that no longer match the typed letters disappear, instead of being dimmed.
- A letter that matches no hint is ignored and the typed-text box shakes, so a typo needs no Backspace.
- Labels follow reading order (rows top to bottom, each left to right) instead of the UI Automation tree order.
- "Nothing to click here" / "This window didn't respond" appears briefly when there are no hints, instead of nothing.
- Options: Hint style (classic yellow, follow the Windows theme, high contrast).

### Performance
- Only elements a hint can act on are fetched from the target app: about half the elements, 0–20% faster scans.

### Development
- GitHub Actions builds and tests every push; a `v*` tag publishes the release zip and `SHA256SUMS.txt`.
- Integration tests scan a WinForms fixture window through UI Automation.
- A test keeps `app.manifest`'s version in step with `SolutionInfo.cs`.

## 1.8.2
- Elements whose UI Automation action fails or does nothing are clicked with the mouse; Shift + last letter forces a click.
- About HAP tray menu item.

## 1.8.1
- Taskbar app hints are clickable; themed typed-text box.

## 1.8.0
- Bars mode (Ctrl+;): hints for the taskbar, including an auto-hidden one, and edge-docked bars.
