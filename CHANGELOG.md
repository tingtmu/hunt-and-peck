# Changelog

## 1.9.0 (unreleased)

### Mouse actions
- **Ctrl + last letter** right-clicks the element (context menus); **Ctrl+Shift + last letter** double-clicks it.
  Shift + last letter still left-clicks.

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
