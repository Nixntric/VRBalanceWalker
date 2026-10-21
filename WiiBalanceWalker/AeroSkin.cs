//----------------------------------------------------------------------------------------------------------------------+
// Frutiger Aero skin: the second look for the window, after Mac OS X Aqua, Windows Vista, the Wii menu / Wii Fit and
// the Xbox 360 blades. Glossy gel, frosted glass, bubbles and an aurora sky, set in Segoe UI (a Frutiger descendant).
//
// Materials and sprites live here; each control's Aero painting sits next to its Eva painting. In Aero mode controls
// look "transparent": they first copy the pixels behind them from the nearest IAeroHost (the window backdrop or a glass
// panel, both cached bitmaps), so glass, shadows and gradients run seamlessly under every control.
//
// Anim is the shared frame clock: a control that is still moving asks for another frame from inside its OnPaint.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    // Something whose background its children copy in Aero mode. `r` is in the host's own coordinates and `g` is
    // already translated so that drawing there lands behind the child.
    interface IAeroHost
    {
        void PaintAeroBackground(Graphics g, Rectangle r);
    }

    // Frame scheduling for animated controls. Request = repaint on the next tick; RequestAt = repaint at a time.
    static class Anim
    {
        public static readonly Stopwatch Clock = Stopwatch.StartNew();
        public static float Now { get { return (float)Clock.Elapsed.TotalSeconds; } }

        static readonly HashSet<Control> next = new HashSet<Control>();
        static readonly Dictionary<Control, float> later = new Dictionary<Control, float>();
        static Timer timer;

        public static void Request(Control c) { next.Add(c); Start(); }

        public static void RequestAt(Control c, float when)
        {
            float t;
            if (!later.TryGetValue(c, out t) || when < t) later[c] = when;
            Start();
        }

        static void Start()
        {
            if (timer == null) { timer = new Timer { Interval = 25 }; timer.Tick += Tick; }
            if (!timer.Enabled) timer.Start();
        }

        static void Tick(object sender, EventArgs e)
        {
            var now = Now;
            var due = new List<Control>(next);
            next.Clear();
            if (later.Count > 0)
                foreach (var kv in new List<KeyValuePair<Control, float>>(later))
                    if (kv.Value <= now) { due.Add(kv.Key); later.Remove(kv.Key); }

            foreach (var c in due)
            {
                if (c.IsDisposed) continue;
                var f = c.FindForm();
                if (f != null && f.WindowState == FormWindowState.Minimized) { next.Add(c); continue; }   // resume on restore
                c.Invalidate();
            }
            if (next.Count == 0 && later.Count == 0) timer.Stop();
        }
    }

    // Time-based exponential ease. To() returns true while still moving.
    class Tw
    {
        public float V;
        float last = -1;

        public Tw(float v = 0f) { V = v; }

        public bool To(float target, float rate)
        {
            var now = Anim.Now;
            var dt = last < 0 ? 0f : Math.Min(0.1f, now - last);
            last = now;
            V += (target - V) * (1f - (float)Math.Exp(-rate * dt));
            if (Math.Abs(target - V) < 0.003f) { V = target; return false; }
            return true;
        }
    }

    // Damped spring for things that should overshoot and wobble into place.
    class Spr
    {
        public float X, V;
        float last = -1;

        public bool To(float target, float omega, float zeta)
        {
            var now = Anim.Now;
            var dt = last < 0 ? 0f : Math.Min(0.05f, now - last);
            last = now;
            for (int i = 0; i < 4; i++)
            {
                var h = dt / 4f;
                var a = omega * omega * (target - X) - 2f * zeta * omega * V;
                V += a * h;
                X += V * h;
            }
            if (Math.Abs(target - X) < 0.002f && Math.Abs(V) < 0.01f) { X = target; V = 0; return false; }
            return true;
        }
    }

    // A control's static artwork cached over its background; rebuilt on a theme change, resize or a new key.
    class AeroLayer
    {
        Bitmap bmp;
        int gen = -1;
        string key;

        public Bitmap Get(Control c, string k, Action<Graphics> draw)
        {
            var w = Math.Max(1, c.Width); var h = Math.Max(1, c.Height);
            if (bmp != null && gen == Aero.Gen && key == k && bmp.Width == w && bmp.Height == h) return bmp;
            if (bmp != null) bmp.Dispose();
            bmp = Aero.NewBitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                Aero.PaintBehind(g, c);
                Eva.Quality(g);
                draw(g);
            }
            gen = Aero.Gen; key = k;
            return bmp;
        }
    }

    static class Aero
    {
        // Bumped on every theme change; cached Aero bitmaps compare against it.
        public static int Gen;

        // ---- colours ---------------------------------------------------------------------------------------------

        public static readonly Color Aqua   = Color.FromArgb(28, 136, 228);
        public static readonly Color Lime   = Color.FromArgb(88, 188, 38);
        public static readonly Color Silver = Color.FromArgb(214, 223, 233);
        public static readonly Color Sun    = Color.FromArgb(255, 176, 30);
        public static readonly Color Cherry = Color.FromArgb(232, 62, 56);
        public static readonly Color Navy   = Color.FromArgb(10, 40, 80);

        public static Color Lighten(Color c, float t) { return Eva.Mix(c, Color.FromArgb(c.A, 255, 255, 255), t); }
        public static Color Darken(Color c, float t)  { return Eva.Mix(c, Color.FromArgb(c.A, 0, 0, 0), t); }
        public static Color Wh(int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), 255, 255, 255); }

        // ---- fonts -----------------------------------------------------------------------------------------------

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        static Font Get(string family, float pt, FontStyle style)
        {
            var key = family + "|" + pt + "|" + style;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            f = new Font(family, pt, style, GraphicsUnit.Point);
            if (f.Name != family) { f.Dispose(); f = new Font("Segoe UI", pt, style, GraphicsUnit.Point); }
            fonts[key] = f;
            return f;
        }

        public static Font Ui(float pt)    { return Get("Segoe UI", pt, FontStyle.Regular); }
        public static Font Semi(float pt)  { return Get("Segoe UI Semibold", pt, FontStyle.Regular); }
        public static Font Light(float pt) { return Get("Segoe UI Light", pt, FontStyle.Regular); }

        // ---- text ------------------------------------------------------------------------------------------------

        static readonly Dictionary<string, string> friendly = new Dictionary<string, string>
        {
            { "SYSTEM", "Balance Board" }, { "CONSOLE / KEYS", "Console & Keys" }, { "LOAD CELLS", "Weight Sensors" },
            { "OUTPUT VECTOR", "Movement" }, { "STARTUP", "Startup" }, { "SENSORS", "Pressure" }, { "OUTPUT", "Outputs" },
            { "LEAN", "Lean to Move" }, { "WALK IN PLACE", "Walk in Place" }, { "TWIST TO TURN", "Twist to Turn" },
        };

        // Panel titles get friendlier names; anything else in all caps becomes Title Case.
        public static string Friendly(string s)
        {
            string f;
            if (s != null && friendly.TryGetValue(s, out f)) return f;
            return Title(s);
        }

        public static string Title(string s)
        {
            if (string.IsNullOrEmpty(s) || s != s.ToUpperInvariant()) return s;
            var lower = s.ToLowerInvariant();
            return Regex.Replace(lower, @"\b[a-z]", m => m.Value.ToUpperInvariant());
        }

        // Dark text with a white emboss line under it (reads as pressed into glass).
        public static void Emboss(Graphics g, string s, Font f, Color c, float x, float y)
        {
            Eva.Text_(g, s, f, Wh(Math.Min(200, (int)c.A)), x, y + Eva.F(1));
            Eva.Text_(g, s, f, c, x, y);
        }

        // Light text with a soft drop shadow (for gel and sky).
        public static void Shadowed(Graphics g, string s, Font f, Color c, Color shadow, float x, float y)
        {
            Eva.Text_(g, s, f, shadow, x, y + Eva.F(1));
            Eva.Text_(g, s, f, c, x, y);
        }

        // ---- copying the background ------------------------------------------------------------------------------

        // Paints what is behind `c` (window backdrop or glass panel) into c's own coordinates.
        public static void PaintBehind(Graphics g, Control c)
        {
            int ox = 0, oy = 0;
            for (Control p = c; p.Parent != null; p = p.Parent)
            {
                ox += p.Left; oy += p.Top;
                var host = p.Parent as IAeroHost;
                if (host == null) continue;
                var st = g.Save();
                g.TranslateTransform(-ox, -oy);
                host.PaintAeroBackground(g, new Rectangle(ox, oy, c.Width, c.Height));
                g.Restore(st);
                return;
            }
            g.Clear(Eva.Bg1);
        }

        // Pixel-exact copy of part of a cached bitmap to the same place.
        public static void Blit(Graphics g, Bitmap src, Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, src.Width, src.Height));
            if (r.Width <= 0 || r.Height <= 0) return;
            var im = g.InterpolationMode; var po = g.PixelOffsetMode; var cm = g.CompositingMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(src, r, r.X, r.Y, r.Width, r.Height, GraphicsUnit.Pixel);
            g.InterpolationMode = im; g.PixelOffsetMode = po; g.CompositingMode = cm;
        }

        // Cache bitmap at screen DPI, so point-sized fonts drawn into it come out the same size as on screen.
        public static Bitmap NewBitmap(int w, int h)
        {
            var b = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
            b.SetResolution(96f * Eva.S, 96f * Eva.S);
            return b;
        }

        // Alpha-blended copy of part of a cached bitmap to the same place.
        public static void Overlay(Graphics g, Bitmap src, Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, src.Width, src.Height));
            if (r.Width <= 0 || r.Height <= 0) return;
            var im = g.InterpolationMode; var po = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(src, r, r.X, r.Y, r.Width, r.Height, GraphicsUnit.Pixel);
            g.InterpolationMode = im; g.PixelOffsetMode = po;
        }

        // Cheap blur: shrink with area averaging, grow back with bicubic.
        public static Bitmap Blur(Bitmap src, int factor)
        {
            var w = Math.Max(1, src.Width / factor); var h = Math.Max(1, src.Height / factor);
            var big = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
            using (var small = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                }
                using (var g = Graphics.FromImage(big))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(small, new Rectangle(0, 0, big.Width, big.Height), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                }
            }
            return big;
        }

        // ---- materials -------------------------------------------------------------------------------------------

        public static GraphicsPath Pill(RectangleF r) { return Eva.Round(r, Math.Min(r.Width, r.Height) / 2f); }

        // Soft radial glow filling an ellipse: a cached gradient sprite stretched over it (PathGradientBrush fills of
        // large areas are slow enough to matter at 60 fps).
        static readonly Dictionary<int, Bitmap> glows = new Dictionary<int, Bitmap>();

        public static void Glow(Graphics g, RectangleF r, Color c)
        {
            if (r.Width < 2 || r.Height < 2 || c.A < 4) return;
            var key = Color.FromArgb(Math.Min(255, (c.A + 4) / 8 * 8), c).ToArgb();
            Bitmap bmp;
            if (!glows.TryGetValue(key, out bmp))
            {
                if (glows.Count > 200) { foreach (var b in glows.Values) b.Dispose(); glows.Clear(); }
                glows[key] = bmp = MakeGlow(Color.FromArgb(key));
            }
            var im = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(bmp, r);
            g.InterpolationMode = im;
        }

        static Bitmap MakeGlow(Color c)
        {
            const int n = 96;
            var bmp = new Bitmap(n, n, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(1, 1, n - 2, n - 2);
                using (var pg = new PathGradientBrush(path) { CenterColor = c, SurroundColors = new[] { Color.FromArgb(0, c) } })
                    g.FillPath(pg, path);
            }
            return bmp;
        }

        // Soft drop shadow under a rounded shape, built from stacked translucent outlines.
        public static void Shadow(Graphics g, RectangleF r, float radius, float spread, int alpha, float dy)
        {
            if (alpha <= 0 || spread <= 0) return;
            var n = Math.Max(2, (int)(spread / Eva.F(1.2f)));
            using (var b = new SolidBrush(Color.FromArgb(Math.Max(1, alpha / n), 8, 36, 76)))
                for (int i = n; i >= 1; i--)
                {
                    var k = i * spread / n;
                    var rr = RectangleF.Inflate(r, k, k);
                    rr.Offset(0, dy);
                    using (var path = Eva.Round(rr, radius + k)) g.FillPath(b, path);
                }
        }

        // Glossy gel (Aqua / Xbox 360): bright top, saturated middle, a darker line at the waist, a glowing bottom,
        // a white gloss cap over the upper half and a darker rim. `bright` lifts it (hover, pulse), `press` dims the cap.
        public static void Gel(Graphics g, RectangleF r, float radius, Color c, float bright = 0f, float press = 0f)
        {
            if (r.Width < 3 || r.Height < 3) return;
            c = Eva.Mix(Lighten(c, 0.18f * bright), Darken(c, 0.18f), press);
            using (var path = Eva.Round(r, radius))
            {
                var top = Lighten(c, 0.42f); var waist = Darken(c, 0.06f); var bot = Lighten(c, 0.38f + 0.15f * bright);
                using (var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bot, 90f))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { top, c, waist, c, bot }, Positions = new[] { 0f, 0.46f, 0.5f, 0.75f, 1f } };
                    g.FillPath(lg, path);
                }

                var st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                Glow(g, new RectangleF(r.X + r.Width * 0.08f, r.Y + r.Height * 0.55f, r.Width * 0.84f, r.Height * 0.95f), Wh((int)(110 + 60 * bright)));
                g.Restore(st);

                var inset = Math.Max(Eva.F(1.5f), r.Height * 0.07f);
                var cap = new RectangleF(r.X + inset * 1.6f, r.Y + inset * 0.7f, r.Width - inset * 3.2f, r.Height * 0.47f);
                if (cap.Width > 2 && cap.Height > 2)
                    using (var cp = Eva.Round(cap, Math.Max(1f, Math.Min(radius - inset, cap.Height / 2f))))
                    using (var lg = new LinearGradientBrush(new RectangleF(cap.X, cap.Y - 1, cap.Width, cap.Height + 2), Wh((int)(225 - 110 * press)), Wh(30), 90f))
                        g.FillPath(lg, cp);

                using (var pen = new Pen(Color.FromArgb(190, Darken(c, 0.38f)), Eva.F(1f))) g.DrawPath(pen, path);
            }
        }

        // Frosted glass pill: white glass on light surfaces, a clear sheet on dark sky.
        public static void GlassPill(Graphics g, RectangleF r, float m = 1f, bool onSky = false)
        {
            if (r.Width < 3 || r.Height < 3 || m <= 0.01f) return;
            var rad = r.Height / 2f;
            Shadow(g, r, rad, Eva.F(4), (int)((onSky ? 40 : 60) * m), Eva.F(2));
            using (var path = Eva.Round(r, rad))
            {
                if (onSky) using (var b = new SolidBrush(Wh((int)(50 * m)))) g.FillPath(b, path);
                else using (var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Wh((int)(245 * m)), Color.FromArgb((int)(235 * m), 228, 241, 252), 90f)) g.FillPath(lg, path);

                var cap = new RectangleF(r.X + rad * 0.5f, r.Y + Eva.F(1.5f), r.Width - rad, r.Height * 0.46f);
                if (cap.Width > 2 && cap.Height > 2)
                    using (var cp = Pill(cap))
                    using (var lg = new LinearGradientBrush(new RectangleF(cap.X, cap.Y - 1, cap.Width, cap.Height + 2), Wh((int)((onSky ? 120 : 210) * m)), Wh((int)(15 * m)), 90f))
                        g.FillPath(lg, cp);

                using (var pen = new Pen(onSky ? Wh((int)(180 * m)) : Color.FromArgb((int)(220 * m), 128, 172, 214), Eva.F(1f))) g.DrawPath(pen, path);
            }
        }

        // Recessed track / well: darker at the top (inner shadow) with a white emboss line along the bottom edge.
        public static void Inset(Graphics g, RectangleF r, float radius, float m = 1f)
        {
            if (r.Width < 2 || r.Height < 2) return;
            using (var emb = Eva.Round(new RectangleF(r.X, r.Y + Eva.F(1), r.Width, r.Height), radius))
            using (var b = new SolidBrush(Wh((int)(210 * m)))) g.FillPath(b, emb);
            using (var path = Eva.Round(r, radius))
            {
                using (var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.Black, Color.White, 90f))
                {
                    lg.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb((int)(255 * m), 150, 170, 194), Color.FromArgb((int)(255 * m), 204, 217, 231), Color.FromArgb((int)(255 * m), 240, 245, 250) },
                        Positions = new[] { 0f, 0.4f, 1f }
                    };
                    g.FillPath(lg, path);
                }
                using (var pen = new Pen(Color.FromArgb((int)(200 * m), 118, 144, 176), Eva.F(1f))) g.DrawPath(pen, path);
            }
        }

        // ---- sprites ---------------------------------------------------------------------------------------------

        static readonly Dictionary<long, Bitmap> orbs = new Dictionary<long, Bitmap>();
        static readonly Dictionary<int, Bitmap> bubbles = new Dictionary<int, Bitmap>();

        // Glossy marble: lit from inside at the bottom, a white gloss cap on top.
        public static void Orb(Graphics g, float cx, float cy, float rad, Color c)
        {
            var px = Math.Max(4, (int)Math.Ceiling(rad * 2) + 2);
            var key = ((long)px << 32) | (uint)c.ToArgb();
            Bitmap bmp;
            if (!orbs.TryGetValue(key, out bmp))
            {
                if (orbs.Count > 300) { foreach (var b in orbs.Values) b.Dispose(); orbs.Clear(); }
                orbs[key] = bmp = MakeOrb(px, c);
            }
            g.DrawImage(bmp, new RectangleF(cx - px / 2f, cy - px / 2f, px, px));
        }

        static Bitmap MakeOrb(int px, Color c)
        {
            var bmp = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Eva.Quality(g);
                var r = new RectangleF(0.75f, 0.75f, px - 1.5f, px - 1.5f);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(r);
                    using (var pg = new PathGradientBrush(path) { CenterPoint = new PointF(px * 0.5f, px * 0.74f), CenterColor = Lighten(c, 0.5f), SurroundColors = new[] { Darken(c, 0.16f) } })
                        g.FillPath(pg, path);
                }
                var cap = new RectangleF(px * 0.18f, px * 0.07f, px * 0.64f, px * 0.45f);
                if (cap.Height >= 1.5f)
                    using (var lg = new LinearGradientBrush(new RectangleF(cap.X, cap.Y - 0.5f, cap.Width, cap.Height + 1), Wh(240), Wh(25), 90f))
                        g.FillEllipse(lg, cap);
                using (var pen = new Pen(Color.FromArgb(150, Darken(c, 0.42f)), Math.Max(1f, px / 24f))) g.DrawEllipse(pen, r);
            }
            return bmp;
        }

        // Soap bubble: clear middle, white rim, a highlight and a faint cyan crescent.
        public static void Bubble(Graphics g, float cx, float cy, float rad)
        {
            var px = Math.Max(5, (int)Math.Ceiling(rad * 2) + 2);
            Bitmap bmp;
            if (!bubbles.TryGetValue(px, out bmp)) bubbles[px] = bmp = MakeBubble(px);
            g.DrawImage(bmp, new RectangleF(cx - px / 2f, cy - px / 2f, px, px));
        }

        static Bitmap MakeBubble(int px)
        {
            var bmp = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Eva.Quality(g);
                var r = new RectangleF(0.75f, 0.75f, px - 1.5f, px - 1.5f);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(r);
                    using (var pg = new PathGradientBrush(path) { CenterColor = Wh(8), SurroundColors = new[] { Wh(130) }, FocusScales = new PointF(0.72f, 0.72f) })
                        g.FillPath(pg, path);
                }
                using (var b = new SolidBrush(Color.FromArgb(70, 120, 220, 255))) g.FillEllipse(b, px * 0.46f, px * 0.56f, px * 0.36f, px * 0.24f);
                using (var pen = new Pen(Wh(170), Math.Max(1f, px / 18f))) g.DrawEllipse(pen, r);
                using (var b = new SolidBrush(Wh(215))) g.FillEllipse(b, px * 0.22f, px * 0.16f, px * 0.30f, px * 0.20f);
            }
            return bmp;
        }

        // Small pre-rendered pieces (pills, badges) on a transparent background, by key.
        static readonly Dictionary<string, Bitmap> sprites = new Dictionary<string, Bitmap>();

        public static Bitmap Sprite(string key, int w, int h, Action<Graphics> draw)
        {
            Bitmap b;
            key = key + "|" + w + "x" + h;
            if (sprites.TryGetValue(key, out b)) return b;
            if (sprites.Count > 64) { foreach (var old in sprites.Values) old.Dispose(); sprites.Clear(); }
            b = NewBitmap(w, h);
            using (var g = Graphics.FromImage(b)) { Eva.Quality(g); draw(g); }
            sprites[key] = b;
            return b;
        }

        // ---- window backdrop -------------------------------------------------------------------------------------

        // Vista-style sky: deep azure to aqua, sunlight from the top left, aurora light streaks, bokeh, grassy hills,
        // and soft shadows under the given panels (baked in, the layout is fixed).
        public static Bitmap BuildBackdrop(int W, int H, IList<Rectangle> shadows)
        {
            W = Math.Max(1, W); H = Math.Max(1, H);
            var bmp = NewBitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            {
                Eva.Quality(g);
                var full = new Rectangle(0, 0, W, H);
                using (var lg = new LinearGradientBrush(new Rectangle(0, -1, W, H + 2), Color.Black, Color.White, 90f))
                {
                    lg.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb(6, 66, 146), Color.FromArgb(20, 116, 200), Color.FromArgb(70, 172, 230), Color.FromArgb(150, 216, 238), Color.FromArgb(150, 214, 190), Color.FromArgb(76, 166, 70) },
                        Positions = new[] { 0f, 0.2f, 0.48f, 0.72f, 0.88f, 1f }
                    };
                    g.FillRectangle(lg, full);
                }

                Glow(g, new RectangleF(-W * 0.18f, -H * 0.6f, W * 0.8f, H * 1.15f), Wh(160));
                Glow(g, new RectangleF(W * 0.55f, H * 0.25f, W * 0.7f, H * 0.9f), Color.FromArgb(60, 190, 255, 200));

                // Aurora: three ribbons of fine light streaks with a broad soft glow under each.
                var rnd = new Random(11);
                for (int fam = 0; fam < 3; fam++)
                {
                    float y0 = H * (0.22f + fam * 0.24f), amp = H * (0.16f + fam * 0.05f);
                    Func<float, PointF[]> curve = off => new[]
                    {
                        new PointF(-W * 0.1f, y0 + amp + off * 1.4f), new PointF(W * 0.32f, y0 - amp * 0.7f + off),
                        new PointF(W * 0.62f, y0 + amp * 0.9f + off * 0.6f), new PointF(W * 1.1f, y0 - amp + off * 1.2f)
                    };
                    var tint = fam == 0 ? Color.FromArgb(255, 255, 255) : fam == 1 ? Color.FromArgb(205, 255, 170) : Color.FromArgb(170, 235, 255);
                    var c0 = curve(0);
                    using (var pen = new Pen(Color.FromArgb(22, tint), H * 0.09f)) g.DrawBezier(pen, c0[0], c0[1], c0[2], c0[3]);
                    using (var pen = new Pen(Color.FromArgb(26, tint), H * 0.035f)) g.DrawBezier(pen, c0[0], c0[1], c0[2], c0[3]);
                    const int lines = 28;
                    for (int i = 0; i < lines; i++)
                    {
                        var t = i / (float)(lines - 1);
                        var a = (int)(Math.Sin(t * Math.PI) * (fam == 1 ? 80 : 55));
                        var c = curve((t - 0.5f) * H * 0.15f);
                        using (var pen = new Pen(Color.FromArgb(a, tint), Eva.F(0.8f + (float)rnd.NextDouble() * 1.4f)))
                            g.DrawBezier(pen, c[0], c[1], c[2], c[3]);
                    }
                }

                // Bokeh.
                for (int i = 0; i < 50; i++)
                {
                    var r = Eva.F(5 + (float)rnd.NextDouble() * 36);
                    var x = (float)rnd.NextDouble() * W; var y = (float)rnd.NextDouble() * H;
                    var a = 8 + rnd.Next(26);
                    using (var b = new SolidBrush(Wh(a))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                    using (var pen = new Pen(Wh(a * 2), 1f)) g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
                }

                // Rolling hills along the bottom.
                for (int k = 0; k < 2; k++)
                {
                    var yb = H * (k == 0 ? 0.84f : 0.9f);
                    var crest = new[] { new PointF(-2, yb), new PointF(W * (k == 0 ? 0.35f : 0.2f), yb - H * 0.08f), new PointF(W * (k == 0 ? 0.65f : 0.7f), yb + H * 0.07f), new PointF(W + 2, yb - H * 0.03f) };
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(crest[0], crest[1], crest[2], crest[3]);
                        path.AddLine(W + 2, yb, W + 2, H + 2);
                        path.AddLine(W + 2, H + 2, -2, H + 2);
                        path.CloseFigure();
                        using (var lg = new LinearGradientBrush(new RectangleF(0, yb - H * 0.1f, W, H * 0.3f), k == 0 ? Color.FromArgb(150, 220, 90) : Color.FromArgb(100, 196, 50), k == 0 ? Color.FromArgb(60, 150, 40) : Color.FromArgb(34, 120, 26), 90f))
                            g.FillPath(lg, path);
                    }
                    using (var pen = new Pen(Wh(110), Eva.F(1.5f))) g.DrawBezier(pen, crest[0], crest[1], crest[2], crest[3]);
                }

                if (shadows != null)
                    foreach (var s in shadows) Shadow(g, s, Eva.F(16), Eva.F(10), 80, Eva.F(4));
            }
            return bmp;
        }
    }
}
