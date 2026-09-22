using System;
using System.Collections.Generic;
using System.Linq;

namespace grapher.Models.Calculations
{
    public class AccelChartData
    {
        #region Constructors

        public AccelChartData()
        {
            AccelPoints = new SortedDictionary<double, double>();
            VelocityPoints = new SortedDictionary<double, double>();
            GainPoints = new SortedDictionary<double, double>();
            OutVelocityToPoints = new Dictionary<double, (double, double, double)>();
            LogToIndex = new int[701];
        }

        #endregion Constructors

        #region Properties

        public SortedDictionary<double, double> AccelPoints { get; }

        public double MaxAccel { get; set; }

        public double MinAccel { get; set; }

        public double MaxGain { get; set; }

        public double MinGain { get; set; }

        public SortedDictionary<double, double> VelocityPoints { get; }

        public SortedDictionary<double, double> GainPoints { get; }

        public int[] LogToIndex { get; }

        public Dictionary<double, (double, double, double)> OutVelocityToPoints { get; }

        private double[] SortedInVelocities { get; set; }

        private double[] SortedOutVelocities { get; set; }

        #endregion Properties

        #region Methods

        public void Clear()
        {
            AccelPoints.Clear();
            VelocityPoints.Clear();
            GainPoints.Clear();
            OutVelocityToPoints.Clear();
            Array.Clear(LogToIndex, 0, LogToIndex.Length);
            SortedInVelocities = null;
            SortedOutVelocities = null;
        }

        public double EstimateInVelocity(double outVelocityValue)
        {
            if (!(outVelocityValue > 0) || VelocityPoints.Count == 0)
            {
                return 0;
            }

            if (SortedInVelocities == null)
            {
                SortedInVelocities = VelocityPoints.Keys.ToArray();
                SortedOutVelocities = VelocityPoints.Values.ToArray();
            }

            var upper = Array.BinarySearch(SortedOutVelocities, outVelocityValue);

            if (upper >= 0)
            {
                return SortedInVelocities[upper];
            }

            upper = ~upper;

            if (upper == 0 || upper == SortedOutVelocities.Length)
            {
                var edge = Math.Min(upper, SortedOutVelocities.Length - 1);
                return SortedOutVelocities[edge] > 0 ?
                    outVelocityValue * SortedInVelocities[edge] / SortedOutVelocities[edge] :
                    SortedInVelocities[edge];
            }

            var lower = upper - 1;
            var outSpan = SortedOutVelocities[upper] - SortedOutVelocities[lower];

            if (!(outSpan > 0))
            {
                return SortedInVelocities[lower];
            }

            var fraction = (outVelocityValue - SortedOutVelocities[lower]) / outSpan;
            return SortedInVelocities[lower] + fraction * (SortedInVelocities[upper] - SortedInVelocities[lower]);
        }

        public (double, double, double) FindPointValuesFromOut(double outVelocityValue)
        {
            if (OutVelocityToPoints.TryGetValue(outVelocityValue, out var values))
            {
                return values;
            }
            else
            {
                var velIdx = GetVelocityIndex(outVelocityValue);

                values = (VelocityPoints.ElementAt(velIdx).Key, AccelPoints.ElementAt(velIdx).Value, GainPoints.ElementAt(velIdx).Value);
                OutVelocityToPoints.Add(outVelocityValue, values);
                return values;
            }
        }

        public (double, double, double) ValuesAtIndex(int index)
        {
            return (AccelPoints.ElementAt(index).Value, VelocityPoints.ElementAt(index).Value, GainPoints.ElementAt(index).Value);
        }

        public (double, double, double) ValuesAtInVelocity(double inVelocity)
        {
            return (AccelPoints[inVelocity], VelocityPoints[inVelocity], GainPoints[inVelocity]);
        }

        public int GetVelocityIndex(double outVelocityValue)
        {
            if (outVelocityValue < 0)
            {
                throw new ArgumentException($"invalid velocity: {outVelocityValue}");
            }

            var log = Math.Log10(outVelocityValue);
            if (log < -2)
            {
                log = -2;
            }
            else if (log > 5)
            {
                log = 5;
            }

            log = log * 100 + 200;

            var velIdx = LogToIndex[(int)log];

            return velIdx;
        }

        #endregion Methods
    }
}
