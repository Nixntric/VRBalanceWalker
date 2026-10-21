namespace WiiBalanceWalker
{
    using System.Drawing;
    using System.Windows.Forms;

    partial class FormMain
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // Hand-written layout (the reworked UI is owner drawn, so there is no designer surface). Positions are in
        // 96-dpi units and scaled by Eva.S; each one is relative to its parent panel.

        static T At<T>(Control parent, T c, float x, float y, float w, float h) where T : Control
        {
            c.SetBounds(Eva.P(x), Eva.P(y), Eva.P(w), Eva.P(h));
            parent.Controls.Add(c);
            return c;
        }

        static EvaPanel MakePanel(string code, string title, string kanji)
        {
            return new EvaPanel { Code = code, Title = title, Kanji = kanji };
        }

        static EvaSlider MakeSlider(string caption, int min, int max, int value, System.Func<int, string> format)
        {
            var s = new EvaSlider { Text = caption, Format = format };
            s.Maximum = max; s.Minimum = min; s.Value = value;
            return s;
        }

        static Label MakeLabel(string text)
        {
            return new Label { Text = text, BackColor = Eva.Bg1, ForeColor = Eva.Text, Font = Eva.Ui(9.5f), AutoSize = false };
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.SuspendLayout();

            this.toolTip1 = new ToolTip(this.components) { OwnerDraw = true, AutoPopDelay = 20000, InitialDelay = 450 };
            this.toolTip1.Popup += (s, e) =>
            {
                if (Eva.Classic) return;
                var text = this.toolTip1.GetToolTip(e.AssociatedControl);
                var size = TextRenderer.MeasureText(text, Eva.Ui(9f), new Size(Eva.P(360), 0), TextFormatFlags.WordBreak);
                e.ToolTipSize = new Size(size.Width + Eva.P(20), size.Height + Eva.P(14));
            };
            this.toolTip1.Draw += (s, e) =>
            {
                if (Eva.Aero)
                {
                    var tg = e.Graphics;
                    using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(e.Bounds, Color.White, Color.FromArgb(222, 238, 252), 90f)) tg.FillRectangle(lg, e.Bounds);
                    using (var b = new SolidBrush(Aero.Wh(150))) tg.FillRectangle(b, 1, 1, e.Bounds.Width - 2, e.Bounds.Height / 2);
                    using (var p = new Pen(Color.FromArgb(110, 160, 210))) tg.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                    var tr = new Rectangle(Eva.P(10), Eva.P(7), e.Bounds.Width - Eva.P(16), e.Bounds.Height - Eva.P(10));
                    TextRenderer.DrawText(tg, e.ToolTipText, Aero.Ui(9f), tr, Eva.Text, TextFormatFlags.WordBreak);
                    return;
                }
                using (var b = new SolidBrush(Eva.Bg2)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var p = new Pen(Eva.Orange)) e.Graphics.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                using (var b = new SolidBrush(Eva.Orange)) e.Graphics.FillRectangle(b, 0, 0, Eva.P(3), e.Bounds.Height);
                var r = new Rectangle(Eva.P(10), Eva.P(7), e.Bounds.Width - Eva.P(16), e.Bounds.Height - Eva.P(10));
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, Eva.Ui(9f), r, Eva.Text, TextFormatFlags.WordBreak);
            };

            // ---- title bar ----

            this.titleBar = At(this, new TitleBar(), 0, 0, 1360, 56);

            // ---- left: system + console/keys ----

            var pSystem = At(this, MakePanel("01", "SYSTEM", "接続"), 14, 64, 300, 236);
            this.button_Connect            = At(pSystem, new EvaButton { Primary = true, Text = "Connect to balance board" }, 12, 40, 276, 40);
            this.button_BluetoothAddDevice = At(pSystem, new EvaButton { Text = "Pair / remove Bluetooth device" }, 12, 88, 276, 30);
            this.zeroout                   = At(pSystem, new EvaButton { Text = "Tare balance board", Enabled = false }, 12, 124, 276, 30);
            this.button_SetCenterOffset    = At(pSystem, new EvaButton { Text = "Set current balance as center", Enabled = false }, 12, 160, 276, 30);
            this.button_ResetDefaults      = At(pSystem, new EvaButton { Text = "Load default settings and exit" }, 12, 196, 276, 30);

            var pLog = At(this, MakePanel("02", "CONSOLE / KEYS", "記録"), 14, 312, 300, 486);
            this.tabs_Log = At(pLog, new EvaTabs { Tabs = new[] { "CONSOLE", "KEY ACTIONS" } }, 12, 38, 276, 24);
            this.consoleBox = At(pLog, new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.None, BorderStyle = BorderStyle.None,
                BackColor = Eva.Bg0, ForeColor = Color.FromArgb(178, 222, 146), Font = Eva.Mono(8.5f), Text = "vJoy Console"
            }, 12, 70, 276, 404);

            this.page_Actions = At(pLog, new Panel { BackColor = Eva.Bg1, Visible = false }, 6, 66, 288, 414);
            this.tabs_Log.SelectedIndexChanged += (s, e) =>
            {
                this.consoleBox.Visible   = this.tabs_Log.SelectedIndex == 0;
                this.page_Actions.Visible = this.tabs_Log.SelectedIndex == 1;
            };

            this.checkBox_DisableActions = At(this.page_Actions, new EvaCheck { Text = "Disable all keyboard / mouse actions" }, 6, 2, 276, 24);

            var actionNames = new[] { "Left", "Right", "Forward", "Backward", "Modifier", "Jump", "Diag. left", "Diag. right" };
            var actionLabels = this.actionLabels = new Label[8];
            var actionCombos = this.actionCombos = new ComboBox[8];
            var actionAmounts = this.actionAmounts = new NumericUpDown[8];
            for (int i = 0; i < 8; i++)
            {
                var y = 32 + i * 27;
                actionLabels[i] = At(this.page_Actions, MakeLabel(actionNames[i]), 6, y + 3, 76, 20);
                actionCombos[i] = At(this.page_Actions, new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                    BackColor = Eva.Bg2, ForeColor = Eva.Text, Font = Eva.Ui(9f)
                }, 84, y, 136, 24);
                actionAmounts[i] = At(this.page_Actions, new NumericUpDown
                {
                    Minimum = -999, Maximum = 999, BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Eva.Bg2, ForeColor = Eva.Text, Font = Eva.Num(10f), TextAlign = HorizontalAlignment.Right
                }, 226, y, 56, 24);
            }
            this.label_ActionLeft = actionLabels[0];          this.comboBox_AL  = actionCombos[0]; this.numericUpDown_AL  = actionAmounts[0];
            this.label_ActionRight = actionLabels[1];         this.comboBox_AR  = actionCombos[1]; this.numericUpDown_AR  = actionAmounts[1];
            this.label_ActionForward = actionLabels[2];       this.comboBox_AF  = actionCombos[2]; this.numericUpDown_AF  = actionAmounts[2];
            this.label_ActionBackward = actionLabels[3];      this.comboBox_AB  = actionCombos[3]; this.numericUpDown_AB  = actionAmounts[3];
            this.label_ActionModifier = actionLabels[4];      this.comboBox_AM  = actionCombos[4]; this.numericUpDown_AM  = actionAmounts[4];
            this.label_ActionJump = actionLabels[5];          this.comboBox_AJ  = actionCombos[5]; this.numericUpDown_AJ  = actionAmounts[5];
            this.label_ActionDiagonalLeft = actionLabels[6];  this.comboBox_ADL = actionCombos[6]; this.numericUpDown_ADL = actionAmounts[6];
            this.label_ActionDiagonalRight = actionLabels[7]; this.comboBox_ADR = actionCombos[7]; this.numericUpDown_ADR = actionAmounts[7];

            this.label_TriggerHead = At(this.page_Actions, MakeLabel("TRIGGERS  ·  % off centre that fires an action"), 6, 252, 276, 16);
            this.label_TriggerHead.ForeColor = Eva.TextDim; this.label_TriggerHead.Font = Eva.UiBold(7.5f);
            System.Func<int, string> pct = v => v + "%";
            this.label_TLR  = At(this.page_Actions, MakeLabel("Left / right"), 6, 280, 120, 20);
            this.numericUpDown_TLR  = At(this.page_Actions, MakeSlider("", 1, 51, 1, pct), 128, 270, 154, 34);
            this.label_TFB  = At(this.page_Actions, MakeLabel("Forward / back"), 6, 316, 120, 20);
            this.numericUpDown_TFB  = At(this.page_Actions, MakeSlider("", 1, 51, 1, pct), 128, 306, 154, 34);
            this.label_TMLR = At(this.page_Actions, MakeLabel("Modifier + L / R"), 6, 352, 120, 20);
            this.numericUpDown_TMLR = At(this.page_Actions, MakeSlider("", 1, 51, 1, pct), 128, 342, 154, 34);
            this.label_TMFB = At(this.page_Actions, MakeLabel("Modifier + F / B"), 6, 388, 120, 20);
            this.numericUpDown_TMFB = At(this.page_Actions, MakeSlider("", 1, 51, 1, pct), 128, 378, 154, 34);

            // ---- middle: load cells, board, result, output ----

            this.panel_Weights = At(this, MakePanel("03", "LOAD CELLS", "荷重"), 326, 64, 768, 128);
            this.panel_Weights.Paint += panel_Weights_Paint;
            System.Func<float, float, EvaReadout> Cell = (col, row) =>
                At(this.panel_Weights, new EvaReadout { ValuePt = 13f }, 116 + col * 127 + 10, 50 + row * 24, 108, 24);
            this.label_rwTL = Cell(0, 0); this.label_rwTR = Cell(1, 0); this.label_rwBL = Cell(2, 0); this.label_rwBR = Cell(3, 0); this.label_rwWT = Cell(4, 0);
            this.label_owTL = Cell(0, 1); this.label_owTR = Cell(1, 1); this.label_owBL = Cell(2, 1); this.label_owBR = Cell(3, 1); this.label_owWT = Cell(4, 1);
            this.label_owrTL = Cell(0, 2); this.label_owrTR = Cell(1, 2); this.label_owrBL = Cell(2, 2); this.label_owrBR = Cell(3, 2);
            foreach (var l in new Label[] { label_rwTL, label_rwTR, label_rwBL, label_rwBR, label_rwWT, label_owTL, label_owTR, label_owBL, label_owBR, label_owWT, label_owrTL, label_owrTR, label_owrBL, label_owrBR })
                l.Text = "--.-";
            this.label_rwWT.ValueColor = Eva.Amber; this.label_owWT.ValueColor = Eva.Amber;
            foreach (var l in new[] { label_owrTL, label_owrTR, label_owrBL, label_owrBR }) l.ValueColor = Eva.Green;

            // Balance ratios are shown on the board itself; these labels just keep the values.
            this.label_brX = new EvaReadout(); this.label_brY = new EvaReadout();
            this.label_brDL = new EvaReadout(); this.label_brDR = new EvaReadout(); this.label_brDF = new EvaReadout();

            this.boardView = At(this, new BoardView(), 326, 204, 590, 406);
            this.label_Status = At(this, new EvaStatus { Text = "Result:" }, 326, 620, 590, 36);

            var pOut = At(this, MakePanel("06", "OUTPUT VECTOR", "出力"), 326, 666, 590, 132);
            this.outputScope = At(pOut, new OutputScope(), 10, 36, 570, 90);

            var pStart = At(this, MakePanel("04", "STARTUP", "起動"), 928, 204, 166, 166);
            this.startupOptions = pStart;
            this.checkBox_StartupAutoConnect = At(pStart, new EvaCheck { Text = "Connect on launch" }, 10, 38, 150, 36);
            this.checkBox_AutoTare           = At(pStart, new EvaCheck { Text = "Tare after connecting" }, 10, 78, 150, 36);
            this.checkBox_StartMinimized     = At(pStart, new EvaCheck { Text = "Start minimized" }, 10, 118, 150, 36);

            var pSensors = At(this, MakePanel("05", "SENSORS", "感圧"), 928, 380, 166, 418);
            this.sensorBars = At(pSensors, new SensorBars(), 8, 36, 150, 374);

            // ---- right: the settings that matter ----

            this.groupBox_vJoy_output = At(this, MakePanel("07", "OUTPUT", "出力経路"), 1106, 64, 240, 220);
            this.checkBox_EnableJoystick      = At(this.groupBox_vJoy_output, new EvaCheck { Text = "vJoy output" }, 10, 38, 222, 22);
            this.checkBox_SendCGtoXY          = At(this.groupBox_vJoy_output, new EvaCheck { Text = "Lean → X / Y axes", Checked = true }, 10, 62, 222, 22);
            this.checkBox_Send4LoadSensors    = At(this.groupBox_vJoy_output, new EvaCheck { Text = "Load cells → Z / RX / RY / RZ" }, 10, 86, 222, 22);
            this.checkBox_ShowValuesInConsole = At(this.groupBox_vJoy_output, new EvaCheck { Text = "Echo values to console" }, 10, 110, 222, 22);
            this.VJoyIDUpDown                 = At(this.groupBox_vJoy_output, MakeSlider("vJoy device", 1, 16, 1, v => "#" + v), 10, 136, 220, 36);
            this.checkBox_SteamVR             = At(this.groupBox_vJoy_output, new EvaCheck { Text = "SteamVR treadmill driver" }, 10, 182, 222, 30);

            this.groupBox_Lean = At(this, MakePanel("08", "LEAN", "傾斜"), 1106, 294, 240, 120);
            this.trackBar_LeanSensitivity = At(this.groupBox_Lean, MakeSlider("Sensitivity", 5, 50, 20, v => (v / 10.0).ToString("0.0") + "x"), 10, 38, 220, 36);
            this.trackBar_LeanDeadzone    = At(this.groupBox_Lean, MakeSlider("Deadzone", 0, 20, 0, pct), 10, 78, 220, 36);

            this.groupBox_Walk = At(this, MakePanel("09", "WALK IN PLACE", "歩行"), 1106, 424, 240, 168);
            this.groupBox_Walk.Accent = Eva.Green;
            this.checkBox_WalkInPlace   = At(this.groupBox_Walk, new EvaCheck { Text = "Step in place to walk" }, 10, 36, 222, 22);
            this.numericUpDown_WalkStep  = At(this.groupBox_Walk, MakeSlider("Step threshold", 5, 45, 20, pct), 10, 62, 220, 36);
            this.numericUpDown_WalkSpeed = At(this.groupBox_Walk, MakeSlider("Top speed", 10, 100, 100, pct), 10, 102, 220, 36);
            this.numericUpDown_WalkStep.Accent = Eva.Green; this.numericUpDown_WalkSpeed.Accent = Eva.Green;
            this.label_Walk = At(this.groupBox_Walk, new EvaReadout { Plain = true, ValuePt = 8.5f, ValueColor = Eva.Green, Text = "Steps: -  Walk: -" }, 10, 142, 220, 20);

            this.groupBox_Twist = At(this, MakePanel("10", "TWIST TO TURN", "旋回"), 1106, 602, 240, 196);
            this.groupBox_Twist.Accent = Eva.Teal;
            this.checkBox_SendTwistToRX      = At(this.groupBox_Twist, new EvaCheck { Text = "Twist turns (Z axis)" }, 10, 36, 222, 22);
            this.numericUpDown_TwistDeadzone = At(this.groupBox_Twist, MakeSlider("Deadzone", 0, 49, 4, pct), 10, 60, 220, 34);
            this.numericUpDown_TwistFull     = At(this.groupBox_Twist, MakeSlider("Full turn at", 1, 50, 15, pct), 10, 96, 220, 34);
            this.trackBar_TwistSensitivity   = At(this.groupBox_Twist, MakeSlider("Sensitivity", 5, 50, 20, v => (v / 10.0).ToString("0.0") + "x"), 10, 132, 220, 34);
            foreach (var s in new[] { numericUpDown_TwistDeadzone, numericUpDown_TwistFull, trackBar_TwistSensitivity }) s.Accent = Eva.Teal;
            this.label_Twist = At(this.groupBox_Twist, new EvaReadout { Plain = true, ValuePt = 8.5f, ValueColor = Eva.Teal, Text = "Twist: -  Turn: -" }, 10, 170, 220, 20);

            // Old caption labels, kept for the code that still sets their text.
            this.label_TwistSensitivity = new Label(); this.label_LeanSensitivity = new Label(); this.label_LeanDeadzone = new Label();

            // ---- tooltips ----

            this.toolTip1.SetToolTip(this.zeroout, "Press this button while no weight is on the balance board, to reset all current raw weight values to zero");
            this.toolTip1.SetToolTip(this.button_SetCenterOffset, "While standing or sitting on the balance board, click this button to set your current balance point as center");
            this.toolTip1.SetToolTip(this.checkBox_SendCGtoXY, "Send your center of gravity (balance point) as an X/Y input, to the virtual joystick device.");
            this.toolTip1.SetToolTip(this.checkBox_EnableJoystick, "Sends the data to vJoy driver");
            this.toolTip1.SetToolTip(this.checkBox_Send4LoadSensors, resources.GetString("checkBox_Send4LoadSensors.ToolTip"));
            this.toolTip1.SetToolTip(this.checkBox_ShowValuesInConsole, "print the measured values to the vJoy Console");
            this.toolTip1.SetToolTip(this.VJoyIDUpDown, "Normally leave this on 1. Unless you have several virtual joystick devices configured.");
            this.toolTip1.SetToolTip(this.trackBar_LeanSensitivity, "How far you have to lean for full speed. Higher = less lean needed. 2.0x is the original WiiBalanceWalker scaling.");
            this.toolTip1.SetToolTip(this.trackBar_LeanDeadzone, "Leans smaller than this (% of the way to the board edge) are ignored, so standing still doesn't drift.");
            this.toolTip1.SetToolTip(this.checkBox_SteamVR, "Sends lean (with walk-in-place applied), twist and the A button to the wbw SteamVR driver on UDP 127.0.0.1:26790.\nWorks with or without vJoy output. Bind the \"Wii Balance Board\" treadmill to Move in the game's SteamVR controller bindings.");
            this.toolTip1.SetToolTip(this.checkBox_SendTwistToRX, "Hoverboard-style turning: weight two diagonally opposite corners to turn.\nFront-right + back-left = turn left, front-left + back-right = turn right.\nSent on the Z axis, which browsers read as right stick X. RX/RY/RZ are held centered. Replaces the load sensor values on Z/RX/RY/RZ.");
            this.toolTip1.SetToolTip(this.numericUpDown_TwistDeadzone, "Twist (in %) ignored before turning starts.");
            this.toolTip1.SetToolTip(this.numericUpDown_TwistFull, "Twist (in %) that gives a full-speed turn.");
            this.toolTip1.SetToolTip(this.trackBar_TwistSensitivity, "Multiplies the measured twist before Dead/Full are applied. Raise it if turning takes too much effort.");
            this.toolTip1.SetToolTip(this.checkBox_WalkInPlace, "Step from foot to foot to walk forward; faster steps walk faster.\nLeft/right lean is held centered while walking so the sway doesn't strafe.");
            this.toolTip1.SetToolTip(this.numericUpDown_WalkStep, "How far past center (in %) your weight must swing onto one foot to count as a step. 20 = 70% on one foot.");
            this.toolTip1.SetToolTip(this.numericUpDown_WalkSpeed, "Forward speed (in %) at a brisk walking-in-place pace.");
            this.toolTip1.SetToolTip(this.checkBox_StartupAutoConnect, "To save you from clicking 'Connect to Wii Balance Board' each time you launch VR Balance Walker");

            // ---- events ----

            this.button_Connect.Click += new System.EventHandler(this.button_Connect_Click);
            this.zeroout.Click += new System.EventHandler(this.zeroout_Click);
            this.button_SetCenterOffset.Click += new System.EventHandler(this.button_SetCenterOffset_Click);
            this.button_ResetDefaults.Click += new System.EventHandler(this.button_ResetDefaults_Click);
            this.button_BluetoothAddDevice.Click += new System.EventHandler(this.button_BluetoothAddDevice_Click);
            this.checkBox_SendCGtoXY.CheckedChanged += new System.EventHandler(this.checkBox_SendCGtoXY_CheckedChanged);
            this.numericUpDown_TMFB.ValueChanged += new System.EventHandler(this.numericUpDown_TMFB_ValueChanged);
            this.numericUpDown_TMLR.ValueChanged += new System.EventHandler(this.numericUpDown_TMLR_ValueChanged);
            this.numericUpDown_TFB.ValueChanged += new System.EventHandler(this.numericUpDown_TFB_ValueChanged);
            this.numericUpDown_TLR.ValueChanged += new System.EventHandler(this.numericUpDown_TLR_ValueChanged);
            this.checkBox_DisableActions.CheckedChanged += new System.EventHandler(this.checkBox_DisableActions_CheckedChanged);
            this.checkBox_EnableJoystick.CheckedChanged += new System.EventHandler(this.checkBox_EnableJoystick_CheckedChanged);
            this.checkBox_Send4LoadSensors.CheckedChanged += new System.EventHandler(this.checkBox_Send4LoadSensors_CheckedChanged);
            this.checkBox_ShowValuesInConsole.CheckedChanged += new System.EventHandler(this.ShowValues_CheckedChanged);
            this.trackBar_LeanSensitivity.ValueChanged += new System.EventHandler(this.trackBar_LeanSensitivity_ValueChanged);
            this.trackBar_LeanDeadzone.ValueChanged += new System.EventHandler(this.trackBar_LeanDeadzone_ValueChanged);
            this.checkBox_SteamVR.CheckedChanged += new System.EventHandler(this.checkBox_SteamVR_CheckedChanged);
            this.checkBox_SendTwistToRX.CheckedChanged += new System.EventHandler(this.checkBox_SendTwistToRX_CheckedChanged);
            this.numericUpDown_TwistDeadzone.ValueChanged += new System.EventHandler(this.numericUpDown_TwistDeadzone_ValueChanged);
            this.numericUpDown_TwistFull.ValueChanged += new System.EventHandler(this.numericUpDown_TwistFull_ValueChanged);
            this.trackBar_TwistSensitivity.ValueChanged += new System.EventHandler(this.trackBar_TwistSensitivity_ValueChanged);
            this.checkBox_WalkInPlace.CheckedChanged += new System.EventHandler(this.checkBox_WalkInPlace_CheckedChanged);
            this.numericUpDown_WalkStep.ValueChanged += new System.EventHandler(this.numericUpDown_WalkStep_ValueChanged);
            this.numericUpDown_WalkSpeed.ValueChanged += new System.EventHandler(this.numericUpDown_WalkSpeed_ValueChanged);
            this.checkBox_StartupAutoConnect.CheckedChanged += new System.EventHandler(this.checkBox_StartupAutoConnect_CheckedChanged);
            this.checkBox_StartMinimized.CheckedChanged += new System.EventHandler(this.checkBox_StartMinimized_CheckedChanged);
            this.checkBox_AutoTare.CheckedChanged += new System.EventHandler(this.checkBox_AutoTare_CheckedChanged);

            // ---- form ----

            this.AutoScaleMode = AutoScaleMode.None;
            this.BackColor = Eva.Bg0;
            this.ClientSize = new Size(Eva.P(1360), Eva.P(812));
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "VR Balance Walker";
            this.FormClosing += new FormClosingEventHandler(this.FormMain_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.ResumeLayout(false);
        }

        private TitleBar titleBar;
        private BoardView boardView;
        private SensorBars sensorBars;
        private OutputScope outputScope;
        private EvaTabs tabs_Log;
        private Panel page_Actions;
        private EvaPanel panel_Weights;
        private Label label_TriggerHead;
        private Label[] actionLabels;           // the Key Actions rows, also shown in the Debloated theme's Actions box
        private ComboBox[] actionCombos;
        private NumericUpDown[] actionAmounts;

        private EvaReadout label_rwWT;
        private EvaButton button_Connect;
        private EvaReadout label_brX;
        private EvaReadout label_brY;
        private EvaReadout label_brDL;
        private EvaReadout label_brDR;
        private EvaReadout label_rwBR;
        private EvaReadout label_rwBL;
        private EvaReadout label_rwTR;
        private EvaReadout label_rwTL;
        private EvaReadout label_owTL;
        private EvaReadout label_owTR;
        private EvaReadout label_owBL;
        private EvaReadout label_owBR;
        private EvaReadout label_owrTL;
        private EvaReadout label_owrTR;
        private EvaReadout label_owrBL;
        private EvaReadout label_owrBR;
        private EvaReadout label_brDF;
        private EvaSlider numericUpDown_TMFB;
        private EvaSlider numericUpDown_TMLR;
        private EvaSlider numericUpDown_TFB;
        private EvaSlider numericUpDown_TLR;
        private Label label_TMFB;
        private Label label_TMLR;
        private Label label_TFB;
        private Label label_TLR;
        private EvaStatus label_Status;
        private EvaButton button_BluetoothAddDevice;
        private ComboBox comboBox_AM;
        private ComboBox comboBox_AF;
        private ComboBox comboBox_AB;
        private ComboBox comboBox_AR;
        private ComboBox comboBox_AL;
        private Label label_ActionJump;
        private Label label_ActionDiagonalRight;
        private Label label_ActionDiagonalLeft;
        private Label label_ActionModifier;
        private Label label_ActionBackward;
        private Label label_ActionForward;
        private Label label_ActionRight;
        private Label label_ActionLeft;
        private NumericUpDown numericUpDown_AR;
        private NumericUpDown numericUpDown_AL;
        private NumericUpDown numericUpDown_ADR;
        private NumericUpDown numericUpDown_ADL;
        private NumericUpDown numericUpDown_AJ;
        private NumericUpDown numericUpDown_AM;
        private NumericUpDown numericUpDown_AB;
        private NumericUpDown numericUpDown_AF;
        private ComboBox comboBox_ADR;
        private ComboBox comboBox_AJ;
        private ComboBox comboBox_ADL;
        private EvaButton button_SetCenterOffset;
        private EvaButton button_ResetDefaults;
        private EvaCheck checkBox_DisableActions;
        private EvaReadout label_owWT;
        private EvaCheck checkBox_EnableJoystick;
        private EvaCheck checkBox_SendCGtoXY;
        private EvaButton zeroout;
        private TextBox consoleBox;
        private EvaCheck checkBox_Send4LoadSensors;
        private EvaPanel groupBox_vJoy_output;
        private ToolTip toolTip1;
        private EvaSlider VJoyIDUpDown;
        private EvaPanel groupBox_Twist;
        private EvaCheck checkBox_SendTwistToRX;
        private EvaCheck checkBox_SteamVR;
        private EvaPanel groupBox_Lean;
        private Label label_LeanSensitivity;
        private EvaSlider trackBar_LeanSensitivity;
        private Label label_LeanDeadzone;
        private EvaSlider trackBar_LeanDeadzone;
        private EvaSlider numericUpDown_TwistDeadzone;
        private EvaSlider numericUpDown_TwistFull;
        private EvaReadout label_Twist;
        private EvaPanel groupBox_Walk;
        private EvaCheck checkBox_WalkInPlace;
        private EvaSlider numericUpDown_WalkStep;
        private EvaSlider numericUpDown_WalkSpeed;
        private EvaReadout label_Walk;
        private Label label_TwistSensitivity;
        private EvaSlider trackBar_TwistSensitivity;
        private EvaCheck checkBox_ShowValuesInConsole;
        private EvaPanel startupOptions;
        private EvaCheck checkBox_StartupAutoConnect;
        private EvaCheck checkBox_AutoTare;
        private EvaCheck checkBox_StartMinimized;
    }
}
