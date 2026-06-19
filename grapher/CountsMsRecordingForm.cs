using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using grapher.Models.Mouse;
using grapher.Models.Theming;
using Timer = System.Windows.Forms.Timer;

namespace grapher
{
    public class CountsMsRecordingForm : Form
    {
        private const int RefreshIntervalMilliseconds = 100;
        private const int MaxDisplayedSamples = 600;

        private readonly MouseWatcher mouseWatcher;
        private readonly Timer refreshTimer;

        private Chart countsChart;
        private Label elapsedValueLabel;
        private Label samplesValueLabel;
        private Label minAverageValueLabel;
        private Label averageValueLabel;
        private Label maxAverageValueLabel;
        private Label statusValueLabel;
        private Button stopButton;
        private Button resetButton;

        public CountsMsRecordingForm(MouseWatcher mouseWatcher)
        {
            this.mouseWatcher = mouseWatcher ?? throw new ArgumentNullException(nameof(mouseWatcher));
            refreshTimer = new Timer { Interval = RefreshIntervalMilliseconds };
            refreshTimer.Tick += RefreshTimer_Tick;

            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            Theme.Apply(this);
            StartRecording();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer.Stop();

            if (mouseWatcher.IsRecordingCountsMs)
            {
                mouseWatcher.StopCountsMsRecording();
            }

            base.OnFormClosed(e);
        }

        private void InitializeComponent()
        {
            Text = "Counts/ms Recording";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 420);
            ClientSize = new Size(760, 500);

            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var summaryPanel = CreateSummaryPanel();
            countsChart = CreateCountsChart();
            var buttonPanel = CreateButtonPanel();

            container.Controls.Add(summaryPanel, 0, 0);
            container.Controls.Add(countsChart, 0, 1);
            container.Controls.Add(buttonPanel, 0, 2);

            Controls.Add(container);
        }

        private TableLayoutPanel CreateSummaryPanel()
        {
            var summaryPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };

            for (var i = 0; i < summaryPanel.ColumnCount; i++)
            {
                summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / summaryPanel.ColumnCount));
            }

            summaryPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            summaryPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            elapsedValueLabel = CreateMetricValueLabel();
            samplesValueLabel = CreateMetricValueLabel();
            minAverageValueLabel = CreateMetricValueLabel();
            averageValueLabel = CreateMetricValueLabel();
            maxAverageValueLabel = CreateMetricValueLabel();
            statusValueLabel = CreateMetricValueLabel();

            AddMetric(summaryPanel, 0, "Elapsed", elapsedValueLabel);
            AddMetric(summaryPanel, 1, "Samples", samplesValueLabel);
            AddMetric(summaryPanel, 2, "Min avg", minAverageValueLabel);
            AddMetric(summaryPanel, 3, "Avg", averageValueLabel);
            AddMetric(summaryPanel, 4, "Max avg", maxAverageValueLabel);
            AddMetric(summaryPanel, 5, "Status", statusValueLabel);

            return summaryPanel;
        }

        private FlowLayoutPanel CreateButtonPanel()
        {
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 6, 0, 0)
            };

            stopButton = new Button
            {
                Text = "Stop",
                Size = new Size(90, 28),
                Margin = new Padding(8, 0, 0, 0)
            };
            stopButton.Click += StopButton_Click;

            resetButton = new Button
            {
                Text = "Reset",
                Size = new Size(90, 28),
                Margin = new Padding(8, 0, 0, 0)
            };
            resetButton.Click += ResetButton_Click;

            buttonPanel.Controls.Add(stopButton);
            buttonPanel.Controls.Add(resetButton);

            return buttonPanel;
        }

        private Chart CreateCountsChart()
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            var chartArea = new ChartArea("CountsMsArea");
            chartArea.AxisX.Title = "Time (s)";
            chartArea.AxisY.Title = "Counts/ms";
            chartArea.AxisX.Minimum = 0;
            chartArea.AxisX.LabelStyle.Format = "0.##";
            chartArea.AxisY.LabelStyle.Format = "0.###";
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart.ChartAreas.Add(chartArea);

            var legend = new Legend("Legend")
            {
                Docking = Docking.Top,
                LegendStyle = LegendStyle.Row
            };
            chart.Legends.Add(legend);

            chart.Titles.Add("Counts/ms Recording");
            chart.Series.Add(CreateSeries("Counts/ms", SeriesChartType.Line, ChartDashStyle.Solid, 2));
            chart.Series.Add(CreateSeries("Avg", SeriesChartType.Line, ChartDashStyle.Solid, 2));
            chart.Series.Add(CreateSeries("Min avg", SeriesChartType.Line, ChartDashStyle.Dash, 2));
            chart.Series.Add(CreateSeries("Max avg", SeriesChartType.Line, ChartDashStyle.Dash, 2));

            return chart;
        }

        private static Series CreateSeries(
            string name,
            SeriesChartType chartType,
            ChartDashStyle dashStyle,
            int borderWidth)
        {
            return new Series(name)
            {
                ChartType = chartType,
                BorderDashStyle = dashStyle,
                BorderWidth = borderWidth,
                XValueType = ChartValueType.Double,
                YValueType = ChartValueType.Double
            };
        }

        private static Label CreateMetricTitleLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomCenter,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                AutoSize = false
            };
        }

        private static Label CreateMetricValueLabel()
        {
            return new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
        }

        private static void AddMetric(
            TableLayoutPanel summaryPanel,
            int column,
            string title,
            Label valueLabel)
        {
            summaryPanel.Controls.Add(CreateMetricTitleLabel(title), column, 0);
            summaryPanel.Controls.Add(valueLabel, column, 1);
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            UpdateRecordingView(mouseWatcher.GetCountsMsRecordingSnapshot());
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            if (mouseWatcher.IsRecordingCountsMs)
            {
                StopRecording();
                return;
            }

            StartRecording();
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            StartRecording();
        }

        private void StartRecording()
        {
            mouseWatcher.StartCountsMsRecording();
            stopButton.Text = "Stop";
            refreshTimer.Start();
            UpdateRecordingView(mouseWatcher.GetCountsMsRecordingSnapshot());
        }

        private void StopRecording()
        {
            var result = mouseWatcher.StopCountsMsRecording();
            refreshTimer.Stop();
            stopButton.Text = "Start";
            UpdateRecordingView(result);
        }

        private void UpdateRecordingView(CountsMsRecordingResult result)
        {
            elapsedValueLabel.Text = string.Format("{0:0.000} s", result.DurationMilliseconds / 1000.0);
            samplesValueLabel.Text = result.SampleCount.ToString();
            minAverageValueLabel.Text = FormatCountsPerMillisecond(result.MinAverageCountsPerMillisecond, result.HasSamples);
            averageValueLabel.Text = FormatCountsPerMillisecond(result.AverageCountsPerMillisecond, result.HasSamples);
            maxAverageValueLabel.Text = FormatCountsPerMillisecond(result.MaxAverageCountsPerMillisecond, result.HasSamples);
            statusValueLabel.Text = mouseWatcher.IsRecordingCountsMs ? "Recording" : "Stopped";

            UpdateChart(result);
        }

        private void UpdateChart(CountsMsRecordingResult result)
        {
            foreach (var series in countsChart.Series)
            {
                series.Points.Clear();
            }

            if (!result.HasSamples)
            {
                countsChart.ChartAreas[0].AxisX.Minimum = 0;
                return;
            }

            var samples = result.Samples;
            var firstSampleIndex = Math.Max(0, samples.Count - MaxDisplayedSamples);

            for (var i = firstSampleIndex; i < samples.Count; i++)
            {
                var sample = samples[i];
                countsChart.Series["Counts/ms"].Points.AddXY(
                    sample.ElapsedMilliseconds / 1000.0,
                    sample.CountsPerMillisecond);
            }

            var startSeconds = firstSampleIndex < samples.Count
                ? samples[firstSampleIndex].ElapsedMilliseconds / 1000.0
                : 0;
            var endSeconds = Math.Max(result.DurationMilliseconds / 1000.0, startSeconds + 0.001);

            AddHorizontalLine("Avg", startSeconds, endSeconds, result.AverageCountsPerMillisecond);
            AddHorizontalLine("Min avg", startSeconds, endSeconds, result.MinAverageCountsPerMillisecond);
            AddHorizontalLine("Max avg", startSeconds, endSeconds, result.MaxAverageCountsPerMillisecond);

            countsChart.ChartAreas[0].AxisX.Minimum = Math.Max(0, startSeconds);
        }

        private void AddHorizontalLine(string seriesName, double startSeconds, double endSeconds, double value)
        {
            var series = countsChart.Series[seriesName];
            series.Points.AddXY(startSeconds, value);
            series.Points.AddXY(endSeconds, value);
        }

        private static string FormatCountsPerMillisecond(double value, bool hasSamples)
        {
            return hasSamples ? string.Format("{0:0.###}", value) : "-";
        }
    }
}
