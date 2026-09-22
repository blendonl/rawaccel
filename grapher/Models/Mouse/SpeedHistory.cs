using System;

namespace grapher.Models.Mouse
{
    public class SpeedHistory
    {
        #region Constructors

        public SpeedHistory(int capacity)
        {
            Times = new double[capacity];
            InputDistances = new double[capacity];
            OutputDistances = new double[capacity];
        }

        #endregion Constructors

        #region Properties

        private double[] Times { get; }

        private double[] InputDistances { get; }

        private double[] OutputDistances { get; }

        private int Next { get; set; }

        private int Count { get; set; }

        #endregion Properties

        #region Methods

        public void Add(double timeInMs, double inputDistance, double outputDistance)
        {
            Times[Next] = timeInMs;
            InputDistances[Next] = inputDistance;
            OutputDistances[Next] = outputDistance;
            Next = (Next + 1) % Times.Length;
            Count = Math.Min(Count + 1, Times.Length);
        }

        public void Clear()
        {
            Next = 0;
            Count = 0;
        }

        public void FillSpeeds(double startInMs, double binInMs, bool input, double[] bins)
        {
            Array.Clear(bins, 0, bins.Length);
            var distances = input ? InputDistances : OutputDistances;

            for (int age = 0; age < Count; age++)
            {
                var index = (Next - 1 - age + Times.Length) % Times.Length;
                var bin = (int)Math.Floor((Times[index] - startInMs) / binInMs);

                if (bin < 0)
                {
                    break;
                }

                if (bin < bins.Length)
                {
                    bins[bin] += distances[index];
                }
            }

            for (int bin = 0; bin < bins.Length; bin++)
            {
                bins[bin] /= binInMs;
            }
        }

        #endregion Methods
    }
}
