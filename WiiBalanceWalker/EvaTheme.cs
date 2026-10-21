//----------------------------------------------------------------------------------------------------------------------+
// Visual theme for the reworked VR Balance Walker window: palettes, fonts, DPI scaling and shared drawing helpers.
//
// Palettes follow the Eva units (01 violet/green, 02 crimson/amber, 00 blue/yellow) with warm paper-white type. Headings use a heavy
// serif squeezed horizontally (the title-card look); interface text and numbers use Bahnschrift's condensed cuts.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    static class Eva
    {
        // Layout is written in 96-dpi units and multiplied by S (the process is DPI aware, so 1.75 at 175%).

        public static float S = 1f;

        public static int   P(float v) { return (int)Math.Round(v * S); }
        public static float F(float v) { return v * S; }

        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        public static void Init()
        {
            try { SetProcessDPIAware(); } catch { }
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
        }

        // Palette. These are the live colours of the current theme; SetTheme swaps them all at once.

        public static Color Bg0, Bg1, Bg2, Bg3;     // window, panel body, control wells, hover
        public static Color Line, LineHi;           // hairlines
        public static Color Orange, OrangeLo;       // primary accent
        public static Color Amber;
        public static Color Green, GreenLo;         // live / on
        public static Color Purple, PurpleLo;       // unit colour
        public static Color Red, Teal;
        public static Color Text, TextDim, TextOff;
        public static Color[] BodyRamp, PadRamp;    // board cel ramps: highlight, lit, mid, shadow
        public static Color Ink;                    // board outlines

        public class Theme
        {
            public string Name;
            public bool IsAero;                     // drawn with the Frutiger Aero skin instead of the Eva one
            public bool IsClassic;                  // the original stock layout in standard Windows controls
            public bool IsRetro;                    // ...with visual styles off: Windows Classic bevels and title bar
            public Color Bg0, Bg1, Bg2, Bg3, Line, LineHi, Orange, OrangeLo, Amber, Green, GreenLo, Purple, PurpleLo, Red, Teal, Text, TextDim, TextOff, Ink;
            public Color[] BodyRamp, PadRamp;

            public Color[] Slots() { return new[] { Bg0, Bg1, Bg2, Bg3, Line, LineHi, Orange, OrangeLo, Amber, Green, GreenLo, Purple, PurpleLo, Red, Teal, Text, TextDim, TextOff }; }
        }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static readonly Theme[] Themes =
        {
            // Unit-01: violet-black, purple and green, NERV warning orange.
            new Theme
            {
                Name = "UNIT-01",
                Bg0 = C(14, 10, 18), Bg1 = C(24, 18, 31), Bg2 = C(36, 27, 46), Bg3 = C(52, 39, 66), Line = C(62, 47, 78), LineHi = C(96, 74, 118),
                Orange = C(255, 124, 30), OrangeLo = C(130, 66, 26), Amber = C(255, 186, 72), Green = C(156, 227, 75), GreenLo = C(70, 104, 40),
                Purple = C(126, 87, 194), PurpleLo = C(70, 48, 110), Red = C(232, 52, 63), Teal = C(64, 214, 196),
                Text = C(240, 232, 218), TextDim = C(150, 137, 160), TextOff = C(92, 82, 104), Ink = C(22, 13, 30),
                BodyRamp = new[] { C(253, 250, 243), C(238, 231, 219), C(208, 197, 187), C(150, 134, 166) },
                PadRamp  = new[] { C(160, 138, 200), C(128, 105, 168), C(100, 80, 140), C(68, 54, 104) },
            },
            // Unit-02: oxblood base, crimson body, amber accents, teal-green eyes.
            new Theme
            {
                Name = "UNIT-02",
                Bg0 = C(18, 9, 10), Bg1 = C(31, 15, 17), Bg2 = C(47, 23, 26), Bg3 = C(68, 33, 37), Line = C(84, 42, 47), LineHi = C(128, 66, 72),
                Orange = C(255, 160, 40), OrangeLo = C(132, 78, 24), Amber = C(255, 214, 92), Green = C(96, 230, 170), GreenLo = C(40, 104, 78),
                Purple = C(214, 46, 58), PurpleLo = C(118, 26, 34), Red = C(255, 72, 96), Teal = C(80, 200, 236),
                Text = C(246, 234, 226), TextDim = C(170, 138, 136), TextOff = C(106, 80, 82), Ink = C(30, 10, 12),
                BodyRamp = new[] { C(253, 248, 240), C(240, 228, 216), C(214, 194, 182), C(168, 120, 116) },
                PadRamp  = new[] { C(236, 104, 104), C(206, 66, 70), C(166, 44, 52), C(112, 28, 36) },
            },
            // Unit-00: deep navy, Rei's blue and white, yellow accents.
            new Theme
            {
                Name = "UNIT-00",
                Bg0 = C(8, 12, 20), Bg1 = C(14, 21, 33), Bg2 = C(22, 32, 49), Bg3 = C(33, 47, 70), Line = C(42, 58, 84), LineHi = C(70, 94, 130),
                Orange = C(255, 196, 46), OrangeLo = C(128, 98, 28), Amber = C(255, 226, 120), Green = C(120, 226, 255), GreenLo = C(40, 96, 118),
                Purple = C(70, 128, 230), PurpleLo = C(34, 64, 124), Red = C(240, 70, 80), Teal = C(110, 236, 196),
                Text = C(234, 240, 248), TextDim = C(134, 150, 174), TextOff = C(78, 92, 114), Ink = C(10, 16, 30),
                BodyRamp = new[] { C(250, 252, 255), C(230, 236, 244), C(196, 206, 222), C(126, 142, 170) },
                PadRamp  = new[] { C(134, 176, 240), C(98, 144, 222), C(70, 112, 190), C(44, 74, 136) },
            },
            // Aero: ice-white glass, Aqua blue, lime and sunny orange on navy type. Slot names keep their Eva roles
            // (Orange = primary accent), so code shared between the skins picks sensible colours.
            new Theme
            {
                Name = "AERO", IsAero = true,
                Bg0 = C(234, 243, 250), Bg1 = C(247, 251, 254), Bg2 = C(226, 237, 247), Bg3 = C(208, 226, 242), Line = C(176, 202, 224), LineHi = C(128, 168, 204),
                Orange = C(26, 140, 230), OrangeLo = C(150, 196, 236), Amber = C(250, 140, 20), Green = C(70, 180, 40), GreenLo = C(170, 215, 150),
                Purple = C(60, 160, 230), PurpleLo = C(150, 200, 240), Red = C(228, 58, 52), Teal = C(0, 168, 186),
                Text = C(26, 50, 80), TextDim = C(92, 116, 146), TextOff = C(158, 174, 192), Ink = C(96, 126, 160),
                BodyRamp = new[] { C(255, 255, 255), C(244, 248, 252), C(222, 230, 239), C(172, 188, 206) },
                PadRamp  = new[] { C(232, 240, 248), C(208, 222, 236), C(184, 202, 220), C(146, 168, 192) },
            },
            // Debloated: the stock Windows look (the Debloated theme shows the original layout; these colours are for the
            // plain WinForms controls it shares with the other layouts). Every slot must differ from the others (even by one step), because
            // SetTheme maps colours back to slots by value.
            new Theme
            {
                Name = "DEBLOATED", IsClassic = true,
                Bg0 = C(240, 240, 240), Bg1 = C(240, 240, 241), Bg2 = C(255, 255, 255), Bg3 = C(229, 241, 251), Line = C(173, 173, 173), LineHi = C(112, 112, 112),
                Orange = C(0, 120, 215), OrangeLo = C(204, 228, 247), Amber = C(0, 0, 128), Green = C(0, 128, 0), GreenLo = C(144, 200, 144),
                Purple = C(0, 99, 177), PurpleLo = C(153, 180, 209), Red = C(196, 43, 28), Teal = C(0, 128, 128),
                Text = C(0, 0, 0), TextDim = C(96, 96, 96), TextOff = C(160, 160, 160), Ink = C(0, 0, 0),
                BodyRamp = new[] { C(255, 255, 255), C(240, 240, 240), C(220, 220, 220), C(180, 180, 180) },
                PadRamp  = new[] { C(220, 220, 220), C(205, 205, 205), C(190, 190, 190), C(160, 160, 160) },
            },
            // Retro: the stock layout as Windows 95-2000 drew it (visual styles off), on the old 212/208/200 grey.
            new Theme
            {
                Name = "RETRO", IsClassic = true, IsRetro = true,
                Bg0 = C(212, 208, 200), Bg1 = C(212, 208, 201), Bg2 = C(255, 255, 255), Bg3 = C(10, 36, 106), Line = C(128, 128, 128), LineHi = C(64, 64, 64),
                Orange = C(0, 0, 128), OrangeLo = C(166, 202, 240), Amber = C(0, 0, 127), Green = C(0, 128, 0), GreenLo = C(144, 200, 144),
                Purple = C(0, 0, 160), PurpleLo = C(153, 180, 209), Red = C(128, 0, 0), Teal = C(0, 128, 128),
                Text = C(0, 0, 0), TextDim = C(96, 96, 96), TextOff = C(160, 160, 160), Ink = C(0, 0, 1),
                BodyRamp = new[] { C(255, 255, 255), C(212, 208, 200), C(160, 160, 160), C(128, 128, 128) },
                PadRamp  = new[] { C(212, 208, 200), C(192, 192, 192), C(160, 160, 160), C(128, 128, 128) },
            },
        };

        public const int AeroIndex = 3, ClassicIndex = 4, RetroIndex = 5;
        public static bool Aero { get { return Current.IsAero; } }
        public static bool Classic { get { return Current.IsClassic; } }
        public static bool Retro { get { return Current.IsRetro; } }

        public static int ThemeIndex { get; private set; }
        public static Theme Current { get { return Themes[ThemeIndex]; } }

        static Eva() { Load(Themes[0]); }

        static void Load(Theme t)
        {
            Bg0 = t.Bg0; Bg1 = t.Bg1; Bg2 = t.Bg2; Bg3 = t.Bg3; Line = t.Line; LineHi = t.LineHi;
            Orange = t.Orange; OrangeLo = t.OrangeLo; Amber = t.Amber; Green = t.Green; GreenLo = t.GreenLo;
            Purple = t.Purple; PurpleLo = t.PurpleLo; Red = t.Red; Teal = t.Teal;
            Text = t.Text; TextDim = t.TextDim; TextOff = t.TextOff; Ink = t.Ink;
            BodyRamp = t.BodyRamp; PadRamp = t.PadRamp;
        }

        // Switches theme. Controls under `root` that copied palette colours (BackColor/ForeColor, accents) are retinted to
        // the matching slot of the new theme, then everything repaints.

        public static void SetTheme(int index, Control root)
        {
            index = ((index % Themes.Length) + Themes.Length) % Themes.Length;
            var from = Current.Slots();
            var to = Themes[index].Slots();
            var map = new Dictionary<int, Color>();
            for (int i = 0; i < from.Length; i++) if (!map.ContainsKey(from[i].ToArgb())) map[from[i].ToArgb()] = to[i];

            ThemeIndex = index;
            Load(Themes[index]);
            WiiBalanceWalker.Aero.Gen++;
            if (root == null) return;

            Action<Control> walk = null;
            walk = c =>
            {
                Color n;
                if ((c.Tag as string) == "no-retint") return;       // the Debloated theme's stock controls keep system colours
                if (map.TryGetValue(c.BackColor.ToArgb(), out n)) c.BackColor = n;
                if (map.TryGetValue(c.ForeColor.ToArgb(), out n)) c.ForeColor = n;
                var panel = c as EvaPanel;   if (panel != null && map.TryGetValue(panel.Accent.ToArgb(), out n)) panel.Accent = n;
                var slider = c as EvaSlider; if (slider != null && map.TryGetValue(slider.Accent.ToArgb(), out n)) slider.Accent = n;
                var ro = c as EvaReadout;    if (ro != null && map.TryGetValue(ro.ValueColor.ToArgb(), out n)) ro.ValueColor = n;
                foreach (Control k in c.Controls) walk(k);
            };
            walk(root);
            root.Invalidate(true);
        }

        public static Color A(Color c, int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c); }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // Fonts, cached. Sizes are in points, which already follow the display DPI.

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        static Font Get(string family, string fallback, float pt, FontStyle style)
        {
            var key = family + "|" + pt + "|" + style;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            f = new Font(family, pt, style, GraphicsUnit.Point);
            if (f.Name != family) { f.Dispose(); f = new Font(fallback, pt, style, GraphicsUnit.Point); }
            fonts[key] = f;
            return f;
        }

        public static Font Ui(float pt)       { return Get("Bahnschrift SemiCondensed", "Segoe UI", pt, FontStyle.Regular); }
        public static Font UiBold(float pt)   { return Get("Bahnschrift SemiBold SemiConden", "Segoe UI Semibold", pt, FontStyle.Regular); }
        public static Font Num(float pt)      { return Get("Bahnschrift SemiBold Condensed", "Segoe UI Semibold", pt, FontStyle.Regular); }
        public static Font NumLight(float pt) { return Get("Bahnschrift Light Condensed", "Segoe UI Light", pt, FontStyle.Regular); }
        public static Font Kanji(float pt)    { return Get("Yu Gothic UI Semibold", "Segoe UI", pt, FontStyle.Regular); }
        public static Font Mono(float pt)     { return Get("Cascadia Mono", "Consolas", pt, FontStyle.Regular); }

        static FontFamily serif;
        static FontFamily Serif { get { return serif ?? (serif = new FontFamily("Times New Roman")); } }

        public static void Quality(Graphics g)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        // Title-card type: heavy serif, squeezed to `squeeze` of its width and thickened with an outline of the same
        // colour. `px` is the em size in device pixels. Returns the drawn width.

        public static GraphicsPath TitlePath(string text, float x, float y, float px, float squeeze)
        {
            var path = new GraphicsPath();
            path.AddString(text, Serif, (int)FontStyle.Bold, px, new PointF(0, 0), StringFormat.GenericTypographic);
            using (var m = new Matrix())
            {
                m.Translate(x, y);
                m.Scale(squeeze, 1f);
                path.Transform(m);
            }
            return path;
        }

        public static float DrawTitle(Graphics g, string text, float x, float y, float px, Color color, float squeeze = 0.74f, float weight = 0.035f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            using (var path = TitlePath(text, x, y, px, squeeze))
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(color, px * weight) { LineJoin = LineJoin.Round })
            {
                g.FillPath(brush, path);
                if (weight > 0) g.DrawPath(pen, path);
                var b = path.GetBounds();
                return b.Right - x;
            }
        }

        public static float MeasureTitle(string text, float px, float squeeze = 0.74f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            using (var path = TitlePath(text, 0, 0, px, squeeze)) return path.GetBounds().Right;
        }

        // Chamfered rectangle: corners listed in `cut` (TL, TR, BR, BL bits 1,2,4,8) are clipped at 45 degrees.

        public const int TL = 1, TR = 2, BR = 4, BL = 8;

        public static GraphicsPath Chamfer(RectangleF r, float c, int cut)
        {
            var p = new GraphicsPath();
            var pts = new List<PointF>();
            if ((cut & TL) != 0) { pts.Add(new PointF(r.Left, r.Top + c)); pts.Add(new PointF(r.Left + c, r.Top)); } else pts.Add(new PointF(r.Left, r.Top));
            if ((cut & TR) != 0) { pts.Add(new PointF(r.Right - c, r.Top)); pts.Add(new PointF(r.Right, r.Top + c)); } else pts.Add(new PointF(r.Right, r.Top));
            if ((cut & BR) != 0) { pts.Add(new PointF(r.Right, r.Bottom - c)); pts.Add(new PointF(r.Right - c, r.Bottom)); } else pts.Add(new PointF(r.Right, r.Bottom));
            if ((cut & BL) != 0) { pts.Add(new PointF(r.Left + c, r.Bottom)); pts.Add(new PointF(r.Left, r.Bottom - c)); } else pts.Add(new PointF(r.Left, r.Bottom));
            p.AddPolygon(pts.ToArray());
            return p;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            var d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // L-shaped corner brackets around a rectangle.

        public static void Brackets(Graphics g, RectangleF r, float len, Pen pen)
        {
            g.DrawLines(pen, new[] { new PointF(r.Left, r.Top + len), new PointF(r.Left, r.Top), new PointF(r.Left + len, r.Top) });
            g.DrawLines(pen, new[] { new PointF(r.Right - len, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Top + len) });
            g.DrawLines(pen, new[] { new PointF(r.Right, r.Bottom - len), new PointF(r.Right, r.Bottom), new PointF(r.Right - len, r.Bottom) });
            g.DrawLines(pen, new[] { new PointF(r.Left + len, r.Bottom), new PointF(r.Left, r.Bottom), new PointF(r.Left, r.Bottom - len) });
        }

        public static void Text_(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y, StringFormat.GenericTypographic);
        }

        public static SizeF Measure(Graphics g, string s, Font f)
        {
            return g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, float right, float y)
        {
            var w = Measure(g, s, f).Width;
            Text_(g, s, f, c, right - w, y);
        }

        public static void TextCenter(Graphics g, string s, Font f, Color c, float cx, float y)
        {
            var w = Measure(g, s, f).Width;
            Text_(g, s, f, c, cx - w / 2, y);
        }
    }
}
