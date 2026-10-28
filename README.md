# A-Pen

**English** | [한국어](README.ko.md) | [日本語](README.ja.md)

A Windows pen app for online lectures and classroom teaching. Write over your slides, switch to a board, and save your board work to open it again in the next class. It works with pen tablets such as Wacom, a mouse, and interactive displays.

Version 1.0.0 · Windows 10/11 · [MIT License](LICENSE) · Based on [HighPen](https://github.com/stepanowon/high-pen)

![Writing over a slide with A-Pen](screen/hero.jpg)

## Install

1. Download **[A-Pen-Setup.exe](https://github.com/biyanet-ap/A-Pen/releases/latest/download/A-Pen-Setup.exe)** from the [latest release](https://github.com/biyanet-ap/A-Pen/releases/latest).
2. Run it. If **"Windows protected your PC"** appears, click **More info → Run anyway**. (The file is not code-signed, so Windows shows this warning.)
3. Answer the three questions: install, create a desktop shortcut, and start A-Pen now.

A-Pen is installed in `%LOCALAPPDATA%\Programs\A-Pen` and added to the Start menu. No administrator rights are needed.

- **Without installing:** download [A-Pen.exe](https://github.com/biyanet-ap/A-Pen/releases/latest/download/A-Pen.exe) and run it directly.
- **Update:** run the new installer. It replaces the old version.
- **Uninstall:** Settings → Apps → **Installed apps** → A-Pen → Uninstall. Your settings stay in `%APPDATA%\A-Pen`; delete that folder to remove them too.
- **Install without questions:** `A-Pen-Setup.exe /S`
- **Smart App Control:** if Windows 11 Smart App Control is on, the unsigned file is blocked with no "Run anyway" option. You have to turn Smart App Control off (Windows Security → App & browser control) to run A-Pen. Without the April 2026 Windows update (KB5083769) it can't be turned back on, so update Windows first.

The toolbar appears at the top left of the screen in English. To change the language, click **Settings** and pick one under **Language** (한국어, 日本語, English).

## Features

| Feature | What it does |
|---|---|
| **Pen** | Write over anything on the screen. Turn Pen off and the ink stays while you click through your slides. 8 colors, width 1–30, pen pressure. |
| **Board** | Cover the screen with a board: **Green → White → Black → Off**. Each board has its own pages (`◀ 2/5 ▶`, **+** new page, trash to delete). A pen color that would disappear into the board changes automatically. |
| **Eraser** | Wipes only where you rub, like a chalkboard eraser. Size 10–150. |
| **Area** | Drag a rectangle to erase everything inside it. |
| **Clear** | Erases the whole current page. |
| **Undo / Redo** | Takes back or reapplies one step at a time, separately for each page. |
| **Note** | Puts typed text in a box on the page. |
| **Capture** | Saves the active monitor as a PNG on your Desktop. The toolbar and mouse spot are not included. |
| **Open** | Opens an image as a new board page so you can keep writing on it. |
| **Zoom** | Zooms in 1.5x, 2x or 2.5x around the pointer. You can write while zoomed. |
| **Spot** | A see-through circle that follows the pointer so viewers can find it. |
| **Monitor** | With two or more monitors, pick the one A-Pen works on from the toolbar. |

Settings (pen, eraser, spot, zoom level, monitor, language) are saved right away to `%APPDATA%\A-Pen\settings.txt`.

<p>
<img src="screen/board.jpg" alt="Green board with a page of notes" width="49%">
<img src="screen/area-erase.jpg" alt="Dragging a rectangle to erase a note" width="49%">
</p>

Left: a page on the green board (page 1 of 2). Right: area erase. Everything inside the dragged rectangle is erased.

### Toolbar and settings

<img src="screen/settings.png" alt="Toolbar and settings panel" height="420">

> Ink and board pages are gone when you close A-Pen. Capture the board work you want to keep, and open it again with **Open**.

## Using an interactive display

1. Connect both the **HDMI port paired for touch** and the **USB touch port** of the display. With HDMI alone, the picture shows but touch does not work.
2. In Windows Settings → System → About, "Pen and touch" should show touch points.
3. If touching the display moves things on the laptop screen instead, run `tabletpc.cpl` → **Display** tab → **Setup** → **Touch input**, and touch the display when asked.
4. In A-Pen, pick the display's number in the monitor row on the toolbar.

## Build it yourself

No Visual Studio or .NET SDK needed. The script uses the C# compiler that comes with Windows.

```powershell
.\build.ps1    # builds dist\A-Pen.exe and dist\A-Pen-Setup.exe
```

## Based on HighPen

A-Pen is built on **[HighPen](https://github.com/stepanowon/high-pen)** by StephenWon, released under the MIT License. Thank you to the original author.

![About window with the original credit](screen/about.png)

**Added in A-Pen**

- Green board, and multiple pages on each board
- Eraser that wipes only where you rub (with size), and area erase
- Redo
- Capture to a PNG, and opening a capture as a board page
- Picking the monitor from the toolbar
- New toolbar layout and app icon

**Differences from HighPen**

- A-Pen uses a different name, install folder and settings folder, so you can install and run both at the same time.
- HighPen on winget and the Microsoft Store is the original app and doesn't include the features above.
- The original copyright notice is kept in [LICENSE](LICENSE).

## Privacy

A-Pen doesn't connect to the network and doesn't collect or send anything. Your drawings are saved only when you click Capture. See the [privacy policy](PRIVACY.md#english).

## License

[MIT License](LICENSE). Copyright (c) 2026 StephenWon (HighPen), Copyright (c) 2026 Andy (A-Pen changes).
