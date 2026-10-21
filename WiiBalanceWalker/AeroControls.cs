//----------------------------------------------------------------------------------------------------------------------+
// Aero painting for the owner-drawn controls (the Eva painting is in EvaControls.cs). Every control starts by copying
// what is behind it (Aero.PaintBehind), so the glass and gradients run on underneath; moving parts ask Anim for frames.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    // Frosted-glass card with a glossy blade for a header, a glint that sweeps along it now and then.
    partial class EvaPanel : IAeroHost
    {
        Bitmap aeroCache;
        int aeroGen = -1;
        bool aeroEnabled;

        public void PaintAeroBackground(Graphics g, Rectangle r)
        {
            EnsureAeroCache();
            Aero.Blit(g, aeroCache, r);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && aeroCache != null) { aeroCache.Dispose(); aeroCache = null; }
            base.Dispose(disposing);
        }

        RectangleF AeroHeader { get { return new RectangleF(Eva.F(5), Eva.F(5), Width - Eva.F(10), HeaderHeight - Eva.F(7)); } }

        void EnsureAeroCache()
        {
            if (aeroCache != null && aeroGen == Aero.Gen && aeroEnabled == Enabled && aeroCache.Width == Math.Max(1, Width) && aeroCache.Height == Math.Max(1, Height)) return;
            if (aeroCache != null) aeroCache.Dispose();
            aeroCache = BuildAeroCache();
            aeroGen = Aero.Gen;
            aeroEnabled = Enabled;
        }

        Bitmap BuildAeroCache()
        {
            var W = Math.Max(1, Width); var H = Math.Max(1, Height);
            var bmp = Aero.NewBitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            {
                Aero.PaintBehind(g, this);
                Eva.Quality(g);
                var body = new RectangleF(Eva.F(1), Eva.F(1), W - Eva.F(2.5f), H - Eva.F(3));
                var rad = Eva.F(14);
                using (var path = Eva.Round(body, rad))
                {
                    // Frosted glass: the sky behind, blurred, under a white wash and a diagonal sheen.
                    using (var blur = Aero.Blur(bmp, 10))
                    {
                        var st = g.Save();
                        g.SetClip(path, CombineMode.Intersect);
                        g.DrawImage(blur, new Rectangle(0, 0, W, H), 0, 0, W, H, GraphicsUnit.Pixel);
                        using (var lg = new LinearGradientBrush(new RectangleF(0, body.Y - 1, W, body.Height + 2), Aero.Wh(226), Color.FromArgb(204, 234, 245, 253), 90f)) g.FillPath(lg, path);
                        using (var lg = new LinearGradientBrush(new PointF(0, 0), new PointF(Math.Max(2, W * 0.62f), 0), Aero.Wh(80), Aero.Wh(0)))
                            g.FillPolygon(lg, new[] { new PointF(0, 0), new PointF(W * 0.62f, 0), new PointF(W * 0.18f, H), new PointF(0, H) });
                        g.Restore(st);
                    }

                    // White wells under text boxes.
                    foreach (Control c in Controls)
                        if (c is TextBox)
                        {
                            Aero.Inset(g, RectangleF.Inflate(c.Bounds, Eva.F(5), Eva.F(5)), Eva.F(9));
                            using (var b = new SolidBrush(Color.White))
                            using (var p = Eva.Round(RectangleF.Inflate(c.Bounds, Eva.F(3), Eva.F(3)), Eva.F(7))) g.FillPath(b, p);
                        }

                    // Header blade with a pearl and the title.
                    var hr = AeroHeader;
                    var acc = Enabled ? Accent : Color.FromArgb(168, 182, 198);
                    Aero.Gel(g, hr, Eva.F(10), acc);
                    Aero.Orb(g, hr.X + Eva.F(13), hr.Y + hr.Height / 2f, Eva.F(6.5f), Color.White);
                    var f = Aero.Semi(10.5f);
                    var title = Aero.Friendly(Title);
                    var th = Eva.Measure(g, title, f).Height;
                    Aero.Shadowed(g, title, f, Color.White, Color.FromArgb(150, Aero.Darken(acc, 0.55f)), hr.X + Eva.F(26), hr.Y + (hr.Height - th) / 2f - Eva.F(0.5f));
                    if (!Enabled) Eva.TextRight(g, "Off", Aero.Semi(8.5f), Aero.Wh(235), hr.Right - Eva.F(12), hr.Y + (hr.Height - Eva.Measure(g, "Off", Aero.Semi(8.5f)).Height) / 2f);

                    using (var pen = new Pen(Color.FromArgb(210, 104, 150, 198), Eva.F(1f))) g.DrawPath(pen, path);
                    using (var inner = Eva.Round(RectangleF.Inflate(body, -Eva.F(1), -Eva.F(1)), rad - Eva.F(1)))
                    using (var pen = new Pen(Aero.Wh(200), Eva.F(1f))) g.DrawPath(pen, inner);
                }
            }
            return bmp;
        }

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            EnsureAeroCache();
            Aero.Blit(g, aeroCache, new Rectangle(0, 0, Width, Height));
            Eva.Quality(g);

            // Glint along the header, staggered per panel.
            const float period = 9f, dur = 1.2f;
            var seed = ((Title ?? "").GetHashCode() & 0xffff) / 65535f * period;
            var ph = (Anim.Now + seed) % period;
            if (ph < dur && Enabled)
            {
                var hr = AeroHeader;
                var x = hr.X - Eva.F(60) + ph / dur * (hr.Width + Eva.F(120));
                using (var path = Eva.Round(hr, Eva.F(10)))
                using (var lg = new LinearGradientBrush(new PointF(x - Eva.F(22), 0), new PointF(x + Eva.F(36), 0), Aero.Wh(0), Aero.Wh(0)))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { Aero.Wh(0), Aero.Wh(150), Aero.Wh(0) }, Positions = new[] { 0f, 0.5f, 1f } };
                    var st = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    g.FillPolygon(lg, new[] { new PointF(x, hr.Y), new PointF(x + Eva.F(34), hr.Y), new PointF(x + Eva.F(14), hr.Bottom), new PointF(x - Eva.F(20), hr.Bottom) });
                    g.Restore(st);
                }
                Anim.Request(this);
            }
            else Anim.RequestAt(this, Anim.Now + (ph < dur ? 0.5f : period - ph));

            base.OnPaint(e);
        }
    }

    // Toggle switch: lime gel fills the track behind a pearl knob that springs across.
    partial class EvaCheck
    {
        readonly Spr knob = new Spr();
        readonly Tw aHover = new Tw();
        bool knobSet;

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            Aero.PaintBehind(g, this);
            Eva.Quality(g);

            var target = Checked ? 1f : 0f;
            if (!knobSet) { knob.X = target; knobSet = true; }
            var moving = knob.To(target, 20f, 0.45f);
            moving |= aHover.To(hover && Enabled ? 1f : 0f, 14f);

            var tr = new RectangleF(Eva.F(2), Eva.F(3), Eva.F(36), Eva.F(18));
            var rad = tr.Height / 2f;
            Aero.Inset(g, tr, rad, Enabled ? 1f : 0.55f);

            var k = Math.Max(-0.1f, Math.Min(1.1f, knob.X));
            var kx = tr.X + rad + k * (tr.Width - 2 * rad);
            if (k > 0.04f)
            {
                var fr = new RectangleF(tr.X + Eva.F(1), tr.Y + Eva.F(1), Math.Max(rad * 2, kx + rad - tr.X - Eva.F(2)), tr.Height - Eva.F(2));
                Aero.Gel(g, fr, fr.Height / 2f, Enabled ? Aero.Lime : Color.FromArgb(178, 210, 160));
            }

            var kr = new RectangleF(kx - rad, tr.Y, rad * 2, rad * 2);
            Aero.Shadow(g, kr, rad, Eva.F(2), 50, Eva.F(1.5f));
            if (aHover.V > 0.01f) Aero.Glow(g, RectangleF.Inflate(kr, rad * 0.9f, rad * 0.9f), Color.FromArgb((int)(130 * aHover.V), Aero.Aqua));
            Aero.Orb(g, kx, tr.Y + rad, rad + Eva.F(1), Enabled ? Color.FromArgb(250, 252, 255) : Color.FromArgb(220, 226, 232));

            var tx = tr.Right + Eva.F(9);
            using (var b = new SolidBrush(Enabled ? Eva.Text : Eva.TextOff))
                g.DrawString(Text, Aero.Ui(9.5f), b, new RectangleF(tx, Eva.F(3), Width - tx, Height - Eva.F(3)));

            if (moving) Anim.Request(this);
        }
    }

    // Slider: recessed tube, gel fill up to a marble that glides to the value and swells on hover.
    partial class EvaSlider
    {
        readonly Tw shownT = new Tw(), aHover = new Tw();
        bool shownSet;

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            Aero.PaintBehind(g, this);
            Eva.Quality(g);

            var on = Enabled;
            var acc = on ? Accent : Color.FromArgb(170, 184, 200);
            var t = max > min ? (val - min) / (float)(max - min) : 0f;
            if (!shownSet) { shownT.V = t; shownSet = true; }
            var moving = shownT.To(t, 22f);
            moving |= aHover.To(on && (hover || drag) ? 1f : 0f, 14f);

            var text = Format != null ? Format(val) : val.ToString();
            if (!string.IsNullOrEmpty(Text)) Aero.Emboss(g, Text, Aero.Ui(8.5f), on ? Eva.TextDim : Eva.TextOff, Eva.F(2), 0);
            var vf = Aero.Semi(10f);
            Aero.Emboss(g, text, vf, on ? Aero.Darken(acc, 0.25f) : Eva.TextOff, Width - Eva.F(2) - Eva.Measure(g, text, vf).Width, Eva.F(-1.5f));

            var y = TrackY;
            var xv = X0 + shownT.V * (X1 - X0);
            var tube = new RectangleF(X0 - Eva.F(7), y - Eva.F(4), X1 - X0 + Eva.F(14), Eva.F(8));
            Aero.Inset(g, tube, tube.Height / 2f, on ? 1f : 0.6f);
            var fill = new RectangleF(tube.X + Eva.F(1), tube.Y + Eva.F(1), Math.Max(tube.Height, xv - tube.X), tube.Height - Eva.F(2));
            Aero.Gel(g, fill, fill.Height / 2f, acc);

            var rad = Eva.F(7.5f) + Eva.F(1.5f) * aHover.V;
            var orb = new RectangleF(xv - rad, y - rad, rad * 2, rad * 2);
            Aero.Shadow(g, orb, rad, Eva.F(2.5f), 55, Eva.F(2));
            var glow = Math.Max(aHover.V, Focused ? 0.5f : 0f);
            if (glow > 0.01f) Aero.Glow(g, RectangleF.Inflate(orb, rad * 0.8f, rad * 0.8f), Color.FromArgb((int)(120 * glow), acc));
            Aero.Orb(g, xv, y, rad, on ? Aero.Lighten(acc, 0.72f) : Color.FromArgb(225, 230, 236));

            if (moving) Anim.Request(this);
        }
    }

    // Aqua gel pill. The primary button breathes like the Aqua default button until the board is connected, then
    // turns lime with a check mark.
    partial class EvaButton
    {
        readonly Tw aHover = new Tw(), aDown = new Tw();

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            Aero.PaintBehind(g, this);
            Eva.Quality(g);

            var moving = aHover.To(hover && Enabled ? 1f : 0f, 14f);
            moving |= aDown.To(down ? 1f : 0f, 30f);

            var r = new RectangleF(Eva.F(3), Eva.F(2), Width - Eva.F(6), Height - Eva.F(6));
            r.Offset(0, Eva.F(1) * aDown.V);
            var rad = r.Height / 2f;
            var connected = Primary && !Enabled;

            Color c; var bright = aHover.V;
            if (connected) c = Aero.Lime;
            else if (Primary)
            {
                c = Aero.Aqua;
                var pulse = 0.5f + 0.5f * (float)Math.Sin(Anim.Now * 2.8f);
                bright = Math.Max(bright, 0.8f * pulse);
                moving = true;
            }
            else if (!Enabled) c = Color.FromArgb(226, 231, 237);
            else c = Eva.Mix(Aero.Silver, Color.FromArgb(176, 214, 248), aHover.V);

            Aero.Shadow(g, r, rad, Eva.F(3), Enabled || connected ? 70 : 30, Eva.F(1.5f) * (1 - aDown.V));
            if (aHover.V > 0.01f)
                using (var path = Eva.Round(RectangleF.Inflate(r, Eva.F(1.5f), Eva.F(1.5f)), rad + Eva.F(1.5f)))
                using (var pen = new Pen(Color.FromArgb((int)(130 * aHover.V), Aero.Lighten(Aero.Aqua, 0.25f)), Eva.F(2.5f)))
                    g.DrawPath(pen, path);
            Aero.Gel(g, r, rad, c, bright, aDown.V);

            var font = Primary ? Aero.Semi(10.5f) : Aero.Semi(9f);
            var s = Text;
            var sz = Eva.Measure(g, s, font);
            var tx = r.X + (r.Width - sz.Width) / 2f;
            var ty = r.Y + (r.Height - sz.Height) / 2f - Eva.F(0.5f);
            if (connected)
            {
                tx += Eva.F(10);
                using (var pen = new Pen(Color.White, Eva.F(2.4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[] { new PointF(tx - Eva.F(19), ty + sz.Height * 0.52f), new PointF(tx - Eva.F(13.5f), ty + sz.Height * 0.82f), new PointF(tx - Eva.F(5), ty + sz.Height * 0.18f) });
            }
            if (Primary) Aero.Shadowed(g, s, font, Color.White, Color.FromArgb(150, Aero.Darken(c, 0.5f)), tx, ty);
            else Aero.Emboss(g, s, font, Enabled ? Eva.Text : Eva.TextOff, tx, ty);

            if (moving) Anim.Request(this);
        }
    }

    partial class EvaReadout
    {
        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            Aero.PaintBehind(g, this);
            Eva.Quality(g);

            var top = 0f;
            if (!string.IsNullOrEmpty(Caption))
            {
                Aero.Emboss(g, Caption, Aero.Ui(7.5f), Eva.TextDim, 0, 0);
                top = Eva.F(12);
            }

            var lines = (Text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var color = Enabled ? ValueColor : Eva.TextOff;

            if (Plain)
            {
                using (var b = new SolidBrush(color))
                    g.DrawString(Text, Aero.Ui(ValuePt), b, new RectangleF(0, top, Width, Height - top));
                return;
            }

            var font = Aero.Semi(ValuePt * 0.86f);
            var sz = Eva.Measure(g, lines[0], font);
            var y = top + (Height - top - sz.Height) / 2f;
            Aero.Emboss(g, lines[0], font, color, Width - sz.Width - Eva.F(2), y);

            if (lines.Length > 1 && lines[1].Length > 0)
            {
                var sf = Aero.Ui(7.5f);
                var ssz = Eva.Measure(g, lines[1], sf);
                Eva.Text_(g, lines[1], sf, Eva.TextDim, Width - sz.Width - ssz.Width - Eva.F(8), y + sz.Height - ssz.Height - Eva.F(1));
            }
        }
    }

    // Glass capsule showing which keys are being pressed; new text slides in.
    partial class EvaStatus
    {
        string lastShown;
        readonly Tw enter = new Tw(1f);
        readonly AeroLayer layer = new AeroLayer();

        RectangleF Capsule { get { return new RectangleF(Eva.F(3), Eva.F(3), Width - Eva.F(6), Height - Eva.F(9)); } }
        RectangleF KeysTag { get { var r = Capsule; return new RectangleF(r.X + Eva.F(4), r.Y + Eva.F(4), Eva.F(64), r.Height - Eva.F(8)); } }

        void DrawStatic(Graphics g, bool disabled)
        {
            var r = Capsule;
            Aero.GlassPill(g, r);
            var tag = KeysTag;
            Aero.Gel(g, tag, tag.Height / 2f, Aero.Aqua);
            var tf = Aero.Semi(9.5f);
            var tsz = Eva.Measure(g, "Keys", tf);
            Aero.Shadowed(g, "Keys", tf, Color.White, Color.FromArgb(140, Aero.Navy), tag.X + (tag.Width - tsz.Width) / 2f, tag.Y + (tag.Height - tsz.Height) / 2f - Eva.F(0.5f));
            if (!disabled) return;
            var lf = Aero.Semi(8.5f);
            var lab = "Keys off";
            var lsz = Eva.Measure(g, lab, lf);
            var pr = new RectangleF(r.Right - lsz.Width - Eva.F(30), r.Y + Eva.F(5), lsz.Width + Eva.F(24), r.Height - Eva.F(10));
            Aero.Gel(g, pr, pr.Height / 2f, Aero.Cherry);
            Aero.Shadowed(g, lab, lf, Color.White, Color.FromArgb(140, 90, 0, 0), pr.X + Eva.F(12), pr.Y + (pr.Height - lsz.Height) / 2f - Eva.F(0.5f));
        }

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            var text = (Text ?? "").Replace("Result:", "").Trim();
            var disabled = text.Contains("( DISABLED )");
            Aero.Blit(g, layer.Get(this, disabled ? "off" : "on", lg => DrawStatic(lg, disabled)), ClientRectangle);
            Eva.Quality(g);

            text = text.Replace("( DISABLED )", "").Trim();
            var neutral = text.Length == 0;
            text = neutral ? "Centered" : Aero.Title(Regex.Replace(text, "(?<=[a-z])(?=[A-Z])", " + "));
            if (text != lastShown) { if (lastShown != null) enter.V = 0; lastShown = text; }
            var moving = enter.To(1f, 10f);

            var r = Capsule;
            var f = Aero.Light(15f);
            var sz = Eva.Measure(g, text, f);
            var x = KeysTag.Right + Eva.F(14) + (1 - enter.V) * Eva.F(16);
            Aero.Emboss(g, text, f, Color.FromArgb((int)(255 * enter.V), neutral ? Eva.TextDim : Eva.Text), x, r.Y + (r.Height - sz.Height) / 2f - Eva.F(1));

            if (moving) Anim.Request(this);
        }
    }

    // Segmented control (Mac OS X): silver capsule, an aqua segment that slides to the selected tab.
    partial class EvaTabs
    {
        readonly Tw selT = new Tw(-1f);

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            Aero.PaintBehind(g, this);
            Eva.Quality(g);

            if (selT.V < 0) selT.V = selected;
            var moving = selT.To(selected, 16f);

            var r = new RectangleF(Eva.F(1), Eva.F(1), Width - Eva.F(2), Height - Eva.F(4));
            var n = Math.Max(1, Tabs.Length);
            Aero.Shadow(g, r, r.Height / 2f, Eva.F(2), 40, Eva.F(1));
            Aero.Gel(g, r, r.Height / 2f, Aero.Silver);
            var w = r.Width / n;
            for (int i = 1; i < n; i++)
            {
                var x = r.X + i * w;
                using (var pen = new Pen(Color.FromArgb(70, 60, 90, 130), 1f)) g.DrawLine(pen, x, r.Y + Eva.F(3), x, r.Bottom - Eva.F(3));
                using (var pen = new Pen(Aero.Wh(170), 1f)) g.DrawLine(pen, x + 1, r.Y + Eva.F(3), x + 1, r.Bottom - Eva.F(3));
            }
            var sr = new RectangleF(r.X + selT.V * w + Eva.F(2), r.Y + Eva.F(2), w - Eva.F(4), r.Height - Eva.F(4));
            Aero.Gel(g, sr, sr.Height / 2f, Aero.Aqua);

            var f = Aero.Semi(8.5f);
            for (int i = 0; i < Tabs.Length; i++)
            {
                var name = Aero.Title(Tabs[i]);
                var sz = Eva.Measure(g, name, f);
                var x = r.X + i * w + (w - sz.Width) / 2f; var y = r.Y + (r.Height - sz.Height) / 2f - Eva.F(0.5f);
                if (Math.Abs(selT.V - i) < 0.5f) Aero.Shadowed(g, name, f, Color.White, Color.FromArgb(140, Aero.Navy), x, y);
                else Aero.Emboss(g, name, f, Eva.Text, x, y);
            }

            if (moving) Anim.Request(this);
        }
    }

    // Four glass test tubes filling with liquid: the surface ripples and bubbles rise while someone is on the board.
    partial class SensorBars
    {
        static readonly string[] aeroNames = { "Front left", "Front right", "Back left", "Back right" };
        readonly AeroLayer layer = new AeroLayer();

        static Color Liquid(float level)
        {
            if (level < 0.6f) return Eva.Mix(Aero.Aqua, Color.FromArgb(40, 190, 200), level / 0.6f);
            if (level < 0.8f) return Eva.Mix(Color.FromArgb(40, 190, 200), Aero.Lime, (level - 0.6f) / 0.2f);
            return Eva.Mix(Aero.Sun, Aero.Cherry, Math.Min(1f, (level - 0.8f) / 0.2f));
        }

        RectangleF Tube(int i)
        {
            var cw = Width / 2f; var ch = Height / 2f;
            var tw = Eva.F(26);
            return new RectangleF((i % 2) * cw + cw / 2f - tw / 2f, (i / 2) * ch + Eva.F(26), tw, ch - Eva.F(58));
        }

        // Labels, empty glass tubes, even-stance markers and the dividers.
        void DrawStatic(Graphics g)
        {
            var cw = Width / 2f; var ch = Height / 2f;
            for (int i = 0; i < 4; i++)
            {
                var cx = (i % 2) * cw; var cy = (i / 2) * ch;
                var tube = Tube(i);
                var nf = Aero.Semi(8f);
                Aero.Emboss(g, aeroNames[i], nf, Eva.TextDim, cx + cw / 2f - Eva.Measure(g, aeroNames[i], nf).Width / 2f, cy + Eva.F(5));
                Aero.Shadow(g, tube, tube.Width / 2f, Eva.F(3), 45, Eva.F(2));
                using (var path = Aero.Pill(tube))
                using (var lg = new LinearGradientBrush(new RectangleF(tube.X - 1, tube.Y, tube.Width + 2, tube.Height), Color.Black, Color.White, 0f))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(196, 214, 232), Color.FromArgb(246, 250, 253), Color.FromArgb(214, 228, 241) }, Positions = new[] { 0f, 0.42f, 1f } };
                    g.FillPath(lg, path);
                }
                var inner = RectangleF.Inflate(tube, -Eva.F(3), -Eva.F(3));
                var my = inner.Bottom - inner.Height * 0.5f;
                using (var b = new SolidBrush(Aero.Aqua))
                    g.FillPolygon(b, new[] { new PointF(tube.X - Eva.F(8), my - Eva.F(4)), new PointF(tube.X - Eva.F(2.5f), my), new PointF(tube.X - Eva.F(8), my + Eva.F(4)) });
            }
            using (var pen = new Pen(Color.FromArgb(50, 60, 100, 140), 1f)) { g.DrawLine(pen, cw, Eva.F(8), cw, Height - Eva.F(8)); g.DrawLine(pen, Eva.F(8), ch, Width - Eva.F(8), ch); }
            using (var pen = new Pen(Aero.Wh(170), 1f)) { g.DrawLine(pen, cw + 1, Eva.F(8), cw + 1, Height - Eva.F(8)); g.DrawLine(pen, Eva.F(8), ch + 1, Width - Eva.F(8), ch + 1); }
        }

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            Aero.Blit(g, layer.Get(this, "", DrawStatic), ClientRectangle);
            Eva.Quality(g);

            var t = Anim.Now;
            var cw = Width / 2f;
            for (int i = 0; i < 4; i++)
            {
                var cx = (i % 2) * cw;
                var tube = Tube(i);
                var tw = tube.Width;
                var inner = RectangleF.Inflate(tube, -Eva.F(3), -Eva.F(3));
                var level = active ? Math.Max(0f, Math.Min(1f, shown[i] / 50f)) : 0f;
                if (level > 0.004f)
                {
                    var ly = inner.Bottom - level * inner.Height;
                    var col = Liquid(level);
                    var surface = new PointF[11];
                    for (int k = 0; k <= 10; k++)
                        surface[k] = new PointF(inner.X + inner.Width * k / 10f, ly + (float)Math.Sin(k * 0.9f + t * 5f + i * 1.7f) * Eva.F(1.4f));
                    var poly = new PointF[13];
                    Array.Copy(surface, poly, 11);
                    poly[11] = new PointF(inner.Right, inner.Bottom + 1); poly[12] = new PointF(inner.X, inner.Bottom + 1);

                    // The liquid is clipped to the tube by a cheap rectangle plus round caps instead of a path region.
                    var st = g.Save();
                    g.SetClip(new RectangleF(inner.X, inner.Y, inner.Width, inner.Height));
                    using (var lg = new LinearGradientBrush(new RectangleF(inner.X - 1, 0, inner.Width + 2, 1), Color.Black, Color.White, 0f))
                    {
                        lg.InterpolationColors = new ColorBlend { Colors = new[] { Aero.Darken(col, 0.12f), Aero.Lighten(col, 0.4f), Aero.Darken(col, 0.05f) }, Positions = new[] { 0f, 0.38f, 1f } };
                        using (var liquid = new GraphicsPath())
                        {
                            liquid.AddPolygon(poly);
                            using (var cap = new Region(liquid))
                            using (var pill = Aero.Pill(inner))
                            {
                                cap.Intersect(pill);
                                g.FillRegion(lg, cap);
                            }
                        }
                    }
                    for (int b = 0; b < 5; b++)
                    {
                        var phase = (b * 0.37f + i * 0.21f) % 1f;
                        var p = (t * (0.22f + b * 0.06f) + phase) % 1f;
                        var by = inner.Bottom - inner.Width / 2f - p * Math.Max(0, inner.Bottom - inner.Width / 2f - ly);
                        var bx = inner.X + inner.Width * (0.3f + 0.4f * ((b * 0.618f) % 1f)) + (float)Math.Sin(t * 3f + b) * Eva.F(2);
                        if (by > ly + Eva.F(3)) Aero.Bubble(g, bx, by, Eva.F(1.6f + (b % 3) * 0.8f));
                    }
                    if (ly > inner.Y + inner.Width / 2f)
                        using (var pen = new Pen(Aero.Wh(170), Eva.F(1.2f))) g.DrawLines(pen, surface);
                    g.Restore(st);
                }

                // Glass highlight and rim over the liquid.
                var hl = new RectangleF(tube.X + tw * 0.16f, tube.Y + Eva.F(5), tw * 0.2f, tube.Height - Eva.F(10));
                using (var hp = Aero.Pill(hl))
                using (var lg = new LinearGradientBrush(new RectangleF(hl.X, hl.Y - 1, hl.Width, hl.Height + 2), Aero.Wh(170), Aero.Wh(40), 90f)) g.FillPath(lg, hp);
                using (var path = Aero.Pill(tube))
                using (var pen = new Pen(Color.FromArgb(190, 110, 150, 192), Eva.F(1f))) g.DrawPath(pen, path);

                var vs = active ? shown[i].ToString("0") + "%" : "--";
                var vf = Aero.Light(14f);
                Aero.Emboss(g, vs, vf, active ? Eva.Text : Eva.TextOff, cx + cw / 2f - Eva.Measure(g, vs, vf).Width / 2f, tube.Bottom + Eva.F(2));
            }

            if (active) Anim.Request(this);
        }
    }

    // Output: a recessed trackball-style scope with a glossy marble, liquid bars, and LED pills for each route.
    partial class OutputScope
    {
        readonly AeroLayer layer = new AeroLayer();
        static readonly string[] barNames = { "Strafe", "Forward", "Turn", "Walk" };

        RectangleF ScopeRect { get { var d = Height - Eva.F(4); return new RectangleF(Eva.F(2), Eva.F(1), d, d); } }
        float RowY(int i) { return Eva.F(3) + (Height - Eva.F(6)) / 4f * i; }

        RectangleF BarTube(int i)
        {
            var bx = ScopeRect.Right + Eva.F(16);
            var bw = Width - bx - Eva.F(130);
            return new RectangleF(bx + Eva.F(52), RowY(i) + Eva.F(2), bw - Eva.F(52) - Eva.F(54), Eva.F(10));
        }

        RectangleF LampRect(int i) { return new RectangleF(Width - Eva.F(112), Eva.F(6) + i * Eva.F(34), Eva.F(108), Eva.F(26)); }

        void DrawLamp(Graphics g, int i, string name, bool on, float pulse)
        {
            var r = LampRect(i);
            Aero.Shadow(g, r, r.Height / 2f, Eva.F(2.5f), 45, Eva.F(1.5f));
            Aero.Gel(g, r, r.Height / 2f, on ? Aero.Lime : Aero.Silver, on ? 0.5f * pulse : 0f);
            var led = new PointF(r.X + Eva.F(14), r.Y + r.Height / 2f);
            if (on) Aero.Glow(g, new RectangleF(led.X - Eva.F(12), led.Y - Eva.F(12), Eva.F(24), Eva.F(24)), Color.FromArgb((int)(90 + 120 * pulse), 220, 255, 140));
            Aero.Orb(g, led.X, led.Y, Eva.F(5.5f), on ? Color.FromArgb(210, 255, 120) : Color.FromArgb(160, 170, 182));
            var nf = Aero.Semi(9f);
            var ny = r.Y + (r.Height - Eva.Measure(g, name, nf).Height) / 2f - Eva.F(0.5f);
            var sf = Aero.Ui(8f);
            var st = on ? "Live" : "Off";
            var sx = r.Right - Eva.F(12) - Eva.Measure(g, st, sf).Width;
            var sy = r.Y + (r.Height - Eva.Measure(g, st, sf).Height) / 2f - Eva.F(0.5f);
            if (on)
            {
                Aero.Shadowed(g, name, nf, Color.White, Color.FromArgb(130, 20, 70, 0), r.X + Eva.F(26), ny);
                Aero.Shadowed(g, st, sf, Color.White, Color.FromArgb(130, 20, 70, 0), sx, sy);
            }
            else
            {
                Aero.Emboss(g, name, nf, Eva.TextDim, r.X + Eva.F(26), ny);
                Aero.Emboss(g, st, sf, Eva.TextOff, sx, sy);
            }
        }

        // Scope well and grid, deadzone, bar labels and empty tubes, and the lamps that aren't live.
        void DrawStatic(Graphics g, bool vjoyLive, bool steamLive)
        {
            var sc = ScopeRect; var d = sc.Width;
            var cx = sc.X + d / 2; var cy = sc.Y + d / 2; var rad = d / 2 - Eva.F(5);
            Aero.Inset(g, sc, d / 2f);
            Aero.Glow(g, RectangleF.Inflate(sc, -Eva.F(6), -Eva.F(6)), Aero.Wh(200));
            using (var pen = new Pen(Color.FromArgb(80, 110, 150, 195), 1f))
            {
                g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                g.DrawEllipse(pen, cx - rad / 2, cy - rad / 2, rad, rad);
                g.DrawLine(pen, cx - rad, cy, cx + rad, cy);
                g.DrawLine(pen, cx, cy - rad, cx, cy + rad);
            }
            if (deadR > 0)
            {
                var dr = (float)Math.Min(1, deadR) * rad;
                using (var b = new SolidBrush(Color.FromArgb(40, Aero.Cherry))) g.FillEllipse(b, cx - dr, cy - dr, dr * 2, dr * 2);
                using (var pen = new Pen(Color.FromArgb(130, Aero.Cherry), 1f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(pen, cx - dr, cy - dr, dr * 2, dr * 2);
            }
            Eva.TextCenter(g, "Fwd", Aero.Ui(6.5f), Eva.TextDim, cx, sc.Y + Eva.F(3));

            for (int i = 0; i < 4; i++)
            {
                Aero.Emboss(g, barNames[i], Aero.Semi(8f), Eva.TextDim, ScopeRect.Right + Eva.F(16), RowY(i) - Eva.F(1));
                var tube = BarTube(i);
                Aero.Inset(g, tube, tube.Height / 2f);
                if (i < 3) using (var pen = new Pen(Color.FromArgb(120, 90, 120, 160), 1f)) g.DrawLine(pen, tube.X + tube.Width / 2, tube.Y - Eva.F(2), tube.X + tube.Width / 2, tube.Bottom + Eva.F(2));
            }
            if (!vjoyLive) DrawLamp(g, 0, "vJoy", false, 0);
            if (!steamLive) DrawLamp(g, 1, "SteamVR", false, 0);
        }

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            var vjoyLive = vjoy && active; var steamLive = steamvr && active;
            var key = deadR.ToString("0.000") + vjoyLive + steamLive;
            Aero.Blit(g, layer.Get(this, key, lg => DrawStatic(lg, vjoyLive, steamLive)), ClientRectangle);
            Eva.Quality(g);
            var t = Anim.Now;

            var sc = ScopeRect; var d = sc.Width;
            var cx = sc.X + d / 2; var cy = sc.Y + d / 2; var rad = d / 2 - Eva.F(5);
            Func<double, double, PointF> P = (x, y) => new PointF(cx + (float)Math.Max(-1.1, Math.Min(1.1, x)) * rad, cy - (float)Math.Max(-1.1, Math.Min(1.1, y)) * rad);
            if (active)
            {
                for (int i = 0; i < trail.Count; i++)
                {
                    var p = P(trail[i].X, trail[i].Y);
                    var tr = Eva.F(1.2f + 2.4f * i / Math.Max(1, trail.Count));
                    using (var b = new SolidBrush(Color.FromArgb(i * 150 / Math.Max(1, trail.Count), Aero.Aqua))) g.FillEllipse(b, p.X - tr, p.Y - tr, tr * 2, tr * 2);
                }
                var pi = P(inX, inY);
                using (var pen = new Pen(Color.FromArgb(120, Aero.Navy), Eva.F(1.2f))) g.DrawEllipse(pen, pi.X - Eva.F(5), pi.Y - Eva.F(5), Eva.F(10), Eva.F(10));
                var po = P(outX, outY);
                using (var pen = new Pen(Color.FromArgb(150, Aero.Aqua), Eva.F(2)) { EndCap = LineCap.Round }) g.DrawLine(pen, cx, cy, po.X, po.Y);
                Aero.Orb(g, po.X, po.Y, Eva.F(6.5f), Aero.Aqua);
            }

            double[] vals = { outX, outY, turn, walk };
            Color[] cols = { Aero.Aqua, Aero.Aqua, Eva.Teal, Aero.Lime };
            for (int i = 0; i < 4; i++)
            {
                var centered = i < 3;
                var v = vals[i];
                var tube = BarTube(i);
                if (active)
                {
                    var vv = (float)Math.Max(centered ? -1 : 0, Math.Min(1, v));
                    var inner = RectangleF.Inflate(tube, -Eva.F(1), -Eva.F(1));
                    RectangleF fr;
                    if (centered) { var mid = inner.X + inner.Width / 2; var w = vv * inner.Width / 2; fr = new RectangleF(Math.Min(mid, mid + w), inner.Y, Math.Abs(w), inner.Height); }
                    else fr = new RectangleF(inner.X, inner.Y, vv * inner.Width, inner.Height);
                    if (fr.Width > Eva.F(2)) Aero.Gel(g, fr, fr.Height / 2f, cols[i]);
                }
                var vs = active ? (centered ? v.ToString("+0.00;-0.00; 0.00") : v.ToString("0%")) : "--";
                var vf = Aero.Semi(10f);
                Aero.Emboss(g, vs, vf, active ? Eva.Text : Eva.TextOff, tube.Right + Eva.F(50) - Eva.Measure(g, vs, vf).Width, RowY(i) - Eva.F(2.5f));
            }

            if (vjoyLive) DrawLamp(g, 0, "vJoy", true, 0.5f + 0.5f * (float)Math.Sin(t * 3.2f));
            if (steamLive) DrawLamp(g, 1, "SteamVR", true, 0.5f + 0.5f * (float)Math.Sin(t * 3.2f + 1f));
            if (vjoyLive || steamLive) Anim.Request(this);
        }
    }

    // Title bar over the sky: an Aqua orb logo, the name in Segoe, drifting bubbles, a glass theme pill and Mac-style
    // traffic lights for minimize / close.
    partial class TitleBar
    {
        Bitmap aeroLayer;
        int aeroGen = -1;
        GraphicsPath titlePath;
        readonly Tw aStyle = new Tw(), aLights = new Tw();

        void EnsureAeroLayer()
        {
            if (aeroLayer != null && aeroGen == Aero.Gen && aeroLayer.Width == Math.Max(1, Width) && aeroLayer.Height == Math.Max(1, Height)) return;
            if (aeroLayer != null) aeroLayer.Dispose();
            if (titlePath != null) titlePath.Dispose();
            aeroLayer = BuildAeroLayer();
            aeroGen = Aero.Gen;
        }

        Bitmap BuildAeroLayer()
        {
            var bmp = Aero.NewBitmap(Math.Max(1, Width), Math.Max(1, Height));
            using (var g = Graphics.FromImage(bmp))
            {
                Aero.PaintBehind(g, this);
                Eva.Quality(g);
                var cy = Height / 2f - Eva.F(1);

                // Logo: an Aqua orb holding a little white balance board.
                var lx = Eva.F(32); var lr = Eva.F(17);
                Aero.Glow(g, new RectangleF(lx - lr * 2, cy - lr * 2, lr * 4, lr * 4), Aero.Wh(90));
                Aero.Orb(g, lx, cy, lr, Color.FromArgb(40, 160, 240));
                var br = new RectangleF(lx - Eva.F(11), cy - Eva.F(1), Eva.F(22), Eva.F(10));
                Aero.Shadow(g, br, Eva.F(4), Eva.F(2), 60, Eva.F(1));
                using (var p = Eva.Round(br, Eva.F(4.5f))) using (var b = new SolidBrush(Aero.Wh(245))) g.FillPath(b, p);
                using (var b = new SolidBrush(Color.FromArgb(170, 210, 242)))
                    for (int s = 0; s < 2; s++)
                        using (var p = Eva.Round(new RectangleF(br.X + Eva.F(2.5f) + s * Eva.F(9), br.Y + Eva.F(2), Eva.F(8), Eva.F(6)), Eva.F(2))) g.FillPath(b, p);

                // Name: "VR" in semibold, "Balance Walker" in light, white with a soft shadow and halo.
                var f1 = Aero.Semi(19f); var f2 = Aero.Light(19f);
                var em1 = f1.SizeInPoints * g.DpiY / 72f; var em2 = f2.SizeInPoints * g.DpiY / 72f;
                var h1 = Eva.Measure(g, "VR", f1);
                var tx = Eva.F(60); var ty = cy - h1.Height / 2f;
                titlePath = new GraphicsPath();
                titlePath.AddString("VR", f1.FontFamily, (int)f1.Style, em1, new PointF(tx, ty), StringFormat.GenericTypographic);
                titlePath.AddString("Balance Walker", f2.FontFamily, (int)f2.Style, em2, new PointF(tx + h1.Width + Eva.F(6), ty), StringFormat.GenericTypographic);
                var tb = titlePath.GetBounds();
                using (var sp = (GraphicsPath)titlePath.Clone())
                {
                    using (var m = new Matrix()) { m.Translate(0, Eva.F(2)); sp.Transform(m); }
                    using (var b = new SolidBrush(Color.FromArgb(80, 0, 30, 70))) g.FillPath(b, sp);
                }
                using (var pen = new Pen(Aero.Wh(45), Eva.F(4)) { LineJoin = LineJoin.Round }) g.DrawPath(pen, titlePath);
                using (var lg = new LinearGradientBrush(new RectangleF(tb.X, tb.Y - 1, tb.Width, tb.Height + 2), Color.White, Color.FromArgb(200, 234, 255), 90f)) g.FillPath(lg, titlePath);

                var sf = Aero.Ui(8.5f);
                Aero.Shadowed(g, "Balance board locomotion for VR", sf, Aero.Wh(225), Color.FromArgb(90, 0, 30, 70), tb.Right + Eva.F(16), cy - Eva.Measure(g, "Ag", sf).Height / 2f + Eva.F(2));
            }
            return bmp;
        }

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            EnsureAeroLayer();
            Aero.Blit(g, aeroLayer, ClientRectangle);
            Eva.Quality(g);
            var t = Anim.Now;

            // A shine runs through the name every few seconds.
            var ph = t % 6f;
            if (ph < 1.4f && titlePath != null)
            {
                var tb = titlePath.GetBounds();
                var x = tb.X - Eva.F(40) + ph / 1.4f * (tb.Width + Eva.F(80));
                using (var lg = new LinearGradientBrush(new PointF(x - Eva.F(26), 0), new PointF(x + Eva.F(26), 0), Aero.Wh(0), Aero.Wh(0)))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 160, 225, 255), Color.FromArgb(230, 150, 220, 255), Color.FromArgb(0, 160, 225, 255) }, Positions = new[] { 0f, 0.5f, 1f } };
                    var st = g.Save();
                    g.SetClip(titlePath, CombineMode.Intersect);
                    g.FillRectangle(lg, x - Eva.F(26), tb.Y, Eva.F(52), tb.Height);
                    g.Restore(st);
                }
            }

            // Bubbles drifting along.
            for (int i = 0; i < 8; i++)
            {
                var r = Eva.F(2.5f + (i * 5) % 7);
                var speed = Eva.F(10 + (i % 4) * 6);
                var span = Width + r * 4;
                var x = (t * speed + i * 0.37f * span) % span - r * 2;
                var y = Height * (0.25f + 0.55f * ((i * 0.618f) % 1f)) + (float)Math.Sin(t * 1.3f + i) * Eva.F(5);
                Aero.Bubble(g, x, y, r);
            }

            // Theme pill.
            aStyle.To(hoverBtn == 2 ? 1f : 0f, 14f);
            var f = Aero.Semi(9f);
            var label = "Theme: Aero";
            var lsz = Eva.Measure(g, label, f);
            var w = lsz.Width + Eva.F(42);
            var pr = new RectangleF(Btn(0).Left - Eva.F(18) - w, Eva.F(13), w, Eva.F(26));
            styleRect = pr; unitRect = RectangleF.Empty;
            Aero.GlassPill(g, pr, 1f, true);
            if (aStyle.V > 0.01f) using (var p = Aero.Pill(pr)) using (var b = new SolidBrush(Aero.Wh((int)(70 * aStyle.V)))) g.FillPath(b, p);
            Aero.Orb(g, pr.X + Eva.F(15), pr.Y + pr.Height / 2f, Eva.F(6.5f), Aero.Lime);
            Aero.Shadowed(g, label, f, Color.White, Color.FromArgb(110, 0, 30, 70), pr.X + Eva.F(28), pr.Y + (pr.Height - lsz.Height) / 2f - Eva.F(0.5f));

            // Traffic lights; the glyphs show while the pointer is over either one.
            aLights.To(hoverBtn == 0 || hoverBtn == 1 ? 1f : 0f, 14f);
            for (int i = 0; i < 2; i++)
            {
                var r = Btn(i);
                var c = new PointF(r.X + r.Width / 2, r.Y + r.Height / 2);
                var rad = Eva.F(8.5f);
                var col = i == 0 ? Color.FromArgb(252, 188, 46) : Color.FromArgb(240, 76, 66);
                if (hoverBtn == i) Aero.Glow(g, new RectangleF(c.X - rad * 2, c.Y - rad * 2, rad * 4, rad * 4), Color.FromArgb(150, col));
                Aero.Orb(g, c.X, c.Y, rad, col);
                if (aLights.V > 0.02f)
                    using (var pen = new Pen(Color.FromArgb((int)(180 * aLights.V), Aero.Darken(col, 0.62f)), Eva.F(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        var s = Eva.F(3.6f);
                        if (i == 0) g.DrawLine(pen, c.X - s, c.Y, c.X + s, c.Y);
                        else { g.DrawLine(pen, c.X - s, c.Y - s, c.X + s, c.Y + s); g.DrawLine(pen, c.X - s, c.Y + s, c.X + s, c.Y - s); }
                    }
            }

            Anim.Request(this);     // the bubbles never stop
        }
    }
}
