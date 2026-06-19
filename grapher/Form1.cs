using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using grapher.Models;
using System.IO;
using grapher.Models.Serialized;
using grapher.Models.Theming;
using grapher.Common;

namespace grapher
{
    public partial class RawAcceleration : Form
    {
        private readonly bool startInBackground;
        private readonly NotifyIcon trayIcon;
        private readonly ToolStripMenuItem recordCountsMsMenuItem;
        private readonly ToolStripMenuItem startOnWindowsStartupMenuItem;
        private readonly ToolStripMenuItem runInBackgroundMenuItem;
        private CountsMsRecordingForm countsMsRecordingForm;
        private bool updatingApplicationSettingsMenuItems;
        private bool allowClose;

        #region Constructor


        public RawAcceleration(bool startInBackground = false)
        {
            this.startInBackground = startInBackground;

            InitializeComponent();

            Version driverVersion = VersionHelper.ValidOrThrow();

            ToolStripMenuItem HelpMenuItem = new ToolStripMenuItem("&Help");

            HelpMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                    new ToolStripMenuItem("&About", null, (s, e) => {
                        using (var form = new AboutBox(driverVersion))
                        {
                            Theme.Apply(form);

                            form.ShowDialog();
                        }
                    })
            });
            
            var schemes = ColorSchemeManager.LoadSchemes().ToList();
            var themeMenuItem = new ToolStripMenuItem("&Themes");

            themeMenuItem.DropDownItemClicked += (s, e) =>
            {
                if (e.ClickedItem == null) return;

                foreach (ToolStripMenuItem item in themeMenuItem.DropDownItems)
                {
                    item.Checked = e.ClickedItem.Text == item.Text;
                }
            };

            var settings = GUISettings.MaybeLoad();

            Theme.CurrentScheme = ColorSchemeManager.GetSelected(settings, schemes);

            startOnWindowsStartupMenuItem = new ToolStripMenuItem("Start Raw Accel with Windows")
            {
                CheckOnClick = true,
                Checked = settings?.StartOnWindowsStartup ?? StartupShortcutExists()
            };

            runInBackgroundMenuItem = new ToolStripMenuItem("Run in Background")
            {
                CheckOnClick = true,
                Checked = settings?.RunInBackground ?? true
            };
            
            foreach (var colorScheme in schemes)
            {
                var menuItem = new ToolStripMenuItem(colorScheme.Name);

                menuItem.Checked = settings?.CurrentColorScheme == colorScheme.Name;
                themeMenuItem.DropDownItems.Add(menuItem);
            }

            menuStrip1.Items.AddRange(new ToolStripItem[] { themeMenuItem, HelpMenuItem });
            advancedToolStripMenuItem.DropDownItems.Insert(0, new ToolStripSeparator());
            advancedToolStripMenuItem.DropDownItems.Insert(0, runInBackgroundMenuItem);
            advancedToolStripMenuItem.DropDownItems.Insert(0, startOnWindowsStartupMenuItem);

            recordCountsMsMenuItem = CreateRecordCountsMsMenuItem();
            menuStrip1.Items.Insert(1, recordCountsMsMenuItem);

            Theme.Apply(this, menuStrip1);

            trayIcon = CreateTrayIcon();

            if (startInBackground)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            AccelGUI = AccelGUIFactory.Construct(
                this,
                AccelerationChart,
                AccelerationChartY,
                VelocityChart,
                VelocityChartY,
                GainChart,
                GainChartY,
                chartContainer,
                accelTypeDropX,
                accelTypeDropY,
                XLutApplyDropdown,
                YLutApplyDropdown,
                CapTypeDropdownXClassic,
                CapTypeDropdownYClassic,
                CapTypeDropdownXPower,
                CapTypeDropdownYPower,
                writeButton,
                toggleButton,
                showVelocityGainToolStripMenuItem,
                showLastMouseMoveToolStripMenuItem,
                AutoWriteMenuItem,
                startOnWindowsStartupMenuItem,
                runInBackgroundMenuItem,
                DeviceMenuItem,
                ScaleMenuItem,
                themeMenuItem,
                DPITextBox,
                PollRateTextBox,
                DirectionalityPanel,
                sensitivityBoxX,
                VertHorzRatioBox,
                rotationBox,
                inCapBoxXClassic,
                inCapBoxYClassic,
                outCapBoxXClassic,
                outCapBoxYClassic,
                inCapBoxXPower,
                inCapBoxYPower,
                outCapBoxXPower,
                outCapBoxYPower,
                inputJumpBoxX,
                inputJumpBoxY,
                outputJumpBoxX,
                outputJumpBoxY,
                inputOffsetBoxX,
                inputOffsetBoxY,
                outputOffsetBoxX,
                outputOffsetBoxY,
                accelerationBoxX,
                accelerationBoxY,
                decayRateBoxX,
                decayRateBoxY,
                gammaBoxX,
                gammaBoxY,
                smoothBoxX,
                smoothBoxY,
                scaleBoxX,
                scaleBoxY,
                limitBoxX,
                limitBoxY,
                powerBoxX,
                powerBoxY,
                expBoxX,
                expBoxY,
                syncSpeedBoxX,
                syncSpeedBoxY,
                DomainBoxX,
                DomainBoxY,
                RangeBoxX,
                RangeBoxY,
                LpNormBox,
                sensXYLock,
                ByComponentXYLock,
                FakeBox,
                WholeCheckBox,
                ByComponentCheckBox,
                gainSwitchX,
                gainSwitchY,
                XLutActiveValuesBox,
                YLutActiveValuesBox,
                XLutPointsBox,
                YLutPointsBox,
                LockXYLabel,
                sensitivityLabel,
                VertHorzRatioLabel,
                rotationLabel,
                inCapLabelXClassic,
                inCapLabelYClassic,
                outCapLabelXClassic,
                outCapLabelYClassic,
                CapTypeLabelXClassic,
                CapTypeLabelYClassic,
                inCapLabelXPower,
                inCapLabelYPower,
                outCapLabelXPower,
                outCapLabelYPower,
                CapTypeLabelXPower,
                CapTypeLabelYPower,
                inputJumpLabelX,
                inputJumpLabelY,
                outputJumpLabelX,
                outputJumpLabelY,
                inputOffsetLabelX,
                inputOffsetLabelY,
                outputOffsetLabelX,
                outputOffsetLabelY,
                constantOneLabelX,
                constantOneLabelY,
                decayRateLabelX,
                decayRateLabelY,
                gammaLabelX,
                gammaLabelY,
                smoothLabelX,
                smoothLabelY,
                scaleLabelX,
                scaleLabelY,
                limitLabelX,
                limitLabelY,
                powerLabelX,
                powerLabelY,
                expLabelX,
                expLabelY,
                LUTTextLabelX,
                LUTTextLabelY,
                constantThreeLabelX,
                constantThreeLabelY,
                ActiveValueTitle,
                ActiveValueTitleY,
                SensitivityMultiplierActiveLabel,
                VertHorzRatioActiveLabel,
                RotationActiveLabel,
                InCapActiveXLabelClassic,
                InCapActiveYLabelClassic,
                OutCapActiveXLabelClassic,
                OutCapActiveYLabelClassic,
                CapTypeActiveXLabelClassic,
                CapTypeActiveYLabelClassic,
                InCapActiveXLabelPower,
                InCapActiveYLabelPower,
                OutCapActiveXLabelPower,
                OutCapActiveYLabelPower,
                CapTypeActiveXLabelPower,
                CapTypeActiveYLabelPower,
                InputJumpActiveXLabel,
                InputJumpActiveYLabel,
                OutputJumpActiveXLabel,
                OutputJumpActiveYLabel,
                InputOffsetActiveXLabel,
                InputOffsetActiveYLabel,
                OutputOffsetActiveXLabel,
                OutputOffsetActiveYLabel,
                AccelerationActiveLabelX,
                AccelerationActiveLabelY,
                DecayRateActiveXLabel,
                DecayRateActiveYLabel,
                GammaActiveXLabel,
                GammaActiveYLabel,
                SmoothActiveXLabel,
                SmoothActiveYLabel,
                ScaleActiveXLabel,
                ScaleActiveYLabel,
                LimitActiveXLabel,
                LimitActiveYLabel,
                PowerClassicActiveXLabel,
                PowerClassicActiveYLabel,
                ExpActiveXLabel,
                ExpActiveYLabel,
                SyncSpeedActiveXLabel,
                SyncSpeedActiveYLabel,
                AccelTypeActiveLabelX,
                AccelTypeActiveLabelY,
                gainSwitchActiveLabelX,
                gainSwitchActiveLabelY,
                OptionSetXTitle,
                OptionSetYTitle,
                MouseLabel,
                DirectionalityLabel,
                DirectionalityX,
                DirectionalityY,
                DirectionalityActiveValueTitle,
                LPNormLabel,
                LpNormActiveValue,
                DirectionalDomainLabel,
                DomainActiveValueX,
                DomainActiveValueY,
                DirectionalityRangeLabel,
                RangeActiveValueX,
                RangeActiveValueY,
                XLutApplyLabel,
                YLutApplyLabel,
                LutApplyActiveXLabel,
                LutApplyActiveYLabel);

            WireApplicationSettingsMenuItems();

        }

        #endregion Constructor

        #region Properties

        public AccelGUI AccelGUI { get; }

        #endregion Properties

        #region Methods

        private NotifyIcon CreateTrayIcon()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Raw Accel", null, (s, e) => RestoreFromTray());
            menu.Items.Add("Exit", null, (s, e) =>
            {
                allowClose = true;
                Close();
            });

            var icon = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Icon = Icon,
                Text = "Raw Accel",
                Visible = runInBackgroundMenuItem.Checked || startInBackground
            };

            icon.DoubleClick += (s, e) => RestoreFromTray();
            return icon;
        }

        private void WireApplicationSettingsMenuItems()
        {
            startOnWindowsStartupMenuItem.CheckedChanged += StartOnWindowsStartupMenuItem_CheckedChanged;
            runInBackgroundMenuItem.CheckedChanged += RunInBackgroundMenuItem_CheckedChanged;
        }

        private void StartOnWindowsStartupMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (updatingApplicationSettingsMenuItems) return;

            try
            {
                if (startOnWindowsStartupMenuItem.Checked)
                {
                    MakeStartupShortcut(true, runInBackgroundMenuItem.Checked);
                }
                else
                {
                    RemoveStartupShortcuts();
                }

                AccelGUI.Settings.SaveGUISettingsFromFields();
            }
            catch (Exception ex)
            {
                updatingApplicationSettingsMenuItems = true;
                startOnWindowsStartupMenuItem.Checked = !startOnWindowsStartupMenuItem.Checked;
                updatingApplicationSettingsMenuItems = false;

                ShowApplicationSettingsError("Could not update the Windows startup shortcut.", ex);
            }
        }

        private void RunInBackgroundMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (updatingApplicationSettingsMenuItems) return;

            UpdateTrayIconVisibility();
            AccelGUI.Settings.SaveGUISettingsFromFields();

            if (!startOnWindowsStartupMenuItem.Checked) return;

            try
            {
                MakeStartupShortcut(true, runInBackgroundMenuItem.Checked);
            }
            catch (Exception ex)
            {
                ShowApplicationSettingsError("The background setting was saved, but the Windows startup shortcut could not be updated.", ex);
            }
        }

        private void UpdateTrayIconVisibility()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = runInBackgroundMenuItem.Checked || !Visible;
            }
        }

        private void ShowApplicationSettingsError(string message, Exception ex)
        {
            MessageBox.Show(
                message + "\r\n\r\n" + ex.Message,
                "Raw Accel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private ToolStripMenuItem CreateRecordCountsMsMenuItem()
        {
            var item = new ToolStripMenuItem("Record");
            item.Click += RecordCountsMsMenuItem_Click;
            return item;
        }

        private void RecordCountsMsMenuItem_Click(object sender, EventArgs e)
        {
            if (countsMsRecordingForm != null && !countsMsRecordingForm.IsDisposed)
            {
                countsMsRecordingForm.Activate();
                return;
            }

            countsMsRecordingForm = new CountsMsRecordingForm(AccelGUI.MouseWatcher);
            countsMsRecordingForm.FormClosed += (s, args) => countsMsRecordingForm = null;
            countsMsRecordingForm.Show(this);
        }

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;

            if (trayIcon != null)
            {
                trayIcon.Visible = true;
            }
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            UpdateTrayIconVisibility();
        }

        protected override void WndProc(ref Message m)
        {
            if (!(AccelGUI is null))
            {
                if (m.Msg == 0x00ff) // WM_INPUT
                {
                    AccelGUI.MouseWatcher.ReadMouseMove(m);
                }
                else if (m.Msg == 0x00fe) // WM_INPUT_DEVICE_CHANGE
                {
                    AccelGUI.Settings.OnDeviceChangeMessage();
                }
            }

            base.WndProc(ref m);
        }

        public void ResetAutoScroll()
        {
            chartsPanel.AutoScrollPosition = Constants.Origin;
        }

        public void ResizeAndCenter()
        {
            ResetAutoScroll();

            var workingArea = Screen.FromControl(this).WorkingArea;
            var chartsPreferredSize = chartsPanel.GetPreferredSize(Constants.MaxSize);

            Size = new Size
            {
                Width = Math.Min(workingArea.Width, optionsPanel.Size.Width + chartsPreferredSize.Width),
                Height = Math.Min(workingArea.Height, chartsPreferredSize.Height + 48)
            };

            Location = new Point
            {
                X = workingArea.X + (workingArea.Width - Size.Width) / 2,
                Y = workingArea.Y + (workingArea.Height - Size.Height) / 2
            };

        }

        #endregion Method

        static void MakeStartupShortcut(bool gui, bool background = false)
        {
            var startupFolder = GetStartupFolder();
            dynamic shell = CreateWindowsScriptHostShell();

            try
            {
                RemoveStartupShortcuts(startupFolder, shell);

                var name = gui ? "rawaccel" : "writer";
                var lnk = shell.CreateShortcut(Path.Combine(startupFolder, name + ".lnk"));

                try
                {
                    if (gui && background) lnk.Arguments = "--background";
                    if (!gui) lnk.Arguments = Constants.DefaultSettingsFileName;
                    lnk.TargetPath = Path.Combine(Application.StartupPath, name + ".exe");
                    lnk.WorkingDirectory = Application.StartupPath;
                    lnk.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(lnk);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        static void RemoveStartupShortcuts()
        {
            var startupFolder = GetStartupFolder();
            dynamic shell = CreateWindowsScriptHostShell();

            try
            {
                RemoveStartupShortcuts(startupFolder, shell);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        static void RemoveStartupShortcuts(string startupFolder, dynamic shell)
        {
            foreach (var path in StartupShortcutPaths(startupFolder, shell))
            {
                File.Delete(path);
            }
        }

        static bool StartupShortcutExists()
        {
            try
            {
                var startupFolder = GetStartupFolder();
                dynamic shell = CreateWindowsScriptHostShell();

                try
                {
                    return StartupShortcutPaths(startupFolder, shell).Any();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
            catch
            {
                return false;
            }
        }

        static IList<string> StartupShortcutPaths(string startupFolder, dynamic shell)
        {
            var paths = new List<string>();
            var candidates = new[] { "rawaccel", "raw accel", "writer" };

            foreach (string path in Directory.EnumerateFiles(startupFolder, "*.lnk"))
            {
                var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                if (!candidates.Any(fileName.Contains))
                {
                    continue;
                }

                var link = shell.CreateShortcut(path);
                try
                {
                    string targetPath = link.TargetPath;

                    if (IsRawAccelStartupTarget(targetPath))
                    {
                        paths.Add(path);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(link);
                }
            }

            return paths;
        }

        static bool IsRawAccelStartupTarget(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                return false;
            }

            if (targetPath.EndsWith("rawaccel.exe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!targetPath.EndsWith("writer.exe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var directory = new FileInfo(targetPath).Directory;
            return directory != null && directory.Exists && directory.GetFiles("rawaccel.exe").Any();
        }

        static string GetStartupFolder()
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

            if (string.IsNullOrEmpty(startupFolder))
            {
                throw new Exception("Startup folder does not exist");
            }

            return startupFolder;
        }

        static dynamic CreateWindowsScriptHostShell()
        {
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
            return Activator.CreateInstance(t);
        }

        private void RawAcceleration_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Size = Size;
            Properties.Settings.Default.Location = Location;
            Properties.Settings.Default.Save();

            if (runInBackgroundMenuItem.Checked && !allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
        }

        private void RawAcceleration_Shown(object sender, EventArgs e)
        {
            var sizeDirect = chartsPanel.GetPreferredSize(Constants.MaxSize);
            Console.WriteLine($"[BeginInvoke] Direct size: {sizeDirect}");

            if (Properties.Settings.Default.HasRunBefore)
            {
                var savedSettingsRect = new Rectangle(Properties.Settings.Default.Location, Properties.Settings.Default.Size);
                bool isSavedSettingsVisible = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(savedSettingsRect));
                if (isSavedSettingsVisible)
                {
                    //this.Location = Properties.Settings.Default.Location;
                    //this.Size = Properties.Settings.Default.Size;
                }
                else
                {
                    ResizeAndCenter();
                }
            }
            else
            {
                Properties.Settings.Default.HasRunBefore = true;
                Properties.Settings.Default.Save();
                IAsyncResult result = this.BeginInvoke(new MethodInvoker(() =>
                {
                    ResizeAndCenter();
                }));
                this.EndInvoke(result);
            }

            if (startInBackground)
            {
                BeginInvoke(new MethodInvoker(HideToTray));
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (runInBackgroundMenuItem.Checked && WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        }

    }
}
