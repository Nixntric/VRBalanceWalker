//----------------------------------------------------------------------------------------------------------------------+
// Aero staging for the board view, after the Wii Fit balance screens: a bright sky-to-white stage with a slowly turning
// sunburst and rising bubbles, a glossy white platform with turning aqua rings, the board in smooth glossy plastic with
// a reflection sliding over the deck, a red centre-of-balance marble, and a glass status pill that pops on changes.
// The board geometry, springs and twist arrow are shared with the Eva view (BoardView.cs).
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    partial class BoardView
    {
        Bitmap aeroBack, aeroFrame;     // static stage; the same with its inside cleared (patches the rounded corners)
        Bitmap aeroSky;                 // stage plus the sunburst, refreshed a few times a second (it turns slowly)
        float skyTime = -1;
        int aeroBackGen = -1;
        const float Plat = 1.32f;       // platform radius, board units
        readonly Spr pillPop = new Spr { X = 1f };
        string lastMood;

        void PaintAero(PaintEventArgs e)
        {
            var g = e.Graphics;
            var W = Width; var H = Height;
            camCX = W * 0.5f; camCY = H * 0.52f;
            var e50 = 50f * (float)Math.PI / 180f;
            camCE = (float)Math.Cos(e50); camSE = (float)Math.Sin(e50);
            camF = Math.Min(W * 0.29f, H * 0.50f) * camD;

            if (aeroBack == null || aeroBackGen != Aero.Gen || aeroBack.Width != Math.Max(1, W) || aeroBack.Height != Math.Max(1, H))
            {
                if (aeroBack != null) aeroBack.Dispose();
                if (aeroFrame != null) aeroFrame.Dispose();
                BuildAeroBackdrop(out aeroBack, out aeroFrame);
                aeroBackGen = Aero.Gen;
                skyTime = -1;
            }
            if (aeroSky == null || aeroSky.Width != aeroBack.Width || aeroSky.Height != aeroBack.Height || skyTime < 0 || time - skyTime > 0.1f || time < skyTime)
            {
                if (aeroSky == null || aeroSky.Width != aeroBack.Width || aeroSky.Height != aeroBack.Height)
                {
                    if (aeroSky != null) aeroSky.Dispose();
                    aeroSky = Aero.NewBitmap(W, H);
                }
                using (var sg = Graphics.FromImage(aeroSky))
                {
                    Aero.Blit(sg, aeroBack, new Rectangle(0, 0, W, H));
                    Eva.Quality(sg);
                    DrawSunburst(sg);
                }
                skyTime = time;
            }
            Aero.Blit(g, aeroSky, new Rectangle(0, 0, W, H));
            Eva.Quality(g);

            // No clip region here (it makes every fill slow): draw freely, then put the rounded corners and rim back.
            DrawAeroSky(g);
            DrawAeroFloor(g);
            DrawBoard(g);
            DrawAeroHud(g);
            DrawAeroGloss(g);

            var cs = (int)Math.Ceiling(Eva.F(20));
            Aero.Overlay(g, aeroFrame, new Rectangle(0, 0, cs, cs));
            Aero.Overlay(g, aeroFrame, new Rectangle(W - cs, 0, cs, cs));
            Aero.Overlay(g, aeroFrame, new Rectangle(0, H - cs, cs, cs));
            Aero.Overlay(g, aeroFrame, new Rectangle(W - cs, H - cs, cs, cs));
            StrokeAeroRim(g);
        }

        RectangleF AeroRim { get { return new RectangleF(Eva.F(1), Eva.F(1), Width - Eva.F(2.5f), Height - Eva.F(3)); } }

        void StrokeAeroRim(Graphics g)
        {
            var rr = AeroRim; var rad = Eva.F(16);
            using (var path = Eva.Round(rr, rad))
            using (var pen = new Pen(Color.FromArgb(220, 96, 150, 205), Eva.F(1f))) g.DrawPath(pen, path);
            using (var inner = Eva.Round(RectangleF.Inflate(rr, -Eva.F(1), -Eva.F(1)), rad - Eva.F(1)))
            using (var pen = new Pen(Aero.Wh(220), Eva.F(1.2f))) g.DrawPath(pen, inner);
        }

        // Static stage: sky gradient into a white glow, soft bokeh, the glossy platform, rimmed like a glass screen.
        void BuildAeroBackdrop(out Bitmap back, out Bitmap frame)
        {
            var W = Math.Max(1, Width); var H = Math.Max(1, Height);
            var bmp = Aero.NewBitmap(W, H);
            using (var g = Graphics.FromImage(bmp))
            {
                Aero.PaintBehind(g, this);
                Eva.Quality(g);
                var rr = new RectangleF(Eva.F(1), Eva.F(1), W - Eva.F(2.5f), H - Eva.F(3));
                var rad = Eva.F(16);
                using (var path = Eva.Round(rr, rad))
                {
                    using (var lg = new LinearGradientBrush(new RectangleF(0, rr.Y - 1, W, rr.Height + 2), Color.Black, Color.White, 90f))
                    {
                        lg.InterpolationColors = new ColorBlend
                        {
                            Colors = new[] { Color.FromArgb(46, 140, 222), Color.FromArgb(120, 196, 244), Color.FromArgb(214, 238, 252), Color.FromArgb(200, 230, 248), Color.FromArgb(150, 204, 240) },
                            Positions = new[] { 0f, 0.3f, 0.58f, 0.82f, 1f }
                        };
                        g.FillPath(lg, path);
                    }
                    var st = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    Aero.Glow(g, new RectangleF(W * 0.12f, H * 0.22f, W * 0.76f, H * 0.8f), Aero.Wh(200));
                    var rnd = new Random(5);
                    for (int i = 0; i < 16; i++)
                    {
                        var r = Eva.F(8 + (float)rnd.NextDouble() * 34);
                        var x = (float)rnd.NextDouble() * W; var y = (float)rnd.NextDouble() * H * 0.7f;
                        var a = 14 + rnd.Next(26);
                        using (var b = new SolidBrush(Aero.Wh(a))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                        using (var pen = new Pen(Aero.Wh(a * 2), 1f)) g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
                    }
                    g.Restore(st);
                }
                DrawPlatform(g);
                StrokeAeroRim(g);
            }
            back = bmp;

            frame = (Bitmap)bmp.Clone();
            using (var g = Graphics.FromImage(frame))
            using (var inside = Eva.Round(RectangleF.Inflate(AeroRim, -Eva.F(1.5f), -Eva.F(1.5f)), Eva.F(14.5f)))
            {
                g.SetClip(inside);
                g.Clear(Color.Transparent);
            }
        }

        // Platform: a glossy white disc with a blue lip on the near side and a lit rim.
        void DrawPlatform(Graphics g)
        {
            var disc = FloorCircle(Plat, 72, 0);
            var lip = FloorCircle(Plat, 72, 0);
            for (int i = 0; i < lip.Length; i++) lip[i].Y += Eva.F(5);
            using (var pen = new Pen(Color.FromArgb(150, 90, 160, 220), Eva.F(5f))) g.DrawLines(pen, lip);
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(disc);
                if (path.GetBounds().Width > 2)
                    using (var pg = new PathGradientBrush(path) { CenterColor = Aero.Wh(235), SurroundColors = new[] { Color.FromArgb(220, 168, 214, 246) } })
                        g.FillPath(pg, path);
            }
            using (var pen = new Pen(Aero.Wh(240), Eva.F(2.4f))) g.DrawLines(pen, disc);
            using (var pen = new Pen(Eva.A(Eva.Orange, 150), Eva.F(1.2f))) g.DrawLines(pen, FloorCircle(Plat + 0.05f, 72, 0));
        }

        // A slowly turning sunburst behind the board.
        void DrawSunburst(Graphics g)
        {
            var W = Width; var H = Height;
            var c = new PointF(W * 0.5f, H * 0.46f);
            var len = Math.Max(W, H);
            const int n = 18;
            using (var b = new SolidBrush(Aero.Wh(24)))
                for (int i = 0; i < n; i++)
                {
                    var a = time * 0.06f + i * (float)Math.PI * 2 / n;
                    const float w = 0.075f;
                    g.FillPolygon(b, new[] { c, new PointF(c.X + (float)Math.Cos(a - w) * len, c.Y + (float)Math.Sin(a - w) * len), new PointF(c.X + (float)Math.Cos(a + w) * len, c.Y + (float)Math.Sin(a + w) * len) });
                }
        }

        // Bubbles drifting up behind the board.
        void DrawAeroSky(Graphics g)
        {
            var W = Width; var H = Height;

            for (int i = 0; i < 16; i++)
            {
                var r = Eva.F(3 + (i * 7) % 11);
                var speed = Eva.F(12 + (i * 5 % 7) * 5);
                var span = H + r * 4;
                var y = H + r * 2 - ((time * speed + ((i * 0.618f) % 1f) * span) % span);
                var x = ((i * 0.381966f + 0.07f) % 1f) * W + (float)Math.Sin(time * 0.9f + i) * Eva.F(10);
                Aero.Bubble(g, x, y, r);
            }
        }

        PointF[] FloorCircle(float rad, int n, float spin)
        {
            var pts = new PointF[n + 1];
            for (int i = 0; i <= n; i++)
            {
                var a = i / (float)n * (float)Math.PI * 2 + spin;
                pts[i] = Proj(new V3((float)Math.Cos(a) * rad, 0, (float)Math.Sin(a) * rad));
            }
            return pts;
        }

        // Platform, shadow, turning rings, the gel twist arrow and water ripples.
        void DrawAeroFloor(Graphics g)
        {
            var dim = 0.45f + 0.55f * solid;

            DrawShadow(g, Color.FromArgb(20, 60, 110));

            Action<float, float, int, float, int> Dashes = (rad, spin, segs, width, alpha) =>
            {
                var pts = FloorCircle(rad, segs, spin);
                using (var path = new GraphicsPath())
                using (var pen = new Pen(Eva.A(Eva.Orange, (int)(alpha * dim)), Eva.F(width)))
                {
                    for (int i = 0; i < segs; i += 2) { path.StartFigure(); path.AddLine(pts[i], pts[i + 1]); }
                    g.DrawPath(pen, path);
                }
            };
            Dashes(Plat + 0.24f, time * 0.12f, 60, 2.4f, 190);
            Dashes(Plat + 0.42f, -time * 0.08f, 120, 1.2f, 110);

            // Twist arrow on the middle ring, from straight ahead, sweeping the way you're turning: a gel tube.
            if (Math.Abs(turn) > 0.02f || Math.Abs(twist) > 3f)
            {
                var sweep = Math.Abs(turn) > 0.02f ? turn : Math.Sign(twist) * 0.06f;
                var rad = Plat + 0.12f;
                var a0 = -(float)Math.PI / 2;
                var a1 = a0 + sweep * 1.9f;
                var pts = new PointF[25];
                for (int i = 0; i <= 24; i++)
                {
                    var a = a0 + (a1 - a0) * i / 24f;
                    pts[i] = Proj(new V3((float)Math.Cos(a) * rad, 0, (float)Math.Sin(a) * rad));
                }
                var col = Math.Abs(turn) > 0.02f ? Eva.Teal : Color.FromArgb(150, 175, 200);
                using (var glow = new Pen(Eva.A(col, 55), Eva.F(14)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(glow, pts);
                using (var pen = new Pen(Aero.Darken(col, 0.15f), Eva.F(7)) { StartCap = LineCap.Round }) g.DrawLines(pen, pts);
                using (var pen = new Pen(Aero.Lighten(col, 0.3f), Eva.F(3.5f)) { StartCap = LineCap.Round }) g.DrawLines(pen, pts);
                using (var pen = new Pen(Aero.Wh(170), Eva.F(1.4f)) { StartCap = LineCap.Round }) g.DrawLines(pen, pts);
                var dir = Math.Sign(sweep);
                var tip = Proj(new V3((float)Math.Cos(a1 + dir * 0.2f) * rad, 0, (float)Math.Sin(a1 + dir * 0.2f) * rad));
                var w1 = Proj(new V3((float)Math.Cos(a1) * (rad - 0.13f), 0, (float)Math.Sin(a1) * (rad - 0.13f)));
                var w2 = Proj(new V3((float)Math.Cos(a1) * (rad + 0.13f), 0, (float)Math.Sin(a1) * (rad + 0.13f)));
                var head = new[] { tip, w1, w2 };
                using (var b = new SolidBrush(col)) g.FillPolygon(b, head);
                using (var pen = new Pen(Aero.Wh(210), Eva.F(1.3f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, head);
            }

            foreach (var r in ripples)
            {
                var t = (time - r.Born) / 1.1f;
                var rad = 1.15f + 1.2f * (1 - (1 - t) * (1 - t));
                var a = (int)(220 * (1 - t) * (1 - t) * r.Strength);
                using (var pen = new Pen(Eva.A(r.Color, a), Eva.F(1 + 3 * (1 - t)))) g.DrawLines(pen, FloorCircle(rad, 48, 0));
                using (var pen = new Pen(Aero.Wh(a * 3 / 4), Eva.F(1))) g.DrawLines(pen, FloorCircle(rad * 0.94f, 48, 0));
            }
        }

        // Top face in one gradient fill: the shading is sampled along the board's length (torsion and bend vary along x).
        void FillDeckAero(Graphics g, PointF[] pTop, float yTop, V3 up, int fill)
        {
            const int ns = 9;
            var cols = new Color[ns]; var pos = new float[ns];
            for (int i = 0; i < ns; i++)
            {
                var x = -HalfLen + 2 * HalfLen * i / (ns - 1);
                var a = Deform(x - 0.05f, yTop, -0.1f); var b = Deform(x + 0.05f, yTop, -0.1f); var d = Deform(x - 0.05f, yTop, 0.1f);
                var nrm = (d - a).Cross(b - a).Norm();
                if (nrm.Dot(up) < 0) nrm = nrm * -1f;
                cols[i] = Tone(BodyRamp, nrm, fill);
                pos[i] = i / (float)(ns - 1);
            }
            var p0 = Proj(Deform(-HalfLen, yTop, 0)); var p1 = Proj(Deform(HalfLen, yTop, 0));
            if (Math.Abs(p0.X - p1.X) + Math.Abs(p0.Y - p1.Y) < 2) { using (var b = new SolidBrush(cols[ns / 2])) g.FillPolygon(b, pTop); return; }
            using (var lg = new LinearGradientBrush(p0, p1, cols[0], cols[ns - 1]) { WrapMode = WrapMode.TileFlipX })
            {
                lg.InterpolationColors = new ColorBlend { Colors = cols, Positions = pos };
                g.FillPolygon(lg, pTop);
            }
        }

        // A window reflection that slides across the deck as the board rolls and turns (kept inside the deck, so no clip).
        void DrawDeckGloss(Graphics g, PointF[] pTop, float yTop, int fill)
        {
            // Both streaks share one offset, eased (tanh) into the range that keeps the pair on the deck, so they always
            // move together: the pair spans x = -0.78 .. 0.10 at rest and the deck allows -0.9 .. 0.9.
            var raw = -roll.X * 2.4f + yaw.X * 1.2f;
            var off = raw >= 0 ? 0.80f * (float)Math.Tanh(raw / 0.80f) : 0.12f * (float)Math.Tanh(raw / 0.12f);
            Action<float, float, int> Streak = (x0, width, alpha) =>
            {
                var a = Proj(Deform(x0 + 0.16f, yTop + 0.003f, -HalfDep + 0.06f));
                var b = Proj(Deform(x0 + 0.16f + width, yTop + 0.003f, -HalfDep + 0.06f));
                var c = Proj(Deform(x0 - 0.16f + width, yTop + 0.003f, HalfDep - 0.06f));
                var d = Proj(Deform(x0 - 0.16f, yTop + 0.003f, HalfDep - 0.06f));
                var p1 = new PointF((a.X + d.X) / 2, (a.Y + d.Y) / 2); var p2 = new PointF((b.X + c.X) / 2, (b.Y + c.Y) / 2);
                if (Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y) < 1) return;
                using (var lg = new LinearGradientBrush(p1, p2, Color.White, Color.White))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { Aero.Wh(0), Aero.Wh(alpha * fill / 255), Aero.Wh(0) }, Positions = new[] { 0f, 0.5f, 1f } };
                    g.FillPolygon(lg, new[] { a, b, c, d });
                }
            };
            Streak(-0.62f + off, 0.34f, 150);
            Streak(-0.18f + off, 0.12f, 110);
        }

        // Centre of balance, Wii Fit style: dashed guides, a trail of fading red dots, a pulsing ring and a red marble.
        void DrawAeroCog(Graphics g, float yTop)
        {
            float cx = LeanX * 0.86f, cz = LeanY * 0.50f;
            var lx = new PointF[13]; var lz = new PointF[9];
            for (int i = 0; i < lx.Length; i++) lx[i] = Proj(Deform(-0.92f + 1.84f * i / (lx.Length - 1), yTop + 0.015f, cz));
            for (int i = 0; i < lz.Length; i++) lz[i] = Proj(Deform(cx, yTop + 0.015f, -0.52f + 1.04f * i / (lz.Length - 1)));
            using (var pen = new Pen(Eva.A(Eva.Orange, (int)(140 * solid)), Eva.F(1.2f)) { DashStyle = DashStyle.Dash })
            {
                g.DrawLines(pen, lx);
                g.DrawLines(pen, lz);
            }
            var red = Color.FromArgb(232, 48, 52);
            var cp = Proj(Deform(cx, yTop + 0.02f, cz));
            DrawCogRibbon(g, yTop, cp, red);
            var ph = (time * 1.1f) % 1f;
            var pr = Eva.F(8 + 16 * ph);
            using (var pen = new Pen(Eva.A(red, (int)(190 * (1 - ph))), Eva.F(2f))) g.DrawEllipse(pen, cp.X - pr, cp.Y - pr * 0.75f, pr * 2, pr * 1.5f);
            using (var b = new SolidBrush(Color.FromArgb(60, 60, 0, 0))) g.FillEllipse(b, cp.X - Eva.F(7), cp.Y - Eva.F(2), Eva.F(14), Eva.F(7));
            Aero.Orb(g, cp.X, cp.Y - Eva.F(3), Eva.F(7.5f), red);
        }

        // Short comet tail behind the balance point: the newest tenth of the trail, smoothed (Chaikin), drawn as a glossy
        // tube like the marble (dark rim, red body, bright core, white highlight on top) that tapers to a point and fades
        // in towards the marble.
        void DrawCogRibbon(Graphics g, float yTop, PointF head, Color red)
        {
            const int keep = 3;
            if (cogTrail.Count < 2) return;
            var pts = new System.Collections.Generic.List<PointF>();
            for (int i = Math.Max(0, cogTrail.Count - keep); i < cogTrail.Count; i++) pts.Add(Proj(Deform(cogTrail[i].X, yTop + 0.016f, cogTrail[i].Y)));
            pts.Add(head);
            for (int pass = 0; pass < 3; pass++)
            {
                var sm = new System.Collections.Generic.List<PointF> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    PointF a = pts[i], b = pts[i + 1];
                    sm.Add(new PointF(a.X * 0.75f + b.X * 0.25f, a.Y * 0.75f + b.Y * 0.25f));
                    sm.Add(new PointF(a.X * 0.25f + b.X * 0.75f, a.Y * 0.25f + b.Y * 0.75f));
                }
                sm.Add(pts[pts.Count - 1]);
                pts = sm;
            }

            var n = pts.Count;
            float len = 0;
            for (int i = 1; i < n; i++) { float dx = pts[i].X - pts[i - 1].X, dy = pts[i].Y - pts[i - 1].Y; len += (float)Math.Sqrt(dx * dx + dy * dy); }
            if (len < Eva.F(2)) return;

            var nrm = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                var a = pts[Math.Max(0, i - 1)]; var b = pts[Math.Min(n - 1, i + 1)];
                float dx = b.X - a.X, dy = b.Y - a.Y;
                var l = (float)Math.Sqrt(dx * dx + dy * dy);
                nrm[i] = l < 1e-3f ? new PointF(0, 1) : new PointF(-dy / l, dx / l);
            }

            var mode = g.SmoothingMode;
            Action<float, float, Color, int> Layer = (halfWidth, lift, col, alphaMax) =>
            {
                const int strips = 4;
                var left = new PointF[n]; var right = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    var w = halfWidth * (float)Math.Pow(i / (float)(n - 1), 0.75);
                    left[i]  = new PointF(pts[i].X + nrm[i].X * w, pts[i].Y + nrm[i].Y * w - lift);
                    right[i] = new PointF(pts[i].X - nrm[i].X * w, pts[i].Y - nrm[i].Y * w - lift);
                }
                for (int k = 0; k < strips; k++)
                {
                    int i0 = (n - 1) * k / strips, i1 = (n - 1) * (k + 1) / strips;
                    if (i1 <= i0) continue;
                    var alpha = (int)(alphaMax * solid * Math.Pow((k + 1) / (float)strips, 1.2));
                    var strip = new PointF[(i1 - i0 + 1) * 2];
                    for (int i = i0; i <= i1; i++) { strip[i - i0] = left[i]; strip[strip.Length - 1 - (i - i0)] = right[i]; }
                    g.SmoothingMode = SmoothingMode.None;
                    using (var br = new SolidBrush(Eva.A(col, alpha))) g.FillPolygon(br, strip);
                    g.SmoothingMode = mode;
                    var le = new PointF[i1 - i0 + 1]; var re = new PointF[i1 - i0 + 1];
                    for (int i = i0; i <= i1; i++) { le[i - i0] = left[i]; re[i - i0] = right[i]; }
                    using (var pen = new Pen(Eva.A(col, alpha), Eva.F(0.8f)) { LineJoin = LineJoin.Round })
                    {
                        g.DrawLines(pen, le);
                        g.DrawLines(pen, re);
                    }
                }
            };
            Layer(Eva.F(6.5f), 0, Aero.Darken(red, 0.3f), 230);
            Layer(Eva.F(5.2f), Eva.F(0.3f), red, 235);
            Layer(Eva.F(3f), Eva.F(1.2f), Aero.Lighten(red, 0.45f), 210);
            Layer(Eva.F(1.2f), Eva.F(2.2f), Color.White, 220);
        }

        // What the rider is doing, in words, for the status pill (and the Default view's status line).
        void Mood(out string mood, out string word, out Color wc)
        {
            if (!linked)                                        { mood = "wait";  word = "Waiting for the board"; wc = Color.FromArgb(150, 172, 196); }
            else if (airborne > 0)                              { mood = "air";   word = "Airborne!";             wc = Aero.Sun; }
            else if (!Loaded)                                   { mood = "empty"; word = "Step on the board";     wc = Aero.Aqua; }
            else if (walk > 0.05f)                              { mood = "walk";  word = "Walking";               wc = Aero.Lime; }
            else if (Math.Abs(turn) > 0.05f)                    { mood = "turn";  word = "Turning";               wc = Eva.Teal; }
            else if (Math.Abs(LeanX) + Math.Abs(LeanY) > 0.25f)
            {
                mood = "lean"; wc = Aero.Aqua;
                word = "Leaning " + (Math.Abs(LeanX) >= Math.Abs(LeanY) ? (LeanX > 0 ? "right" : "left") : (LeanY > 0 ? "back" : "forward"));
            }
            else                                                { mood = "stand"; word = "Standing still";        wc = Aero.Lime; }
        }

        // Status pill at the top (pops when the state changes), footstep pills, the no-board card and a demo tag.
        void DrawAeroHud(Graphics g)
        {
            var W = Width; var H = Height;
            var loaded = Loaded;

            string mood, word; Color wc;
            Mood(out mood, out word, out wc);

            if (mood != lastMood) { if (lastMood != null) { pillPop.X = 0.78f; pillPop.V = 0; } lastMood = mood; }
            pillPop.To(1f, 16f, 0.3f);

            var f = Aero.Semi(12f);
            var tsz = Eva.Measure(g, word, f);
            var ph = Eva.F(34); var pw = tsz.Width + Eva.F(52);
            var m = (int)Math.Ceiling(Eva.F(8));
            var spw = (int)Math.Ceiling(pw) + m * 2; var sph = (int)Math.Ceiling(ph) + m * 2;
            var pill = Aero.Sprite("mood|" + word + "|" + wc.ToArgb(), spw, sph, sg =>
            {
                var r = new RectangleF(m, m, pw, ph);
                Aero.GlassPill(sg, r);
                Aero.Orb(sg, r.X + Eva.F(20), r.Y + ph / 2, Eva.F(7.5f), wc);
                Aero.Emboss(sg, word, f, Eva.Text, r.X + Eva.F(36), r.Y + ph / 2 - tsz.Height / 2 - Eva.F(1));
            });
            var sc = pillPop.X;
            var pcx = W / 2f; var pcy = Eva.F(14) + ph / 2;
            g.DrawImage(pill, new RectangleF(pcx - spw / 2f * sc, pcy - sph / 2f * sc, spw * sc, sph * sc));
            var pulse = 0.5f + 0.5f * (float)Math.Sin(time * 3f);
            var ox = pcx + (Eva.F(20) - pw / 2) * sc;
            Aero.Glow(g, new RectangleF(ox - Eva.F(12), pcy - Eva.F(12), Eva.F(24), Eva.F(24)), Color.FromArgb((int)(30 + 70 * pulse), Color.White));

            // Footstep pills while walking in place.
            if (walkEnabled && linked)
            {
                var cy = H - Eva.F(36); var cx = W / 2f;
                var fm = (int)Math.Ceiling(Eva.F(5));
                int fw = (int)Math.Ceiling(Eva.F(18)) + fm * 2, fh = (int)Math.Ceiling(Eva.F(28)) + fm * 2;
                for (int s = -1; s <= 1; s += 2)
                {
                    var on = walkSide == s;
                    var lt = s < 0 ? "L" : "R";
                    var foot = Aero.Sprite("foot|" + lt + "|" + on, fw, fh, sg =>
                    {
                        var r = new RectangleF(fm, fm, Eva.F(18), Eva.F(28));
                        Aero.Shadow(sg, r, Eva.F(9), Eva.F(2), 40, Eva.F(1.5f));
                        Aero.Gel(sg, r, Eva.F(9), on ? Aero.Lime : Aero.Silver, on ? 0.4f : 0f);
                        var lf = Aero.Semi(8.5f);
                        var lsz = Eva.Measure(sg, lt, lf);
                        if (on) Aero.Shadowed(sg, lt, lf, Color.White, Color.FromArgb(120, 20, 70, 0), r.X + (r.Width - lsz.Width) / 2, r.Y + (r.Height - lsz.Height) / 2);
                        else Aero.Emboss(sg, lt, lf, Eva.TextDim, r.X + (r.Width - lsz.Width) / 2, r.Y + (r.Height - lsz.Height) / 2);
                    });
                    g.DrawImage(foot, new RectangleF(cx + s * Eva.F(20) - Eva.F(9) - fm, cy - Eva.F(14) - fm, fw, fh));
                }
                var wt = "Walk " + walk.ToString("0%");
                var wf = Aero.Semi(9f);
                Aero.Emboss(g, wt, wf, walk > 0.05f ? Aero.Darken(Aero.Lime, 0.2f) : Eva.TextDim, cx - Eva.Measure(g, wt, wf).Width / 2, cy + Eva.F(16));
            }

            // No board: a glass card with a breathing aqua halo.
            if (solid < 0.98f)
            {
                var a = 1f - solid;
                var t1 = "No board connected";
                var t2 = "Click “Connect to balance board”, then press the red sync button";
                var f1 = Aero.Semi(13f); var f2 = Aero.Ui(8.5f);
                var s1 = Eva.Measure(g, t1, f1); var s2 = Eva.Measure(g, t2, f2);
                var cw = Math.Max(s1.Width, s2.Width) + Eva.F(48);
                var card = new RectangleF(W / 2f - cw / 2, H * 0.70f, cw, Eva.F(58));
                var breathe = 0.5f + 0.5f * (float)Math.Sin(time * 2.4f);
                using (var path = Eva.Round(RectangleF.Inflate(card, Eva.F(3), Eva.F(3)), Eva.F(20)))
                using (var pen = new Pen(Eva.A(Aero.Aqua, (int)((70 + 110 * breathe) * a)), Eva.F(4))) g.DrawPath(pen, path);
                Aero.GlassPill(g, card, a);
                Aero.Emboss(g, t1, f1, Eva.A(Eva.Text, (int)(255 * a)), card.X + (cw - s1.Width) / 2, card.Y + Eva.F(8));
                Eva.Text_(g, t2, f2, Eva.A(Eva.TextDim, (int)(255 * a)), card.X + (cw - s2.Width) / 2, card.Y + Eva.F(33));
            }

            if (demo)
            {
                var df = Aero.Semi(8f);
                var dsz = Eva.Measure(g, "Demo", df);
                var dr = new RectangleF(W - Eva.F(22) - dsz.Width - Eva.F(24), Eva.F(18), dsz.Width + Eva.F(24), Eva.F(22));
                Aero.Gel(g, dr, dr.Height / 2f, Eva.Teal);
                Aero.Shadowed(g, "Demo", df, Color.White, Color.FromArgb(120, 0, 50, 60), dr.X + Eva.F(12), dr.Y + (dr.Height - dsz.Height) / 2 - Eva.F(0.5f));
            }
        }

        // Glass over the whole stage: a broad curved gloss across the top, pre-rendered and copied on unscaled.
        void DrawAeroGloss(Graphics g)
        {
            var W = Width; var H = Height;
            var gh = (int)(H * 0.37f);
            var gloss = Aero.Sprite("stage-gloss", Math.Max(1, W), Math.Max(1, gh), sg =>
            {
                var r = new RectangleF(-W * 0.25f, -H * 0.62f, W * 1.6f, H * 0.98f);
                using (var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Aero.Wh(0), Aero.Wh(0), 90f))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { Aero.Wh(0), Aero.Wh(16), Aero.Wh(44) }, Positions = new[] { 0f, 0.62f, 1f } };
                    sg.FillEllipse(lg, r);
                }
            });
            Aero.Overlay(g, gloss, new Rectangle(0, 0, gloss.Width, gloss.Height));
        }
    }
}
