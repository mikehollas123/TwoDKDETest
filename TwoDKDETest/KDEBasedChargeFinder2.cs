using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TopDown.Cadmium;
using TopDown.Cadmium.ChargeAssignment;
using TopDown.Tools;

namespace TwoDKDETest
{
    public class KDEBasedChargeFinder2 : IChargeFinder
    {
        private IChargeData _chargeData;
        public KDEBasedChargeFinder2(IChargeData chargeData)
        {
            _chargeData = chargeData;
        }

        public IChargeFinderResultSet FindCharge(ICollection<double> slopeSamples)
        {
            double[] chargeProbs = new double[this._chargeData.SingleChargeDatas.Count];

            foreach (var slope in slopeSamples)
            {
                Parallel.ForEach(this._chargeData.SingleChargeDatas, (singleChargeData) =>
                {
                    //foreach (ISingleChargeData singleChargeData in this._chargeData.SingleChargeDatas)

                    double mean = singleChargeData.Mean;
                    double standardDeviation = singleChargeData.StandardDeviation;

                    var norm = new Normal(mean, standardDeviation);

                    chargeProbs[singleChargeData.Charge - 1] += norm.Density(slope);
                });
            }
            var maxProb = chargeProbs.Max();
            chargeProbs = chargeProbs.Select(x => (x / maxProb) * 100).ToArray();

            IList<IChargeFinderResult> results = new List<IChargeFinderResult>();
            for (int i = 1; i < chargeProbs.Length - 1; i++)
            {
                if (chargeProbs[i] > 1)
                    results.Add(new ChargeFinderResult(i + 1, chargeProbs[i]));
            }
            if (results.Count != 0)
            {
                return new ChargeFinderResultSet(results);
            }
            else
            {
                results.Add(new ChargeFinderResult(0, 1));
                return new ChargeFinderResultSet(results);
            }
        }

        public IChargeFinderResultSet FindCharge(double slope)
        {
            ICollection<double> stupidWorkAround = new List<double>() { slope };

            return this.FindCharge(stupidWorkAround);
        }

        public double GetBestChargeStdv(double slope)
        {
            ICollection<double> stupidWorkAround = new List<double>() {slope };

            var bestCharge = this.FindCharge(stupidWorkAround).BestResult.Charge;

            var stdv = _chargeData.SingleChargeDatas.Where(x => x.Charge == bestCharge).Select(x => x.StandardDeviation).FirstOrDefault();

            return stdv;
                
        }
    }
}
