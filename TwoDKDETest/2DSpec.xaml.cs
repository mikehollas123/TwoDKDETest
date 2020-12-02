using MathNet.Numerics.Statistics;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.RenderableSeries;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TopDown;
using TopDown.Cadmium.Kernel;
using TopDown.MassSpectrometry;
using TopDown.Tools;
using static TwoDKDETest.MainWindow.TwoDKDEChargeAssignment;

namespace TwoDKDETest
{
    /// <summary>
    /// Interaction logic for _2DSpec.xaml
    /// </summary>
    public partial class _2DSpec : Window
    {
        public MainDbContex viewModel;

        public _2DSpec()
        {
            InitializeComponent();
           this.Loaded += new RoutedEventHandler(_2DSpec_Loaded);
        }


        

        void _2DSpec_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecrum();
        }

        public void LoadSpecrum()
        {
            this.viewModel = this.DataContext as MainDbContex;
            var series = new XyDataSeries<double>();

            var peaks = this.viewModel._peaks.OrderBy(x=>(x.Mass)).ToArray();


            //pEAK filter

           



            var profileSpec = KDEofPeaks(peaks);

            var masses = profileSpec.GetMz();
            var ints = profileSpec.GetIntensity();


            for (int i=0;i< masses.Length; i++)
            {
                series.Append(masses[i], ints[i]);
            }

            

            var xyplot = new FastLineRenderableSeries() { DataSeries = series };
            var tempColection = new ObservableCollection<IRenderableSeries>() { xyplot};
            sciChartSurface.RenderableSeries = tempColection;
            sciChartSurface.ZoomExtents();

        }


        public IProfileSpectrum KDEofPeaks(Peak[] peaks)
        {
            var rescal = new InstrumentI2MSResolutionCalculator(140000, 200);
            IKernelFactory KernelFactory = new NormalDistFactory(rescal);
            double cutoff = 0.01;
            var sampleRate = 0.02;
            var chargeGrouped = peaks.GroupBy(ch => ch.BestCharge).ToArray();

            double minMass = peaks.Min(m => m.Mass);
            double maxMass = peaks.Max(m => m.Mass);

            int sampleCount = (int)((maxMass - minMass) / sampleRate) + 1;

            var masses = new double[sampleCount];
         
            var intensities = new double[sampleCount];

            Parallel.For(0, sampleCount, i =>
            {
                double mass = minMass + i * sampleRate;
                masses[i] = mass;
              
            });

            Parallel.ForEach(chargeGrouped,  (chargeCips) =>
            {
                var chargedCIPSArray = chargeCips.ToArray();
                int charge = chargedCIPSArray[0].BestCharge; //probably a better way to do this

                if (charge != 0)
                {
                    var mzSampleRate = sampleRate / charge; //sample rate for this charge

                    for (int i = 0; i < chargedCIPSArray.Length; i++)
                    {
                        // RTF: Interface changes make this hard to do ... really needed?
                        //if (chargedCIPSArray[i].Intensity == 0) 
                        //    chargedCIPSArray[i].Intensity = 1;

                        //create distribution from desired KDE
                        var dist = KernelFactory.CreateKernel(chargedCIPSArray[i].mz);

                        //index in the mass bit to start at
                        int index = (int)((chargedCIPSArray[i].Mass - minMass) / sampleRate);

                        // Look left
                        int currentIndex = index;
                        while (currentIndex >= 0)
                        {
                            //find mz value that would be otained from one unit change
                            var mzToCheck = chargedCIPSArray[i].mz - (index - currentIndex) * mzSampleRate;

                            double density = dist.Density(mzToCheck, chargedCIPSArray[i].intensity);

                            // TODO: Use multiplier on stddev cutoff, not absolute cutoff
                            if (density < cutoff) break;

                            Utility.AtomicDoubleAdd(ref intensities[currentIndex--], density);
                        }

                        // Look right
                        currentIndex = index + 1;
                        while (currentIndex < masses.Length)
                        {
                            var mzToCheck = chargedCIPSArray[i].mz + (currentIndex - index) * mzSampleRate;
                            double density = dist.Density(mzToCheck, chargedCIPSArray[i].intensity);

                            if (density < cutoff) break;

                            Utility.AtomicDoubleAdd(ref intensities[currentIndex++], density);
                        }
                    };
                }
            });

            return new ProfileSpectrum(masses, intensities);


        }
    }
}
