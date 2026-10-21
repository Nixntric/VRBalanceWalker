//----------------------------------------------------------------------------------------------------------------------+
// Owner-drawn controls for the reworked window. The checkbox, button and label subclasses keep their WinForms base
// types so the existing form logic (Checked, PerformClick, Text...) works unchanged; EvaSlider replaces both TrackBar
// and NumericUpDown with an int Value / Minimum / Maximum / ValueChanged.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    // Panel with a numbered title plate, chamfered corners and accent brackets. Hatched over when disabled.
    partial class EvaPanel : Panel
    {
        public string Title = "";
        public string Code  = "00";
        public string Kanji = "";
        public Color  Accent = Eva.Orange;

        public int HeaderHeight { get { return Eva.P(30); } }

        public EvaPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg0;
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(Eva.Bg0);
            Eva.Quality(g);

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var c = Eva.F(11);
            var hh = HeaderHeight;

            using (var body = Eva.Chamfer(r, c, Eva.TR | Eva.BL))
            {
                using (var b = new SolidBrush(Eva.Bg1)) g.FillPath(b, body);

                // Header wash: accent bleeding in from the left.
                var clip = g.Clip;
                g.SetClip(body);
                using (var wash = new LinearGradientBrush(new RectangleF(0, 0, Width, hh), Eva.A(Accent, 34), Eva.A(Accent, 0), 0f))
                    g.FillRectangle(wash, 0, 0, Width * 0.6f, hh);
                g.Clip = clip;

                using (var pen = new Pen(Eva.Line, 1f)) g.DrawPath(pen, body);
            }

            // Code plate (a small slanted tab) and the title.
            var px = Eva.F(9); var py = Eva.F(8); var pw = Eva.F(24); var ph = Eva.F(14); var sk = Eva.F(4);
            using (var b = new SolidBrush(Enabled ? Accent : Eva.TextOff))
                g.FillPolygon(b, new[] { new PointF(px + sk, py), new PointF(px + pw + sk, py), new PointF(px + pw, py + ph), new PointF(px, py + ph) });
            Eva.TextCenter(g, Code, Eva.Num(7.5f), Eva.Bg0, px + pw / 2 + sk / 2, py + Eva.F(0.5f));

            var tx = px + pw + sk + Eva.F(8);
            var tw = Eva.DrawTitle(g, Title, tx, Eva.F(5), Eva.F(19), Enabled ? Eva.Text : Eva.TextDim);
            var kx = tx + tw + Eva.F(8);
            if (!string.IsNullOrEmpty(Kanji))
            {
                Eva.Text_(g, Kanji, Eva.Kanji(7f), Eva.A(Accent, Enabled ? 200 : 90), kx, Eva.F(9.5f));
                kx += Eva.Measure(g, Kanji, Eva.Kanji(7f)).Width + Eva.F(8);
            }

            // Hairline running out to the chamfer, ending in ticks.
            var ly = Eva.F(15.5f);
            var lx1 = Width - c - Eva.F(10);
            using (var pen = new Pen(Eva.Line, 1f))
            {
                if (lx1 > kx + Eva.F(10)) g.DrawLine(pen, kx, ly, lx1, ly);
                for (int i = 0; i < 3; i++) g.DrawLine(pen, lx1 - i * Eva.F(4), ly - Eva.F(3), lx1 - i * Eva.F(4), ly + Eva.F(3));
                g.DrawLine(pen, Eva.F(8), hh - 0.5f, Width - Eva.F(8), hh - 0.5f);
            }

            if (!Enabled)
            {
                Eva.TextRight(g, "STANDBY", Eva.UiBold(7f), Eva.TextDim, lx1 - Eva.F(16), Eva.F(10));
                using (var hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Eva.A(Accent, 20), Color.Transparent))
                    g.FillRectangle(hatch, 1, hh, Width - 2, Height - hh - 1);
            }

            // Accent brackets on the two square corners.
            var bl = Eva.F(16);
            using (var pen = new Pen(Enabled ? Accent : Eva.TextOff, Eva.F(2)))
            {
                g.DrawLines(pen, new[] { new PointF(Eva.F(1), bl), new PointF(Eva.F(1), Eva.F(1)), new PointF(bl, Eva.F(1)) });
                g.DrawLines(pen, new[] { new PointF(Width - Eva.F(1.5f), Height - bl), new PointF(Width - Eva.F(1.5f), Height - Eva.F(1.5f)), new PointF(Width - bl, Height - Eva.F(1.5f)) });
            }

            base.OnPaint(e);    // raises Paint for panel-specific extras
        }
    }

    // Toggle drawn as an ON/OFF lamp followed by its label.
    partial class EvaCheck : CheckBox
    {
        bool hover;

        public EvaCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg1;
            AutoSize = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            var lw = Eva.F(30); var lh = Eva.F(15);
            var lamp = new RectangleF(Eva.F(1), Eva.F(3), lw, lh);
            using (var path = Eva.Chamfer(lamp, Eva.F(4), Eva.TL | Eva.BR))
            {
                if (Checked)
                {
                    var c = Enabled ? Eva.Green : Eva.GreenLo;
                    if (Enabled) using (var glow = new Pen(Eva.A(Eva.Green, 55), Eva.F(4))) g.DrawPath(glow, path);
                    using (var b = new SolidBrush(c)) g.FillPath(b, path);
                    Eva.TextCenter(g, "ON", Eva.Num(7.5f), Eva.Bg0, lamp.X + lw / 2, lamp.Y + Eva.F(0.5f));
                }
                else
                {
                    using (var b = new SolidBrush(Eva.Bg2)) g.FillPath(b, path);
                    using (var pen = new Pen(hover && Enabled ? Eva.Orange : Eva.LineHi, 1f)) g.DrawPath(pen, path);
                    Eva.TextCenter(g, "OFF", Eva.Num(7.5f), Eva.TextOff, lamp.X + lw / 2, lamp.Y + Eva.F(0.5f));
                }
            }

            var tr = new RectangleF(lw + Eva.F(9), Eva.F(2.5f), Width - lw - Eva.F(10), Height - Eva.F(2));
            using (var b = new SolidBrush(!Enabled ? Eva.TextOff : hover ? Eva.Amber : Eva.Text))
                g.DrawString(Text, Eva.Ui(9.5f), b, tr);
        }
    }

    // Horizontal slider with caption and value readout; stands in for TrackBar and NumericUpDown.
    partial class EvaSlider : Control
    {
        int min = 0, max = 100, val = 0;
        bool drag, hover;

        public Func<int, string> Format;
        public Color Accent = Eva.Orange;
        public int LargeChange = 5;
        public event EventHandler ValueChanged;

        public EvaSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Eva.Bg1;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public int Minimum { get { return min; } set { min = value; if (max < min) max = min; Value = val; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; if (min > max) min = max; Value = val; Invalidate(); } }

        public int Value
        {
            get { return val; }
            set
            {
                var v = Math.Max(min, Math.Min(max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        float X0 { get { return Eva.F(Eva.Aero ? 11 : 3); } }
        float X1 { get { return Width - Eva.F(Eva.Aero ? 11 : 5); } }
        float TrackY { get { return Height - Eva.F(10); } }

        void SetFromX(int x)
        {
            var t = (x - X0) / Math.Max(1f, X1 - X0);
            Value = min + (int)Math.Round(Math.Max(0, Math.Min(1, t)) * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Left) { drag = true; Capture = true; SetFromX(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e)   { drag = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e)     { hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e)     { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { Value += Math.Sign(e.Delta); base.OnMouseWheel(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e)  { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value -= 1;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value += 1;
            if (e.KeyCode == Keys.PageDown) Value -= LargeChange;
            if (e.KeyCode == Keys.PageUp) Value += LargeChange;
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            var on   = Enabled;
            var acc  = on ? Accent : Eva.TextOff;
            var text = Format != null ? Format(val) : val.ToString();

            if (!string.IsNullOrEmpty(Text)) Eva.Text_(g, Text.ToUpperInvariant(), Eva.UiBold(7.5f), on ? Eva.TextDim : Eva.TextOff, X0, Eva.F(2));
            Eva.TextRight(g, text, Eva.Num(11f), on ? (drag || Focused ? Eva.Amber : Eva.Text) : Eva.TextOff, X1 + Eva.F(2), Eva.F(-1));

            var y  = TrackY;
            var t  = max > min ? (val - min) / (float)(max - min) : 0f;
            var xv = X0 + t * (X1 - X0);

            // Ticks: at most ~25, every fifth one longer.
            var span = max - min;
            var step = Math.Max(1, (int)Math.Ceiling(span / 25.0));
            using (var pen = new Pen(on ? Eva.LineHi : Eva.Line, 1f))
                for (int v = min, i = 0; v <= max; v += step, i++)
                {
                    var x = X0 + (v - min) / (float)Math.Max(1, span) * (X1 - X0);
                    g.DrawLine(pen, x, y + Eva.F(4), x, y + Eva.F(i % 5 == 0 ? 8 : 6));
                }

            using (var b = new SolidBrush(Eva.Bg3)) g.FillRectangle(b, X0, y - Eva.F(1), X1 - X0, Eva.F(2.5f));
            using (var b = new LinearGradientBrush(new RectangleF(X0 - 1, 0, Math.Max(2, xv - X0 + 2), 1), Eva.A(acc, 90), acc, 0f))
                g.FillRectangle(b, X0, y - Eva.F(1.5f), Math.Max(0, xv - X0), Eva.F(3.5f));

            // Thumb: a notched bar with a small pointer on top.
            if (on && (hover || drag)) using (var glow = new SolidBrush(Eva.A(acc, 50))) g.FillEllipse(glow, xv - Eva.F(9), y - Eva.F(9), Eva.F(18), Eva.F(18));
            using (var b = new SolidBrush(on ? Eva.Text : Eva.TextOff))
            {
                g.FillRectangle(b, xv - Eva.F(1.5f), y - Eva.F(6), Eva.F(3), Eva.F(12));
                g.FillPolygon(b, new[] { new PointF(xv - Eva.F(4), y - Eva.F(9)), new PointF(xv + Eva.F(4), y - Eva.F(9)), new PointF(xv, y - Eva.F(5)) });
            }
        }
    }

    // Chamfered push button. Primary buttons are filled orange.
    partial class EvaButton : Button
    {
        bool hover, down;
        public bool Primary;

        public EvaButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg1;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)   { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e)    { base.OnTextChanged(e); Invalidate(); }

        // Click from code even while hidden (PerformClick does nothing unless the button can take focus); the Default
        // theme's stock buttons use this.
        public void Fire() { OnClick(EventArgs.Empty); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fill, border, text, bar;
            // A primary button greys out to solid orange: for Connect, "disabled" means the board is connected.
            if (!Enabled && Primary) { fill = Eva.Orange; border = Eva.Orange; text = Eva.Bg0; bar = Eva.Bg0; }
            else if (!Enabled) { fill = Eva.Bg1; border = Eva.Line; text = Eva.TextOff; bar = Eva.Line; }
            else if (Primary)  { fill = down ? Eva.OrangeLo : hover ? Eva.A(Eva.Orange, 60) : Eva.Bg2; border = Eva.Orange; text = hover ? Eva.Amber : Eva.Orange; bar = Eva.Orange; }
            else               { fill = down ? Eva.Orange : hover ? Eva.Bg3 : Eva.Bg2; border = hover || down ? Eva.Orange : Eva.OrangeLo; text = down ? Eva.Bg0 : Eva.Text; bar = down ? Eva.Bg0 : Eva.Orange; }

            using (var path = Eva.Chamfer(r, Eva.F(8), Eva.TL | Eva.BR))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var pen = new Pen(border, Primary ? Eva.F(2) : 1f)) g.DrawPath(pen, path);
            }
            using (var b = new SolidBrush(bar)) g.FillRectangle(b, Eva.F(9), Height / 2f - Eva.F(6), Eva.F(3), Eva.F(12));

            var font = Primary ? Eva.UiBold(10.5f) : Eva.UiBold(9f);
            var s = Text.ToUpperInvariant();
            var sz = Eva.Measure(g, s, font);
            Eva.Text_(g, s, font, text, Eva.F(20), (Height - sz.Height) / 2f);
        }
    }

    // Read-only value: optional small caption, then the value. A second line of Text is drawn small, to the left.
    partial class EvaReadout : Label
    {
        public string Caption;
        public Color  ValueColor = Eva.Text;
        public float  ValuePt = 13f;
        public bool   Plain;            // interface font instead of numerals, left aligned

        public EvaReadout()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg1;
            AutoSize = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            var top = 0f;
            if (!string.IsNullOrEmpty(Caption))
            {
                Eva.Text_(g, Caption, Eva.UiBold(7f), Eva.TextDim, 0, 0);
                top = Eva.F(11);
            }

            var lines = (Text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var color = Enabled ? ValueColor : Eva.TextOff;

            if (Plain)
            {
                using (var b = new SolidBrush(color))
                    g.DrawString(Text, Eva.Ui(ValuePt), b, new RectangleF(0, top, Width, Height - top));
                return;
            }

            var font = Eva.Num(ValuePt);
            var sz = Eva.Measure(g, lines[0], font);
            var y = top + (Height - top - sz.Height) / 2f;
            Eva.Text_(g, lines[0], font, color, Width - sz.Width - Eva.F(2), y);

            if (lines.Length > 1 && lines[1].Length > 0)
            {
                var sub = lines[1];
                var sf = Eva.NumLight(8f);
                var ssz = Eva.Measure(g, sub, sf);
                Eva.Text_(g, sub, sf, Eva.TextDim, Width - sz.Width - ssz.Width - Eva.F(8), y + sz.Height - ssz.Height - Eva.F(1));
            }
        }
    }

    // The RESULT strip: the active keyboard actions set in heavy title type.
    partial class EvaStatus : Label
    {
        public EvaStatus()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg0;
            AutoSize = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Eva.Chamfer(r, Eva.F(9), Eva.TL | Eva.BR))
            {
                using (var b = new SolidBrush(Eva.Bg1)) g.FillPath(b, path);
                using (var pen = new Pen(Eva.Line, 1f)) g.DrawPath(pen, path);
            }

            var tagW = Eva.F(84);
            using (var b = new SolidBrush(Eva.Orange))
                g.FillPolygon(b, new[] { new PointF(Eva.F(1), Eva.F(1)), new PointF(tagW, Eva.F(1)), new PointF(tagW - Eva.F(10), Height - Eva.F(1)), new PointF(Eva.F(1), Height - Eva.F(1)) });
            Eva.DrawTitle(g, "RESULT", Eva.F(10), (Height - Eva.F(18)) / 2f - Eva.F(1), Eva.F(18), Eva.Bg0, 0.72f, 0.02f);

            var text = (Text ?? "").Replace("Result:", "").Trim();
            var disabled = text.Contains("( DISABLED )");
            text = text.Replace("( DISABLED )", "").Trim();
            if (text.Length == 0) text = "NEUTRAL";

            var x = tagW + Eva.F(8);
            var px = Eva.F(21);
            x += Eva.DrawTitle(g, text.ToUpperInvariant(), x, (Height - px) / 2f - Eva.F(1), px, text == "NEUTRAL" ? Eva.TextDim : Eva.Text, 0.72f) + Eva.F(14);
            if (disabled)
            {
                var lab = "KEYS DISABLED";
                var f = Eva.UiBold(8f);
                var w = Eva.Measure(g, lab, f).Width + Eva.F(12);
                var rr = new RectangleF(Width - w - Eva.F(12), (Height - Eva.F(16)) / 2f, w, Eva.F(16));
                using (var pen = new Pen(Eva.Red, 1f)) g.DrawRectangle(pen, rr.X, rr.Y, rr.Width, rr.Height);
                Eva.Text_(g, lab, f, Eva.Red, rr.X + Eva.F(6), rr.Y + Eva.F(2));
            }
        }
    }

    // Slanted tab headers.
    partial class EvaTabs : Control
    {
        public string[] Tabs = new string[0];
        int selected;
        public event EventHandler SelectedIndexChanged;

        public EvaTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg1;
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return selected; }
            set { if (value == selected) return; selected = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }

        float TabW { get { return (Width - Eva.F(6)) / (float)Math.Max(1, Tabs.Length); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var i = (int)(e.X / TabW);
            if (i >= 0 && i < Tabs.Length) SelectedIndex = i;
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);
            var w = TabW; var sk = Eva.F(8); var h = Height - Eva.F(1);
            for (int i = 0; i < Tabs.Length; i++)
            {
                var x = i * w;
                var poly = new[] { new PointF(x + sk, 0), new PointF(x + w, 0), new PointF(x + w - sk, h), new PointF(x, h) };
                var sel = i == selected;
                using (var b = new SolidBrush(sel ? Eva.Orange : Eva.Bg2)) g.FillPolygon(b, poly);
                if (!sel) using (var pen = new Pen(Eva.Line, 1f)) g.DrawPolygon(pen, poly);
                Eva.TextCenter(g, Tabs[i], Eva.UiBold(8.5f), sel ? Eva.Bg0 : Eva.TextDim, x + w / 2, (h - Eva.F(12)) / 2f);
            }
            using (var pen = new Pen(Eva.Orange, Eva.F(1.5f))) g.DrawLine(pen, 0, Height - Eva.F(0.75f), Width, Height - Eva.F(0.75f));
        }
    }

    // Four segmented load-cell meters, one per corner (TL TR / BL BR).
    partial class SensorBars : Control
    {
        readonly float[] target = new float[4], shown = new float[4];
        bool active;
        readonly Timer anim = new Timer { Interval = 16 };
        static readonly string[] names = { "TL", "TR", "BL", "BR" };
        static readonly string[] where = { "FRONT L", "FRONT R", "BACK L", "BACK R" };

        public SensorBars()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg1;
            anim.Tick += (s, e) =>
            {
                var moving = false;
                for (int i = 0; i < 4; i++)
                {
                    var d = target[i] - shown[i];
                    if (Math.Abs(d) > 0.05f) { shown[i] += d * 0.35f; moving = true; } else shown[i] = target[i];
                }
                Invalidate();
                if (!moving) anim.Stop();
            };
        }

        public void SetValues(float tl, float tr, float bl, float br, bool isActive)
        {
            target[0] = Clean(tl); target[1] = Clean(tr); target[2] = Clean(bl); target[3] = Clean(br);
            active = isActive;
            if (!anim.Enabled) anim.Start();
        }

        static float Clean(float v) { return float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(100f, v)); }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            var cw = Width / 2f; var ch = Height / 2f;
            const int segs = 18;
            for (int i = 0; i < 4; i++)
            {
                var cx = (i % 2) * cw; var cy = (i / 2) * ch;
                var barX = cx + cw / 2f - Eva.F(10);
                var barW = Eva.F(20);
                var top = cy + Eva.F(30);
                var bottom = cy + ch - Eva.F(30);

                Eva.TextCenter(g, names[i], Eva.Num(12f), Eva.Text, cx + cw / 2f, cy + Eva.F(2));
                Eva.TextCenter(g, where[i], Eva.UiBold(6.5f), Eva.TextDim, cx + cw / 2f, cy + Eva.F(18));

                // 0..50% fills the meter; an even stance (25%) lights half of it.
                var lit = shown[i] / 50f * segs;
                var gap = Eva.F(2);
                var sh = (bottom - top - gap * (segs - 1)) / segs;
                for (int s = 0; s < segs; s++)
                {
                    var y = bottom - (s + 1) * sh - s * gap;
                    var t = s / (float)(segs - 1);
                    var col = t < 0.45f ? Eva.Mix(Eva.Green, Eva.Amber, t / 0.45f) : t < 0.8f ? Eva.Mix(Eva.Amber, Eva.Orange, (t - 0.45f) / 0.35f) : Eva.Mix(Eva.Orange, Eva.Red, (t - 0.8f) / 0.2f);
                    var on = active && s < lit;
                    var partial = active && s < lit && s + 1 > lit ? lit - s : 1f;
                    using (var b = new SolidBrush(on ? Eva.A(col, (int)(90 + 165 * partial)) : Eva.Bg2)) g.FillRectangle(b, barX, y, barW, sh);
                }
                using (var pen = new Pen(Eva.Line, 1f)) g.DrawRectangle(pen, barX - Eva.F(3), top - Eva.F(3), barW + Eva.F(6), bottom - top + Eva.F(6));

                // Even-stance marker at 25%.
                var my = bottom - (bottom - top) * 0.5f;
                using (var pen = new Pen(Eva.Teal, Eva.F(1.5f))) g.DrawLine(pen, barX - Eva.F(7), my, barX - Eva.F(3), my);

                Eva.TextCenter(g, active ? shown[i].ToString("0.0") : "--.-", Eva.Num(11f), active ? Eva.Amber : Eva.TextOff, cx + cw / 2f, bottom + Eva.F(6));
            }

            using (var pen = new Pen(Eva.Line, 1f) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(pen, cw, Eva.F(6), cw, Height - Eva.F(6));
                g.DrawLine(pen, Eva.F(6), ch, Width - Eva.F(6), ch);
            }
        }
    }

    // What actually leaves the program: lean scope (input vs. output after deadzone/sensitivity), turn and walk.
    partial class OutputScope : Control
    {
        double inX, inY, outX, outY, turn, walk, deadR;
        bool vjoy, steamvr, active;
        readonly List<PointF> trail = new List<PointF>();

        public OutputScope()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg1;
        }

        // in*/out* are -1..1 with +Y forward; deadRadius is the deadzone in the same units as in*.
        public void SetOutput(double inx, double iny, double outx, double outy, double turnValue, double walkValue, double deadRadius, bool toVJoy, bool toSteamVR, bool isActive)
        {
            inX = Clean(inx); inY = Clean(iny); outX = Clean(outx); outY = Clean(outy);
            turn = Clean(turnValue); walk = Clean(walkValue); deadR = Clean(deadRadius);
            vjoy = toVJoy; steamvr = toSteamVR; active = isActive;
            trail.Add(new PointF((float)outX, (float)outY));
            if (trail.Count > 24) trail.RemoveAt(0);
            Invalidate();
        }

        static double Clean(double v) { return double.IsNaN(v) ? 0 : Math.Max(-1.5, Math.Min(1.5, v)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            // Scope.
            var d = Height - Eva.F(4);
            var sc = new RectangleF(Eva.F(2), Eva.F(2), d, d);
            var cx = sc.X + d / 2; var cy = sc.Y + d / 2; var rad = d / 2 - Eva.F(2);
            using (var b = new SolidBrush(Eva.Bg0)) g.FillEllipse(b, sc);
            using (var pen = new Pen(Eva.Line, 1f))
            {
                g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                g.DrawEllipse(pen, cx - rad / 2, cy - rad / 2, rad, rad);
                g.DrawLine(pen, cx - rad, cy, cx + rad, cy);
                g.DrawLine(pen, cx, cy - rad, cx, cy + rad);
            }
            if (deadR > 0)
            {
                var dr = (float)Math.Min(1, deadR) * rad;
                using (var hb = new HatchBrush(HatchStyle.DarkUpwardDiagonal, Eva.A(Eva.Red, 70), Color.Transparent)) g.FillEllipse(hb, cx - dr, cy - dr, dr * 2, dr * 2);
                using (var pen = new Pen(Eva.A(Eva.Red, 160), 1f) { DashStyle = DashStyle.Dash }) g.DrawEllipse(pen, cx - dr, cy - dr, dr * 2, dr * 2);
            }

            Func<double, double, PointF> P = (x, y) => new PointF(cx + (float)Math.Max(-1.1, Math.Min(1.1, x)) * rad, cy - (float)Math.Max(-1.1, Math.Min(1.1, y)) * rad);
            if (active)
            {
                if (trail.Count > 1)
                    for (int i = 1; i < trail.Count; i++)
                        using (var pen = new Pen(Eva.A(Eva.Green, i * 120 / trail.Count), Eva.F(1.5f)))
                            g.DrawLine(pen, P(trail[i - 1].X, trail[i - 1].Y), P(trail[i].X, trail[i].Y));

                var pi = P(inX, inY);
                using (var pen = new Pen(Eva.TextDim, 1f)) g.DrawEllipse(pen, pi.X - Eva.F(4), pi.Y - Eva.F(4), Eva.F(8), Eva.F(8));
                var po = P(outX, outY);
                using (var pen = new Pen(Eva.Green, Eva.F(2))) g.DrawLine(pen, cx, cy, po.X, po.Y);
                using (var b = new SolidBrush(Eva.Green)) g.FillEllipse(b, po.X - Eva.F(4), po.Y - Eva.F(4), Eva.F(8), Eva.F(8));
            }
            Eva.Text_(g, "FWD", Eva.UiBold(6f), Eva.TextDim, cx + Eva.F(3), sc.Y + Eva.F(1));

            // Bars and numbers.
            var bx = sc.Right + Eva.F(16);
            var bw = Width - bx - Eva.F(130);
            Action<string, double, bool, float, Color> Bar = (name, v, centered, y, col) =>
            {
                Eva.Text_(g, name, Eva.UiBold(7.5f), Eva.TextDim, bx, y);
                var tx = bx + Eva.F(44);
                var tw = bw - Eva.F(44) - Eva.F(52);
                var ty = y + Eva.F(3);
                using (var b = new SolidBrush(Eva.Bg2)) g.FillRectangle(b, tx, ty, tw, Eva.F(8));
                if (active)
                {
                    var vv = (float)Math.Max(centered ? -1 : 0, Math.Min(1, v));
                    using (var b = new SolidBrush(col))
                        if (centered) { var mid = tx + tw / 2; var w = vv * tw / 2; g.FillRectangle(b, Math.Min(mid, mid + w), ty, Math.Abs(w), Eva.F(8)); }
                        else g.FillRectangle(b, tx, ty, vv * tw, Eva.F(8));
                }
                using (var pen = new Pen(Eva.LineHi, 1f)) if (centered) g.DrawLine(pen, tx + tw / 2, ty - Eva.F(2), tx + tw / 2, ty + Eva.F(10));
                Eva.TextRight(g, active ? (centered ? v.ToString("+0.00;-0.00; 0.00") : v.ToString("0%")) : "--", Eva.Num(10f), active ? Eva.Text : Eva.TextOff, tx + tw + Eva.F(48), y - Eva.F(2));
            };
            var rowH = (Height - Eva.F(6)) / 4f;
            Bar("STRAFE", outX, true, Eva.F(3), Eva.Orange);
            Bar("FWD",    outY, true, Eva.F(3) + rowH, Eva.Orange);
            Bar("TURN",   turn, true, Eva.F(3) + rowH * 2, Eva.Teal);
            Bar("WALK",   walk, false, Eva.F(3) + rowH * 3, Eva.Green);

            // Route lamps.
            var lx = Width - Eva.F(112);
            Action<string, bool, float> Lamp = (name, on, y) =>
            {
                var r = new RectangleF(lx, y, Eva.F(108), Eva.F(26));
                using (var path = Eva.Chamfer(r, Eva.F(6), Eva.TL | Eva.BR))
                {
                    using (var b = new SolidBrush(on ? Eva.A(Eva.Green, 40) : Eva.Bg2)) g.FillPath(b, path);
                    using (var pen = new Pen(on ? Eva.Green : Eva.Line, 1f)) g.DrawPath(pen, path);
                }
                using (var b = new SolidBrush(on ? Eva.Green : Eva.TextOff)) g.FillEllipse(b, r.X + Eva.F(8), r.Y + Eva.F(9), Eva.F(8), Eva.F(8));
                Eva.Text_(g, name, Eva.UiBold(8.5f), on ? Eva.Text : Eva.TextOff, r.X + Eva.F(22), r.Y + Eva.F(6));
                Eva.TextRight(g, on ? "LIVE" : "OFF", Eva.Num(7.5f), on ? Eva.Green : Eva.TextOff, r.Right - Eva.F(7), r.Y + Eva.F(7));
            };
            Lamp("vJoy", vjoy && active, Eva.F(4));
            Lamp("SteamVR", steamvr && active, Eva.F(36));
            Eva.Text_(g, "出力経路", Eva.Kanji(7f), Eva.A(Eva.Orange, 170), lx, Eva.F(68));
        }
    }

    // Window title bar: title card, theme buttons, minimize/close. Dragging it moves the window.
    partial class TitleBar : Control
    {
        int hoverBtn = -1;
        RectangleF styleRect, unitRect;     // laid out while painting; unitRect is empty in Aero

        public event EventHandler StyleClicked;     // Eva <-> Aero
        public event EventHandler UnitClicked;      // next Eva unit colourway

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        public TitleBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg0;
        }

        RectangleF Btn(int i) { var s = Eva.F(30); return new RectangleF(Width - Eva.F(14) - (2 - i) * (s + Eva.F(6)) + Eva.F(6), Eva.F(10), s, Eva.F(26)); }

        // 0 = minimize, 1 = close, 2 = style, 3 = unit.
        int HitBtn(Point p)
        {
            for (int i = 0; i < 2; i++) if (Btn(i).Contains(p)) return i;
            if (styleRect.Contains(p)) return 2;
            return unitRect.Contains(p) ? 3 : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = HitBtn(e.Location);
            if (h != hoverBtn) { hoverBtn = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverBtn = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var form = FindForm();
            if (form == null || e.Button != MouseButtons.Left) return;
            var h = HitBtn(e.Location);
            if (h == 0) { form.WindowState = FormWindowState.Minimized; return; }
            if (h == 1) { form.Close(); return; }
            if (h == 2) { if (StyleClicked != null) StyleClicked(this, EventArgs.Empty); return; }
            if (h == 3) { if (UnitClicked != null) UnitClicked(this, EventArgs.Empty); return; }
            ReleaseCapture();
            SendMessage(form.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);     // WM_NCLBUTTONDOWN, HTCAPTION
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            g.Clear(BackColor);
            Eva.Quality(g);

            // Title card.
            var x = Eva.F(16);
            var tw = Eva.DrawTitle(g, "VR BALANCE WALKER", x, Eva.F(7), Eva.F(31), Eva.Text, 0.70f, 0.04f);
            var sx = x + tw + Eva.F(14);
            Eva.Text_(g, "体重移動制御システム", Eva.Kanji(8.5f), Eva.Orange, sx, Eva.F(9));
            Eva.Text_(g, "BALANCE BOARD LOCOMOTION INTERFACE  //  v0.5-VRBW", Eva.UiBold(7f), Eva.TextDim, sx, Eva.F(27));

            // Rule with ticks along the bottom; a heavier orange run under the title.
            var by = Height - Eva.F(5);
            using (var pen = new Pen(Eva.Line, 1f))
            {
                g.DrawLine(pen, Eva.F(14), by, Width - Eva.F(14), by);
                for (var tx = Eva.F(14); tx < Width - Eva.F(14); tx += Eva.F(8))
                    g.DrawLine(pen, tx, by, tx, by + Eva.F(((int)(tx / Eva.F(8)) % 5 == 0) ? 4 : 2));
            }
            using (var b = new SolidBrush(Eva.Orange)) g.FillRectangle(b, Eva.F(14), by - Eva.F(1.5f), tw + Eva.F(4), Eva.F(3));

            // Theme buttons: THEME switches to Aero, UNIT cycles the Eva colourways (with swatches of the unit colours).
            var right = Btn(0).Left - Eva.F(18);
            Func<string, string, bool, Color[], RectangleF> Plate = (cap, val, hot, swatches) =>
            {
                var vf = Eva.Num(11f);
                var sw = Eva.F(9);
                var extra = swatches != null ? sw * swatches.Length + Eva.F(2) * (swatches.Length - 1) + Eva.F(10) : 0;
                var w = Math.Max(Eva.Measure(g, val, vf).Width, Eva.Measure(g, cap, Eva.UiBold(6.5f)).Width) + Eva.F(20) + extra;
                var r = new RectangleF(right - w, Eva.F(9), w, Eva.F(30));
                using (var path = Eva.Chamfer(r, Eva.F(6), Eva.TR | Eva.BL))
                {
                    using (var b = new SolidBrush(hot ? Eva.A(Eva.Orange, 46) : Eva.Bg1)) g.FillPath(b, path);
                    using (var pen = new Pen(hot ? Eva.Orange : Eva.LineHi, 1f)) g.DrawPath(pen, path);
                }
                Eva.Text_(g, cap, Eva.UiBold(6.5f), Eva.TextDim, r.X + Eva.F(9), r.Y + Eva.F(2));
                Eva.Text_(g, val, vf, hot ? Eva.Text : Eva.Orange, r.X + Eva.F(9), r.Y + Eva.F(11));
                if (swatches != null)
                    for (int i = 0; i < swatches.Length; i++)
                    {
                        var sr = new RectangleF(r.Right - Eva.F(12) - (swatches.Length - i) * sw - (swatches.Length - 1 - i) * Eva.F(2), r.Y + (r.Height - sw * 1.6f) / 2, sw, sw * 1.6f);
                        using (var b = new SolidBrush(swatches[i])) g.FillRectangle(b, sr);
                    }
                right = r.Left - Eva.F(8);
                return r;
            };
            unitRect  = Plate("UNIT", Eva.Current.Name, hoverBtn == 3, new[] { Eva.Purple, Eva.Green, Eva.Orange });
            styleRect = Plate("THEME", "EVA", hoverBtn == 2, null);

            // Window buttons.
            for (int i = 0; i < 2; i++)
            {
                var r = Btn(i);
                var hot = hoverBtn == i;
                using (var path = Eva.Chamfer(r, Eva.F(5), Eva.TL | Eva.BR))
                {
                    using (var b = new SolidBrush(hot ? (i == 1 ? Eva.Red : Eva.Bg3) : Eva.Bg1)) g.FillPath(b, path);
                    using (var pen = new Pen(hot ? (i == 1 ? Eva.Red : Eva.Orange) : Eva.Line, 1f)) g.DrawPath(pen, path);
                }
                using (var pen = new Pen(hot ? Eva.Text : Eva.TextDim, Eva.F(1.6f)))
                {
                    var cx = r.X + r.Width / 2; var cy = r.Y + r.Height / 2; var s = Eva.F(5);
                    if (i == 0) g.DrawLine(pen, cx - s, cy + s / 2, cx + s, cy + s / 2);
                    else { g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s); g.DrawLine(pen, cx - s, cy + s, cx + s, cy - s); }
                }
            }
        }
    }
}
