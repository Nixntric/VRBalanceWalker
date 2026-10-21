//----------------------------------------------------------------------------------------------------------------------+
// WiiBalanceWalker v0.5, by Shachar Liberman
// Originally Released by Richard Perry from GreyCube.com - Under the Microsoft Public License.
//
// released for windows 10 x64 systems, x86 should be supported too.
//
// Uses lshachar's WiimoteLib DLL:                  https://github.com/lshachar/WiimoteLib
// Uses the 32Feet.NET bluetooth DLL:               http://32feet.codeplex.com/
// Uses vJoy device driver (by Shaul Eizikovich):   http://vjoystick.sourceforge.net/site/index.php/download-a-install/download
// (Previous to WiiBalanceWalker v0.5
//  VJoy by headsoft was used)                      http://headsoft.com.au/index.php?category=vjoy
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Forms;
using WiimoteLib;

namespace WiiBalanceWalker
{
    public partial class FormMain : Form, IAeroHost
    {
        System.Timers.Timer infoUpdateTimer = new System.Timers.Timer() { Interval = 50,     Enabled = false };
        System.Timers.Timer joyResetTimer   = new System.Timers.Timer() { Interval = 240000, Enabled = false };

        ActionList actionList = new ActionList();
        SteamVRFeeder steamVR = new SteamVRFeeder();
        GameFeed      gameFeed = new GameFeed();
        Wiimote wiiDevice     = new Wiimote();
        DateTime jumpTime     = DateTime.UtcNow;
        double   turnSmoothed = 0;
        int      walkSide     = 0;                  // which foot holds the weight: -1 left, 1 right, 0 not yet seen
        DateTime walkLastStep = DateTime.MinValue;
        double   walkInterval = 0;                  // seconds between the last two alternating steps
        double   walkSmoothed = 0;

        // --demo feeds a scripted rider instead of the board (nothing is sent to vJoy or SteamVR);
        // --shot <file.png> <ms,ms,...> saves the window at those times after start, then exits;
        // --themes <i,i,...> switches to that theme shortly before each shot (not saved), to check live theme changes.
        public static bool demoMode;
        public static string shotPath;
        public static int[] shotTimes = { 1500 };
        public static int[] shotThemes;
        readonly DateTime demoStart = DateTime.UtcNow;

        bool setCenterOffset = false;
        bool resetCenterOffsetPossible = false;

        float naCorners     = 0f;
        float oaTopLeft     = 0f;
        float oaTopRight    = 0f;
        float oaBottomLeft  = 0f;
        float oaBottomRight = 0f;

        public FormMain()
        {
            InitializeComponent();
            DoubleBuffered = true;
            BuildClassicView();
            BuildClassicMenu();
            // The theme button goes Aero -> EVA -> Debloated; Debloated (and Retro) pick themes from the menu bar.
            titleBar.StyleClicked += (s, e) => SwitchTheme(Eva.Aero ? EvaUnit : Eva.ClassicIndex, true);
            titleBar.UnitClicked += (s, e) =>
            {
                var unit = (Eva.ThemeIndex + 1) % Eva.AeroIndex;
                Properties.Settings.Default.EvaUnit = unit;
                SwitchTheme(unit, true);
            };
            ApplyStyleExtras();
        }

        // ---- themes ------------------------------------------------------------------------------------------------

        public void SwitchTheme(int index, bool save)
        {
            ShareWithClassicView(false);    // shared controls go home first, so the retint reaches them
            Eva.SetTheme(index, this);
            ApplyStyleExtras();
            if (!save) return;
            Properties.Settings.Default.Theme = Eva.ThemeIndex;
            Properties.Settings.Default.Save();
        }

        static int EvaUnit { get { return Math.Max(0, Math.Min(Eva.AeroIndex - 1, Properties.Settings.Default.EvaUnit)); } }

        // Menu bar of the Debloated and Retro themes (the stock layout has no title bar of its own): File > Exit, and
        // Theme > every theme, Retro only once it has been unlocked with the board code.
        MenuStrip classicMenu;
        ToolStripMenuItem[] themeItems;

        void BuildClassicMenu()
        {
            classicMenu = new MenuStrip { RenderMode = ToolStripRenderMode.System, Visible = false };
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (s, e) => Close()));
            var theme = new ToolStripMenuItem("&Theme");
            string[] labels = { "&Evangelion Unit-01", "Evangelion Unit-0&2", "Evangelion Unit-0&0", "Frutiger &Aero", "&Debloated", "&Retro (Windows Classic)" };
            themeItems = new ToolStripMenuItem[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var index = i;
                themeItems[i] = new ToolStripMenuItem(labels[i], null, (s, e) =>
                {
                    if (index < Eva.AeroIndex) Properties.Settings.Default.EvaUnit = index;
                    SwitchTheme(index, true);
                });
            }
            theme.DropDownItems.Add(themeItems[Eva.AeroIndex]);
            theme.DropDownItems.Add(new ToolStripSeparator());
            for (int i = 0; i < Eva.AeroIndex; i++) theme.DropDownItems.Add(themeItems[i]);
            theme.DropDownItems.Add(new ToolStripSeparator());
            theme.DropDownItems.Add(themeItems[Eva.ClassicIndex]);
            theme.DropDownItems.Add(themeItems[Eva.RetroIndex]);
            themeItems[Eva.RetroIndex].Visible = Properties.Settings.Default.RetroUnlocked;
            classicMenu.Items.Add(file);
            classicMenu.Items.Add(theme);
            Controls.Add(classicMenu);
            MainMenuStrip = classicMenu;
        }

        // The board code (SecretCode) was done: Retro is unlocked for good, with a ding and a switch straight to it.
        readonly SecretCode secretCode = new SecretCode();

        void UnlockRetro()
        {
            Properties.Settings.Default.RetroUnlocked = true;
            themeItems[Eva.RetroIndex].Visible = true;
            System.Media.SystemSounds.Exclamation.Play();
            consoleBoxWriteLine("*** Retro theme unlocked! (Theme > Retro) ***");
            SwitchTheme(Eva.RetroIndex, true);     // saves the settings too
        }

        // What the palette retint can't do: plain WinForms controls that go see-through or change look per skin, the
        // Debloated theme's swap to the original layout (normal window frame, menu bar), and the corners (rounded by DWM
        // in Aero).
        void ApplyStyleExtras()
        {
            var aero = Eva.Aero;
            var classic = Eva.Classic;

            SuspendLayout();
            FormBorderStyle = classic ? FormBorderStyle.FixedSingle : FormBorderStyle.None;
            MaximizeBox = false;
            foreach (Control c in Controls) if (c != classicView && c != classicMenu) c.Visible = !classic;
            classicMenu.Visible = classic;
            classicView.Visible = classic;
            ShareWithClassicView(classic);
            var menuH = classicMenu.GetPreferredSize(System.Drawing.Size.Empty).Height;     // .Height is stale until first shown
            classicMenu.Height = menuH;
            classicView.Top = menuH;
            ClientSize = classic ? new System.Drawing.Size(classicView.Width, menuH + classicView.Height) : new System.Drawing.Size(Eva.P(1360), Eva.P(812));
            for (int i = 0; i < themeItems.Length; i++) themeItems[i].Checked = i == Eva.ThemeIndex;
            ResumeLayout();

            var stock = Control.DefaultFont;
            consoleBox.BackColor = classic ? Eva.Bg0 : aero ? System.Drawing.Color.White : Eva.Bg0;
            consoleBox.ForeColor = classic ? Eva.Text : aero ? System.Drawing.Color.FromArgb(24, 70, 120) : System.Drawing.Color.FromArgb(178, 222, 146);
            consoleBox.BorderStyle = classic ? BorderStyle.Fixed3D : BorderStyle.None;
            consoleBox.ScrollBars = classic ? ScrollBars.Vertical : ScrollBars.None;
            consoleBox.Font = classic ? stock : Eva.Mono(8.5f);
            page_Actions.BackColor = aero ? System.Drawing.Color.Transparent : Eva.Bg1;
            foreach (Control c in page_Actions.Controls)
                if (c.GetType() == typeof(Label)) { c.BackColor = aero ? System.Drawing.Color.Transparent : Eva.Bg1; c.Font = aero ? Aero.Ui(9f) : Eva.Ui(9.5f); }
            foreach (var combo in actionCombos) { combo.FlatStyle = classic ? FlatStyle.Standard : FlatStyle.Flat; combo.Font = classic ? stock : Eva.Ui(9f); }
            foreach (var num in actionAmounts)
            {
                num.BorderStyle = classic ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
                num.Font = classic ? stock : Eva.Num(10f);
                num.TextAlign = classic ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            }
            label_TriggerHead.Text = aero ? "Triggers \u00b7 % off centre that fires an action" : "TRIGGERS  \u00b7  % off centre that fires an action";
            label_TriggerHead.Font = aero ? Aero.Semi(7.5f) : Eva.UiBold(7.5f);
            toolTip1.OwnerDraw = !classic;
            ApplyRetro(Eva.Retro);
            ApplyCorners();
            Invalidate(true);
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        void ApplyCorners()
        {
            if (!IsHandleCreated) return;
            try { int pref = Eva.Aero ? 2 : Eva.Classic && !Eva.Retro ? 0 : 1; DwmSetWindowAttribute(Handle, 33, ref pref, 4); }    // DWMWA_WINDOW_CORNER_PREFERENCE: round / system default / don't
            catch { }   // before Windows 11
            try
            {
                // Retro: DWM stops drawing the frame (DWMWA_NCRENDERING_POLICY = disabled), so with visual styles off
                // Windows paints the old classic caption and border itself.
                int policy = Eva.Retro ? 1 : 0;
                DwmSetWindowAttribute(Handle, 2, ref policy, 4);
                SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);     // NOSIZE|NOMOVE|NOZORDER|NOACTIVATE|FRAMECHANGED
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);

        static readonly System.Drawing.Color RetroGrey = System.Drawing.Color.FromArgb(212, 208, 200);

        // Retro switches the whole program's visual styles off (Windows Classic rendering) and repaints the stock
        // layout in the old grey; buttons need their colour set themselves to draw classic bevels in it.
        void ApplyRetro(bool retro)
        {
            var state = retro ? System.Windows.Forms.VisualStyles.VisualStyleState.NoneEnabled : System.Windows.Forms.VisualStyles.VisualStyleState.ClientAndNonClientAreasEnabled;
            if (Application.VisualStyleState != state) Application.VisualStyleState = state;
            classicView.BackColor = retro ? RetroGrey : System.Drawing.SystemColors.Control;
            classicMenu.BackColor = retro ? RetroGrey : System.Drawing.SystemColors.Control;
            Action<Control> walk = null;
            walk = parent =>
            {
                foreach (Control c in parent.Controls)
                {
                    var b = c as Button;
                    if (b != null)
                    {
                        if (retro) { b.UseVisualStyleBackColor = false; b.BackColor = RetroGrey; }
                        else { b.ResetBackColor(); b.UseVisualStyleBackColor = true; }
                    }
                    else if (c is GroupBox || c is CheckBox || c is Label || c is TrackBar) c.ResetBackColor();
                    // Classic check boxes measure wider than themed ones; auto-sized controls only re-measure when told.
                    if (c.AutoSize && (c is CheckBox || c is Label)) { c.AutoSize = false; c.AutoSize = true; }
                    walk(c);
                }
            };
            walk(classicView);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCorners();
        }

        // Aero window backdrop (sky, hills, panel shadows), shared with every child through IAeroHost.
        System.Drawing.Bitmap aeroBackdrop;
        int aeroBackdropGen = -1;

        public void PaintAeroBackground(System.Drawing.Graphics g, System.Drawing.Rectangle r)
        {
            var size = ClientSize;
            if (aeroBackdrop == null || aeroBackdropGen != Aero.Gen || aeroBackdrop.Width != Math.Max(1, size.Width) || aeroBackdrop.Height != Math.Max(1, size.Height))
            {
                if (aeroBackdrop != null) aeroBackdrop.Dispose();
                var shadows = new System.Collections.Generic.List<System.Drawing.Rectangle>();
                foreach (Control c in Controls) if (c is EvaPanel || c is BoardView) shadows.Add(c.Bounds);
                aeroBackdrop = Aero.BuildBackdrop(size.Width, size.Height, shadows);
                aeroBackdropGen = Aero.Gen;
            }
            Aero.Blit(g, aeroBackdrop, r);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!Eva.Aero) base.OnPaintBackground(e);
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // Setup a timer which controls the rate at which updates are processed.

            infoUpdateTimer.Elapsed += new ElapsedEventHandler(infoUpdateTimer_Elapsed);
            gameFeed.Start();

            // Load trigger settings.

            numericUpDown_TLR.Value  = Properties.Settings.Default.TriggerLeftRight;
            numericUpDown_TFB.Value  = Properties.Settings.Default.TriggerForwardBackward;
            numericUpDown_TMLR.Value = Properties.Settings.Default.TriggerModifierLeftRight;
            numericUpDown_TMFB.Value = Properties.Settings.Default.TriggerModifierForwardBackward;

            // Link up form controls with action settings.

            actionList.Left          = new ActionItem("Left",          comboBox_AL,  numericUpDown_AL);
            actionList.Right         = new ActionItem("Right",         comboBox_AR,  numericUpDown_AR);
            actionList.Forward       = new ActionItem("Forward",       comboBox_AF,  numericUpDown_AF);
            actionList.Backward      = new ActionItem("Backward",      comboBox_AB,  numericUpDown_AB);
            actionList.Modifier      = new ActionItem("Modifier",      comboBox_AM,  numericUpDown_AM);
            actionList.Jump          = new ActionItem("Jump",          comboBox_AJ,  numericUpDown_AJ);
            actionList.DiagonalLeft  = new ActionItem("DiagonalLeft",  comboBox_ADL, numericUpDown_ADL);
            actionList.DiagonalRight = new ActionItem("DiagonalRight", comboBox_ADR, numericUpDown_ADR);

            // Load saved preference.

            checkBox_SendCGtoXY.Checked = Properties.Settings.Default.SendCGtoXY;
            checkBox_Send4LoadSensors.Checked = Properties.Settings.Default.Send4LoadSensors;
            checkBox_SendTwistToRX.Checked = Properties.Settings.Default.SendTwistToRX;
            numericUpDown_TwistDeadzone.Value = Properties.Settings.Default.TwistDeadzone;
            numericUpDown_TwistFull.Value = Properties.Settings.Default.TwistFull;
            checkBox_WalkInPlace.Checked = Properties.Settings.Default.WalkInPlace;
            checkBox_SteamVR.Checked = Properties.Settings.Default.SendToSteamVR;
            numericUpDown_WalkStep.Value = Properties.Settings.Default.WalkStepThreshold;
            numericUpDown_WalkSpeed.Value = Properties.Settings.Default.WalkSpeed;
            trackBar_TwistSensitivity.Value = Math.Max(trackBar_TwistSensitivity.Minimum, Math.Min(trackBar_TwistSensitivity.Maximum, Properties.Settings.Default.TwistSensitivity));
            label_TwistSensitivity.Text = string.Format("Sens {0:0.0}x", trackBar_TwistSensitivity.Value / 10.0);
            trackBar_LeanSensitivity.Value = Math.Max(trackBar_LeanSensitivity.Minimum, Math.Min(trackBar_LeanSensitivity.Maximum, Properties.Settings.Default.LeanSensitivity));
            trackBar_LeanDeadzone.Value = Math.Max(trackBar_LeanDeadzone.Minimum, Math.Min(trackBar_LeanDeadzone.Maximum, Properties.Settings.Default.LeanDeadzone));
            label_LeanSensitivity.Text = string.Format("Sens {0:0.0}x", trackBar_LeanSensitivity.Value / 10.0);
            label_LeanDeadzone.Text = string.Format("Dead {0}%", trackBar_LeanDeadzone.Value);
            checkBox_ShowValuesInConsole.Checked = Properties.Settings.Default.ShowValuesInConsole;
            checkBox_EnableJoystick.Checked = Properties.Settings.Default.EnableJoystick;
            checkBox_DisableActions.Checked = Properties.Settings.Default.DisableActions;
            checkBox_StartupAutoConnect.Checked = Properties.Settings.Default.StartupAutoConnect;
            checkBox_AutoTare.Checked = Properties.Settings.Default.AutoTare;
            checkBox_StartMinimized.Checked = Properties.Settings.Default.StartMinimized;

            if (demoMode)
            {
                infoUpdateTimer.Enabled = true;
            }
            else if (checkBox_StartupAutoConnect.Checked)
            { 
                button_Connect.PerformClick();
            }

            if (shotPath != null) StartScreenshots();

            if (checkBox_StartMinimized.Checked)
            {
                this.WindowState = FormWindowState.Minimized;
            }
        }

        private void numericUpDown_TLR_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerLeftRight = (int)numericUpDown_TLR.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TFB_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerForwardBackward = (int)numericUpDown_TFB.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TMLR_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerModifierLeftRight = (int)numericUpDown_TMLR.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TMFB_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerModifierForwardBackward = (int)numericUpDown_TMFB.Value;
            Properties.Settings.Default.Save();
        }

        private void button_SetCenterOffset_Click(object sender, EventArgs e)
        {
            if (resetCenterOffsetPossible && wiiDevice.WiimoteState.BalanceBoardState.WeightKg <= 5)
            {
                naCorners = 0f;
                oaTopLeft = 0f;
                oaTopRight = 0f;
                oaBottomLeft = 0f;
                oaBottomRight = 0f;
                button_SetCenterOffset.Enabled = false;
                resetCenterOffsetPossible = false;
                button_SetCenterOffset.Text = "Set current balance as center";
                //toolTip1.SetToolTip(button_SetCenterOffset, "While standing or sitting on the balance board, click this button to set your current balance point as center");
            }

            else
            { 
                setCenterOffset = true;
                //toolTip1.SetToolTip(button_SetCenterOffset, "Revert back to the original center balance point, for the X/Y controls");
            }
        }

        private void button_ResetDefaults_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reset();
            this.Close();
        }

        private void button_BluetoothAddDevice_Click(object sender, EventArgs e)
        {
            var form = new FormBluetooth();
            form.ShowDialog(this);
        }

        private void button_Connect_Click(object sender, EventArgs e)
        {
            try
            {
                // Find all connected Wii devices.

                var deviceCollection = new WiimoteCollection();
                deviceCollection.FindAllWiimotes();

                for (int i = 0; i < deviceCollection.Count; i++)
                {
                    wiiDevice = deviceCollection[i];

                    // Device type can only be found after connection, so prompt for multiple devices.

                    if (deviceCollection.Count > 1)
                    {
                        var devicePathId = new Regex("e_pid&.*?&(.*?)&").Match(wiiDevice.HIDDevicePath).Groups[1].Value.ToUpper();

                        var response = MessageBox.Show("Connect to HID " + devicePathId + " device " + (i + 1) + " of " + deviceCollection.Count + " ?", "Multiple Wii Devices Found", MessageBoxButtons.YesNoCancel);
                        if (response == DialogResult.Cancel) return;
                        if (response == DialogResult.No) continue;
                    }

                    // Setup update handlers.

                    wiiDevice.WiimoteChanged          += wiiDevice_WiimoteChanged;
                    wiiDevice.WiimoteExtensionChanged += wiiDevice_WiimoteExtensionChanged;

                    // Connect and send a request to verify it worked.

                    wiiDevice.Connect();
                    wiiDevice.SetReportType(InputReport.IRAccel, false); // FALSE = DEVICE ONLY SENDS UPDATES WHEN VALUES CHANGE!
                    wiiDevice.SetLEDs(true, false, false, false);

                    // Enable processing of updates.

                    infoUpdateTimer.Enabled = true;

                    // Prevent connect being pressed more than once.

                    button_Connect.Enabled = false;
                    button_Connect.Text = "Board connected";
                    button_BluetoothAddDevice.Enabled = false;
                    zeroout.Enabled = true;

                    if (checkBox_AutoTare.Checked)
                        zeroout.PerformClick();

                    break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void wiiDevice_WiimoteChanged(object sender, WiimoteChangedEventArgs e)
        {
            // Called every time there is a sensor update, values available using e.WiimoteState.
            // Use this for tracking and filtering rapid accelerometer and gyroscope sensor data.
            // The balance board values are basic, so can be accessed directly only when needed.
            // The game feed wants every report though (tricks are quick), so it goes out from here.

            if (!gameFeed.HasClients || e.WiimoteState.ExtensionType != ExtensionType.BalanceBoard) return;
            var bb = e.WiimoteState.BalanceBoardState;
            gameFeed.Send(bb.WeightKg, bb.SensorValuesKg.TopLeft, bb.SensorValuesKg.TopRight,
                          bb.SensorValuesKg.BottomLeft, bb.SensorValuesKg.BottomRight, e.WiimoteState.ButtonState.A);
        }

        private void wiiDevice_WiimoteExtensionChanged(object sender, WiimoteExtensionChangedEventArgs e)
        {
            // This is not needed for balance boards.
        }

        void infoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Pass event onto the form GUI thread.

            this.BeginInvoke(new Action(() => InfoUpdate()));
        }

        private void InfoUpdate()
        {
            if (!demoMode && wiiDevice.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                label_Status.Text = "DEVICE IS NOT A BALANCE BOARD...";
                return;
            }

            // Get the current sensor KG values. (no temperature / latitude correction, can't set zero point properly.)

            float rwWeight, rwTopLeft, rwTopRight, rwBottomLeft, rwBottomRight;
            bool aButton;
            if (demoMode)
            {
                DemoSensors(out rwWeight, out rwTopLeft, out rwTopRight, out rwBottomLeft, out rwBottomRight, out aButton);
                gameFeed.Send(rwWeight, rwTopLeft, rwTopRight, rwBottomLeft, rwBottomRight, aButton);
            }
            else
            {
                rwWeight      = wiiDevice.WiimoteState.BalanceBoardState.WeightKg;
                rwTopLeft     = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesKg.TopLeft;
                rwTopRight    = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesKg.TopRight;
                rwBottomLeft  = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesKg.BottomLeft;
                rwBottomRight = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesKg.BottomRight;
                aButton       = wiiDevice.WiimoteState.ButtonState.A;
            }

            // The alternative .SensorValuesRaw is meaningless in terms of actual weight. not adjusted with 0KG, 17KG and 34KG calibration data.
            
            //var rwTopLeft     = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesRaw.TopLeft     - wiiDevice.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.TopLeft;
            //var rwTopRight    = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesRaw.TopRight    - wiiDevice.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.TopRight;
            //var rwBottomLeft  = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesRaw.BottomLeft  - wiiDevice.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.BottomLeft;
            //var rwBottomRight = wiiDevice.WiimoteState.BalanceBoardState.SensorValuesRaw.BottomRight - wiiDevice.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.BottomRight;

            // Show the sensor values in kg.
            label_rwWT.Text = rwWeight.ToString("0.0");
            label_rwTL.Text = rwTopLeft.ToString("0.0");
            label_rwTR.Text = rwTopRight.ToString("0.0");
            label_rwBL.Text = rwBottomLeft.ToString("0.0");
            label_rwBR.Text = rwBottomRight.ToString("0.0");

            // Prevent negative values by tracking lowest possible value and making it a zero based offset.

            if (rwWeight > 5)
            {
                button_SetCenterOffset.Enabled = true;
                button_SetCenterOffset.Text = "Set current balance as center";
                //toolTip1.SetToolTip(button_SetCenterOffset, "While standing or sitting on the balance board, click this button to set your current balance point as center");
            }
            else
            {
                if (resetCenterOffsetPossible)
                {
                    button_SetCenterOffset.Enabled = true;
                    button_SetCenterOffset.Text = "Reset Center Offset";
                    //toolTip1.SetToolTip(button_SetCenterOffset, "Revert back to the original center balance point, for the X/Y controlsl");
                }

                else
                {
                    button_SetCenterOffset.Text = "Set current balance as center";
                    button_SetCenterOffset.Enabled = false;
                }
            }

            if (rwTopLeft     < naCorners) naCorners = rwTopLeft;
            if (rwTopRight    < naCorners) naCorners = rwTopRight;
            if (rwBottomLeft  < naCorners) naCorners = rwBottomLeft;
            if (rwBottomRight < naCorners) naCorners = rwBottomRight;

            // Negative total weight is reset to zero as jumping or lifting the board causes negative spikes, which would break 'in use' checks.

            var owWeight      = rwWeight < 0f ? 0f : rwWeight;

            var owTopLeft     = rwTopLeft     -= naCorners;
            var owTopRight    = rwTopRight    -= naCorners;
            var owBottomLeft  = rwBottomLeft  -= naCorners;
            var owBottomRight = rwBottomRight -= naCorners;

            // Get offset that would make current values the center of mass.

            if (setCenterOffset)
            {
                setCenterOffset = false;
                resetCenterOffsetPossible = true;

                var rwHighest = Math.Max(Math.Max(rwTopLeft, rwTopRight), Math.Max(rwBottomLeft, rwBottomRight));

                oaTopLeft     = rwHighest - rwTopLeft;
                oaTopRight    = rwHighest - rwTopRight;
                oaBottomLeft  = rwHighest - rwBottomLeft;
                oaBottomRight = rwHighest - rwBottomRight;
            }

            // Keep values only when board is being used, otherwise offsets and small value jitters can trigger unwanted actions.

            if (owWeight > 5f)  // minimal weight of 5kg
            {
                owTopLeft     += oaTopLeft;
                owTopRight    += oaTopRight;
                owBottomLeft  += oaBottomLeft;
                owBottomRight += oaBottomRight;
            }
            else
            {
                owTopLeft     = 0;
                owTopRight    = 0;
                owBottomLeft  = 0;
                owBottomRight = 0;
            }
            
            label_owWT.Text = owWeight.ToString("0.0");
            label_owTL.Text = owTopLeft.ToString("0.0")     + "\r\n" + oaTopLeft.ToString("0.0");
            label_owTR.Text = owTopRight.ToString("0.0")    + "\r\n" + oaTopRight.ToString("0.0");
            label_owBL.Text = owBottomLeft.ToString("0.0")  + "\r\n" + oaBottomLeft.ToString("0.0");
            label_owBR.Text = owBottomRight.ToString("0.0") + "\r\n" + oaBottomRight.ToString("0.0");

            // Calculate each weight ratio.

            var owrPercentage  = 100 / (owTopLeft + owTopRight + owBottomLeft + owBottomRight);
            var owrTopLeft     = owrPercentage * owTopLeft;
            var owrTopRight    = owrPercentage * owTopRight;
            var owrBottomLeft  = owrPercentage * owBottomLeft;
            var owrBottomRight = owrPercentage * owBottomRight;

            label_owrTL.Text = owrTopLeft.ToString("0.0");
            label_owrTR.Text = owrTopRight.ToString("0.0");
            label_owrBL.Text = owrBottomLeft.ToString("0.0");
            label_owrBR.Text = owrBottomRight.ToString("0.0");

            // Calculate balance ratio.

            var brX = owrBottomRight + owrTopRight;
            var brY = owrBottomRight + owrBottomLeft;

            label_brX.Text = brX.ToString("0.0");
            label_brY.Text = brY.ToString("0.0");

            // Diagonal ratio used for turning on the spot.

            var brDL = owrPercentage * (owBottomLeft + owTopRight);
            var brDR = owrPercentage * (owBottomRight + owTopLeft);
            var brDF = Math.Abs(brDL - brDR);

            label_brDL.Text = brDL.ToString("0.0");
            label_brDR.Text = brDR.ToString("0.0");
            label_brDF.Text = brDF.ToString("0.0");

            // Convert sensor values into actions.

            var sendLeft          = false;
            var sendRight         = false;
            var sendForward       = false;
            var sendBackward      = false;
            var sendModifier      = false;
            var sendJump          = false;
            var sendDiagonalLeft  = false;
            var sendDiagonalRight = false;

            if (brX < (float)(50 - numericUpDown_TLR.Value)) sendLeft     = true;
            if (brX > (float)(50 + numericUpDown_TLR.Value)) sendRight    = true;
            if (brY < (float)(50 - numericUpDown_TFB.Value)) sendForward  = true;
            if (brY > (float)(50 + numericUpDown_TFB.Value)) sendBackward = true;

            if      (brX < (float)(50 - numericUpDown_TMLR.Value)) sendModifier = true;
            else if (brX > (float)(50 + numericUpDown_TMLR.Value)) sendModifier = true;
            else if (brY < (float)(50 - numericUpDown_TMFB.Value)) sendModifier = true;
            else if (brY > (float)(50 + numericUpDown_TMFB.Value)) sendModifier = true;

            // Detect jump but use a time limit to stop it being active while off the board.

            if (owWeight < 1f)
            {
                if (DateTime.UtcNow.Subtract(jumpTime).Seconds < 2) sendJump = true;
            }
            else
            {
                jumpTime = DateTime.UtcNow;
            }

            // Check for diagonal pressure only when no other movement actions are active.

            if (!sendLeft && !sendRight && !sendForward && !sendBackward && brDF > 15)
            {
                if (brDL > brDR) sendDiagonalLeft  = true;
                else             sendDiagonalRight = true;
            }

            // Display actions.

            label_Status.Text = "Result: ";

            if (sendForward)       label_Status.Text += "Forward";
            if (sendLeft)          label_Status.Text += "Left";
            if (sendBackward)      label_Status.Text += "Backward";
            if (sendRight)         label_Status.Text += "Right";
            if (sendModifier)      label_Status.Text += " + Modifier";
            if (sendJump)          label_Status.Text += "Jump";
            if (sendDiagonalLeft)  label_Status.Text += "Diagonal Left";
            if (sendDiagonalRight) label_Status.Text += "Diagonal Right";

            if (checkBox_DisableActions.Checked) label_Status.Text += " ( DISABLED )";

            // Hoverboard-style twist: weight on one diagonal pair of corners vs the other.
            // Front-right + back-left (brDL) turns left, front-left + back-right (brDR) turns right.
            // Leaning into a single corner also loads one diagonal, by as much as it shifts X and Y,
            // so subtract the smaller of the two leans to keep corner leans from turning.

            var twist     = brDR - 50;
            var leanX     = Math.Abs(brX - 50);
            var leanY     = Math.Abs(brY - 50);
            var twistEff  = Math.Max(0f, Math.Abs(twist) - Math.Min(leanX, leanY)) * (trackBar_TwistSensitivity.Value / 10f);
            var twistDead = (float)numericUpDown_TwistDeadzone.Value;
            var twistFull = Math.Max((float)numericUpDown_TwistFull.Value, twistDead + 1);
            var turn      = 0.0;

            if (!float.IsNaN(twistEff) && twistEff > twistDead)
            {
                turn = Math.Min(1.0, (twistEff - twistDead) / (twistFull - twistDead)) * Math.Sign(twist);
            }

            // Smooth out sensor noise so the camera turns evenly (a short exponential average, ~30 ms at the board's ~100 Hz).
            turnSmoothed += (turn - turnSmoothed) * 0.3;
            if (turn == 0 && Math.Abs(turnSmoothed) < 0.01) turnSmoothed = 0;

            if (!demoMode && !Properties.Settings.Default.RetroUnlocked && secretCode.Feed(owWeight, brX, brY, twist, DateTime.UtcNow)) UnlockRetro();

            // Walking in place: the weight swings almost wholly onto one foot, then the other. Each swing past the
            // step threshold onto the other side is a step; alternating steps within about a second of each other
            // walk forward, faster for a quicker pace, and stop about a second after the last step.

            var now      = DateTime.UtcNow;
            var stepLine = (float)numericUpDown_WalkStep.Value;
            var side     = 0;
            if (!float.IsNaN(brX) && owWeight > 5f)
            {
                if      (brX < 50 - stepLine) side = -1;
                else if (brX > 50 + stepLine) side = 1;
            }

            if (side != 0 && side != walkSide)
            {
                walkInterval = walkSide != 0 ? (now - walkLastStep).TotalSeconds : 0;
                walkLastStep = now;
                walkSide     = side;
            }

            var sinceStep = (now - walkLastStep).TotalSeconds;
            var walking   = walkInterval > 0 && walkInterval < 1.2 && sinceStep < Math.Max(0.8, walkInterval * 1.5);
            if (!walking && sinceStep > 1.2) { walkSide = 0; walkInterval = 0; }

            // Brisk (a step every 0.5 s or quicker) is full speed, easing down to 40% at a step a second.
            var walkTarget = walking ? Math.Max(0.4, Math.Min(1.0, (1.1 - walkInterval) / 0.6)) * (double)numericUpDown_WalkSpeed.Value / 100.0 : 0.0;
            walkSmoothed += (walkTarget - walkSmoothed) * 0.1;
            if (walkTarget == 0 && walkSmoothed < 0.01) walkSmoothed = 0;

            label_Walk.Text = !checkBox_WalkInPlace.Checked ? "Steps: off" : walking ? string.Format("Steps: {0:0.00}s apart  Walk: {1:0%}", walkInterval, walkSmoothed) : string.Format("Steps: -  Walk: {0:0%}", walkSmoothed);

            label_Twist.Text = float.IsNaN(twist) ? "Twist: -  Turn: -" : string.Format("Twist: {0:0.0}  Turn: {1:0%}", twist, turnSmoothed);

            // Send actions.

            if (!checkBox_DisableActions.Checked)
            {
                if (sendLeft)          actionList.Left.Start();          else actionList.Left.Stop();
                if (sendRight)         actionList.Right.Start();         else actionList.Right.Stop();
                if (sendForward)       actionList.Forward.Start();       else actionList.Forward.Stop();
                if (sendBackward)      actionList.Backward.Start();      else actionList.Backward.Stop();
                if (sendModifier)      actionList.Modifier.Start();      else actionList.Modifier.Stop();
                if (sendJump)          actionList.Jump.Start();          else actionList.Jump.Stop();
                if (sendDiagonalLeft)  actionList.DiagonalLeft.Start();  else actionList.DiagonalLeft.Stop();
                if (sendDiagonalRight) actionList.DiagonalRight.Start(); else actionList.DiagonalRight.Stop();
            }

            // Update joystick emulator and/or the SteamVR driver. The vector is always worked out so the window can show
            // it; it only goes out to vJoy / SteamVR when those are enabled (and never from the demo feed).

            double joyX = 0, joyY = 0;
            var leanSens = trackBar_LeanSensitivity.Value / 10.0;
            var leanDX   = brX - 50.0;
            var leanDY   = brY - 50.0;
            var leanDead = (double)trackBar_LeanDeadzone.Value;

            // send X/Y position of the player's Center of Gravity through vJoy (SteamVR always gets it)

            if (checkBox_SendCGtoXY.Checked || !checkBox_EnableJoystick.Checked)
            {
                //var cgF = wiiDevice.WiimoteState.BalanceBoardState.CenterOfGravity; // this is a nice function, but since I found out that wiimote library won't let me: 1.tare the balance board 2.compensate for temperature / latitude 3.has a bug with the kg values (each sensor is 4 times too big, but the overall weight is fine) then I'm not using it. It cannot give calibrated results.

                // Uses Int16 ( -32767 to +32767 ) where 0 is the center. Lean is measured from the 50% center (+-50 at the
                // board edges); a round deadzone is cut out of the middle, the rest scaled up by the sensitivity (2.0x by
                // default, because realistic usage is between the 30-70% ratio).

                var leanMag   = Math.Sqrt(leanDX * leanDX + leanDY * leanDY);
                var leanScale = leanMag > leanDead ? (leanMag - leanDead) / leanMag * leanSens / 50.0 : 0.0;

                joyX = leanDX * leanScale * 32767.0;
                joyY = leanDY * leanScale * 32767.0;
                //Console.WriteLine("joyX {0} JoyY {1}",joyX, joyY);

                // Limit values to Int16, you cannot just (cast) or Convert.ToIn16() as the value '+ - sign' may invert.

                if (joyX < short.MinValue) joyX = short.MinValue;
                if (joyY < short.MinValue) joyY = short.MinValue;

                if (joyX > short.MaxValue) joyX = short.MaxValue;
                if (joyY > short.MaxValue) joyY = short.MaxValue;

                if (Double.IsNaN(joyX)) joyX = 0.0;         // send the dead center value if not enough weight is on the board
                if (Double.IsNaN(joyY)) joyY = 0.0;
            }

            // Walking in place pushes forward (negative Y) and holds left/right centered, both while walking and
            // while the weight is on one foot, so the sway from foot to foot never strafes.

            if (checkBox_WalkInPlace.Checked)
            {
                if (walking || walkSmoothed > 0 || side != 0) joyX = 0.0;
                if (walkSmoothed > 0) joyY = Math.Min(joyY, -walkSmoothed * 32767.0);
            }

            var sendTurn = checkBox_SendTwistToRX.Checked ? turnSmoothed : 0.0;
            UpdateDisplay(owWeight, owrTopLeft, owrTopRight, owrBottomLeft, owrBottomRight, brX, brY, brDL, brDR, brDF, twist,
                          sendTurn, walkSmoothed, walkSide, aButton, leanDX * leanSens / 50.0, -leanDY * leanSens / 50.0,
                          joyX / 32767.0, -joyY / 32767.0, leanDead * leanSens / 50.0);

            if (demoMode) return;

            // SteamVR wants -1..1 with forward as +Y, the opposite of the joystick's Y.

            if (checkBox_SteamVR.Checked)
            {
                steamVR.Send(joyX / 32767.0, -joyY / 32767.0, sendTurn, aButton);
            }

            if (!checkBox_EnableJoystick.Checked) return;

            // Z/RX/RY/RZ: twist on Z with the rest centered, or the raw load sensors.
            // Browsers (Gamepad API) expose a generic HID stick in usage order X, Y, Z, RX..., so Z is
            // axes[2], the standard mapping's right stick X; RX lands on axes[3], right stick Y.
            // Unused axes are centered so games don't read them as a stick pushed to one side.

            int center = VJoyFeeder.ToAxisRange(0);
            int joyZ = center, joyXR = center, joyYR = center, joyZR = center;

            if (checkBox_SendTwistToRX.Checked)
            {
                joyZ = VJoyFeeder.ToAxisRange((int)(turnSmoothed * 32767));
            }
            else if (checkBox_Send4LoadSensors.Checked)
            {
                joyZ = (int)(rwTopLeft * 100); joyXR = (int)(rwTopRight * 100); joyYR = (int)(rwBottomLeft * 100); joyZR = (int)(rwBottomRight * 100);
            }
            string values = string.Format("X:{0,6:#####}  Y:{1,6:#####}  Z:{2,6}  XR:{3,6}  YR:{4,6}  ZR:{5,6}", joyX, joyY, joyZ, joyXR, joyYR, joyZR);
            
            if (checkBox_ShowValuesInConsole.Checked)
            {
                BalanceWalker.FormMain.consoleBoxWriteLine(values);
            }
            VJoyFeeder.Setjoystick((int)joyX, (int)joyY, joyZ, joyXR, joyYR, joyZR, aButton);
        }

        // Pushes one update into the board view, sensor meters, output scope and title bar.
        private void UpdateDisplay(float kg, float rTL, float rTR, float rBL, float rBR, float x, float y, float dl, float dr, float df,
                                   float twistRaw, double turn, double walk, int side, bool button, double inX, double inY, double outX, double outY, double deadR)
        {
            var vjoy    = checkBox_EnableJoystick.Checked && !demoMode;
            var steamvr = checkBox_SteamVR.Checked && !demoMode;
            var loaded  = kg > 5f;

            boardView.SetState(true, demoMode, rTL, rTR, rBL, rBR, x, y, dl, dr, df, kg, twistRaw, (float)turn, (float)walk, side, button, checkBox_WalkInPlace.Checked);
            sensorBars.SetValues(rTL, rTR, rBL, rBR, loaded);
            outputScope.SetOutput(loaded ? inX : 0, loaded ? inY : 0, outX, outY, turn, walk, deadR, vjoy, steamvr, true);
        }

        // Window chrome: borderless, but still minimizable from the taskbar and with a drop shadow.
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00020000 | 0x00080000;    // WS_MINIMIZEBOX | WS_SYSMENU
                cp.ClassStyle |= 0x00020000;            // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Eva.Aero) { PaintAeroBackground(e.Graphics, e.ClipRectangle); return; }
            base.OnPaint(e);
            using (var pen = new System.Drawing.Pen(Eva.Line, 1f)) e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // Column and row headings for the load cell table.
        private void panel_Weights_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            if (Eva.Aero) { PaintWeightHeadingsAero(g); return; }
            string[] cols = { "TL  ·  FRONT L", "TR  ·  FRONT R", "BL  ·  BACK L", "BR  ·  BACK R", "Σ  TOTAL" };
            string[] rows = { "RAW", "OFFSET", "RATIO" };
            string[] units = { "kg", "kg  ±adj", "%" };
            for (int c = 0; c < cols.Length; c++)
            {
                var x = Eva.F(116 + c * 127 + 10);
                Eva.Text_(g, cols[c], Eva.UiBold(7f), Eva.TextDim, x, Eva.F(35));
                using (var pen = new System.Drawing.Pen(Eva.Line, 1f)) g.DrawLine(pen, x - Eva.F(8), Eva.F(36), x - Eva.F(8), Eva.F(122));
            }
            for (int r = 0; r < rows.Length; r++)
            {
                var y = Eva.F(50 + r * 24);
                Eva.DrawTitle(g, rows[r], Eva.F(14), y + Eva.F(4), Eva.F(15), Eva.Text, 0.74f, 0.02f);
                Eva.Text_(g, units[r], Eva.Ui(7f), Eva.TextDim, Eva.F(68), y + Eva.F(8));
                using (var pen = new System.Drawing.Pen(Eva.A(Eva.Line, 140), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                    g.DrawLine(pen, Eva.F(12), y + Eva.F(24), Width - Eva.F(14), y + Eva.F(24));
            }
            Eva.TextRight(g, "—", Eva.Num(13f), Eva.TextOff, Eva.F(116 + 4 * 127 + 10 + 108), Eva.F(50 + 2 * 24 + 3));
        }

        private void PaintWeightHeadingsAero(System.Drawing.Graphics g)
        {
            string[] cols = { "Front left", "Front right", "Back left", "Back right", "Total" };
            string[] rows = { "Raw", "Offset", "Ratio" };
            string[] units = { "kg", "kg ± adj", "%" };
            using (var dark = new System.Drawing.Pen(System.Drawing.Color.FromArgb(60, 60, 100, 140), 1f))
            using (var light = new System.Drawing.Pen(Aero.Wh(190), 1f))
            {
                for (int c = 0; c < cols.Length; c++)
                {
                    var x = Eva.F(116 + c * 127 + 10);
                    Aero.Emboss(g, cols[c], Aero.Semi(8f), Eva.TextDim, x, Eva.F(33));
                    g.DrawLine(dark, x - Eva.F(8), Eva.F(38), x - Eva.F(8), Eva.F(120));
                    g.DrawLine(light, x - Eva.F(7), Eva.F(38), x - Eva.F(7), Eva.F(120));
                }
                for (int r = 0; r < rows.Length; r++)
                {
                    var y = Eva.F(50 + r * 24);
                    Aero.Emboss(g, rows[r], Aero.Semi(10.5f), Eva.Text, Eva.F(14), y + Eva.F(1.5f));
                    Aero.Emboss(g, units[r], Aero.Ui(7.5f), Eva.TextDim, Eva.F(66), y + Eva.F(6));
                    if (r < rows.Length - 1)
                    {
                        g.DrawLine(dark, Eva.F(12), y + Eva.F(24), Width - Eva.F(14), y + Eva.F(24));
                        g.DrawLine(light, Eva.F(12), y + Eva.F(25), Width - Eva.F(14), y + Eva.F(25));
                    }
                }
            }
            Eva.TextRight(g, "—", Aero.Semi(11f), Eva.TextOff, Eva.F(116 + 4 * 127 + 10 + 108), Eva.F(50 + 2 * 24 + 3));
        }

        private void checkBox_EnableJoystick_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.EnableJoystick = isChecked;
            Properties.Settings.Default.Save();

            bool status;
            if (checkBox_EnableJoystick.Checked)
            {
                VJoyFeeder.Initialize((uint)VJoyIDUpDown.Value);
                status = true;
            }
            else
            { 
                status = false;
            }
            checkBox_SendCGtoXY.Enabled = status;
            checkBox_Send4LoadSensors.Enabled = status;
            groupBox_Twist.Enabled = status || checkBox_SteamVR.Checked;
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop updates.

            infoUpdateTimer.Enabled = false;
            wiiDevice.Disconnect();

            // Prevent 'stuck' down keys from closing while doing an action.

            actionList.Left.Stop();
            actionList.Right.Stop();
            actionList.Forward.Stop();
            actionList.Backward.Stop();
            actionList.Modifier.Stop();
            actionList.Jump.Stop();
            actionList.DiagonalLeft.Stop();
            actionList.DiagonalRight.Stop();
        }

        private void zeroout_Click(object sender, EventArgs e)
        {
            wiiDevice.WiimoteState.BalanceBoardState.ZeroPoint.Reset = true;
            naCorners = 0f;
            oaTopLeft = 0f;
            oaTopRight = 0f;
            oaBottomLeft = 0f;
            oaBottomRight = 0f;
        }

        // Scripted rider for --demo: steps on, leans around, twists both ways, walks in place, steps off, repeats.
        private void DemoSensors(out float weight, out float tl, out float tr, out float bl, out float br, out bool button)
        {
            var t = (DateTime.UtcNow - demoStart).TotalSeconds % 26.0;
            double kg = 74, lx = 0, ly = 0, tw = 0;
            button = false;
            if (t < 1.5) kg = 0;                                                    // empty board
            else if (t < 7) { var u = (t - 1.5) * 1.3; lx = Math.Sin(u) * 0.18; ly = Math.Sin(u * 0.7 + 1) * 0.15; }   // lean around
            else if (t < 11) tw = Math.Sin((t - 7) * Math.PI / 2) * 0.16;           // twist left, then right
            else if (t < 16) lx = Math.Sign(Math.Sin((t - 11) * Math.PI * 2.2)) * 0.55;   // walk in place: foot to foot
            else if (t < 19) { ly = -0.22; button = t > 18.4 && t < 18.7; }          // lean forward, press A
            else if (t < 21) { lx = 0.2; ly = 0.18; }                               // back-right corner
            else if (t < 23) kg = 74;                                               // stand still
            else kg = 0;                                                            // step off (jump)

            // Split the weight over the corners: lx/ly are the shift towards right/back, tw loads the TL+BR diagonal.
            var w = kg / 4.0;
            tl = (float)(w * (1 - lx) * (1 - ly) * (1 + tw * 2));
            tr = (float)(w * (1 + lx) * (1 - ly) * (1 - tw * 2));
            bl = (float)(w * (1 - lx) * (1 + ly) * (1 - tw * 2));
            br = (float)(w * (1 + lx) * (1 + ly) * (1 + tw * 2));
            weight = tl + tr + bl + br;
        }

        private void StartScreenshots()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(-30000, -30000);
            var shown = DateTime.UtcNow;
            var next = 0;
            var switched = -1;
            var timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.UtcNow - shown).TotalMilliseconds;
                if (shotThemes != null && next < shotThemes.Length && switched != next && elapsed >= shotTimes[next] - 800)
                {
                    SwitchTheme(shotThemes[next], false);
                    WindowState = FormWindowState.Normal;
                    Location = new System.Drawing.Point(-30000, -30000);
                    switched = next;
                }
                if (elapsed < shotTimes[next]) return;
                using (var bmp = new System.Drawing.Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, Width, Height));
                    var path = shotTimes.Length == 1 ? shotPath : System.IO.Path.ChangeExtension(shotPath, null) + "_" + next + ".png";
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                if (++next >= shotTimes.Length) { timer.Stop(); Close(); }
            };
            timer.Start();
        }

        public void consoleBoxWriteLine(string line)
        {
            consoleBox.AppendText(line);
            consoleBox.AppendText(Environment.NewLine);
        }

        private void checkBox_DisableActions_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.DisableActions = isChecked;
            Properties.Settings.Default.Save();

            bool status;
            if (checkBox_DisableActions.Checked)
                status = false;
            else
                status = true;

            label_ActionLeft.Enabled = status;
            label_ActionRight.Enabled = status;
            label_ActionForward.Enabled = status;
            label_ActionBackward.Enabled = status;
            label_ActionModifier.Enabled = status;
            label_ActionJump.Enabled = status;
            label_ActionDiagonalLeft.Enabled = status;
            label_ActionDiagonalRight.Enabled = status;
            label_TLR.Enabled = status;
            label_TFB.Enabled = status;
            label_TMLR.Enabled = status;
            label_TMFB.Enabled = status;
        }

        private void ShowValues_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.ShowValuesInConsole = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_SendCGtoXY_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.SendCGtoXY = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_Send4LoadSensors_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.Send4LoadSensors = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_SendTwistToRX_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.SendTwistToRX = isChecked;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TwistDeadzone_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TwistDeadzone = (int)numericUpDown_TwistDeadzone.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TwistFull_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TwistFull = (int)numericUpDown_TwistFull.Value;
            Properties.Settings.Default.Save();
        }

        private void trackBar_TwistSensitivity_ValueChanged(object sender, EventArgs e)
        {
            label_TwistSensitivity.Text = string.Format("Sens {0:0.0}x", trackBar_TwistSensitivity.Value / 10.0);
            Properties.Settings.Default.TwistSensitivity = trackBar_TwistSensitivity.Value;
            Properties.Settings.Default.Save();
        }

        private void trackBar_LeanSensitivity_ValueChanged(object sender, EventArgs e)
        {
            label_LeanSensitivity.Text = string.Format("Sens {0:0.0}x", trackBar_LeanSensitivity.Value / 10.0);
            Properties.Settings.Default.LeanSensitivity = trackBar_LeanSensitivity.Value;
            Properties.Settings.Default.Save();
        }

        private void trackBar_LeanDeadzone_ValueChanged(object sender, EventArgs e)
        {
            label_LeanDeadzone.Text = string.Format("Dead {0}%", trackBar_LeanDeadzone.Value);
            Properties.Settings.Default.LeanDeadzone = trackBar_LeanDeadzone.Value;
            Properties.Settings.Default.Save();
        }

        private void checkBox_SteamVR_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.SendToSteamVR = ((CheckBox)sender).Checked;
            Properties.Settings.Default.Save();
            groupBox_Twist.Enabled = checkBox_EnableJoystick.Checked || checkBox_SteamVR.Checked;
        }

        private void checkBox_WalkInPlace_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WalkInPlace = ((CheckBox)sender).Checked;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_WalkStep_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WalkStepThreshold = (int)numericUpDown_WalkStep.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_WalkSpeed_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.WalkSpeed = (int)numericUpDown_WalkSpeed.Value;
            Properties.Settings.Default.Save();
        }

        private void checkBox_StartupAutoConnect_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.StartupAutoConnect = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_AutoTare_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.AutoTare = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_StartMinimized_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.StartMinimized = isChecked;
            Properties.Settings.Default.Save();
        }
    }
}
