//----------------------------------------------------------------------------------------------------------------------+
// The balance board, drawn as a cel-shaded 3D slab that reacts to the rider with springy, rubbery motion:
//
//   lean      the loaded edge or corner dips away from the camera (roll / pitch springs that overshoot and settle)
//   twist     the board wrings around its long axis (torsion grows along the board) and spins a little
//   weight    stepping on squashes it, stepping off pops it up, walking bounces it on every step
//   idle      with nobody on it the board hovers and bobs; with no board connected it is a wireframe
//
// Everything is software-projected with GDI+: a perspective camera looks down at ~50 degrees, side walls are
// back-face culled (the slab is convex), the top is shaded per grid cell in three cel tones, and the silhouette is
// inked from the convex hull of the projected vertices. Floor rings, ripples and the HUD are drawn around it.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    partial class BoardView : Control
    {
        // ---- input state (pushed in at the board's update rate) --------------------------------------------------

        bool  linked, demo, walkEnabled, button;
        float tl = 25, tr = 25, bl = 25, br = 25;
        float brX = 50, brY = 50, brDL = 50, brDR = 50, brDF, weight, twist, turn, walk;
        int   walkSide;

        // ---- animation state -------------------------------------------------------------------------------------

        class Spring
        {
            public float X, V;
            public void Step(float target, float dt, float omega, float zeta)
            {
                var a = omega * omega * (target - X) - 2f * zeta * omega * V;
                V += a * dt;
                X += V * dt;
            }
        }

        readonly Spring roll = new Spring(), pitch = new Spring(), torsion = new Spring(), yaw = new Spring();
        readonly Spring lift = new Spring { X = 0.12f }, squash = new Spring { X = 1f };
        float solid, time, buttonFlash, airborne;
        float lastWeight;
        int   lastSide;
        bool  lastButton;

        struct Ripple { public float Born; public Color Color; public float Strength; }
        readonly List<Ripple> ripples = new List<Ripple>();
        readonly List<PointF> cogTrail = new List<PointF>();

        readonly Timer frame = new Timer { Interval = 15 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        double lastTick;
        Bitmap backdrop;

        public BoardView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Eva.Bg0;
            frame.Tick += (s, e) =>
            {
                var form = FindForm();
                if (form != null && form.WindowState == FormWindowState.Minimized) { lastTick = clock.Elapsed.TotalSeconds; return; }
                Simulate();
                Invalidate();
            };
            frame.Start();
            Simulate();     // so the first paint has real geometry, not every vertex at the origin
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { frame.Dispose(); if (backdrop != null) backdrop.Dispose(); if (aeroBack != null) aeroBack.Dispose(); if (aeroFrame != null) aeroFrame.Dispose(); if (aeroSky != null) aeroSky.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            if (backdrop != null) { backdrop.Dispose(); backdrop = null; }
            base.OnResize(e);
        }

        public void SetState(bool isLinked, bool isDemo, float rTL, float rTR, float rBL, float rBR, float x, float y, float dl, float dr, float df,
                             float kg, float twistRaw, float turnOut, float walkOut, int side, bool aButton, bool walkOn)
        {
            linked = isLinked; demo = isDemo; walkEnabled = walkOn;
            weight = Math.Max(0, kg);
            var loaded = weight > 5f && !float.IsNaN(x) && !float.IsNaN(y);

            tl = Clean(rTL, 25); tr = Clean(rTR, 25); bl = Clean(rBL, 25); br = Clean(rBR, 25);
            brX = loaded ? x : 50; brY = loaded ? y : 50;
            brDL = loaded ? Clean(dl, 50) : 50; brDR = loaded ? Clean(dr, 50) : 50; brDF = loaded ? Clean(df, 0) : 0;
            twist = loaded ? Clean(twistRaw, 0) : 0;
            turn = Clean(turnOut, 0); walk = Clean(walkOut, 0);

            // Events that kick the springs.
            if (lastWeight < 5f && weight >= 5f) { squash.V -= 3.2f; lift.V -= 1.4f; AddRipple(Eva.Orange, 1f); }
            if (lastWeight >= 12f && weight < 3f) { lift.V += 2.6f; squash.V += 3.5f; airborne = 1f; AddRipple(Eva.Amber, 0.8f); }
            if (walkEnabled && loaded && side != 0 && side != lastSide) { squash.V -= 1.6f; roll.V += side * -0.9f; AddRipple(Eva.Green, 0.55f); }
            if (aButton && !lastButton) { buttonFlash = 1f; AddRipple(Eva.Teal, 0.7f); }

            lastWeight = weight; lastSide = side; lastButton = aButton; button = aButton;
            walkSide = side;

            if (loaded)
            {
                cogTrail.Add(new PointF(LeanX * 0.86f, LeanY * 0.50f));
                if (cogTrail.Count > 28) cogTrail.RemoveAt(0);
            }
            else if (cogTrail.Count > 0) cogTrail.RemoveAt(0);
        }

        static float Clean(float v, float fallback) { return float.IsNaN(v) || float.IsInfinity(v) ? fallback : v; }

        bool  Loaded { get { return linked && weight > 5f; } }
        float LeanX  { get { return Loaded ? Math.Max(-1f, Math.Min(1f, (brX - 50f) / 25f)) : 0f; } }
        float LeanY  { get { return Loaded ? Math.Max(-1f, Math.Min(1f, (brY - 50f) / 25f)) : 0f; } }

        void AddRipple(Color c, float strength) { ripples.Add(new Ripple { Born = time, Color = c, Strength = strength }); }

        // ---- geometry --------------------------------------------------------------------------------------------

        struct V3
        {
            public float X, Y, Z;
            public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
            public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
            public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
            public static V3 operator *(V3 a, float s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
            public float Dot(V3 b) { return X * b.X + Y * b.Y + Z * b.Z; }
            public V3 Cross(V3 b) { return new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X); }
            public V3 Norm() { var l = (float)Math.Sqrt(X * X + Y * Y + Z * Z); return l > 1e-6f ? this * (1f / l) : this; }
        }

        const float HalfLen = 1.0f, HalfDep = 0.60f, Corner = 0.30f, Thick = 0.15f, Bevel = 0.05f;
        const int NArc = 7, NLong = 12, NShort = 6;

        // Rounded-rectangle outline in the x/z plane. Every call yields the same vertex count, so rings line up.
        static PointF[] RoundRect(float a, float b, float r)
        {
            var pts = new List<PointF>();
            float[][] cs = { new[] { a - r, b - r, 0f }, new[] { -(a - r), b - r, 90f }, new[] { -(a - r), -(b - r), 180f }, new[] { a - r, -(b - r), 270f } };
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i <= NArc; i++)
                {
                    var ang = (cs[c][2] + 90f * i / NArc) * (float)Math.PI / 180f;
                    pts.Add(new PointF(cs[c][0] + r * (float)Math.Cos(ang), cs[c][1] + r * (float)Math.Sin(ang)));
                }
                var n = cs[(c + 1) % 4];
                var na = n[2] * (float)Math.PI / 180f;
                var end = new PointF(n[0] + r * (float)Math.Cos(na), n[1] + r * (float)Math.Sin(na));
                var start = pts[pts.Count - 1];
                var count = c % 2 == 0 ? NLong : NShort;
                for (int k = 1; k < count; k++)
                    pts.Add(new PointF(start.X + (end.X - start.X) * k / count, start.Y + (end.Y - start.Y) * k / count));
            }
            return pts.ToArray();
        }

        static readonly PointF[] ringTop = RoundRect(HalfLen - Bevel, HalfDep - Bevel, Corner - Bevel);
        static readonly PointF[] ringMid = RoundRect(HalfLen, HalfDep, Corner);
        static readonly PointF[] ringBot = RoundRect(HalfLen - 0.03f, HalfDep - 0.03f, Corner - 0.03f);
        static readonly PointF[] padRing = RoundRect(0.37f, 0.47f, 0.14f);
        static readonly PointF[] padInner = RoundRect(0.30f, 0.40f, 0.10f);

        // Per-frame deformation parameters.
        float dSxz, dSy, dStretch, dBend, dTors, cP, sP, cR, sR, cW, sW, dLift;

        V3 Deform(float x, float y, float z)
        {
            x *= dSxz * dStretch; z *= dSxz; y *= dSy;
            y += dBend * (x * x - 0.4f);
            var a = dTors * x;
            var ca = (float)Math.Cos(a); var sa = (float)Math.Sin(a);
            var y1 = y * ca - z * sa; var z1 = y * sa + z * ca; y = y1; z = z1;
            y1 = y * cP - z * sP; z1 = y * sP + z * cP; y = y1; z = z1;
            var x1 = x * cR - y * sR; y1 = x * sR + y * cR; x = x1; y = y1;
            x1 = x * cW + z * sW; z1 = -x * sW + z * cW; x = x1; z = z1;
            return new V3(x, y + dLift + Thick / 2f, z);
        }

        // Camera.
        float camCX, camCY, camF, camD = 5.4f, camCE, camSE;
        V3 look = new V3(0, 0.06f, 0.05f);
        V3 CamPos { get { return look + new V3(0, camD * camSE, camD * camCE); } }

        PointF Proj(V3 p)
        {
            var x = p.X - look.X; var y = p.Y - look.Y; var z = p.Z - look.Z;
            var vy = y * camCE - z * camSE;
            var vz = camD - y * camSE - z * camCE;
            var s = camF / Math.Max(0.5f, vz);
            return new PointF(camCX + x * s, camCY - vy * s);
        }

        static readonly V3 Light = new V3(-0.35f, 1f, 0.45f).Norm();

        // Cel ramps (highlight, lit, mid, shadow) and outline colour come from the current theme.
        static Color[] BodyRamp { get { return Eva.BodyRamp; } }
        static Color[] PadRamp  { get { return Eva.PadRamp; } }
        static Color Ink        { get { return Eva.Ink; } }

        static readonly V3 Half = (Light + new V3(0, (float)Math.Sin(50 * Math.PI / 180), (float)Math.Cos(50 * Math.PI / 180))).Norm();

        // Eva: three hard cel tones. Aero: the same ramp blended smoothly, plus a glossy specular highlight.
        static Color Tone(Color[] ramp, V3 n, int alpha)
        {
            var d = n.Dot(Light);
            if (!Eva.Aero)
            {
                var c = d > 0.95f ? ramp[0] : d > 0.80f ? ramp[1] : d > 0.50f ? ramp[2] : ramp[3];
                return Color.FromArgb(alpha, c);
            }
            var t = Math.Max(0f, Math.Min(1f, (d - 0.3f) / 0.7f));
            var s = t < 0.4f ? Eva.Mix(ramp[3], ramp[2], t / 0.4f) : t < 0.75f ? Eva.Mix(ramp[2], ramp[1], (t - 0.4f) / 0.35f) : Eva.Mix(ramp[1], ramp[0], (t - 0.75f) / 0.25f);
            var spec = (float)Math.Pow(Math.Max(0f, n.Dot(Half)), 48) * 0.7f;
            return Color.FromArgb(alpha, Eva.Mix(s, Color.White, spec));
        }

        // ---- frame -----------------------------------------------------------------------------------------------

        void Simulate()
        {
            var now = clock.Elapsed.TotalSeconds;
            var dt = (float)Math.Min(0.05, Math.Max(0.001, now - lastTick));
            lastTick = now;
            time += dt;

            var deg = (float)Math.PI / 180f;
            var loaded = Loaded;
            var twistVis = loaded ? Math.Max(-1f, Math.Min(1f, twist / 18f)) : 0f;

            roll.Step(-LeanX * 17f * deg, dt, 13f, 0.36f);
            pitch.Step(LeanY * 13f * deg, dt, 13f, 0.36f);
            torsion.Step(twistVis * 30f * deg, dt, 11f, 0.28f);
            yaw.Step(-turn * 18f * deg + (loaded ? 0 : (float)Math.Sin(time * 0.7f) * 4f * deg), dt, 8f, 0.42f);

            var load = Math.Min(1f, weight / 90f);
            var hover = linked ? 0.11f + 0.03f * (float)Math.Sin(time * 1.7f) : 0.05f + 0.02f * (float)Math.Sin(time * 1.1f);
            lift.Step(loaded ? -0.015f - 0.02f * load : hover, dt, 10f, 0.42f);
            squash.Step(loaded ? 1f - 0.05f * load : 1f, dt, 17f, 0.24f);

            solid += ((linked ? 1f : 0f) - solid) * Math.Min(1f, dt * 2.6f);
            buttonFlash = Math.Max(0f, buttonFlash - dt * 2.5f);
            airborne = Math.Max(0f, airborne - dt * 1.5f);
            ripples.RemoveAll(r => time - r.Born > 1.1f);

            // Deformation parameters for this frame.
            var sy = Math.Max(0.6f, Math.Min(1.45f, squash.X));
            dSy = sy;
            dSxz = 1f / (float)Math.Sqrt(sy);
            dStretch = 1f + 0.08f * Math.Abs(torsion.X) / (30f * deg);
            dBend = Math.Max(-0.12f, Math.Min(0.12f, -roll.V * 0.045f));
            dTors = torsion.X;
            cP = (float)Math.Cos(pitch.X); sP = (float)Math.Sin(pitch.X);
            cR = (float)Math.Cos(roll.X);  sR = (float)Math.Sin(roll.X);
            cW = (float)Math.Cos(yaw.X);   sW = (float)Math.Sin(yaw.X);
            dLift = lift.X;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        bool paintErrorLogged;

        protected override void OnPaint(PaintEventArgs e)
        {
            // A failed frame must not leave WinForms' red X here for good: log it once and draw again next tick.
            try { PaintFrame(e); }
            catch (Exception ex)
            {
                if (!paintErrorLogged) { paintErrorLogged = true; BalanceWalker.LogError("BoardView paint", ex); }
            }
        }

        void PaintFrame(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAero(e); return; }
            var g = e.Graphics;
            if (backdrop == null) backdrop = BuildBackdrop();
            g.DrawImageUnscaled(backdrop, 0, 0);
            Eva.Quality(g);

            var W = Width; var H = Height;
            camCX = W * 0.5f; camCY = H * 0.50f;
            var e50 = 50f * (float)Math.PI / 180f;
            camCE = (float)Math.Cos(e50); camSE = (float)Math.Sin(e50);
            camF = Math.Min(W * 0.29f, H * 0.50f) * camD;

            DrawFloor(g);
            DrawBoard(g);
            DrawHud(g);
        }

        // Floor: shadow, iridescent rings turning in opposite directions, compass ticks, twist arc and ripples.
        void DrawFloor(Graphics g)
        {
            var dim = 0.45f + 0.55f * solid;
            DrawShadow(g, Color.Black);

            // Rings.
            Color[] hue = { Eva.Orange, Color.FromArgb(232, 70, 120), Eva.Purple, Eva.Teal, Eva.Green, Eva.Amber, Eva.Orange };
            float[] radii = { 1.30f, 1.48f, 1.68f, 1.90f, 2.14f };
            for (int ri = 0; ri < radii.Length; ri++)
            {
                var rad = radii[ri];
                var spin = time * (ri % 2 == 0 ? 0.12f : -0.08f) * (1 + ri * 0.2f);
                var gaps = 3 + ri;
                const int segs = 72;
                var alphaBase = (int)((150 - ri * 18) * dim);
                using (var pen = new Pen(Color.White, Eva.F(ri == 2 ? 2.4f : 1.5f)))
                {
                    var prev = PointF.Empty;
                    for (int i = 0; i <= segs; i++)
                    {
                        var a = i / (float)segs * (float)Math.PI * 2 + spin;
                        var pt = Proj(new V3((float)Math.Cos(a) * rad, 0, (float)Math.Sin(a) * rad));
                        // Gaps: a short break after every 1/gaps of the ring.
                        var frac = (i / (float)segs * gaps) % 1f;
                        if (i > 0 && frac > 0.08f)
                        {
                            var hp = (i / (float)segs + ri * 0.13f + time * 0.03f) % 1f * (hue.Length - 1);
                            var hc = Eva.Mix(hue[(int)hp], hue[Math.Min(hue.Length - 1, (int)hp + 1)], hp % 1f);
                            pen.Color = Eva.A(hc, alphaBase);
                            g.DrawLine(pen, prev, pt);
                        }
                        prev = pt;
                    }
                }
            }

            // Compass ring with ticks and labels (front of the board is away from the camera).
            var tr = 2.36f;
            using (var pen = new Pen(Eva.A(Eva.LineHi, (int)(170 * dim)), 1f))
                for (int d = 0; d < 360; d += 5)
                {
                    var a = d * (float)Math.PI / 180f;
                    var len = d % 30 == 0 ? 0.14f : 0.06f;
                    g.DrawLine(pen, Proj(new V3((float)Math.Cos(a) * tr, 0, (float)Math.Sin(a) * tr)), Proj(new V3((float)Math.Cos(a) * (tr + len), 0, (float)Math.Sin(a) * (tr + len))));
                }
            var lab = new[] { "右", "後", "左", "前" };
            for (int i = 0; i < 4; i++)
            {
                var a = i * (float)Math.PI / 2;
                var p = Proj(new V3((float)Math.Cos(a) * (tr + 0.26f), 0, (float)Math.Sin(a) * (tr + 0.26f)));
                Eva.TextCenter(g, lab[i], Eva.Kanji(9f), Eva.A(Eva.Orange, (int)(200 * dim)), p.X, p.Y - Eva.F(8));
            }

            // Twist arc on the middle ring, from straight ahead, sweeping the way you're turning.
            if (Math.Abs(turn) > 0.02f || Math.Abs(twist) > 3f)
            {
                var sweep = Math.Abs(turn) > 0.02f ? turn : Math.Sign(twist) * 0.06f;
                var rad = 1.68f;
                var a0 = -(float)Math.PI / 2;
                var a1 = a0 + sweep * 1.9f;
                var pts = new List<PointF>();
                for (int i = 0; i <= 24; i++)
                {
                    var a = a0 + (a1 - a0) * i / 24f;
                    pts.Add(Proj(new V3((float)Math.Cos(a) * rad, 0, (float)Math.Sin(a) * rad)));
                }
                var col = Math.Abs(turn) > 0.02f ? Eva.Teal : Eva.TextDim;
                using (var glow = new Pen(Eva.A(col, 60), Eva.F(10)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(glow, pts.ToArray());
                using (var pen = new Pen(col, Eva.F(4)) { StartCap = LineCap.Round }) g.DrawLines(pen, pts.ToArray());
                // Arrowhead.
                var ae = a1; var dir = Math.Sign(sweep);
                var tip = Proj(new V3((float)Math.Cos(ae + dir * 0.12f) * rad, 0, (float)Math.Sin(ae + dir * 0.12f) * rad));
                var w1 = Proj(new V3((float)Math.Cos(ae) * (rad - 0.13f), 0, (float)Math.Sin(ae) * (rad - 0.13f)));
                var w2 = Proj(new V3((float)Math.Cos(ae) * (rad + 0.13f), 0, (float)Math.Sin(ae) * (rad + 0.13f)));
                using (var b = new SolidBrush(col)) g.FillPolygon(b, new[] { tip, w1, w2 });
            }

            // Ripples from stepping on/off, steps and the button.
            foreach (var r in ripples)
            {
                var t = (time - r.Born) / 1.1f;
                var rad = 1.15f + 1.2f * (1 - (1 - t) * (1 - t));
                var a = (int)(220 * (1 - t) * (1 - t) * r.Strength);
                var pts = new PointF[49];
                for (int i = 0; i <= 48; i++)
                {
                    var an = i / 48f * (float)Math.PI * 2;
                    pts[i] = Proj(new V3((float)Math.Cos(an) * rad, 0, (float)Math.Sin(an) * rad));
                }
                using (var pen = new Pen(Eva.A(r.Color, a), Eva.F(1 + 3 * (1 - t)))) g.DrawLines(pen, pts);
            }
        }

        // Shadow, smaller and fainter the higher the board floats.
        void DrawShadow(Graphics g, Color tint)
        {
            var h = Math.Max(0f, lift.X + 0.02f);
            var sh = Math.Max(0.15f, 1f - h * 2.2f);
            var shadow = new List<PointF>();
            for (int i = 0; i < 32; i++)
            {
                var a = i / 32f * (float)Math.PI * 2;
                var p = new V3((float)Math.Cos(a) * 1.15f * (1.1f - sh * 0.1f), 0, (float)Math.Sin(a) * 0.78f * (1.1f - sh * 0.1f));
                var cw = cW; var sw = sW;
                shadow.Add(Proj(new V3(p.X * cw + p.Z * sw, 0, -p.X * sw + p.Z * cw)));
            }
            var sb = new GraphicsPath();
            sb.AddPolygon(shadow.ToArray());
            var bounds = sb.GetBounds();
            sb.Dispose();
            if (Eva.Aero)
            {
                // Three stacked translucent ellipses: soft enough, and far cheaper than a gradient fill.
                var a = (int)((150 * sh * solid + 30) / 3);
                using (var b = new SolidBrush(Color.FromArgb(a, tint)))
                    for (int k = 0; k < 3; k++)
                    {
                        var r = RectangleF.Inflate(bounds, -bounds.Width * 0.12f * k, -bounds.Height * 0.12f * k);
                        g.FillEllipse(b, r);
                    }
                return;
            }
            if (bounds.Width > 2 && bounds.Height > 2)
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(shadow.ToArray());
                using (var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb((int)(150 * sh * solid + 30), tint), SurroundColors = new[] { Color.FromArgb(0, tint) } })
                    g.FillPath(pg, path);
            }
        }

        void DrawBoard(Graphics g)
        {
            int n = ringMid.Length;
            var top = new V3[n]; var mid = new V3[n]; var bot = new V3[n];
            var pTop = new PointF[n]; var pMid = new PointF[n]; var pBot = new PointF[n];
            var yTop = Thick / 2f; var yMid = Thick / 2f - Bevel; var yBot = -Thick / 2f;
            for (int i = 0; i < n; i++)
            {
                top[i] = Deform(ringTop[i].X, yTop, ringTop[i].Y); pTop[i] = Proj(top[i]);
                mid[i] = Deform(ringMid[i].X, yMid, ringMid[i].Y); pMid[i] = Proj(mid[i]);
                bot[i] = Deform(ringBot[i].X, yBot, ringBot[i].Y); pBot[i] = Proj(bot[i]);
            }
            var center = Deform(0, 0, 0);
            var cam = CamPos;
            var fill = (int)(255 * solid);
            var wire = (int)(230 * (1 - solid));

            // Side bands (bevel and wall), far ones first.
            var quads = new List<KeyValuePair<float, Action>>();
            Action<V3, V3, V3, V3, PointF, PointF, PointF, PointF, bool> AddQuad = (a, b, c, d, pa, pb, pc, pd, isBevel) =>
            {
                var nrm = (b - a).Cross(d - a).Norm();
                var qc = (a + b + c + d) * 0.25f;
                if (nrm.Dot(qc - center) < 0) nrm = nrm * -1f;
                if (nrm.Dot(cam - qc) <= 0) return;
                var depth = (cam - qc).Dot(cam - qc);
                var col = Tone(BodyRamp, nrm, fill);
                if (!isBevel) col = Color.FromArgb(fill, Eva.Mix(col, BodyRamp[3], 0.35f));
                quads.Add(new KeyValuePair<float, Action>(depth, () =>
                {
                    using (var b2 = new SolidBrush(col)) g.FillPolygon(b2, new[] { pa, pb, pc, pd });
                }));
            };
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                AddQuad(top[i], top[j], mid[j], mid[i], pTop[i], pTop[j], pMid[j], pMid[i], true);
                AddQuad(mid[i], mid[j], bot[j], bot[i], pMid[i], pMid[j], pBot[j], pBot[i], false);
            }
            quads.Sort((x, y) => y.Key.CompareTo(x.Key));
            var oldMode = g.SmoothingMode;
            if (fill > 0)
            {
                g.SmoothingMode = SmoothingMode.None;      // shared edges between bands stay seamless
                foreach (var q in quads) q.Value();
                g.SmoothingMode = oldMode;
            }

            // Top face: fill once, then cel-shade it per grid cell so torsion shows up as bands of tone.
            var up = (Deform(0, 1, 0) - Deform(0, 0, 0)).Norm();
            if (fill > 0 && Eva.Aero) FillDeckAero(g, pTop, yTop, up, fill);
            else if (fill > 0)
            {
                using (var b = new SolidBrush(Tone(BodyRamp, up, fill))) g.FillPolygon(b, pTop);
                var clip = g.Clip;
                using (var topPath = new GraphicsPath())
                {
                    topPath.AddPolygon(pTop);
                    g.SetClip(topPath, CombineMode.Intersect);
                    g.SmoothingMode = SmoothingMode.None;
                    const int nx = 16, nz = 5;
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                        {
                            float x0 = -HalfLen + 2 * HalfLen * ix / nx, x1 = -HalfLen + 2 * HalfLen * (ix + 1) / nx;
                            float z0 = -HalfDep + 2 * HalfDep * iz / nz, z1 = -HalfDep + 2 * HalfDep * (iz + 1) / nz;
                            var a = Deform(x0, yTop, z0); var b2 = Deform(x1, yTop, z0); var c = Deform(x1, yTop, z1); var d = Deform(x0, yTop, z1);
                            var nrm = (d - a).Cross(b2 - a).Norm();
                            if (nrm.Dot(up) < 0) nrm = nrm * -1f;
                            using (var br2 = new SolidBrush(Tone(BodyRamp, nrm, fill))) g.FillPolygon(br2, new[] { Proj(a), Proj(b2), Proj(c), Proj(d) });
                        }
                    g.SmoothingMode = oldMode;
                }
                g.Clip = clip;
            }

            // Foot pads, then the pressure glow on each corner, then the centre-of-gravity marker.
            foreach (var side in new[] { -1f, 1f })
            {
                var pad = new PointF[padRing.Length];
                for (int i = 0; i < pad.Length; i++) pad[i] = Proj(Deform(padRing[i].X + side * 0.5f, yTop + 0.008f, padRing[i].Y));
                var inner = new PointF[padInner.Length];
                for (int i = 0; i < inner.Length; i++) inner[i] = Proj(Deform(padInner[i].X + side * 0.5f, yTop + 0.009f, padInner[i].Y));
                var pn = (Deform(side * 0.5f, yTop + 1, 0) - Deform(side * 0.5f, yTop, 0)).Norm();
                if (fill > 0)
                {
                    using (var b = new SolidBrush(Tone(PadRamp, pn, fill))) g.FillPolygon(b, pad);
                    using (var pen = new Pen(Color.FromArgb(fill * 3 / 5, PadRamp[0]), Eva.F(1.1f))) g.DrawPolygon(pen, inner);
                    using (var pen = new Pen(Color.FromArgb(fill, Ink), Eva.F(1.3f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pad);
                }
                if (wire > 0) using (var pen = new Pen(Eva.A(Eva.Orange, wire), 1f)) g.DrawPolygon(pen, pad);
            }
            if (Eva.Aero && fill > 0) DrawDeckGloss(g, pTop, yTop, fill);

            var corners = new[] { new PointF(-0.80f, -0.42f), new PointF(0.80f, -0.42f), new PointF(-0.80f, 0.42f), new PointF(0.80f, 0.42f) };
            var loads = new[] { tl, tr, bl, br };
            if (Loaded)
                for (int k = 0; k < 4; k++)
                {
                    var pct = Math.Max(0f, Math.Min(60f, loads[k]));
                    var rad = 0.10f + 0.20f * pct / 50f;
                    var ring = new PointF[24];
                    for (int i = 0; i < 24; i++)
                    {
                        var a = i / 24f * (float)Math.PI * 2;
                        ring[i] = Proj(Deform(corners[k].X + (float)Math.Cos(a) * rad, yTop + 0.012f, corners[k].Y + (float)Math.Sin(a) * rad * 0.9f));
                    }
                    var hot = pct > 37 ? Eva.Mix(Eva.Orange, Eva.Red, (pct - 37) / 13f) : Eva.Orange;
                    if (Eva.Aero)
                    {
                        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                        foreach (var p in ring) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
                        Aero.Glow(g, new RectangleF(x0, y0, x1 - x0, y1 - y0), Eva.A(hot, (int)(Math.Min(1f, pct / 40f) * 235 * solid)));
                        continue;
                    }
                    using (var path = new GraphicsPath())
                    {
                        path.AddPolygon(ring);
                        using (var pg = new PathGradientBrush(path) { CenterColor = Eva.A(hot, (int)(Math.Min(1f, pct / 40f) * 235 * solid)), SurroundColors = new[] { Eva.A(hot, 0) } })
                            g.FillPath(pg, path);
                    }
                }

            // Centre of gravity: laser crosshair across the deck, fading trail, ringed dot (Aero: a Wii Fit red marble).
            if (Loaded && Eva.Aero) DrawAeroCog(g, yTop);
            else if (Loaded)
            {
                float cx = LeanX * 0.86f, cz = LeanY * 0.50f;
                var lx = new PointF[13]; var lz = new PointF[9];
                for (int i = 0; i < lx.Length; i++) lx[i] = Proj(Deform(-0.92f + 1.84f * i / (lx.Length - 1), yTop + 0.015f, cz));
                for (int i = 0; i < lz.Length; i++) lz[i] = Proj(Deform(cx, yTop + 0.015f, -0.52f + 1.04f * i / (lz.Length - 1)));
                using (var pen = new Pen(Eva.A(Eva.Green, (int)(150 * solid)), Eva.F(1.2f)) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLines(pen, lx);
                    g.DrawLines(pen, lz);
                }
                for (int i = 1; i < cogTrail.Count; i++)
                    using (var pen = new Pen(Eva.A(Eva.Green, i * 160 / cogTrail.Count), Eva.F(2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(pen, Proj(Deform(cogTrail[i - 1].X, yTop + 0.016f, cogTrail[i - 1].Y)), Proj(Deform(cogTrail[i].X, yTop + 0.016f, cogTrail[i].Y)));
                var cp = Proj(Deform(cx, yTop + 0.02f, cz));
                var pulse = Eva.F(9f + 2.5f * (float)Math.Sin(time * 6));
                using (var pen = new Pen(Eva.Green, Eva.F(1.6f))) g.DrawEllipse(pen, cp.X - pulse, cp.Y - pulse * 0.75f, pulse * 2, pulse * 1.5f);
                using (var b = new SolidBrush(Eva.Green)) g.FillEllipse(b, cp.X - Eva.F(4), cp.Y - Eva.F(3.2f), Eva.F(8), Eva.F(6.4f));
                using (var b = new SolidBrush(Ink)) g.FillEllipse(b, cp.X - Eva.F(1.5f), cp.Y - Eva.F(1.2f), Eva.F(3), Eva.F(2.4f));
            }

            // Power LED on the front bevel; flashes with the A button.
            var led = Proj(Deform(0, yTop - Bevel * 0.45f, HalfDep - Bevel * 0.5f));
            var ledCol = Eva.Mix(Color.FromArgb(70, 170, 255), Color.White, buttonFlash);
            var lr = Eva.F(3.2f + 4f * buttonFlash);
            if (linked) using (var b = new SolidBrush(Eva.A(ledCol, (int)(70 * solid)))) g.FillEllipse(b, led.X - lr * 2.2f, led.Y - lr * 1.6f, lr * 4.4f, lr * 3.2f);
            using (var b = new SolidBrush(Eva.A(linked ? ledCol : Eva.TextOff, Math.Max(fill, wire)))) g.FillEllipse(b, led.X - lr, led.Y - lr * 0.7f, lr * 2, lr * 1.4f);

            // Ink: silhouette from the convex hull, a finer line around the deck edge.
            var all = new List<PointF>(n * 3);
            all.AddRange(pTop); all.AddRange(pMid); all.AddRange(pBot);
            var hull = Hull(all);
            if (fill > 0 && Eva.Aero)
            {
                using (var pen = new Pen(Color.FromArgb(fill * 3 / 4, Ink), Eva.F(1.6f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hull);
                using (var pen = new Pen(Color.FromArgb(fill * 4 / 5, 255, 255, 255), Eva.F(1.5f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pTop);
            }
            else if (fill > 0)
            {
                using (var pen = new Pen(Color.FromArgb(fill, Ink), Eva.F(1.4f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, pTop);
                using (var pen = new Pen(Color.FromArgb(fill, Ink), Eva.F(2.8f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, hull);
            }

            // Wireframe (fades out once the board is connected): rings, uprights, and a scan line over the deck.
            if (wire > 0)
            {
                using (var pen = new Pen(Eva.A(Eva.Orange, wire), Eva.F(1.2f)))
                {
                    g.DrawPolygon(pen, pTop); g.DrawPolygon(pen, pMid); g.DrawPolygon(pen, pBot);
                    for (int i = 0; i < n; i += 4) { g.DrawLine(pen, pTop[i], pMid[i]); g.DrawLine(pen, pMid[i], pBot[i]); }
                }
                using (var pen = new Pen(Eva.A(Eva.Orange, wire / 3), 1f))
                {
                    for (int ix = 1; ix < 10; ix++)
                    {
                        var x = -HalfLen + 2 * HalfLen * ix / 10f;
                        g.DrawLine(pen, Proj(Deform(x, yTop, -HalfDep + 0.06f)), Proj(Deform(x, yTop, HalfDep - 0.06f)));
                    }
                    for (int iz = 1; iz < 5; iz++)
                    {
                        var z = -HalfDep + 2 * HalfDep * iz / 5f;
                        g.DrawLine(pen, Proj(Deform(-HalfLen + 0.06f, yTop, z)), Proj(Deform(HalfLen - 0.06f, yTop, z)));
                    }
                }
                var sz = (float)Math.Sin(time * 1.6f) * (HalfDep - 0.08f);
                using (var pen = new Pen(Eva.A(Eva.Amber, wire), Eva.F(2f)))
                    g.DrawLine(pen, Proj(Deform(-HalfLen + 0.08f, yTop + 0.01f, sz)), Proj(Deform(HalfLen - 0.08f, yTop + 0.01f, sz)));
            }

            // Callouts from each load cell out to a tag with its share of the weight.
            var names = new[] { "TL", "TR", "BL", "BR" };
            for (int k = 0; k < 4 && !Eva.Aero; k++)
            {
                var anchor = Proj(Deform(corners[k].X * 1.08f, yTop, corners[k].Y * 1.12f));
                var dx = corners[k].X < 0 ? -1 : 1; var dy = corners[k].Y < 0 ? -1 : 1;
                var elbow = new PointF(anchor.X + dx * Eva.F(22), anchor.Y + dy * Eva.F(18));
                // Keep the tags clear of the corner plates.
                elbow.Y = Math.Max(Eva.F(84), Math.Min(Height - Eva.F(84), elbow.Y));
                var end = new PointF(elbow.X + dx * Eva.F(34), elbow.Y);
                var col = Loaded ? Eva.Amber : Eva.TextOff;
                using (var pen = new Pen(Eva.A(col, 190), 1f)) g.DrawLines(pen, new[] { anchor, elbow, end });
                using (var b = new SolidBrush(col)) g.FillEllipse(b, anchor.X - Eva.F(2.5f), anchor.Y - Eva.F(2.5f), Eva.F(5), Eva.F(5));
                var label = names[k] + " " + (Loaded ? loads[k].ToString("0.0") : "--.-");
                var f = Eva.Num(10f);
                var w = Eva.Measure(g, label, f).Width;
                var tx = dx < 0 ? end.X - w : end.X;
                Eva.Text_(g, label, f, Loaded ? Eva.Text : Eva.TextOff, tx, end.Y - Eva.F(15));
            }
        }

        // Andrew's monotone chain.
        static PointF[] Hull(List<PointF> pts)
        {
            pts.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            var h = new PointF[pts.Count * 2];
            int k = 0;
            Func<PointF, PointF, PointF, float> cross = (o, a, b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            for (int i = 0; i < pts.Count; i++) { while (k >= 2 && cross(h[k - 2], h[k - 1], pts[i]) <= 0) k--; h[k++] = pts[i]; }
            for (int i = pts.Count - 2, t = k + 1; i >= 0; i--) { while (k >= t && cross(h[k - 2], h[k - 1], pts[i]) <= 0) k--; h[k++] = pts[i]; }
            var res = new PointF[Math.Max(0, k - 1)];
            Array.Copy(h, res, res.Length);
            return res;
        }

        // ---- HUD -------------------------------------------------------------------------------------------------

        void DrawHud(Graphics g)
        {
            var W = Width; var H = Height;
            var loaded = Loaded;
            var pw = Eva.F(132); var ph = Eva.F(50); var m = Eva.F(14);

            Plate(g, new RectangleF(m, m, pw, ph), "LATERAL", loaded ? brX.ToString("0.0") : "--.-", "%", "L ◂ ▸ R");
            Plate(g, new RectangleF(W - m - pw, m, pw, ph), "AXIAL", loaded ? brY.ToString("0.0") : "--.-", "%", "FWD ▴ ▾ BACK");
            Plate(g, new RectangleF(m, H - m - ph, pw, ph), "TWIST", loaded ? twist.ToString("+0.0;-0.0;0.0") : "--.-", "", loaded ? "DL " + brDL.ToString("0.0") + "  DR " + brDR.ToString("0.0") : "on BOARD-01");
            Plate(g, new RectangleF(W - m - pw, H - m - ph, pw, ph), "LOAD", linked ? weight.ToString("0.0") : "--.-", "kg", loaded ? "Δ DIAG " + brDF.ToString("0.0") : "on BOARD-01");

            // Pattern word, Eva title-card style, centred at the top.
            string word; Color wc;
            if (!linked)                                   { word = "STANDBY";  wc = Eva.TextDim; }
            else if (airborne > 0)                         { word = "AIRBORNE"; wc = Eva.Amber; }
            else if (!loaded)                              { word = "VACANT";   wc = Eva.TextDim; }
            else if (walk > 0.05f)                         { word = "WALKING";  wc = Eva.Green; }
            else if (Math.Abs(turn) > 0.05f)               { word = "TWISTING"; wc = Eva.Teal; }
            else if (Math.Abs(LeanX) + Math.Abs(LeanY) > 0.25f) { word = "LEANING"; wc = Eva.Orange; }
            else                                           { word = "STANDING"; wc = Eva.Text; }
            var px = Eva.F(30);
            var ww = Eva.MeasureTitle(word, px, 0.70f);
            Eva.Text_(g, "PATTERN", Eva.UiBold(7f), Eva.TextDim, W / 2f - ww / 2, Eva.F(12));
            Eva.DrawTitle(g, word, W / 2f - ww / 2, Eva.F(22), px, wc, 0.70f, 0.04f);
            Eva.TextRight(g, "重心観測", Eva.Kanji(7.5f), Eva.A(Eva.Orange, 190), W / 2f + ww / 2, Eva.F(11));

            // Footstep lamps while walking in place.
            if (walkEnabled && linked)
            {
                var cy = H - Eva.F(30); var cx = W / 2f;
                for (int s = -1; s <= 1; s += 2)
                {
                    var on = walkSide == s;
                    var r = new RectangleF(cx + s * Eva.F(18) - Eva.F(8), cy - Eva.F(11), Eva.F(16), Eva.F(22));
                    using (var path = Eva.Round(r, Eva.F(7)))
                    {
                        using (var b = new SolidBrush(on ? Eva.Green : Eva.Bg2)) g.FillPath(b, path);
                        using (var pen = new Pen(on ? Eva.Green : Eva.Line, 1f)) g.DrawPath(pen, path);
                    }
                    Eva.TextCenter(g, s < 0 ? "L" : "R", Eva.Num(8f), on ? Eva.Bg0 : Eva.TextOff, r.X + r.Width / 2, r.Y + Eva.F(4));
                }
                Eva.TextCenter(g, "WALK " + walk.ToString("0%"), Eva.Num(9f), walk > 0.05f ? Eva.Green : Eva.TextDim, cx, cy + Eva.F(13));
            }

            // No signal: hazard band across the middle while the wireframe is showing.
            if (solid < 0.98f)
            {
                var a = 1f - solid;
                var bandH = Eva.F(46);
                var by = H * 0.5f - bandH / 2 + Eva.F(10);
                var clip = g.Clip;
                g.SetClip(new RectangleF(0, by, W, bandH));
                using (var b = new SolidBrush(Eva.A(Eva.Bg0, (int)(190 * a)))) g.FillRectangle(b, 0, by, W, bandH);
                using (var pen = new Pen(Eva.A(Eva.Orange, (int)(70 * a)), Eva.F(6)))
                    for (var x = -bandH + (time * 30 % Eva.F(24)); x < W + bandH; x += Eva.F(24))
                        g.DrawLine(pen, x, by + bandH, x + bandH, by);
                g.Clip = clip;
                using (var pen = new Pen(Eva.A(Eva.Orange, (int)(220 * a)), Eva.F(1.5f)))
                {
                    g.DrawLine(pen, 0, by, W, by);
                    g.DrawLine(pen, 0, by + bandH, W, by + bandH);
                }
                var t = "NO SIGNAL";
                var tpx = Eva.F(34);
                var tw = Eva.MeasureTitle(t, tpx, 0.72f);
                using (var b = new SolidBrush(Eva.A(Eva.Bg0, (int)(230 * a)))) g.FillRectangle(b, W / 2f - tw / 2 - Eva.F(14), by + Eva.F(4), tw + Eva.F(28) + Eva.F(70), bandH - Eva.F(8));
                Eva.DrawTitle(g, t, W / 2f - tw / 2, by + Eva.F(6), tpx, Eva.A(Eva.Red, (int)(255 * a)), 0.72f, 0.04f);
                Eva.Text_(g, "信号途絶", Eva.Kanji(10f), Eva.A(Eva.Orange, (int)(230 * a)), W / 2f + tw / 2 + Eva.F(10), by + Eva.F(9));
                Eva.Text_(g, "CONNECT BOARD", Eva.UiBold(6.5f), Eva.A(Eva.TextDim, (int)(230 * a)), W / 2f + tw / 2 + Eva.F(10), by + Eva.F(27));
            }

            if (demo) Eva.TextRight(g, "DEMO FEED", Eva.UiBold(7f), Eva.Teal, W - Eva.F(16), Eva.F(70));
        }

        // Rounded data plate with side pins, after the MAGI defensive-screen panels.
        static void Plate(Graphics g, RectangleF r, string caption, string value, string unit, string sub)
        {
            using (var b = new SolidBrush(Eva.A(Eva.Bg0, 200)))
            using (var path = Eva.Round(r, Eva.F(7)))
            {
                g.FillPath(b, path);
                using (var pen = new Pen(Eva.Orange, Eva.F(1.5f))) g.DrawPath(pen, path);
            }
            using (var b = new SolidBrush(Eva.OrangeLo))
            {
                using (var pin = Eva.Round(new RectangleF(r.X - Eva.F(4), r.Y + Eva.F(9), Eva.F(6), r.Height - Eva.F(18)), Eva.F(3))) g.FillPath(b, pin);
                using (var pin = Eva.Round(new RectangleF(r.Right - Eva.F(2), r.Y + Eva.F(9), Eva.F(6), r.Height - Eva.F(18)), Eva.F(3))) g.FillPath(b, pin);
            }
            Eva.DrawTitle(g, caption, r.X + Eva.F(9), r.Y + Eva.F(4), Eva.F(13), Eva.Amber, 0.74f, 0.02f);
            using (var pen = new Pen(Eva.A(Eva.Orange, 120), 1f)) g.DrawLine(pen, r.X + Eva.F(8), r.Y + Eva.F(19), r.Right - Eva.F(8), r.Y + Eva.F(19));
            var vf = Eva.Num(17f);
            var uf = Eva.UiBold(8f);
            var uw = string.IsNullOrEmpty(unit) ? 0 : Eva.Measure(g, unit, uf).Width + Eva.F(3);
            var vw = Eva.Measure(g, value, vf).Width;
            Eva.Text_(g, value, vf, Eva.Text, r.Right - Eva.F(9) - uw - vw, r.Y + Eva.F(18));
            if (uw > 0) Eva.Text_(g, unit, uf, Eva.Amber, r.Right - Eva.F(9) - uw + Eva.F(3), r.Y + Eva.F(31));
            Eva.Text_(g, sub, Eva.Ui(6.5f), Eva.TextDim, r.X + Eva.F(9), r.Y + Eva.F(34));
        }

        // Static backdrop: violet radial glow, faint hex lattice, scan lines, rulers and corner brackets.
        Bitmap BuildBackdrop()
        {
            var W = Math.Max(1, Width); var H = Math.Max(1, Height);
            var bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Eva.Quality(g);
                g.Clear(Eva.Bg0);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(-W * 0.25f, -H * 0.2f, W * 1.5f, H * 1.4f);
                    using (var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb(40, 26, 54), SurroundColors = new[] { Eva.Bg0 }, CenterPoint = new PointF(W * 0.5f, H * 0.55f) })
                        g.FillRectangle(pg, 0, 0, W, H);
                }

                // Hex lattice.
                var hs = Eva.F(18);
                using (var pen = new Pen(Color.FromArgb(14, 200, 160, 255), 1f))
                    for (float y = 0, row = 0; y < H + hs; y += hs * 0.866f * 2, row++)
                        for (float x = 0; x < W + hs * 3; x += hs * 3)
                        {
                            HexAt(g, pen, x, y, hs);
                            HexAt(g, pen, x + hs * 1.5f, y + hs * 0.866f, hs);
                        }

                using (var pen = new Pen(Color.FromArgb(10, 0, 0, 0), 1f))
                    for (var y = 0f; y < H; y += Eva.F(3)) g.DrawLine(pen, 0, y, W, y);

                // Left ruler 8..2, right column of kanji numerals, after the MAGI screen.
                var f = Eva.Num(9f);
                for (int i = 0; i < 7; i++)
                {
                    var y = H * 0.30f + i * Eva.F(26);
                    Eva.Text_(g, (8 - i).ToString(), f, Eva.A(Eva.Amber, 90), Eva.F(10), y);
                    using (var pen = new Pen(Eva.A(Eva.Amber, 60), 1f)) g.DrawLine(pen, Eva.F(24), y + Eva.F(7), Eva.F(34), y + Eva.F(7));
                }
                var kn = new[] { "二", "三", "四", "五", "六", "七", "八" };
                for (int i = 0; i < kn.Length; i++)
                    Eva.Text_(g, kn[i], Eva.Kanji(8f), Eva.A(Eva.Orange, 80), W - Eva.F(22), H * 0.30f + i * Eva.F(26));

                using (var pen = new Pen(Eva.A(Eva.Orange, 150), Eva.F(1.5f)))
                    Eva.Brackets(g, new RectangleF(Eva.F(4), Eva.F(4), W - Eva.F(8), H - Eva.F(8)), Eva.F(22), pen);
                using (var pen = new Pen(Eva.Line, 1f)) g.DrawRectangle(pen, 0, 0, W - 1, H - 1);
            }
            return bmp;
        }

        static void HexAt(Graphics g, Pen pen, float cx, float cy, float s)
        {
            var pts = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                var a = i * (float)Math.PI / 3;
                pts[i] = new PointF(cx + s * (float)Math.Cos(a), cy + s * (float)Math.Sin(a));
            }
            g.DrawPolygon(pen, pts);
        }
    }
}
