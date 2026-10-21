//----------------------------------------------------------------------------------------------------------------------+
// The Debloated theme: WiiBalanceWalker's own window, as it was before the Eva rework (the stock v0.5 layout plus the
// Startup, Twist, Lean, Walk and SteamVR additions), rebuilt from that version's designer at the same positions with
// standard WinForms controls.
//
// The program logic keeps working on the Eva-skinned controls (hidden while Debloated is showing); each stock control
// here mirrors one of them both ways - checked state, value, text, enabled - so every setting behaves the same in all
// themes. The action rows' combo boxes and amounts and the console are plain controls already: they move into this
// view while it shows, and back afterwards.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WiiBalanceWalker
{
    public partial class FormMain
    {
        Panel classicView;
        readonly List<KeyValuePair<Control, KeyValuePair<Control, Rectangle>>> sharedHome = new List<KeyValuePair<Control, KeyValuePair<Control, Rectangle>>>();
        bool sharedInClassic;
        GroupBox classicActions;
        bool mirroring;     // set while copying an Eva control's state onto its stock twin, so the twin doesn't echo it back

        // Positions are the original designer's 96-dpi pixels.
        static Rectangle Old(int x, int y, int w, int h) { return new Rectangle(Eva.P(x), Eva.P(y), Eva.P(w), Eva.P(h)); }

        static T Put<T>(Control parent, T c, int x, int y, int w = 0, int h = 0) where T : Control
        {
            if (w > 0) c.Bounds = Old(x, y, w, h);
            else c.Location = new Point(Eva.P(x), Eva.P(y));
            c.Tag = "no-retint";
            parent.Controls.Add(c);
            return c;
        }

        static GroupBox Group(Control parent, string text, int x, int y, int w, int h)
        {
            return Put(parent, new GroupBox { Text = text }, x, y, w, h);
        }

        Label Caption(Control parent, string text, int x, int y)
        {
            return Put(parent, new Label { Text = text, AutoSize = true }, x, y);
        }

        // A read-only label showing what the logic writes into `source`.
        Label Readout(Control parent, Label source, int x, int y)
        {
            var l = Put(parent, new Label { AutoSize = true, Text = source.Text }, x, y);
            source.TextChanged += (s, e) => l.Text = source.Text;
            return l;
        }

        void MirrorEnabled(Control twin, Control source)
        {
            twin.Enabled = source.Enabled;
            source.EnabledChanged += (s, e) => twin.Enabled = source.Enabled;
        }

        void MirrorTip(Control twin, Control source)
        {
            var tip = toolTip1.GetToolTip(source);
            if (!string.IsNullOrEmpty(tip)) toolTip1.SetToolTip(twin, tip);
        }

        Button Push(Control parent, EvaButton source, string text, int x, int y, int w, int h, bool followText = false)
        {
            var b = Put(parent, new Button { Text = text, UseVisualStyleBackColor = true }, x, y, w, h);
            b.Click += (s, e) => source.Fire();
            if (followText) source.TextChanged += (s, e) => b.Text = source.Text;
            MirrorEnabled(b, source);
            MirrorTip(b, source);
            return b;
        }

        CheckBox Check(Control parent, CheckBox source, string text, int x, int y)
        {
            var c = Put(parent, new CheckBox { Text = text, AutoSize = true, Checked = source.Checked, UseVisualStyleBackColor = true }, x, y);
            c.CheckedChanged += (s, e) => { if (!mirroring) source.Checked = c.Checked; };
            source.CheckedChanged += (s, e) => { mirroring = true; c.Checked = source.Checked; mirroring = false; };
            MirrorEnabled(c, source);
            MirrorTip(c, source);
            return c;
        }

        NumericUpDown Spin(Control parent, EvaSlider source, int x, int y, int w, int h = 20)
        {
            var n = Put(parent, new NumericUpDown { Minimum = source.Minimum, Maximum = source.Maximum, Value = source.Value }, x, y, w, h);
            n.ValueChanged += (s, e) => { if (!mirroring) source.Value = (int)n.Value; };
            source.ValueChanged += (s, e) => { mirroring = true; n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, source.Value)); mirroring = false; };
            MirrorEnabled(n, source);
            MirrorTip(n, source);
            return n;
        }

        TrackBar Track(Control parent, EvaSlider source, int x, int y, int w, int h, int tick, int large)
        {
            var t = Put(parent, new TrackBar { AutoSize = false, Minimum = source.Minimum, Maximum = source.Maximum, Value = source.Value, TickFrequency = tick, LargeChange = large }, x, y, w, h);
            t.ValueChanged += (s, e) => { if (!mirroring) source.Value = t.Value; };
            source.ValueChanged += (s, e) => { mirroring = true; t.Value = Math.Max(t.Minimum, Math.Min(t.Maximum, source.Value)); mirroring = false; };
            MirrorEnabled(t, source);
            MirrorTip(t, source);
            return t;
        }

        void BuildClassicView()
        {
            var v = classicView = new Panel { Visible = false, Tag = "no-retint", BackColor = SystemColors.Control, Font = Control.DefaultFont };
            v.Bounds = Old(0, 0, 944, 596);
            Controls.Add(v);

            var raw = Group(v, "Raw Weight ", 12, 12, 150, 139);
            Readout(raw, label_rwTL, 25, 32); Readout(raw, label_rwTR, 101, 32); Readout(raw, label_rwBL, 25, 76); Readout(raw, label_rwBR, 101, 76); Readout(raw, label_rwWT, 63, 113);

            var off = Group(v, "Offset Weight", 168, 12, 150, 139);
            Readout(off, label_owTL, 25, 32); Readout(off, label_owTR, 101, 32); Readout(off, label_owBL, 25, 76); Readout(off, label_owBR, 101, 76); Readout(off, label_owWT, 63, 113);

            var ratio = Group(v, "Offset Weight Ratio", 324, 12, 150, 139);
            Readout(ratio, label_owrTL, 25, 32); Readout(ratio, label_owrTR, 101, 32); Readout(ratio, label_owrBL, 25, 76); Readout(ratio, label_owrBR, 101, 76);

            var balance = Group(v, "Balance Ratio", 480, 12, 150, 139);
            Readout(balance, label_brX, 25, 32); Readout(balance, label_brY, 101, 32); Readout(balance, label_brDL, 25, 76); Readout(balance, label_brDR, 101, 76); Readout(balance, label_brDF, 65, 113);

            var general = Group(v, "General", 12, 157, 373, 136);
            Push(general, zeroout, "Tare Balance Board", 15, 28, 138, 26);
            Push(general, button_SetCenterOffset, "Set current balance as center", 15, 59, 138, 34);
            Push(general, button_ResetDefaults, "Load default settings and exit", 15, 96, 138, 34);
            Push(general, button_BluetoothAddDevice, "Add/Remove bluetooth Wii device", 174, 29, 183, 46);
            Push(general, button_Connect, "Connect to Wii Balance Board", 174, 82, 183, 48, true);

            var startup = Group(v, "Startup Options", 392, 157, 238, 136);
            MirrorEnabled(startup, startupOptions);
            Check(startup, checkBox_StartupAutoConnect, "Connect to Balance Board on startup", 6, 19);
            Check(startup, checkBox_AutoTare, "Tare Balance Board after connection", 6, 42);
            Check(startup, checkBox_StartMinimized, "Start Program Minimized", 6, 65);

            var status = Put(v, new Label { BorderStyle = BorderStyle.Fixed3D, TextAlign = ContentAlignment.MiddleLeft, Text = "STATUS" }, 12, 296, 618, 24);
            label_Status.TextChanged += (s, e) => status.Text = label_Status.Text;

            var vjoy = Group(v, "vJoy output", 453, 326, 177, 123);
            MirrorEnabled(vjoy, groupBox_vJoy_output);
            Check(vjoy, checkBox_SendCGtoXY, "Send CG to X/Y axes", 6, 19);
            Check(vjoy, checkBox_Send4LoadSensors, "Send 4 load sensors values", 6, 42);
            Check(vjoy, checkBox_ShowValuesInConsole, "Show values in console", 6, 65);
            Caption(vjoy, "VJoy Device ID:", 3, 97);
            Spin(vjoy, VJoyIDUpDown, 88, 94, 35);
            Check(v, checkBox_EnableJoystick, "Enable vJoy output", 459, 455);

            var twist = Group(v, "Twist to turn", 453, 476, 177, 114);
            MirrorEnabled(twist, groupBox_Twist);
            Check(twist, checkBox_SendTwistToRX, "Send twist to Z axis", 6, 17);
            Caption(twist, "Dead:", 3, 40);
            Spin(twist, numericUpDown_TwistDeadzone, 40, 37, 40);
            Caption(twist, "Full:", 88, 40);
            Spin(twist, numericUpDown_TwistFull, 118, 37, 40);
            Put(twist, label_TwistSensitivity, 3, 68).AutoSize = true;
            Track(twist, trackBar_TwistSensitivity, 62, 62, 110, 28, 5, 5);
            Readout(twist, label_Twist, 3, 94);

            var lean = Group(v, "Lean to move (vJoy X/Y and SteamVR)", 12, 527, 435, 63);
            MirrorEnabled(lean, groupBox_Lean);
            Put(lean, label_LeanSensitivity, 6, 28).AutoSize = true;
            Track(lean, trackBar_LeanSensitivity, 62, 22, 150, 28, 5, 5);
            Put(lean, label_LeanDeadzone, 222, 28).AutoSize = true;
            Track(lean, trackBar_LeanDeadzone, 278, 22, 150, 28, 2, 2);

            var actions = classicActions = Group(v, "Actions", 636, 12, 296, 437);
            string[] actionText = { "- Left", "- Right", "- Forward", "- Backward", "- Modifier", "- Jump", "- Diagonal Left", "- Diagonal Right" };
            for (int i = 0; i < 8; i++) MirrorEnabled(Caption(actions, actionText[i], 15, 29 + 27 * i), actionLabels[i]);
            var triggers = Group(actions, "Balance Ratio Triggers", 18, 260, 239, 136);
            MirrorEnabled(Caption(triggers, "- Left / Right", 6, 28), label_TLR);                    Spin(triggers, numericUpDown_TLR, 178, 26, 49);
            MirrorEnabled(Caption(triggers, "- Forward / Backward", 6, 54), label_TFB);              Spin(triggers, numericUpDown_TFB, 178, 52, 49);
            MirrorEnabled(Caption(triggers, "- Modifier + Left / Right", 6, 80), label_TMLR);        Spin(triggers, numericUpDown_TMLR, 178, 78, 49);
            MirrorEnabled(Caption(triggers, "- Modifier + Foward / Backward", 6, 106), label_TMFB);  Spin(triggers, numericUpDown_TMFB, 178, 104, 49);
            Check(actions, checkBox_DisableActions, "Disable All Actions", 18, 407);

            var walk = Group(v, "Walk in place", 636, 455, 296, 88);
            MirrorEnabled(walk, groupBox_Walk);
            Check(walk, checkBox_WalkInPlace, "Walking in place moves forward (Y axis)", 6, 17);
            Caption(walk, "Step %:", 3, 42);
            Spin(walk, numericUpDown_WalkStep, 50, 39, 45);
            Caption(walk, "Speed %:", 110, 42);
            Spin(walk, numericUpDown_WalkSpeed, 165, 39, 45);
            Readout(walk, label_Walk, 3, 66);

            Check(v, checkBox_SteamVR, "Send to SteamVR (wbw treadmill driver)", 642, 556);
        }

        // The console and the action rows' combo boxes and amounts are shared: in Debloated they sit in the stock layout.
        void ShareWithClassicView(bool classic)
        {
            if (classic == sharedInClassic) return;
            sharedInClassic = classic;
            if (classic)
            {
                sharedHome.Clear();
                Action<Control, Control, Rectangle> Move = (c, parent, bounds) =>
                {
                    sharedHome.Add(new KeyValuePair<Control, KeyValuePair<Control, Rectangle>>(c, new KeyValuePair<Control, Rectangle>(c.Parent, c.Bounds)));
                    parent.Controls.Add(c);
                    c.Bounds = bounds;
                };
                Move(consoleBox, classicView, Old(12, 323, 435, 198));
                consoleBox.Visible = true;
                consoleBox.Select(consoleBox.TextLength, 0);
                for (int i = 0; i < 8; i++)
                {
                    Move(actionCombos[i], classicActions, Old(104, 26 + 27 * i, 125, 21));
                    Move(actionAmounts[i], classicActions, Old(235, 26 + 27 * i, 49, 20));
                }
            }
            else
            {
                foreach (var kv in sharedHome)
                {
                    kv.Value.Key.Controls.Add(kv.Key);
                    kv.Key.Bounds = kv.Value.Value;
                }
                sharedHome.Clear();
                consoleBox.Visible = tabs_Log.SelectedIndex == 0;
            }
        }
    }
}
