using grapher.Common;
using System.Diagnostics;

namespace grapher.Models.Mouse
{
    public class SpeedRecorder
    {
        #region Constructors

        public SpeedRecorder()
        {
            History = new SpeedHistory(Constants.SpeedHistoryCapacity);
            InputStatistics = new SpeedStatistics();
            OutputStatistics = new SpeedStatistics();
            Clock = Stopwatch.StartNew();
        }

        #endregion Constructors

        #region Properties

        public bool Enabled { get; set; }

        public SpeedHistory History { get; }

        public SpeedStatistics InputStatistics { get; }

        public SpeedStatistics OutputStatistics { get; }

        public double NowInMs => Clock.Elapsed.TotalMilliseconds;

        private Stopwatch Clock { get; }

        #endregion Properties

        #region Methods

        public void Record(double inputSpeed, double outputSpeed, double timeInMs, bool movedFromRest)
        {
            if (!Enabled)
            {
                return;
            }

            History.Add(NowInMs, inputSpeed * timeInMs, outputSpeed * timeInMs);

            if (!movedFromRest)
            {
                InputStatistics.Add(inputSpeed);
                OutputStatistics.Add(outputSpeed);
            }
        }

        public void Reset()
        {
            History.Clear();
            InputStatistics.Reset();
            OutputStatistics.Reset();
        }

        #endregion Methods
    }
}
