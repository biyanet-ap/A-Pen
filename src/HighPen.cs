using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HighPen
{
    static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        public static void AddExStyle(Window w, int style)
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | style);
        }

        public static void SetClickThrough(Window w, bool on)
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            int s = GetWindowLong(h, GWL_EXSTYLE);
            s = on ? (s | WS_EX_TRANSPARENT) : (s & ~WS_EX_TRANSPARENT);
            SetWindowLong(h, GWL_EXSTYLE, s);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);

        public const int WS_EX_LAYERED = 0x80000, WS_EX_TOPMOST = 8;
        public const uint WS_POPUP = 0x80000000, WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(int ex, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);

        [StructLayout(LayoutKind.Sequential)]
        public struct MAGTRANSFORM { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)] public float[] v; }
        [DllImport("user32.dll")] public static extern bool ClipCursor(ref RECT r);
        [DllImport("user32.dll")] public static extern bool ClipCursor(IntPtr r);
        [DllImport("Magnification.dll")] public static extern bool MagInitialize();
        [DllImport("Magnification.dll")] public static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowSource(IntPtr h, RECT r);
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowTransform(IntPtr h, ref MAGTRANSFORM t);
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowFilterList(IntPtr h, uint mode, int count, IntPtr[] list);

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        // Screen capture. CAPTUREBLT includes layered windows (the transparent ink overlay) in the copy.
        public const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);

        static IntPtr lastFg = IntPtr.Zero;
        static bool lastFgIsSnip;

        // True while the Windows snipping overlay (Win+Shift+S) is the foreground, full-screen window.
        public static bool IsSnippingOverlayActive()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) { lastFg = IntPtr.Zero; return false; }
            if (h != lastFg)
            {
                lastFg = h;
                lastFgIsSnip = false;
                try
                {
                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    string n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
                    lastFgIsSnip = n.Equals("SnippingTool", StringComparison.OrdinalIgnoreCase) || n.Equals("ScreenClippingHost", StringComparison.OrdinalIgnoreCase);
                }
                catch { }
            }
            if (!lastFgIsSnip) return false;
            RECT r;
            if (!GetWindowRect(h, out r)) return false;
            System.Drawing.Rectangle b = System.Windows.Forms.Screen.FromHandle(h).Bounds;
            // the overlay spans the whole screen; the editor window of Snipping Tool does not
            return (r.Right - r.Left) >= b.Width * 0.9 && (r.Bottom - r.Top) >= b.Height * 0.9;
        }

        public static System.Windows.Forms.Screen GetScreen(int idx)
        {
            System.Windows.Forms.Screen[] all = System.Windows.Forms.Screen.AllScreens;
            if (idx < 0 || idx >= all.Length) idx = 0;
            return all[idx];
        }

        // Positions a window in physical pixels (avoids DPI rounding on multi-monitor setups).
        public static void PlaceRect(Window w, int x, int y, int cx, int cy, bool resize)
        {
            IntPtr h = new WindowInteropHelper(w).EnsureHandle();
            SetWindowPos(h, HWND_TOPMOST, x, y, cx, cy, SWP_NOACTIVATE | (resize ? 0 : SWP_NOSIZE));
        }

        public static void BringTopmost(Window w)
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }
    }

    // Live zoom (1.5x / 2x / 2.5x) of the monitor under the mouse pointer, using the Windows Magnification API.
    // The view is positioned so the content under the pointer stays under the pointer,
    // so clicks (the host window is click-through) land exactly where they appear.
    class Zoomer
    {
        public static readonly double[] Factors = { 1.5, 2, 2.5 };
        float factor = 2f;
        IntPtr host, mag;
        IntPtr[] exclude;
        // While frozen the view stays put so the user can draw on it.
        public bool Frozen;
        // Reports the zoomed monitor bounds and the source rect being shown (empty bounds = zoom off).
        public Action<System.Drawing.Rectangle, Native.RECT> ViewChanged;
        System.Drawing.Rectangle current, locked;
        DispatcherTimer timer = new DispatcherTimer();
        public bool Active { get { return host != IntPtr.Zero; } }

        // Can be changed while zoomed; the view updates on the spot.
        public double Factor
        {
            get { return factor; }
            set
            {
                factor = (float)value;
                if (!Active) return;
                ApplyTransform();
                current = System.Drawing.Rectangle.Empty;
                bool f = Frozen; Frozen = false; Update(); Frozen = f;
            }
        }

        void ApplyTransform()
        {
            Native.MAGTRANSFORM t = new Native.MAGTRANSFORM();
            t.v = new float[] { factor, 0, 0, 0, factor, 0, 0, 0, 1 };
            Native.MagSetWindowTransform(mag, ref t);
        }

        public Zoomer()
        {
            timer.Interval = TimeSpan.FromMilliseconds(12);
            timer.Tick += delegate { Update(); };
        }

        public void Start(IntPtr[] excludeFromCapture)
        {
            if (Active) return;
            exclude = excludeFromCapture;
            if (!Native.MagInitialize()) return;
            IntPtr inst = Native.GetModuleHandle(null);
            host = Native.CreateWindowEx(Native.WS_EX_LAYERED | Native.WS_EX_TOPMOST | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW,
                "Static", "HighPenZoom", Native.WS_POPUP, 0, 0, 100, 100, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
            if (host == IntPtr.Zero) { Native.MagUninitialize(); return; }
            Native.SetLayeredWindowAttributes(host, 0, 255, 2);
            mag = Native.CreateWindowEx(0, "Magnifier", "HighPenMag", Native.WS_CHILD | Native.WS_VISIBLE, 0, 0, 100, 100, host, IntPtr.Zero, inst, IntPtr.Zero);
            if (mag == IntPtr.Zero) { Stop(); return; }
            ApplyTransform();
            // keep the zoom window itself, the toolbar, the ink overlay and the spot out of the magnified image
            IntPtr[] list = new IntPtr[exclude.Length + 1];
            list[0] = host;
            exclude.CopyTo(list, 1);
            Native.MagSetWindowFilterList(mag, 0, list.Length, list);
            current = System.Drawing.Rectangle.Empty;
            Update();
            Native.SetWindowPos(host, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOMOVE | Native.SWP_NOACTIVATE | 0x40);
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            if (host != IntPtr.Zero)
            {
                Native.ClipCursor(IntPtr.Zero);
                locked = System.Drawing.Rectangle.Empty;
                Native.DestroyWindow(host); host = IntPtr.Zero; mag = IntPtr.Zero; Native.MagUninitialize();
                if (ViewChanged != null) ViewChanged(System.Drawing.Rectangle.Empty, new Native.RECT());
            }
        }

        public void BringTopmost()
        {
            if (Active) Native.SetWindowPos(host, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOMOVE | Native.SWP_NOACTIVATE);
        }

        void Update()
        {
            if (!Active) return;
            if (Frozen && current != System.Drawing.Rectangle.Empty) return;
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return;
            // the zoomed monitor is fixed when zoom starts; the pointer is confined to it
            if (locked.IsEmpty)
            {
                locked = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y)).Bounds;
                Native.RECT clip;
                clip.Left = locked.Left; clip.Top = locked.Top; clip.Right = locked.Right; clip.Bottom = locked.Bottom;
                Native.ClipCursor(ref clip);
            }
            System.Drawing.Rectangle b = locked;
            p.X = Math.Max(b.Left, Math.Min(b.Right - 1, p.X));
            p.Y = Math.Max(b.Top, Math.Min(b.Bottom - 1, p.Y));
            if (b != current)
            {
                current = b;
                Native.SetWindowPos(host, Native.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, Native.SWP_NOACTIVATE | 0x40);
                Native.MoveWindow(mag, 0, 0, b.Width, b.Height, true);
            }
            float k = 1f - 1f / factor;
            Native.RECT r;
            r.Left = b.X + (int)Math.Round((p.X - b.X) * k);
            r.Top = b.Y + (int)Math.Round((p.Y - b.Y) * k);
            r.Right = r.Left + (int)(b.Width / factor);
            r.Bottom = r.Top + (int)(b.Height / factor);
            Native.MagSetWindowSource(mag, r);
            if (ViewChanged != null) ViewChanged(b, r);
        }
    }

    // UI strings in Korean / Japanese / English. Cur is an index into Codes.
    static class Lang
    {
        public static readonly string[] Codes = { "ko", "ja", "en" };
        public static readonly string[] Names = { "한국어", "日本語", "English" };
        public static int Cur;

        static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
        {
            { "Spot",          new[] { "스팟", "スポット", "Spot" } },
            { "MouseSpot",     new[] { "마우스 스팟", "マウススポット", "Mouse spot" } },
            { "Pen",           new[] { "펜", "ペン", "Pen" } },
            { "Eraser",        new[] { "지우개", "消しゴム", "Eraser" } },
            { "Area",          new[] { "영역", "範囲", "Area" } },
            { "AreaTip",       new[] { "영역 지우기 (드래그한 사각형 안을 지움)", "範囲消去 (ドラッグした四角形の中を消す)", "Area erase (erases inside the dragged rectangle)" } },
            { "EraserTip",     new[] { "지우개  (Ctrl+Alt+E, 지우개 상태에서 한 번 더 누르면 전체 삭제)", "消しゴム  (Ctrl+Alt+E、消しゴム中にもう一度押すと全消去)", "Eraser  (Ctrl+Alt+E, press again while erasing to clear all)" } },
            { "Board",         new[] { "칠판", "ボード", "Board" } },
            { "BoardTip",      new[] { "칠판 (녹색/흰색/검정/끄기)", "ボード (緑/白/黒/オフ)", "Board (green/white/black/off)" } },
            { "WhiteBoard",    new[] { "흰 칠판", "白ボード", "White" } },
            { "BlackBoard",    new[] { "검은 칠판", "黒ボード", "Black" } },
            { "GreenBoard",    new[] { "녹색 칠판", "緑ボード", "Green" } },
            { "PrevPage",      new[] { "이전 페이지", "前のページ", "Previous page" } },
            { "NextPage",      new[] { "다음 페이지", "次のページ", "Next page" } },
            { "NewPage",       new[] { "새 페이지", "新しいページ", "New page" } },
            { "DeletePage",    new[] { "페이지 삭제", "ページを削除", "Delete page" } },
            { "DeleteConfirm", new[] { "이 페이지를 삭제할까요? 되돌릴 수 없습니다.", "このページを削除しますか？元に戻せません。", "Delete this page? This cannot be undone." } },
            { "Undo",          new[] { "되돌리기", "元に戻す", "Undo" } },
            { "Redo",          new[] { "다시 실행", "やり直し", "Redo" } },
            { "Clear",         new[] { "지우기", "全消去", "Clear" } },
            { "ClearTip",      new[] { "전체 지우기", "すべて消去", "Clear all" } },
            { "Zoom",          new[] { "확대", "拡大", "Zoom" } },
            { "ZoomTip",       new[] { "{0}배 확대", "{0}倍拡大", "{0}x zoom" } },
            { "ZoomFactor",    new[] { "확대 배율", "拡大倍率", "Zoom level" } },
            { "Bubble",        new[] { "말풍선", "吹き出し", "Note" } },
            { "Capture",       new[] { "캡처", "キャプチャ", "Capture" } },
            { "CaptureTip",    new[] { "화면 캡처 (바탕화면에 저장)", "画面キャプチャ (デスクトップに保存)", "Screenshot (saved to Desktop)" } },
            { "Open",          new[] { "불러오기", "開く", "Open" } },
            { "OpenTip",       new[] { "스크린샷을 칠판 새 페이지로 불러오기", "スクリーンショットをボードの新しいページに開く", "Open a screenshot as a new board page" } },
            { "Images",        new[] { "이미지", "画像", "Images" } },
            { "OpenFailed",    new[] { "이미지를 열 수 없습니다.", "画像を開けません。", "Could not open the image." } },
            { "SaveFailed",    new[] { "스크린샷을 저장하지 못했습니다.", "スクリーンショットを保存できませんでした。", "Could not save the screenshot." } },
            { "Settings",      new[] { "설정", "設定", "Settings" } },
            { "Collapse",      new[] { "접기/펴기", "折りたたみ/展開", "Collapse/expand" } },
            { "Quit",          new[] { "종료", "終了", "Quit" } },
            { "About",         new[] { "정보", "情報", "About" } },
            { "AboutTip",      new[] { "저작자 및 라이선스 정보", "作者とライセンス情報", "Author and license" } },
            { "Tagline",       new[] { "비대면 강의용 Windows 펜 앱", "オンライン授業向け Windows ペンアプリ", "A Windows pen app for online lectures" } },
            { "BasedOn",       new[] { "HighPen (StephenWon, MIT License)을 바탕으로 만들었습니다", "HighPen (StephenWon, MIT License) をもとに作成しました", "Based on HighPen by StephenWon (MIT License)" } },
            { "Author",        new[] { "저작자", "作者", "Author" } },
            { "Email",         new[] { "이메일", "メール", "Email" } },
            { "Website",       new[] { "웹사이트", "Web サイト", "Website" } },
            { "License",       new[] { "라이선스", "ライセンス", "License" } },
            { "Close",         new[] { "닫기", "閉じる", "Close" } },
            { "Monitor",       new[] { "작동 모니터", "使用モニター", "Monitor" } },
            { "MonitorTip",    new[] { "모니터 {0}  (순환: Ctrl+Alt+M)", "モニター {0}  (切替: Ctrl+Alt+M)", "Monitor {0}  (cycle: Ctrl+Alt+M)" } },
            { "Width",         new[] { "두께", "太さ", "Width" } },
            { "Size",          new[] { "크기", "大きさ", "Size" } },
            { "Shape",         new[] { "모양", "形", "Shape" } },
            { "Pressure",      new[] { "필압 반영", "筆圧", "Pressure" } },
            { "Language",      new[] { "언어", "言語", "Language" } },
            { "Red",           new[] { "빨강", "赤", "Red" } },
            { "Orange",        new[] { "주황", "オレンジ", "Orange" } },
            { "Yellow",        new[] { "노랑", "黄", "Yellow" } },
            { "Green",         new[] { "초록", "緑", "Green" } },
            { "Blue",          new[] { "파랑", "青", "Blue" } },
            { "Purple",        new[] { "보라", "紫", "Purple" } },
            { "Black",         new[] { "검정", "黒", "Black" } },
            { "White",         new[] { "흰색", "白", "White" } },
        };

        public static string T(string key) { return Text[key][Cur]; }

        // Saved code, or English when nothing (or an unsupported code) is saved.
        public static int FromCode(string code)
        {
            int i = Array.IndexOf(Codes, code);
            return i < 0 ? 2 : i;
        }
    }

    class Settings
    {
        public string PenColor = "#FF3B30";
        public double PenSize = 4;
        public string SpotColor = "#FFD60A";
        public double SpotSize = 70;
        public bool SpotOn = true;
        public int Monitor = 0;
        public bool Pressure = true;
        public double EraserSize = 40; // width of the eraser; its height is half of it
        public double ZoomFactor = 2;
        public int BarX = 20, BarY = 20; // offset from the monitor work area, in pixels
        public string Language = ""; // "" = English (the default)

        static string FilePath
        {
            get
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ToolbarWindow.AppName);
                Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "settings.txt");
            }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "PenColor") s.PenColor = v;
                    else if (k == "PenSize") s.PenSize = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                    else if (k == "SpotColor") s.SpotColor = v;
                    else if (k == "SpotSize") s.SpotSize = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                    else if (k == "SpotOn") s.SpotOn = v == "1";
                    else if (k == "Pressure") s.Pressure = v == "1";
                    else if (k == "EraserSize") s.EraserSize = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                    else if (k == "ZoomFactor") s.ZoomFactor = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                    else if (k == "Monitor") s.Monitor = int.Parse(v);
                    else if (k == "BarX") s.BarX = int.Parse(v);
                    else if (k == "BarY") s.BarY = int.Parse(v);
                    else if (k == "Language") s.Language = v;
                }
            }
            catch { }
            if (Array.IndexOf(Zoomer.Factors, s.ZoomFactor) < 0) s.ZoomFactor = 2;
            s.EraserSize = Math.Max(10, Math.Min(150, s.EraserSize)); // the eraser shape needs a positive size
            return s;
        }

        public void Save()
        {
            try
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                File.WriteAllLines(FilePath, new string[] {
                    "PenColor=" + PenColor,
                    "PenSize=" + PenSize.ToString(ic),
                    "SpotColor=" + SpotColor,
                    "SpotSize=" + SpotSize.ToString(ic),
                    "SpotOn=" + (SpotOn ? "1" : "0"),
                    "Monitor=" + Monitor,
                    "Pressure=" + (Pressure ? "1" : "0"),
                    "EraserSize=" + EraserSize.ToString(ic),
                    "ZoomFactor=" + ZoomFactor.ToString(ic),
                    "BarX=" + BarX,
                    "BarY=" + BarY,
                    "Language=" + Language });
            }
            catch { }
        }
    }

    // One undo step: a pen stroke, or every stroke removed by one eraser drag.
    class InkEdit
    {
        public StrokeCollection Added = new StrokeCollection();
        public StrokeCollection Removed = new StrokeCollection();
        public List<UIElement> Bubbles = new List<UIElement>(); // speech bubbles removed by an area erase
    }

    // One page: its ink, speech bubbles and undo history, plus the background image of a page opened from a screenshot.
    class Page
    {
        public StrokeCollection Strokes = new StrokeCollection();
        public List<UIElement> Bubbles = new List<UIElement>();
        public List<InkEdit> History = new List<InkEdit>();
        public List<InkEdit> Redo = new List<InkEdit>(); // undone steps, most recent last; emptied by any new step
        public ImageSource Image;
    }

    // Full-screen transparent window that hosts the ink canvas (also used as the whiteboard).
    class OverlayWindow : Window
    {
        public InkCanvas Canvas = new InkCanvas();
        // the screen annotations, and the pages of the white (Boards[0]), black (Boards[1]) and green (Boards[2]) boards
        public Page Screen = new Page();
        public List<Page>[] Boards = new List<Page>[] { new List<Page> { new Page() }, new List<Page> { new Page() }, new List<Page> { new Page() } };

        public void MoveToScreen(int idx)
        {
            System.Drawing.Rectangle b = Native.GetScreen(idx).Bounds;
            Native.PlaceRect(this, b.X, b.Y, b.Width, b.Height, true);
        }

        public OverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Canvas.Background = Brushes.Transparent;
            Stylus.SetIsPressAndHoldEnabled(Canvas, false);
            Stylus.SetIsFlicksEnabled(Canvas, false);
            Stylus.SetIsTapFeedbackEnabled(Canvas, false);
            // the eraser end of a pen erases the same way as the eraser button (the default is whole strokes)
            Canvas.EditingModeInverted = InkCanvasEditingMode.EraseByPoint;
            Canvas.Strokes = Screen.Strokes;
            Content = Canvas;

            SourceInitialized += delegate { Native.AddExStyle(this, Native.WS_EX_TOOLWINDOW); };
        }
    }

    // Click-through window that draws the spot following the mouse pointer.
    class SpotWindow : Window
    {
        Ellipse spot = new Ellipse();
        Canvas root = new Canvas();
        DispatcherTimer timer = new DispatcherTimer();

        public void MoveToScreen(int idx)
        {
            System.Drawing.Rectangle b = Native.GetScreen(idx).Bounds;
            Native.PlaceRect(this, b.X, b.Y, b.Width, b.Height, true);
        }

        public SpotWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            IsHitTestVisible = false;

            spot.IsHitTestVisible = false;
            root.Children.Add(spot);
            Content = root;

            SourceInitialized += delegate
            {
                Native.AddExStyle(this, Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
            };
            timer.Interval = TimeSpan.FromMilliseconds(12);
            timer.Tick += delegate { UpdatePosition(); };
            timer.Start();
        }

        public void SetStyle(Color c, double size)
        {
            spot.Width = size;
            spot.Height = size;
            spot.Fill = new SolidColorBrush(Color.FromArgb(0x66, c.R, c.G, c.B));
            spot.Stroke = null;
            spot.StrokeThickness = 0;
        }

        public void SetSpotVisible(bool on)
        {
            spot.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        void UpdatePosition()
        {
            if (spot.Visibility != Visibility.Visible) return;
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return;
            DpiScale d = VisualTreeHelper.GetDpi(this);
            double x = p.X / d.DpiScaleX - Left;
            double y = p.Y / d.DpiScaleY - Top;
            Canvas.SetLeft(spot, x - spot.Width / 2);
            Canvas.SetTop(spot, y - spot.Height / 2);
        }
    }

    class ToolbarWindow : Window
    {
        // Shown on screen and used for the settings folder, the single-instance names and screenshot files.
        // Keep in sync with AppName in Setup.cs.
        public const string AppName = "A-Pen";
        const string AppVersion = "1.0.0"; // keep in sync with Version in Setup.cs
        const string AuthorName = "Andy";
        const string AuthorEmail = "biyanet.ap@gmail.com";
        const string RepoUrl = "https://github.com/biyanet-ap/A-Pen";
        static readonly string[] Palette = { "#FF3B30", "#FF9500", "#FFD60A", "#34C759", "#0A84FF", "#BF5AF2", "#000000", "#FFFFFF" };
        // board button order: off → green → white → black → off (indexed by the current board number)
        static readonly int[] NextBoard = { 3, 2, 0, 1 };
        static readonly string[] BoardNames = { "Board", "WhiteBoard", "BlackBoard", "GreenBoard" }; // Lang keys
        static readonly Brush GreenBoardBg = new SolidColorBrush(Color.FromRgb(0x2E, 0x5B, 0x3C));
        static readonly string[] ColorNames = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Black", "White" }; // Lang keys
        static readonly Key[] ColorKeys = { Key.R, Key.O, Key.Y, Key.G, Key.B, Key.P, Key.K, Key.W };
        static readonly string[] ColorKeyText = { "R", "O", "Y", "G", "B", "P", "K", "W" };
        static readonly Brush PanelBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x23, 0x29));
        static readonly Brush Active = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xDB));
        static readonly Brush Idle = new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x38));
        static readonly Brush Line = new SolidColorBrush(Color.FromRgb(0x2E, 0x35, 0x3D));
        static readonly Brush CardBg = new SolidColorBrush(Color.FromRgb(0x25, 0x2A, 0x31));      // settings cards
        static readonly Brush SegIdle = new SolidColorBrush(Color.FromRgb(0x2F, 0x35, 0x3D));     // unselected choice in a card
        static readonly Brush SepLine = new SolidColorBrush(Color.FromRgb(0x34, 0x3B, 0x44));     // between toolbar groups
        static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0xA7, 0xB0, 0xBA));       // headings, secondary text
        static readonly Brush DangerText = new SolidColorBrush(Color.FromRgb(0xFF, 0x9C, 0x8F));  // clear page, delete page
        static readonly Brush QuitBg = new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
        static readonly Brush SwatchEdge = new SolidColorBrush(Color.FromRgb(0x5A, 0x63, 0x6E)); // unselected colour swatch
        static readonly Brush[] BoardDotFill = { null, Brushes.White, Brushes.Black, GreenBoardBg }; // by board number
        static readonly FontFamily MdlFont = new FontFamily("Segoe MDL2 Assets");

        Settings cfg;
        OverlayWindow overlay;
        SpotWindow spotWin;
        Zoomer zoomer = new Zoomer();

        bool penMode, eraser, collapsed, settingsOpen, textMode;
        bool areaErase; // with eraser: drag a rectangle to erase inside it, instead of rubbing
        bool AreaErasing { get { return eraser && areaErase; } }
        int board; // 0 none, 1 white, 2 black, 3 green
        int[] pageIndex = { 0, 0, 0 }; // current page of the white, black and green boards
        Page shownPage; // the page whose speech bubbles are on the canvas

        // The page being written on: the screen, or the current page of the board that is on.
        Page Cur { get { return board == 0 ? overlay.Screen : overlay.Boards[board - 1][pageIndex[board - 1]]; } }

        StackPanel buttons = new StackPanel();
        Border settingsPanel;
        TextBlock arrow;
        Border zoomBtn, bubbleBtn, spotBtn, penBtn, eraserBtn, areaBtn, boardBtn, setBtn, captureBtn;
        TextBlock boardLabel;
        Ellipse boardDot; // the colour of the board that is on, next to boardLabel
        FrameworkElement pagePanel;
        TextBlock pageLabel;
        DispatcherTimer topTimer = new DispatcherTimer();

        public ToolbarWindow(Settings s, OverlayWindow o, SpotWindow sw)
        {
            cfg = s; overlay = o; spotWin = sw;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = true; // lets the user pin the running app to the taskbar
            Title = AppName;
            Topmost = true;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            Left = SystemParameters.WorkArea.Right - 80;
            Top = SystemParameters.WorkArea.Top + 140;

            BuildContent();

            SourceInitialized += delegate
            {
                Native.AddExStyle(this, Native.WS_EX_NOACTIVATE);
                RegisterHotkeys();
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(MoveHook);
            };
            // SizeChanged comes before the window itself is resized, so check once the resize has happened
            SizeChanged += delegate { Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(KeepOnMonitor)); };
            shownPage = overlay.Screen;
            Track(overlay.Screen);
            foreach (List<Page> pages in overlay.Boards)
                foreach (Page p in pages) Track(p);
            overlay.Canvas.PreviewStylusDown += delegate { BeginPress(); };
            overlay.Canvas.PreviewMouseLeftButtonDown += delegate { BeginPress(); };
            overlay.Canvas.PreviewStylusUp += delegate { pressed = false; };
            overlay.Canvas.PreviewMouseLeftButtonUp += delegate { pressed = false; };
            overlay.Canvas.PreviewMouseLeftButtonDown += delegate (object sender, MouseButtonEventArgs e)
            {
                if (!textMode) return;
                DependencyObject src = e.OriginalSource as DependencyObject;
                while (src != null && !(src is TextBox) && src != overlay.Canvas)
                    src = (src is Visual || src is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(src) : LogicalTreeHelper.GetParent(src);
                if (src is TextBox) return;
                e.Handled = true;
                BeginBubble(e.GetPosition(overlay.Canvas));
            };
            overlay.Canvas.PreviewMouseLeftButtonDown += delegate (object sender, MouseButtonEventArgs e) { if (AreaErasing) BeginArea(e); };
            overlay.Canvas.PreviewMouseMove += delegate (object sender, MouseEventArgs e) { if (areaBox != null) MoveArea(e.GetPosition(overlay.Canvas)); };
            overlay.Canvas.PreviewMouseLeftButtonUp += delegate (object sender, MouseButtonEventArgs e) { if (areaBox != null) EndArea(e.GetPosition(overlay.Canvas)); };
            Loaded += delegate
            {
                ApplyMonitor();
                Native.SetClickThrough(overlay, true);
                ApplyMode();
                ApplySpot();
                ReassertTop();
            };

            // Slideshow / other topmost windows can cover us; re-assert periodically.
            zoomer.ViewChanged = OnZoomView;
            zoomer.Factor = cfg.ZoomFactor;
            snipTimer.Interval = TimeSpan.FromMilliseconds(100);
            snipTimer.Tick += delegate { CheckSnipping(); };
            snipTimer.Start();
            topTimer.Interval = TimeSpan.FromSeconds(1.5);
            topTimer.Tick += delegate { ReassertTop(); };
            topTimer.Start();
        }

        // Ink lives in screen coordinates of the unzoomed desktop. While zoomed, scale the canvas
        // the same way the magnifier scales the desktop so strokes line up with what is shown,
        // and pen input maps back to the right content position.
        System.Drawing.Rectangle zoomBounds = System.Drawing.Rectangle.Empty;
        Native.RECT zoomSource;

        void OnZoomView(System.Drawing.Rectangle b, Native.RECT r)
        {
            zoomBounds = b;
            zoomSource = r;
            ApplyZoomTransform();
        }

        void ApplyZoomTransform()
        {
            InkCanvas ic = overlay.Canvas;
            if (zoomBounds.IsEmpty || board != 0 || Native.GetScreen(cfg.Monitor).Bounds != zoomBounds)
            {
                ic.RenderTransform = Transform.Identity;
                return;
            }
            DpiScale d = VisualTreeHelper.GetDpi(overlay);
            double sx = (zoomSource.Left - zoomBounds.X) / d.DpiScaleX;
            double sy = (zoomSource.Top - zoomBounds.Y) / d.DpiScaleY;
            double f = zoomer.Factor;
            ic.RenderTransform = new MatrixTransform(f, 0, 0, f, -f * sx, -f * sy);
        }

        void ApplyMonitor()
        {
            if (cfg.Monitor >= System.Windows.Forms.Screen.AllScreens.Length) cfg.Monitor = 0;
            overlay.MoveToScreen(cfg.Monitor);
            spotWin.MoveToScreen(cfg.Monitor);
            System.Drawing.Rectangle wa = Native.GetScreen(cfg.Monitor).WorkingArea;
            DpiScale d = VisualTreeHelper.GetDpi(this);
            int bx = cfg.BarX, by = cfg.BarY;
            if (bx < 0 || by < 0 || bx > wa.Width - 40 || by > wa.Height - 40) { bx = 20; by = 20; }
            ShowSettingsPanel(false);
            Native.PlaceRect(this, wa.Left + bx, wa.Top + by, 0, 0, false);
            ShowSettingsPanel(settingsOpen && !collapsed);
            KeepOnMonitor();
            for (int i = 0; i < monitorBtns.Count; i++)
                monitorBtns[i].Background = i == cfg.Monitor ? Active : SegIdle;
            for (int i = 0; i < barMonitorBtns.Count; i++)
                barMonitorBtns[i].Background = i == cfg.Monitor ? Active : Idle;
            ReassertTop();
        }

        // ---------- keep the toolbar inside the working area of the selected monitor ----------

        const int WM_MOVING = 0x0216;

        void ClampToMonitor(ref Native.RECT r)
        {
            System.Drawing.Rectangle wa = Native.GetScreen(cfg.Monitor).WorkingArea;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            int x = Math.Max(wa.Left, Math.Min(wa.Right - w, r.Left));
            int y = Math.Max(wa.Top, Math.Min(wa.Bottom - h, r.Top));
            r.Left = x; r.Top = y; r.Right = x + w; r.Bottom = y + h;
        }

        // While dragging: adjust the proposed rect before the window moves.
        IntPtr MoveHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_MOVING) return IntPtr.Zero;
            Native.RECT r = (Native.RECT)Marshal.PtrToStructure(lParam, typeof(Native.RECT));
            ClampToMonitor(ref r);
            Marshal.StructureToPtr(r, lParam, false);
            handled = true;
            return (IntPtr)1;
        }

        // After a size or monitor change (expanding, opening settings), pull the toolbar back inside.
        void KeepOnMonitor()
        {
            Native.RECT r;
            IntPtr h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero || !Native.GetWindowRect(h, out r)) return;
            Native.RECT c = r;
            ClampToMonitor(ref c);
            if (c.Left != r.Left || c.Top != r.Top) Native.PlaceRect(this, c.Left, c.Top, 0, 0, false);
        }

        // ---------- about ----------

        Window aboutWin;

        void ShowAbout()
        {
            if (aboutWin != null) { aboutWin.Close(); return; }
            StackPanel p = new StackPanel { Margin = new Thickness(20, 16, 20, 16), Width = 300 };
            p.Children.Add(new TextBlock { Text = AppName + " v" + AppVersion, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold });
            p.Children.Add(new TextBlock { Text = Lang.T("Tagline"), Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0, 2, 0, 12), TextWrapping = TextWrapping.Wrap });
            p.Children.Add(AboutRow(Lang.T("Author"), AuthorName, null));
            p.Children.Add(AboutRow(Lang.T("Email"), AuthorEmail, "mailto:" + AuthorEmail));
            p.Children.Add(AboutRow(Lang.T("Website"), RepoUrl.Replace("https://", ""), RepoUrl));
            p.Children.Add(AboutRow(Lang.T("License"), "MIT License", RepoUrl + "/blob/main/LICENSE"));
            // the original work and its licence (MIT asks for its copyright notice to be kept)
            p.Children.Add(new TextBlock { Text = Lang.T("BasedOn"), Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
            p.Children.Add(new TextBlock { Text = "Copyright (c) 2026 " + AuthorName, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });

            Border close = new Border { Height = 32, Margin = new Thickness(0, 14, 0, 0), CornerRadius = new CornerRadius(8), Background = Idle, Cursor = Cursors.Hand };
            close.Child = new TextBlock { Text = Lang.T("Close"), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            p.Children.Add(close);

            Window w = new Window
            {
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false, Topmost = true, Title = AppName, Background = Brushes.Transparent, AllowsTransparency = true,
                Content = new Border { Background = PanelBg, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Child = p },
            };
            close.MouseLeftButtonUp += delegate { w.Close(); };
            w.KeyDown += delegate (object o, KeyEventArgs e) { if (e.Key == Key.Escape || e.Key == Key.Enter) w.Close(); };
            w.Closed += delegate { aboutWin = null; };
            // centered on the selected monitor
            w.Loaded += delegate
            {
                System.Drawing.Rectangle wa = Native.GetScreen(cfg.Monitor).WorkingArea;
                DpiScale d = VisualTreeHelper.GetDpi(w);
                int pw = (int)(w.ActualWidth * d.DpiScaleX), ph = (int)(w.ActualHeight * d.DpiScaleY);
                Native.PlaceRect(w, wa.Left + (wa.Width - pw) / 2, wa.Top + (wa.Height - ph) / 2, 0, 0, false);
            };
            aboutWin = w;
            w.Show();
        }

        static FrameworkElement AboutRow(string label, string value, string url)
        {
            Grid g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new TextBlock { Text = label, Foreground = Brushes.Gray, FontSize = 12 });
            TextBlock v = new TextBlock { Foreground = Brushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            if (url == null) v.Text = value;
            else
            {
                System.Windows.Documents.Hyperlink link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(value));
                link.Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xB2, 0xFF));
                link.Click += delegate { try { System.Diagnostics.Process.Start(url); } catch { } };
                v.Inlines.Add(link);
            }
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            return g;
        }

        void SaveBarPosition()
        {
            Native.RECT r;
            if (!Native.GetWindowRect(new WindowInteropHelper(this).Handle, out r)) return;
            System.Drawing.Rectangle wa = Native.GetScreen(cfg.Monitor).WorkingArea;
            cfg.BarX = r.Left - wa.Left + (settingsLeft ? SettingsPanelPx() : 0); // position of the bar itself
            cfg.BarY = r.Top - wa.Top;
            cfg.Save();
        }

        void CycleMonitor()
        {
            SetMonitor((cfg.Monitor + 1) % System.Windows.Forms.Screen.AllScreens.Length);
        }

        void SetMonitor(int i)
        {
            if (i == cfg.Monitor) return;
            // zoom belongs to the previous monitor (and confines the pointer to it)
            if (zoomer.Active) { zoomer.Stop(); Highlight(zoomBtn, false); }
            cfg.Monitor = i;
            cfg.Save();
            ApplyMonitor();
        }

        Panel penSwatches;

        void SetPenColor(int i)
        {
            cfg.PenColor = Palette[i];
            cfg.Save();
            foreach (Border b in penSwatches.Children)
            {
                bool sel = string.Equals((string)b.Tag, cfg.PenColor, StringComparison.OrdinalIgnoreCase);
                b.BorderBrush = sel ? Brushes.White : SwatchEdge;
                b.BorderThickness = new Thickness(sel ? 3 : 1);
            }
            ApplyPen();
        }

        Border pressureBtn;   // an on/off switch
        Ellipse pressureKnob;

        void UpdatePressureButton()
        {
            pressureBtn.Background = cfg.Pressure ? Active : SegIdle;
            pressureKnob.HorizontalAlignment = cfg.Pressure ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        void TogglePressure()
        {
            cfg.Pressure = !cfg.Pressure;
            cfg.Save();
            UpdatePressureButton();
            ApplyPen();
        }

        List<Border> monitorBtns = new List<Border>();     // in the settings panel
        List<Border> barMonitorBtns = new List<Border>();  // in the toolbar
        List<Border> zoomFactorBtns = new List<Border>();

        // While the snipping overlay is up, get out of its way: no input capture, no spot,
        // no z-order fighting. Normal state is restored when it closes.
        bool suspended;
        DispatcherTimer snipTimer = new DispatcherTimer();

        void CheckSnipping()
        {
            bool snip = Native.IsSnippingOverlayActive();
            if (snip == suspended) return;
            suspended = snip;
            if (suspended)
            {
                Native.SetClickThrough(overlay, true);
                spotWin.SetSpotVisible(false);
            }
            else
            {
                ApplyMode();
                ApplySpot();
            }
        }

        void ReassertTop()
        {
            if (suspended) return;
            zoomer.BringTopmost();
            Native.BringTopmost(overlay);
            Native.BringTopmost(spotWin);
            Native.BringTopmost(this);
            if (aboutWin != null) Native.BringTopmost(aboutWin);
        }

        // ---------- UI building ----------

        void BuildContent()
        {
            buttons = new StackPanel(); // a fresh panel: the old one is still parented to the previous toolbar
            monitorBtns.Clear();
            barMonitorBtns.Clear();
            barRow = new StackPanel();
            barRow.Orientation = Orientation.Horizontal;
            mainPanel = BuildMain();
            barRow.Children.Add(mainPanel);
            settingsPanel = BuildSettings();
            settingsPanel.Visibility = Visibility.Collapsed;
            settingsLeft = false;
            barRow.Children.Add(settingsPanel);
            Content = barRow;
        }

        // The settings panel opens to the right of the bar, or to its left when it would run past
        // the right edge of the monitor. The window moves so the bar itself stays where it is.
        StackPanel barRow;
        Border mainPanel;
        bool settingsLeft;

        // including the gap between the bar and the panel
        int SettingsPanelPx() { return ToPx(settingsPanel.ActualWidth + settingsPanel.Margin.Left + settingsPanel.Margin.Right); }
        int ToPx(double dip) { return (int)Math.Round(dip * VisualTreeHelper.GetDpi(this).DpiScaleX); }

        void ShowSettingsPanel(bool show)
        {
            if (show == (settingsPanel.Visibility == Visibility.Visible)) return;
            Native.RECT r;
            bool placed = Native.GetWindowRect(new WindowInteropHelper(this).Handle, out r);
            if (!show)
            {
                int w = SettingsPanelPx();
                settingsPanel.Visibility = Visibility.Collapsed;
                if (!settingsLeft) return;
                SetSettingsSide(false);
                UpdateLayout(); // shrink first, so the window never pokes past the monitor edge
                if (placed) Native.PlaceRect(this, r.Left + w, r.Top, 0, 0, false);
                return;
            }
            settingsPanel.Visibility = Visibility.Visible;
            if (!placed || !IsLoaded) return;
            UpdateLayout();
            int pw = SettingsPanelPx();
            // the window rect may not have caught up with the layout yet, so use the bar's own width
            if (r.Left + ToPx(mainPanel.ActualWidth) + pw <= Native.GetScreen(cfg.Monitor).WorkingArea.Right) return;
            SetSettingsSide(true);
            Native.PlaceRect(this, r.Left - pw, r.Top, 0, 0, false);
        }

        void SetSettingsSide(bool left)
        {
            settingsLeft = left;
            barRow.Children.Remove(settingsPanel);
            barRow.Children.Insert(left ? 0 : barRow.Children.Count, settingsPanel);
            settingsPanel.Margin = left ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0);
        }

        // Rebuilds the toolbar in the new language, keeping the current mode.
        void SetLanguage(int i)
        {
            Lang.Cur = i;
            cfg.Language = Lang.Codes[i];
            cfg.Save();
            BuildContent();
            buttons.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            arrow.Text = collapsed ? "" : "";
            Highlight(zoomBtn, zoomer.Active);
            ApplySpot();
            ApplyMonitor();
            ApplyMode();
        }

        // ---------- toolbar (two columns of tiles, grouped) ----------

        Border BuildMain()
        {
            StackPanel col = new StackPanel();

            // header: grip (drag) + collapse arrow
            Grid header = new Grid { Height = 32 };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border grip = new Border { Background = PanelBg, Cursor = Cursors.SizeAll, CornerRadius = new CornerRadius(8) };
            grip.MouseLeftButtonDown += delegate
            {
                try { DragMove(); } catch { }
                SaveBarPosition();
            };
            grip.Child = new TextBlock { Text = AppName, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), IsHitTestVisible = false, TextWrapping = TextWrapping.NoWrap };
            Border ab = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            arrow = new TextBlock { Text = "", FontFamily = MdlFont, Foreground = Muted, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ab.Child = arrow;
            ab.MouseLeftButtonUp += delegate { ToggleCollapse(); };
            Tip(ab, Lang.T("Collapse"), "H");
            Grid.SetColumn(ab, 1);
            header.Children.Add(grip);
            header.Children.Add(ab);
            col.Children.Add(header);

            // drawing
            penBtn = MakeButton("", Lang.T("Pen"), true, delegate { TogglePen(); });
            Tip(penBtn, Lang.T("Pen"), "P");
            bubbleBtn = MakeButton("", Lang.T("Bubble"), true, delegate { ToggleText(); });
            Tip(bubbleBtn, Lang.T("Bubble"), "T");
            eraserBtn = MakeButton("", Lang.T("Eraser"), true, delegate { ToggleEraser(); });
            eraserBtn.ToolTip = Lang.T("EraserTip");
            areaBtn = MakeButton("", Lang.T("Area"), true, delegate { ToggleAreaErase(); });
            Tip(areaBtn, Lang.T("AreaTip"), "A");
            Border undo = MakeButton("", Lang.T("Undo"), true, delegate { UndoStroke(); });
            Tip(undo, Lang.T("Undo"), "Z");
            Border redo = MakeButton("", Lang.T("Redo"), true, delegate { RedoStroke(); });
            Tip(redo, Lang.T("Redo"), "Y");

            // board: the tile shows the colour of the board that is on as a dot next to its name
            boardBtn = MakeButton("▭", "", false, delegate { CycleBoard(); });
            Tip(boardBtn, Lang.T("BoardTip"), "B");
            StackPanel boardSp = (StackPanel)boardBtn.Child;
            boardSp.Children.RemoveAt(1);
            boardDot = new Ellipse { Width = 8, Height = 8, Stroke = Brushes.White, StrokeThickness = 1, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            boardLabel = new TextBlock { Foreground = Brushes.White, FontSize = 11 };
            StackPanel boardName = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            boardName.Children.Add(boardDot);
            boardName.Children.Add(boardLabel);
            boardSp.Children.Add(boardName);

            // screen
            spotBtn = MakeButton("◎", Lang.T("Spot"), false, delegate { ToggleSpot(); });
            Tip(spotBtn, Lang.T("MouseSpot"), "S");
            zoomBtn = MakeButton("", Lang.T("Zoom"), true, delegate { ToggleZoom(); });
            Tip(zoomBtn, ZoomTipText(), "X");
            captureBtn = MakeButton("", Lang.T("Capture"), true, delegate { Capture(); });
            Tip(captureBtn, Lang.T("CaptureTip"), "K");
            Border openBtn = MakeButton("", Lang.T("Open"), true, delegate { OpenScreenshot(); });
            Tip(openBtn, Lang.T("OpenTip"), "O");

            // app
            setBtn = MakeButton("", Lang.T("Settings"), true, delegate { ToggleSettings(); });
            Tip(setBtn, Lang.T("Settings"), "D");
            Border aboutBtn = MakeButton("", Lang.T("About"), true, delegate { ShowAbout(); });
            aboutBtn.ToolTip = Lang.T("AboutTip");

            // cannot be undone: kept at the bottom, in warning colours
            Border clear = MakeButton("", Lang.T("Clear"), true, delegate { ClearPage(); });
            Tip(clear, Lang.T("ClearTip"), "C");
            SetTileForeground(clear, DangerText);
            Border quit = MakeButton("", Lang.T("Quit"), true, delegate { Quit(); });
            Tip(quit, Lang.T("Quit"), "Q");
            quit.Background = QuitBg;

            buttons.Children.Add(Sep());
            buttons.Children.Add(Pair(penBtn, bubbleBtn));
            buttons.Children.Add(Pair(eraserBtn, areaBtn));
            buttons.Children.Add(Pair(undo, redo));
            buttons.Children.Add(Sep());
            buttons.Children.Add(Spaced(boardBtn));
            pagePanel = BuildPagePanel();
            buttons.Children.Add(pagePanel);
            buttons.Children.Add(Sep());
            buttons.Children.Add(Pair(spotBtn, zoomBtn));
            buttons.Children.Add(Pair(captureBtn, openBtn));
            if (System.Windows.Forms.Screen.AllScreens.Length >= 2) buttons.Children.Add(BuildMonitorRow());
            buttons.Children.Add(Sep());
            buttons.Children.Add(Pair(setBtn, aboutBtn));
            buttons.Children.Add(Sep());
            buttons.Children.Add(Pair(clear, quit));

            col.Children.Add(buttons);

            return new Border { Width = 136, Background = PanelBg, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(6), Child = col, VerticalAlignment = VerticalAlignment.Top };
        }

        // Shown under the board button while a board is on: previous / page number / next, then new / delete.
        FrameworkElement BuildPagePanel()
        {
            Grid nav = new Grid();
            nav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            nav.ColumnDefinitions.Add(new ColumnDefinition());
            nav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            Border prev = SmallButton("", Lang.T("PrevPage") + "  (Ctrl+Alt+PgUp)", delegate { ChangePage(-1); });
            Border next = SmallButton("", Lang.T("NextPage") + "  (Ctrl+Alt+PgDn)", delegate { ChangePage(1); });
            prev.Background = Brushes.Transparent;
            next.Background = Brushes.Transparent;
            pageLabel = new TextBlock { Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(pageLabel, 1);
            Grid.SetColumn(next, 2);
            nav.Children.Add(prev);
            nav.Children.Add(pageLabel);
            nav.Children.Add(next);

            Border add = SmallButton("", Lang.T("NewPage") + "  (Ctrl+Alt+N)", NewPage);
            Border del = SmallButton("", Lang.T("DeletePage"), DeletePage);
            ((TextBlock)del.Child).Foreground = DangerText;

            StackPanel sp = new StackPanel { Visibility = Visibility.Collapsed };
            sp.Children.Add(Spaced(new Border { Height = 32, CornerRadius = new CornerRadius(8), Background = Idle, Child = nav }));
            sp.Children.Add(Pair(add, del));
            return sp;
        }

        // Monitor icon, then one number per monitor; picks the monitor directly (only built with two or more monitors).
        FrameworkElement BuildMonitorRow()
        {
            int n = System.Windows.Forms.Screen.AllScreens.Length;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            g.Children.Add(new TextBlock { Text = "", FontFamily = MdlFont, Foreground = Muted, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = Lang.T("Monitor") + "  (Ctrl+Alt+M)" });
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                g.ColumnDefinitions.Add(new ColumnDefinition());
                Border b = new Border { Height = 32, Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Background = Idle, ToolTip = string.Format(Lang.T("MonitorTip"), i + 1) };
                b.Child = new TextBlock { Text = (i + 1).ToString(), Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                b.MouseLeftButtonUp += delegate { SetMonitor(idx); };
                Grid.SetColumn(b, i + 1);
                barMonitorBtns.Add(b);
                g.Children.Add(b);
            }
            return Spaced(g);
        }

        // A thin line between toolbar groups.
        static Border Sep()
        {
            return new Border { Height = 1, Background = SepLine, Margin = new Thickness(2, 8, 2, 4) };
        }

        // Puts an element on its own toolbar row, 4 px below the previous one.
        static T Spaced<T>(T e) where T : FrameworkElement
        {
            e.Margin = new Thickness(0, 4, 0, 0);
            return e;
        }

        // Two buttons side by side on one toolbar row.
        static Grid Pair(Border left, Border right)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(right, 2);
            g.Children.Add(left);
            g.Children.Add(right);
            return Spaced(g);
        }

        // Recolours the icon and the name of a tile made by MakeButton.
        static void SetTileForeground(Border tile, Brush fg)
        {
            StackPanel sp = (StackPanel)tile.Child;
            ((TextBlock)sp.Children[0]).Foreground = fg;
            ((TextBlock)((Viewbox)sp.Children[1]).Child).Foreground = fg;
        }

        Border SmallButton(string glyph, string tip, Action click)
        {
            Border b = new Border { Height = 32, CornerRadius = new CornerRadius(8), Background = Idle, Cursor = Cursors.Hand, ToolTip = tip };
            b.Child = new TextBlock { Text = glyph, FontFamily = MdlFont, Foreground = Brushes.White, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }

        static void Tip(Border b, string name, string key)
        {
            b.ToolTip = name + "  (Ctrl+Alt+" + key + ")";
        }

        // A toolbar tile: an icon over its name. The name shrinks to fit when it is too long for the tile.
        Border MakeButton(string glyph, string label, bool segoe, MouseButtonEventHandler click)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock g = new TextBlock { Text = glyph, Foreground = Brushes.White, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center };
            g.FontFamily = segoe ? MdlFont : new FontFamily("Segoe UI Symbol");
            TextBlock l = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(g);
            sp.Children.Add(new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(3, 3, 3, 0), Child = l });
            Border b = new Border { Height = 56, CornerRadius = new CornerRadius(10), Background = Idle, Cursor = Cursors.Hand, Child = sp };
            b.MouseLeftButtonUp += click;
            return b;
        }

        // ---------- settings panel (cards) ----------

        Border BuildSettings()
        {
            StackPanel p = new StackPanel { Width = 272 };

            Grid head = new Grid { Height = 32, Margin = new Thickness(4, 0, 0, 4) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = Lang.T("Settings"), Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            Border close = SmallButton("", Lang.T("Close"), ToggleSettings);
            close.Width = 28;
            close.Height = 28;
            close.Background = Brushes.Transparent;
            ((TextBlock)close.Child).Foreground = Muted;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            p.Children.Add(head);

            StackPanel monitor = Card(p, Lang.T("Monitor"));
            int n = System.Windows.Forms.Screen.AllScreens.Length;
            System.Windows.Controls.Primitives.UniformGrid mp = Choices(n);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                Border mb = Choice((i + 1).ToString(), i == cfg.Monitor, delegate { SetMonitor(idx); });
                mb.ToolTip = string.Format(Lang.T("MonitorTip"), i + 1);
                monitorBtns.Add(mb);
                mp.Children.Add(mb);
            }
            monitor.Children.Add(mp);

            StackPanel pen = Card(p, Lang.T("Pen"));
            penSwatches = Swatches(cfg.PenColor, delegate (string c) { cfg.PenColor = c; ApplyPen(); cfg.Save(); }, true);
            pen.Children.Add(penSwatches);
            pen.Children.Add(MakeSlider(Lang.T("Width"), 1, 30, cfg.PenSize, delegate (double v) { cfg.PenSize = v; ApplyPen(); cfg.Save(); }));
            Grid pr = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            pr.ColumnDefinitions.Add(new ColumnDefinition());
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pr.Children.Add(new TextBlock { Text = Lang.T("Pressure"), Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            pressureKnob = new Ellipse { Width = 16, Height = 16, Fill = Brushes.White, Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
            pressureBtn = new Border { Width = 40, Height = 22, CornerRadius = new CornerRadius(11), Cursor = Cursors.Hand, ToolTip = Lang.T("Pressure") + "  (Ctrl+Alt+R)", Child = pressureKnob };
            pressureBtn.MouseLeftButtonUp += delegate { TogglePressure(); };
            UpdatePressureButton();
            Grid.SetColumn(pressureBtn, 1);
            pr.Children.Add(pressureBtn);
            pen.Children.Add(pr);

            StackPanel eraserCard = Card(p, Lang.T("Eraser"));
            eraserCard.Children.Add(MakeSlider(Lang.T("Size"), 10, 150, cfg.EraserSize, delegate (double v) { cfg.EraserSize = v; ApplyPen(); cfg.Save(); }));
            StackPanel shape = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            shape.Children.Add(new TextBlock { Text = Lang.T("Shape"), Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            shape.Children.Add(new Border { Width = 40, Height = 20, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(3) });
            shape.Children.Add(new TextBlock { Text = "2 : 1", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            eraserCard.Children.Add(shape);

            StackPanel spot = Card(p, Lang.T("MouseSpot"));
            spot.Children.Add(Swatches(cfg.SpotColor, delegate (string c) { cfg.SpotColor = c; ApplySpot(); cfg.Save(); }, false));
            spot.Children.Add(MakeSlider(Lang.T("Size"), 20, 200, cfg.SpotSize, delegate (double v) { cfg.SpotSize = v; ApplySpot(); cfg.Save(); }));

            StackPanel zoom = Card(p, Lang.T("ZoomFactor"));
            System.Windows.Controls.Primitives.UniformGrid zp = Choices(Zoomer.Factors.Length);
            zoomFactorBtns.Clear();
            foreach (double f in Zoomer.Factors)
            {
                double fv = f;
                Border zb = Choice(FactorText(f) + "×", f == cfg.ZoomFactor, delegate { SetZoomFactor(fv); });
                zoomFactorBtns.Add(zb);
                zp.Children.Add(zb);
            }
            zoom.Children.Add(zp);

            StackPanel language = Card(p, Lang.T("Language"));
            System.Windows.Controls.Primitives.UniformGrid lp = Choices(Lang.Codes.Length);
            for (int i = 0; i < Lang.Codes.Length; i++)
            {
                int idx = i;
                lp.Children.Add(Choice(Lang.Names[i], i == Lang.Cur, delegate { if (idx != Lang.Cur) SetLanguage(idx); }));
            }
            language.Children.Add(lp);

            p.Children.Add(new TextBlock { Text = AppName + " v" + AppVersion, Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });

            return new Border { Background = PanelBg, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Margin = new Thickness(8, 0, 0, 0), Child = p, VerticalAlignment = VerticalAlignment.Top };
        }

        // A settings card: a muted heading over its controls. Returns the panel the controls go in.
        static StackPanel Card(Panel parent, string title)
        {
            StackPanel c = new StackPanel();
            c.Children.Add(new TextBlock { Text = title, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });
            parent.Children.Add(new Border { Background = CardBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), Child = c });
            return c;
        }

        // A row of equal-width choices (monitor, zoom level, language); each choice adds 2 px on both sides.
        static System.Windows.Controls.Primitives.UniformGrid Choices(int count)
        {
            return new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Columns = count, Margin = new Thickness(-2, 0, -2, 0) };
        }

        static Border Choice(string text, bool on, MouseButtonEventHandler click)
        {
            Border b = new Border { Height = 32, Margin = new Thickness(2, 0, 2, 0), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand, Background = on ? Active : SegIdle };
            b.Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            b.MouseLeftButtonUp += click;
            return b;
        }

        static Color ParseColor(string s)
        {
            return (Color)ColorConverter.ConvertFromString(s);
        }

        static Panel Swatches(string current, Action<string> onPick, bool pen)
        {
            WrapPanel wp = new WrapPanel();
            List<Border> list = new List<Border>();
            for (int pi = 0; pi < Palette.Length; pi++)
            {
                string h = Palette[pi];
                Border b = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(ParseColor(h)), BorderBrush = SwatchEdge, BorderThickness = new Thickness(1), Tag = h };
                b.ToolTip = Lang.T(ColorNames[pi]) + (pen ? "  (Ctrl+Shift+" + ColorKeyText[pi] + ")" : "");
                if (string.Equals(h, current, StringComparison.OrdinalIgnoreCase)) { b.BorderBrush = Brushes.White; b.BorderThickness = new Thickness(3); }
                b.MouseLeftButtonUp += delegate
                {
                    foreach (Border o in list) { o.BorderBrush = SwatchEdge; o.BorderThickness = new Thickness(1); }
                    b.BorderBrush = Brushes.White; b.BorderThickness = new Thickness(3);
                    onPick(h);
                };
                list.Add(b);
                wp.Children.Add(b);
            }
            return wp;
        }

        // A named slider with its current value on the right.
        static Grid MakeSlider(string name, double min, double max, double val, Action<double> onChange)
        {
            Grid g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            TextBlock value = new TextBlock { Text = Math.Round(val).ToString(), Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Slider s = new Slider { Minimum = min, Maximum = max, Value = val, VerticalAlignment = VerticalAlignment.Center };
            s.ValueChanged += delegate { value.Text = Math.Round(s.Value).ToString(); onChange(s.Value); };
            g.Children.Add(new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(s, 1);
            g.Children.Add(s);
            Grid.SetColumn(value, 2);
            g.Children.Add(value);
            return g;
        }

        // ---------- behaviour ----------

        void ToggleSpot() { cfg.SpotOn = !cfg.SpotOn; ApplySpot(); cfg.Save(); }
        void TogglePen()
        {
            if (penMode && !eraser && !textMode) { SetPen(false); return; }
            eraser = false;
            textMode = false;
            SetPen(true);
        }
        // From the area eraser it switches to the rubbing eraser; otherwise it turns the eraser on or off.
        void ToggleEraser()
        {
            if (AreaErasing) areaErase = false;
            else { eraser = !eraser; areaErase = false; }
            textMode = false;
            if (eraser) penMode = true;
            ApplyMode();
        }
        // Turns the area eraser on, or off (back to the pen) when it is already on.
        void ToggleAreaErase()
        {
            if (AreaErasing) eraser = false;
            else { eraser = true; penMode = true; }
            areaErase = eraser;
            textMode = false;
            ApplyMode();
        }
        // First press enters eraser mode; pressing again while erasing wipes all strokes and speech bubbles on this page.
        void EraserHotkey()
        {
            if (eraser && !AreaErasing) ClearPage();
            else ToggleEraser();
        }
        void ToggleText() { textMode = !textMode; if (textMode) { penMode = true; eraser = false; } ApplyMode(); }
        void ToggleSettings() { settingsOpen = !settingsOpen; ApplyMode(); }
        void ToggleZoom()
        {
            if (zoomer.Active) zoomer.Stop();
            else zoomer.Start(new IntPtr[] {
                new WindowInteropHelper(this).Handle,
                new WindowInteropHelper(overlay).Handle,
                new WindowInteropHelper(spotWin).Handle });
            Highlight(zoomBtn, zoomer.Active);
            ReassertTop();
        }
        static string FactorText(double f) { return f.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        string ZoomTipText() { return string.Format(Lang.T("ZoomTip"), FactorText(cfg.ZoomFactor)); }

        void SetZoomFactor(double f)
        {
            cfg.ZoomFactor = f;
            cfg.Save();
            zoomer.Factor = f;
            for (int i = 0; i < zoomFactorBtns.Count; i++)
                zoomFactorBtns[i].Background = Zoomer.Factors[i] == f ? Active : SegIdle;
            Tip(zoomBtn, ZoomTipText(), "X");
        }

        void Quit() { zoomer.Stop(); cfg.Save(); Application.Current.Shutdown(); }

        // ---------- global hotkeys (Ctrl+Alt+key) ----------

        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;
        Dictionary<int, Action> hotkeys = new Dictionary<int, Action>();

        void RegisterHotkeys()
        {
            IntPtr h = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(h).AddHook(HotkeyHook);
            AddHotkey(h, Key.S, ToggleSpot);
            AddHotkey(h, Key.P, TogglePen);
            AddHotkey(h, Key.E, EraserHotkey);
            AddHotkey(h, Key.A, ToggleAreaErase);
            AddHotkey(h, Key.B, CycleBoard);
            AddHotkey(h, Key.Z, UndoStroke);
            AddHotkey(h, Key.Y, RedoStroke);
            AddHotkey(h, Key.C, delegate { ClearPage(); });
            AddHotkey(h, Key.D, ToggleSettings);
            AddHotkey(h, Key.T, ToggleText);
            AddHotkey(h, Key.X, ToggleZoom);
            AddHotkey(h, Key.M, CycleMonitor);
            AddHotkey(h, Key.R, TogglePressure);
            AddHotkey(h, Key.N, NewPage);
            AddHotkey(h, Key.K, Capture);
            AddHotkey(h, Key.O, OpenScreenshot);
            AddHotkey(h, Key.PageUp, delegate { ChangePage(-1); });
            AddHotkey(h, Key.PageDown, delegate { ChangePage(1); });
            for (int i = 0; i < Palette.Length; i++)
            {
                int ci = i;
                AddHotkey(h, ColorKeys[i], MOD_CONTROL | MOD_SHIFT, delegate { SetPenColor(ci); });
            }
            AddHotkey(h, Key.H, ToggleCollapse);
            AddHotkey(h, Key.Q, Quit);
        }

        void AddHotkey(IntPtr h, Key key, Action a)
        {
            AddHotkey(h, key, MOD_CONTROL | MOD_ALT, a);
        }

        void AddHotkey(IntPtr h, Key key, uint mods, Action a)
        {
            int id = hotkeys.Count + 1;
            if (Native.RegisterHotKey(h, id, mods | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(key)))
                hotkeys[id] = a;
        }

        IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            Action a;
            if (msg == WM_HOTKEY && hotkeys.TryGetValue(wParam.ToInt32(), out a)) { a(); handled = true; }
            return IntPtr.Zero;
        }

        // Called when the app is launched again while already running.
        public void Summon()
        {
            if (collapsed) ToggleCollapse();
            ReassertTop();
        }

        void ToggleCollapse()
        {
            collapsed = !collapsed;
            buttons.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            arrow.Text = collapsed ? "" : "";
            ApplyMode();
        }

        void SetPen(bool on)
        {
            penMode = on;
            if (!on) textMode = false;
            if (!on) { eraser = false; board = 0; }
            ApplyMode();
        }

        void CycleBoard()
        {
            board = NextBoard[board];
            if (board != 0) { penMode = true; eraser = false; textMode = false; }
            // keep the pen visible against the board colour
            Color c = ParseColor(cfg.PenColor);
            if (board == 2 && c == Colors.Black) cfg.PenColor = "#FFFFFF";
            else if (board == 1 && c == Colors.White) cfg.PenColor = "#000000";
            else if (board == 3 && (c == Colors.Black || c == ParseColor(Palette[3]))) cfg.PenColor = "#FFFFFF"; // black or green pen on the green board
            ApplyMode();
        }

        // ---------- screenshot ----------

        bool capturing;

        // Saves what the selected monitor shows (desktop, ink, speech bubbles or the board) to the Desktop.
        // The toolbar, the spot and the about window are hidden for the shot.
        void Capture()
        {
            if (capturing) return;
            capturing = true;
            Opacity = 0;
            spotWin.SetSpotVisible(false);
            if (aboutWin != null) aboutWin.Opacity = 0;
            // let WPF render the hidden state, then give the compositor a moment to put it on screen
            DispatcherTimer t = new DispatcherTimer(DispatcherPriority.ContextIdle) { Interval = TimeSpan.FromMilliseconds(80) };
            t.Tick += delegate
            {
                t.Stop();
                string error = null;
                try { SaveScreenshot(); }
                catch (IOException ex) { error = ex.Message; }
                catch (UnauthorizedAccessException ex) { error = ex.Message; }
                catch (ExternalException ex) { error = ex.Message; } // GDI: screen not readable (e.g. secure desktop) or the PNG could not be written
                finally
                {
                    Opacity = 1;
                    if (aboutWin != null) aboutWin.Opacity = 1;
                    ApplySpot();
                    capturing = false;
                }
                if (error != null) MessageBox.Show(this, Lang.T("SaveFailed") + "\n" + error, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                else Flash(captureBtn);
            };
            t.Start();
        }

        void SaveScreenshot()
        {
            System.Drawing.Rectangle b = Native.GetScreen(cfg.Monitor).Bounds;
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string name = AppName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string path = System.IO.Path.Combine(dir, name + ".png");
            for (int n = 2; File.Exists(path); n++) path = System.IO.Path.Combine(dir, name + "_" + n + ".png");
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(b.Width, b.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                {
                    // Graphics.CopyFromScreen rejects SourceCopy | CaptureBlt (InvalidEnumArgumentException), so blit directly
                    IntPtr dst = g.GetHdc();
                    IntPtr src = Native.GetDC(IntPtr.Zero);
                    try
                    {
                        if (!Native.BitBlt(dst, 0, 0, b.Width, b.Height, src, b.X, b.Y, Native.SRCCOPY | Native.CAPTUREBLT))
                            throw new System.ComponentModel.Win32Exception(); // an ExternalException: shown as "could not save"
                    }
                    finally
                    {
                        Native.ReleaseDC(IntPtr.Zero, src);
                        g.ReleaseHdc(dst);
                    }
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        bool opening;

        // Opens an image (a saved screenshot) as the background of a new page of the board that is on,
        // turning the first board of the cycle (green) on when no board is.
        void OpenScreenshot()
        {
            if (opening) return; // the hotkey still fires while the file dialog is up
            opening = true;
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                dlg.Filter = AppName + " (" + AppName + "_*.png)|" + AppName + "_*.png|" + Lang.T("Images") + " (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                if (dlg.ShowDialog(this) != true) return;

                System.Windows.Media.Imaging.BitmapImage img = new System.Windows.Media.Imaging.BitmapImage();
                string error = null;
                try
                {
                    img.BeginInit();
                    img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // read it now and release the file
                    img.UriSource = new Uri(dlg.FileName);
                    img.EndInit();
                    img.Freeze();
                }
                catch (IOException ex) { error = ex.Message; }
                catch (UnauthorizedAccessException ex) { error = ex.Message; }
                catch (NotSupportedException ex) { error = ex.Message; } // not an image WPF can decode
                catch (FormatException ex) { error = ex.Message; }       // corrupt image file
                if (error != null)
                {
                    MessageBox.Show(this, Lang.T("OpenFailed") + "\n" + error, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (board == 0) CycleBoard();
                Page p = new Page();
                p.Image = img;
                InsertPage(p);
            }
            finally { opening = false; }
        }

        // Briefly highlights a button to confirm an action.
        void Flash(Border b)
        {
            b.Background = Active;
            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            t.Tick += delegate { t.Stop(); b.Background = Idle; };
            t.Start();
        }

        // ---------- area eraser ----------

        Point areaStart;
        System.Windows.Shapes.Rectangle areaBox; // the dashed rectangle while dragging, null otherwise

        void BeginArea(MouseButtonEventArgs e)
        {
            if (e.StylusDevice != null && e.StylusDevice.Inverted) return; // the pen's eraser end rubs out as usual
            InkCanvas ic = overlay.Canvas;
            if (areaBox != null) ic.Children.Remove(areaBox); // a drag whose button-up was missed
            areaStart = e.GetPosition(ic);
            areaBox = new System.Windows.Shapes.Rectangle
            {
                Stroke = Active, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(0x33, 0x3E, 0x6F, 0xB0)), IsHitTestVisible = false,
            };
            MoveArea(areaStart);
            ic.Children.Add(areaBox);
            ic.CaptureMouse();
            e.Handled = true;
        }

        void MoveArea(Point p)
        {
            Rect r = new Rect(areaStart, p);
            InkCanvas.SetLeft(areaBox, r.X);
            InkCanvas.SetTop(areaBox, r.Y);
            areaBox.Width = r.Width;
            areaBox.Height = r.Height;
        }

        // Cuts out the ink inside the dragged rectangle (strokes crossing its edge are cut there)
        // and removes the speech bubbles lying wholly inside it, as one undo step.
        void EndArea(Point p)
        {
            InkCanvas ic = overlay.Canvas;
            ic.Children.Remove(areaBox);
            areaBox = null;
            ic.ReleaseMouseCapture();
            Rect r = new Rect(areaStart, p);
            if (r.Width < 3 || r.Height < 3) return; // a click, not a drag
            Page page = Cur;
            InkEdit edit = new InkEdit();
            page.History.Add(edit);
            newEdit = false;
            pressed = true; // RecordInkEdit adds every stroke change below to this step
            page.Strokes.Erase(r);
            pressed = false;
            foreach (UIElement b in page.Bubbles.ToArray())
            {
                FrameworkElement fe = (FrameworkElement)b;
                if (!r.Contains(new Rect(InkCanvas.GetLeft(b), InkCanvas.GetTop(b), fe.ActualWidth, fe.ActualHeight))) continue;
                page.Bubbles.Remove(b);
                ic.Children.Remove(b);
                edit.Bubbles.Add(b);
            }
            if (edit.Added.Count == 0 && edit.Removed.Count == 0 && edit.Bubbles.Count == 0) page.History.Remove(edit); // nothing was there
            else page.Redo.Clear();
        }

        // ---------- board pages ----------

        void ChangePage(int delta)
        {
            if (board == 0) return;
            int b = board - 1;
            int i = pageIndex[b] + delta;
            if (i < 0 || i >= overlay.Boards[b].Count) return;
            pageIndex[b] = i;
            ApplyMode();
        }

        // Inserts a page right after the current page of the board that is on, and shows it.
        void InsertPage(Page p)
        {
            int b = board - 1;
            Track(p);
            overlay.Boards[b].Insert(pageIndex[b] + 1, p);
            pageIndex[b]++;
            ApplyMode();
        }

        void NewPage()
        {
            if (board != 0) InsertPage(new Page());
        }

        // Deletes the current board page (asking first if it has anything on it) and shows the next one,
        // or the previous one when it was the last. A board always keeps at least one page.
        void DeletePage()
        {
            if (board == 0) return;
            Page p = Cur;
            bool empty = p.Strokes.Count == 0 && p.Bubbles.Count == 0 && p.Image == null;
            if (!empty && MessageBox.Show(this, Lang.T("DeleteConfirm"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            int b = board - 1;
            List<Page> pages = overlay.Boards[b];
            pages.RemoveAt(pageIndex[b]);
            if (pages.Count == 0)
            {
                Page blank = new Page();
                Track(blank);
                pages.Add(blank);
            }
            if (pageIndex[b] >= pages.Count) pageIndex[b] = pages.Count - 1;
            ApplyMode();
        }

        // Removes the ink and speech bubbles of the current page (a background image stays).
        void ClearPage()
        {
            Page p = Cur;
            p.History.Clear();
            p.Redo.Clear();
            undoing = true;
            p.Strokes.Clear();
            undoing = false;
            overlay.Canvas.Children.Clear();
            p.Bubbles.Clear();
        }

        // ---------- speech bubbles ----------

        void BeginBubble(Point pos)
        {
            InkCanvas ic = overlay.Canvas;
            TextBox tb = new TextBox();
            tb.MinWidth = 120;
            tb.FontSize = Math.Max(16, cfg.PenSize * 4);
            tb.AcceptsReturn = true;
            tb.Padding = new Thickness(6);
            tb.BorderBrush = new SolidColorBrush(ParseColor(cfg.PenColor));
            tb.BorderThickness = new Thickness(2);
            InkCanvas.SetLeft(tb, pos.X);
            InkCanvas.SetTop(tb, pos.Y);
            ic.Children.Add(tb);
            bool done = false;
            Action<bool> finish = delegate (bool commit)
            {
                if (done) return;
                done = true;
                ic.Children.Remove(tb);
                if (commit && tb.Text.Trim().Length > 0) AddBubble(tb.Text.Trim(), pos);
                textMode = false;
                ApplyMode();
            };
            tb.PreviewKeyDown += delegate (object o, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) finish(false);
                else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { finish(true); e.Handled = true; }
            };
            // Focus only after the overlay has actually been activated, and only treat focus loss
            // as "done" once the box has really had focus.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
            {
                overlay.Activate();
                tb.Focus();
                Keyboard.Focus(tb);
                tb.LostKeyboardFocus += delegate { finish(true); };
            }));
        }

        void AddBubble(string text, Point pos)
        {
            Color c = ParseColor(cfg.PenColor);
            Border b = new Border();
            b.Background = new SolidColorBrush(Color.FromArgb(0xEE, 255, 255, 255));
            b.BorderBrush = new SolidColorBrush(c);
            b.BorderThickness = new Thickness(2);
            b.CornerRadius = new CornerRadius(12);
            b.Padding = new Thickness(10, 6, 10, 6);
            b.Child = new TextBlock { Text = text, Foreground = Brushes.Black, FontSize = Math.Max(16, cfg.PenSize * 4), TextWrapping = TextWrapping.Wrap, MaxWidth = 600 };
            InkCanvas.SetLeft(b, pos.X);
            InkCanvas.SetTop(b, pos.Y);
            Cur.Bubbles.Add(b);
            overlay.Canvas.Children.Add(b);
        }

        // ---------- undo ----------

        bool undoing, pressed, newEdit;

        // Every page records its own undo history.
        void Track(Page p)
        {
            p.Strokes.StrokesChanged += delegate (object sender, StrokeCollectionChangedEventArgs e) { RecordInkEdit(p, e); };
        }

        // Stylus input is promoted to mouse events too, so only the first down of a press starts a new step.
        void BeginPress()
        {
            if (pressed) return;
            pressed = true;
            newEdit = true;
        }

        void RecordInkEdit(Page page, StrokeCollectionChangedEventArgs e)
        {
            if (undoing) return;
            List<InkEdit> h = page.History;
            if (newEdit || !pressed || h.Count == 0) { h.Add(new InkEdit()); newEdit = false; page.Redo.Clear(); }
            InkEdit top = h[h.Count - 1];
            foreach (Stroke st in e.Added) top.Added.Add(st);
            foreach (Stroke st in e.Removed)
            {
                if (top.Added.Contains(st)) top.Added.Remove(st);
                else top.Removed.Add(st);
            }
        }

        void UndoStroke()
        {
            List<InkEdit> h = Cur.History;
            if (h.Count == 0) return;
            InkEdit last = h[h.Count - 1];
            h.RemoveAt(h.Count - 1);
            Cur.Redo.Add(last);
            StrokeCollection s = Cur.Strokes;
            undoing = true;
            foreach (Stroke st in last.Added) if (s.Contains(st)) s.Remove(st);
            foreach (Stroke st in last.Removed) if (!s.Contains(st)) s.Add(st);
            undoing = false;
            foreach (UIElement b in last.Bubbles)
            {
                Cur.Bubbles.Add(b);
                overlay.Canvas.Children.Add(b);
            }
        }

        // Applies the most recently undone step again (the reverse of UndoStroke).
        void RedoStroke()
        {
            List<InkEdit> r = Cur.Redo;
            if (r.Count == 0) return;
            InkEdit next = r[r.Count - 1];
            r.RemoveAt(r.Count - 1);
            Cur.History.Add(next);
            StrokeCollection s = Cur.Strokes;
            undoing = true;
            foreach (Stroke st in next.Removed) if (s.Contains(st)) s.Remove(st);
            foreach (Stroke st in next.Added) if (!s.Contains(st)) s.Add(st);
            undoing = false;
            foreach (UIElement b in next.Bubbles)
            {
                Cur.Bubbles.Remove(b);
                overlay.Canvas.Children.Remove(b);
            }
        }

        void ApplyPen()
        {
            DrawingAttributes da = new DrawingAttributes();
            da.Color = ParseColor(cfg.PenColor);
            da.Width = cfg.PenSize;
            da.Height = cfg.PenSize;
            da.FitToCurve = true;
            da.IgnorePressure = !cfg.Pressure;
            overlay.Canvas.DefaultDrawingAttributes = da;
            // a board eraser: a rectangle twice as wide as it is tall
            InkCanvas ic = overlay.Canvas;
            ic.EraserShape = new RectangleStylusShape(cfg.EraserSize, cfg.EraserSize / 2);
            if (ic.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                // the eraser cursor only picks up a new shape when the editing mode changes
                ic.EditingMode = InkCanvasEditingMode.None;
                ic.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
        }

        void ApplySpot()
        {
            spotWin.SetStyle(ParseColor(cfg.SpotColor), cfg.SpotSize);
            spotWin.SetSpotVisible(cfg.SpotOn);
            Highlight(spotBtn, cfg.SpotOn);
        }

        void ApplyMode()
        {
            InkCanvas ic = overlay.Canvas;
            Page page = Cur;
            ic.Strokes = page.Strokes;
            // a page opened from a screenshot shows it on top of the board colour, keeping its aspect ratio
            ic.Background = page.Image != null ? (Brush)new ImageBrush(page.Image) { Stretch = Stretch.Uniform } : Brushes.Transparent;
            // the area eraser handles the mouse itself (see BeginArea)
            ic.EditingMode = textMode || AreaErasing ? InkCanvasEditingMode.None : (eraser ? InkCanvasEditingMode.EraseByPoint : InkCanvasEditingMode.Ink);
            if (shownPage != page)
            {
                shownPage = page;
                ic.Children.Clear();
                foreach (UIElement el in page.Bubbles) ic.Children.Add(el);
            }
            // InkCanvas' built-in ink cursor is a barely visible dot, so use a real pointer;
            // its eraser cursor shows the eraser's shape and size, so keep that one
            ic.UseCustomCursor = !eraser || AreaErasing;
            ic.Cursor = textMode ? Cursors.IBeam : (AreaErasing ? Cursors.Cross : Cursors.Pen);
            ApplyPen();

            if (board == 1) overlay.Background = Brushes.White;
            else if (board == 2) overlay.Background = Brushes.Black;
            else if (board == 3) overlay.Background = GreenBoardBg;
            else overlay.Background = penMode ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) : Brushes.Transparent;

            zoomer.Frozen = penMode;
            ApplyZoomTransform();
            Native.SetClickThrough(overlay, !penMode || suspended);
            ic.IsHitTestVisible = penMode;

            Highlight(penBtn, penMode && !eraser && !textMode);
            Highlight(eraserBtn, eraser && !AreaErasing);
            Highlight(areaBtn, AreaErasing);
            Highlight(bubbleBtn, textMode);
            Highlight(boardBtn, board != 0);
            Highlight(setBtn, settingsOpen);
            boardLabel.Text = Lang.T(BoardNames[board]);
            boardDot.Fill = BoardDotFill[board];
            boardDot.Visibility = board != 0 ? Visibility.Visible : Visibility.Collapsed;
            pagePanel.Visibility = board != 0 ? Visibility.Visible : Visibility.Collapsed;
            if (board != 0) pageLabel.Text = (pageIndex[board - 1] + 1) + "/" + overlay.Boards[board - 1].Count;

            ShowSettingsPanel(settingsOpen && !collapsed);
            ReassertTop();
        }

        static void Highlight(Border b, bool on)
        {
            b.Background = on ? Active : Idle;
        }
    }

    class App : Application
    {
        [STAThread]
        static void Main()
        {
            // One instance per user session: a second launch asks the running one to show its toolbar and exits.
            bool first;
            Mutex single = new Mutex(true, @"Local\" + ToolbarWindow.AppName + ".SingleInstance", out first);
            EventWaitHandle summon = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + ToolbarWindow.AppName + ".Summon");
            if (!first) { summon.Set(); return; }

            App app = new App();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Settings cfg = Settings.Load();
            Lang.Cur = Lang.FromCode(cfg.Language);
            OverlayWindow overlay = new OverlayWindow();
            SpotWindow spot = new SpotWindow();
            ToolbarWindow bar = new ToolbarWindow(cfg, overlay, spot);
            overlay.Show();
            spot.Show();
            bar.Show();
            ThreadPool.RegisterWaitForSingleObject(summon, delegate
            {
                bar.Dispatcher.BeginInvoke(new Action(bar.Summon));
            }, null, Timeout.Infinite, false);
            app.Run();
            GC.KeepAlive(single);
        }
    }
}
