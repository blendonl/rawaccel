using grapher.Common;
using grapher.Models.Mouse;
using grapher.Models.Theming;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace grapher
{
    public class SpeedOverlay : Form
    {
        #region External

        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const int WM_NCLBUTTONDOWN = 0x00A1;

        private const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion External

        #region Constructors

        public SpeedOverlay(SpeedRecorder recorder)
        {
            Recorder = recorder;
            ShowInput = true;
            CurrentSpeedBin = new double[1];
            GraphBins = new double[0];

            Text = "Speed Overlay";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = Constants.SpeedOverlaySize;
            Opacity = Constants.SpeedOverlayOpacity;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            ValueFont = new Font(Font, FontStyle.Bold);
            ContextMenuStrip = CreateContextMenu();

            RefreshTimer = new Timer();
            RefreshTimer.Interval = Constants.SpeedOverlayRefreshIntervalMs;
            RefreshTimer.Tick += (sender, e) => Invalidate();
        }

        #endregion Constructors

        #region Properties

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var createParams = base.CreateParams;
                createParams.ExStyle |= WS_EX_TOOLWINDOW;
                return createParams;
            }
        }

        private SpeedRecorder Recorder { get; }

        private bool ShowInput { get; set; }

        private SpeedStatistics Statistics => ShowInput ? Recorder.InputStatistics : Recorder.OutputStatistics;

        private Timer RefreshTimer { get; }

        private Font ValueFont { get; }

        private ToolStripMenuItem InputSpeedMenuItem { get; set; }

        private ToolStripMenuItem OutputSpeedMenuItem { get; set; }

        private double[] CurrentSpeedBin { get; }

        private double[] GraphBins { get; set; }

        private int RowHeight => Font.Height + 4;

        #endregion Properties

        #region Methods

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Recorder.Reset();
            Recorder.Enabled = true;
            RefreshTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RefreshTimer.Stop();
            Recorder.Enabled = false;
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RefreshTimer.Dispose();
                ValueFont.Dispose();
                ContextMenuStrip?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var scheme = Theme.CurrentScheme;
            var graphics = e.Graphics;
            graphics.Clear(scheme.ChartBackground);

            var content = Rectangle.Inflate(
                ClientRectangle,
                -Constants.SpeedOverlayPadding,
                -Constants.SpeedOverlayPadding);
            var dimColor = Color.FromArgb(160, scheme.ChartForeground);

            TextRenderer.DrawText(
                graphics,
                ShowInput ? Constants.SpeedOverlayInputTitle : Constants.SpeedOverlayOutputTitle,
                Font,
                new Rectangle(content.Left, content.Top, content.Width, RowHeight),
                dimColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var statsTop = content.Top + RowHeight + Constants.SpeedOverlayPadding / 2;
            var statsBottom = DrawStats(graphics, content, statsTop, scheme.ChartForeground, dimColor);

            var graphArea = Rectangle.FromLTRB(
                content.Left,
                statsBottom + Constants.SpeedOverlayPadding,
                content.Right,
                content.Bottom);
            DrawGraph(graphics, graphArea, scheme, dimColor);
        }

        private int DrawStats(Graphics graphics, Rectangle content, int top, Color valueColor, Color labelColor)
        {
            var statistics = Statistics;
            var stats = new[]
            {
                ("Now", FormatSpeed(CurrentSpeed())),
                ("Max", FormatStatistic(statistics, statistics.Max)),
                ("Min", FormatStatistic(statistics, statistics.Min)),
                ("Avg", FormatStatistic(statistics, statistics.Average)),
                ("Median", FormatStatistic(statistics, statistics.Median)),
                ("95th %", FormatStatistic(statistics, statistics.Percentile95)),
            };

            var columnWidth = content.Width / 2;
            var columnGap = Constants.SpeedOverlayPadding * 2;

            for (int i = 0; i < stats.Length; i++)
            {
                var (label, value) = stats[i];
                var cell = new Rectangle(
                    content.Left + (i % 2) * columnWidth,
                    top + (i / 2) * RowHeight,
                    columnWidth - columnGap,
                    RowHeight);

                TextRenderer.DrawText(graphics, label, Font, cell, labelColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(graphics, value, ValueFont, cell, valueColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            return top + (stats.Length + 1) / 2 * RowHeight;
        }

        private void DrawGraph(Graphics graphics, Rectangle area, ColorScheme scheme, Color labelColor)
        {
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            var binCount = Math.Max(2, area.Width / Constants.SpeedOverlayGraphPixelsPerBin);

            if (GraphBins.Length != binCount)
            {
                GraphBins = new double[binCount];
            }

            var binInMs = Constants.SpeedOverlayGraphWindowMs / binCount;
            var firstBin = Math.Floor(Recorder.NowInMs / binInMs) - binCount;
            Recorder.History.FillSpeeds(firstBin * binInMs, binInMs, ShowInput, GraphBins);

            var scale = NiceCeiling(Math.Max(GraphBins.Max(), Constants.SpeedOverlayGraphMinScale));
            var middle = area.Top + area.Height / 2;

            using (var gridPen = new Pen(Color.FromArgb(60, scheme.ChartForeground)))
            {
                gridPen.DashStyle = DashStyle.Dot;
                graphics.DrawLine(gridPen, area.Left, area.Top, area.Right, area.Top);
                graphics.DrawLine(gridPen, area.Left, middle, area.Right, middle);
                gridPen.DashStyle = DashStyle.Solid;
                graphics.DrawLine(gridPen, area.Left, area.Bottom, area.Right, area.Bottom);
            }

            var points = new PointF[binCount + 2];

            for (int bin = 0; bin < binCount; bin++)
            {
                points[bin] = new PointF(
                    area.Left + (bin + 0.5f) * area.Width / binCount,
                    area.Bottom - (float)(Math.Min(GraphBins[bin], scale) / scale * area.Height));
            }

            points[binCount] = new PointF(points[binCount - 1].X, area.Bottom);
            points[binCount + 1] = new PointF(points[0].X, area.Bottom);

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var fill = new SolidBrush(Color.FromArgb(70, scheme.MouseMovement)))
            using (var line = new Pen(scheme.MouseMovement, 1.5f))
            {
                graphics.FillPolygon(fill, points);
                graphics.DrawLines(line, points.Take(binCount).ToArray());
            }

            var labelFlags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(graphics, FormatSpeed(scale), Font, new Point(area.Left + 2, area.Top + 2), labelColor, labelFlags);
            TextRenderer.DrawText(graphics, FormatSpeed(scale / 2), Font, new Point(area.Left + 2, middle + 2), labelColor, labelFlags);
        }

        private double CurrentSpeed()
        {
            var window = Constants.SpeedOverlayCurrentSpeedWindowMs;
            Recorder.History.FillSpeeds(Recorder.NowInMs - window, window, ShowInput, CurrentSpeedBin);
            return CurrentSpeedBin[0];
        }

        private ContextMenuStrip CreateContextMenu()
        {
            InputSpeedMenuItem = new ToolStripMenuItem("Input Speed", null, (sender, e) => ShowInput = true);
            OutputSpeedMenuItem = new ToolStripMenuItem("Output Speed", null, (sender, e) => ShowInput = false);

            var menu = new ContextMenuStrip();
            menu.Renderer = new StyledMenuRenderer();
            menu.Items.AddRange(new ToolStripItem[]
            {
                InputSpeedMenuItem,
                OutputSpeedMenuItem,
                new ToolStripSeparator(),
                new ToolStripMenuItem("Reset Stats", null, (sender, e) => Recorder.Reset()),
                new ToolStripMenuItem("Close", null, (sender, e) => Close()),
            });
            menu.Opening += (sender, e) => PrepareContextMenu(menu);

            return menu;
        }

        private void PrepareContextMenu(ContextMenuStrip menu)
        {
            InputSpeedMenuItem.Checked = ShowInput;
            OutputSpeedMenuItem.Checked = !ShowInput;

            menu.BackColor = Theme.CurrentScheme.MenuBackground;
            menu.ForeColor = Theme.CurrentScheme.OnControl;

            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = Theme.CurrentScheme.MenuBackground;
                item.ForeColor = Theme.CurrentScheme.OnControl;
            }
        }

        private static string FormatStatistic(SpeedStatistics statistics, double speed)
        {
            return statistics.Count > 0 ? FormatSpeed(speed) : "-";
        }

        private static string FormatSpeed(double speed)
        {
            if (speed < 10)
            {
                return speed.ToString("0.00");
            }

            return speed < 100 ? speed.ToString("0.0") : speed.ToString("0");
        }

        private static double NiceCeiling(double value)
        {
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));

            foreach (var step in new[] { 1, 1.5, 2, 3, 4, 5, 6, 8 })
            {
                if (step * magnitude >= value)
                {
                    return step * magnitude;
                }
            }

            return 10 * magnitude;
        }

        #endregion Methods
    }
}
