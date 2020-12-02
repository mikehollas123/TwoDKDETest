using MathNet.Numerics.Statistics;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.PaletteProviders;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting3D.Interop;
using SciChart.Charting3D.Model;
using SciChart.Charting3D.Primitives;
using SciChart.Charting3D.RenderableSeries;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TopDown;
using TopDown.Cadmium;
using TopDown.Cadmium.ChargeAssignment;
using TopDown.Cadmium.IO;
using TopDown.Cadmium.Kernel;
using TopDown.Tools;

namespace TwoDKDETest
{

    public class MainDbContex

    {

      
 public double[,] _matrix { get; set; }

        public ICollection<MainWindow.TwoDKDEChargeAssignment.Peak> _peaks { get; set; }

        public double minMz { get; set; }

        public double minSlope { get; set; }
        public double mzSampleRate { get; set; }
        public double slopeSampleRate { get; set; }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        MainDbContex theViewModel;

        public MainWindow()
        {
            InitializeComponent();

            this.theViewModel = new MainDbContex();

            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            loadData();
        }



        public async void loadData()
        {
            ProgressBar.Visibility = Visibility.Visible;
            var progress = new Progress<double>(percent => ProgressBar.Value = percent * 100);
            await runThis(progress);
            ProgressBar.Visibility = Visibility.Hidden;
        }


        public async Task runThis(IProgress<double> progress)
        {
            
            int multiplier = 1;
            


            theViewModel.minMz = 1000;
            double maxMz = 1500;
            theViewModel.minSlope = 500_000;
            double maxSlope = 4_500_000;
            theViewModel.mzSampleRate = 0.01;
            theViewModel.slopeSampleRate = 5000;


            double rSquared = 0.996;
            double minToD = 0.2;
         
            //double sampleRate = 0.02;
            double resolutionReference = 140_000;
            double resolutionMz = 200;

            string calfile = @"C:\Data\BOB\othercalibrationfolders\boltcal.txt";
            IChargeData chargeData = EmpiricalChargeData.CreateFromCalibrationFile(calfile);
            chargeData = GaussianInterpolatedChargeData.InterpolateMissingCharges(chargeData, 250);


            IInstrumentI2MSResolutionCalculator resolutionCalculator = new InstrumentI2MSResolutionCalculator(140000, 200);

            var po = new ParallelOptions();
            po.MaxDegreeOfParallelism = 7;



            var ChargeAssinger = new TwoDKDEChargeAssignment(chargeData, resolutionCalculator, po);

            string input = @"X:\Projects\2020 Kelleher - GIDI-UP\I2MS_Processing\Completed\CR3022_UREAdoneright_I2MS_1kV_IT0.05_001_data";

            var pingIons =  new PingOutputParser().ParseDirectory(new DirectoryInfo(input), rSquared, minToD, theViewModel.minSlope);
            var singleIons = pingIons.Select(x => new SingleIon(x.ScanNumber, x.Mz, x.Slope)).ToArray();
            Array.Sort(singleIons, new Comparison<ISingleIon>((x, y) => x.Mz.CompareTo(y.Mz)));

            theViewModel._matrix =  ChargeAssinger.Assign(singleIons, theViewModel.minMz, maxMz, theViewModel.minSlope, maxSlope, theViewModel.mzSampleRate, theViewModel.slopeSampleRate);

            theViewModel._peaks = ChargeAssinger.FindPeaks(theViewModel._matrix, theViewModel.minMz, theViewModel.minSlope, theViewModel.mzSampleRate, theViewModel.slopeSampleRate);

           

    
        }
        public void loadKDEPlot()
        {
            var mzLen = theViewModel._matrix.GetLength(0);
            var slopeLen = theViewModel._matrix.GetLength(1);

            UniformGridDataSeries3D<double> series = new UniformGridDataSeries3D<double>(mzLen, slopeLen);

 
            series.StartX = theViewModel.minMz;
            series.StartZ = theViewModel.minSlope;
            series.StepX = theViewModel.mzSampleRate;
            series.StepZ = theViewModel.slopeSampleRate;
            var max = 0.0;

            for (int i = 0; i < mzLen; i++)
            {
                for (int j = 0; j < slopeLen; j++)
                {
                    if (theViewModel._matrix[i, j] > max)
                    {
                        max = theViewModel._matrix[i, j];
                    }
                    series[j, i] = theViewModel._matrix[i, j];
                }
            }
           
            SurfaceMesh.IsVisible = true;
            SurfaceMesh.DataSeries = series;
            SurfaceMesh.Maximum = max;

        }



        public class TwoDKDEChargeAssignment
        {
           
            private IInstrumentI2MSResolutionCalculator _resolutionCalculator;
            private IChargeData _chargeData;
            ParallelOptions _po;
            IProgress<double> _progress; 

            public TwoDKDEChargeAssignment()
            {
            }

            public TwoDKDEChargeAssignment(IChargeData chargeData, IInstrumentI2MSResolutionCalculator resolutionCalculator,  ParallelOptions po, IProgress<double> progress = null)
            {
                _chargeData = chargeData;
          
                _po = po;
                _resolutionCalculator = resolutionCalculator;
                _progress = progress;

            }
            public double[,] Assign(IList<ISingleIon> ions, double minMz = 500, double maxMz = 1500, double minSlope = 1000000, double maxSlope =1500000, double mzSampleRate = 0.1, double slopeSampleRate =10000 )
            {

                IEnumerable<IChargeAssignedIon> output = new List<IChargeAssignedIon>();


                double slopeBandwidth;

                //var minMz = ions.Min(x => x.Mz);
                //var maxMz = ions.Max(x => x.Mz);
                
                // var maxSlope = ions.Max(x => x.Slope);
                //var minSlope = ions.Min(x => x.Slope);

                var mzsampleCount = (int)((maxMz - minMz) / mzSampleRate);
                var slopesampleCount = (int)((maxSlope - minSlope) / slopeSampleRate);

                double[,] matrix = new double[mzsampleCount, slopesampleCount];


                var ChargeFinder = new KDEBasedChargeFinder2(_chargeData);


                var cutoff = 0.01;

                //foreach (var ion in ions)
                //{
                double count = 0.0;
                double total = ions.Count();

                Parallel.ForEach(ions, _po, (ion) =>
                {



                    if (ion.Mz < maxMz && ion.Mz > minMz && ion.Slope > minSlope && ion.Slope < maxSlope)
                    {
                        int mzIndex = (int)((ion.Mz - minMz) / mzSampleRate);
                        int slopeIndex = (int)((ion.Slope - minSlope) / slopeSampleRate);

                        var mzDist = new NormalKDE(ion.Mz, _resolutionCalculator.GetTheoreticalWidth(ion.Mz));


                         slopeBandwidth = ChargeFinder.GetBestChargeStdv(ion.Slope);
                        
                       

                        var slopeDist = new NormalKDE(ion.Slope, slopeBandwidth);

                        var thisCutoff = cutoff * slopeDist.Density(ion.Slope) * mzDist.Density(ion.Mz);
                        int currentSlopeIndex = slopeIndex;

                        while (currentSlopeIndex >= 0)
                        {
                            int currentMzIndex = mzIndex;
                            while (currentMzIndex >= 0)
                            {

                                var mzToCheck = ion.Mz - (mzIndex - currentMzIndex) * mzSampleRate;
                                var slopeToCheck = ion.Slope + (currentSlopeIndex - slopeIndex) * slopeSampleRate;
                                double density = mzDist.Density(mzToCheck) * slopeDist.Density(slopeToCheck);

                                if (density < thisCutoff) break;

                                Utility.AtomicDoubleAdd(ref matrix[currentMzIndex--, currentSlopeIndex], density);
                            }

                            // Look right
                            currentMzIndex = mzIndex + 1;
                            while (currentMzIndex < mzsampleCount - 1)
                            {
                                var mzToCheck = ion.Mz + (currentMzIndex - mzIndex) * mzSampleRate;

                                var slopeToCheck = ion.Slope + (currentSlopeIndex - slopeIndex) * slopeSampleRate;
                                double density = mzDist.Density(mzToCheck) * slopeDist.Density(slopeToCheck);

                                if (density < thisCutoff) break;

                                Utility.AtomicDoubleAdd(ref matrix[currentMzIndex++, currentSlopeIndex], density);
                            }
                            currentSlopeIndex--;

                        }

                        currentSlopeIndex = slopeIndex + 1;

                        while (currentSlopeIndex < slopesampleCount - 1)
                        {
                            int currentMzIndex = mzIndex;
                            while (currentMzIndex >= 0)
                            {

                                var mzToCheck = ion.Mz - (mzIndex - currentMzIndex) * mzSampleRate;
                                var slopeToCheck = ion.Slope + (currentSlopeIndex - slopeIndex) * slopeSampleRate;
                                double density = mzDist.Density(mzToCheck) * slopeDist.Density(slopeToCheck);

                                if (density < thisCutoff) break;

                                Utility.AtomicDoubleAdd(ref matrix[currentMzIndex--, currentSlopeIndex], density);
                            }

                            // Look right
                            currentMzIndex = mzIndex + 1;
                            while (currentMzIndex < mzsampleCount - 1)
                            {
                                var mzToCheck = ion.Mz + (currentMzIndex - mzIndex) * mzSampleRate;

                                var slopeToCheck = ion.Slope + (currentSlopeIndex - slopeIndex) * slopeSampleRate;
                                double density = mzDist.Density(mzToCheck) * slopeDist.Density(slopeToCheck);

                                if (density < thisCutoff) break;

                                Utility.AtomicDoubleAdd(ref matrix[currentMzIndex++, currentSlopeIndex], density);
                            }
                            currentSlopeIndex++;
                        }
                    }






                    count++;
                    if ((count) % 1000 == 0 && _progress !=null)
                    {
                        _progress.Report(count / total);
                    }

                });


                //}
                return matrix;



            }


            public ICollection<Peak> FindPeaks(double[,] matrix, double minMz, double minSlope, double mzSampleRate, double slopeSampleRate)
            {
                List<Peak> peaks = new List<Peak>();

                var mzLen = matrix.GetLength(0);
                var slopeLen = matrix.GetLength(1);

                var chargeFinder = new KDEBasedChargeFinder2(_chargeData);
                for (int i = 1; i < mzLen-1; i++)
                {
                    for (int j = 1; j < slopeLen-1; j++)
                    {
                       
                        if (matrix[i,j] > matrix[i+1, j] && matrix[i, j] > matrix[i - 1, j] && matrix[i, j] > matrix[i , j+1] && matrix[i, j] > matrix[i , j-1])
                        {
                            var bestCharge = chargeFinder.FindCharge(minSlope + (slopeSampleRate * j)).BestResult.Charge;

                            var peak = new Peak() { intensity = matrix[i, j], Slope = minSlope + (slopeSampleRate * j), mz = minMz + (mzSampleRate * i), BestCharge = bestCharge };

                            peaks.Add(peak);
                        }


                    }
                }

                var median = peaks.Select(x => x.intensity).Median();

                //peak Filter

               // var filteredPeaks = peaks.Where(x => x.intensity > median);


                return peaks.ToList();

            }

            public class Peak
            {
                public double mz { get; set; }

                public double Slope { get; set; }
                public int BestCharge { get; set; }
                public double intensity { get; set; }
                public double Mass => (mz * BestCharge) - (1.00728 * BestCharge);

            }


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            loadKDEPlot();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SurfaceMesh.IsVisible = false;


            var series = new XyzDataSeries3D<double>();

            foreach (var peak in theViewModel._peaks)
            {
                series.Append(peak.mz, peak.intensity, peak.Slope);
                
            }

            
            ScatterSeries3D.IsVisible = true;
            ScatterSeries3D.DataSeries = series;
        
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var specWin = new _2DSpec();
            specWin.Owner = this;
            specWin.DataContext = this.theViewModel;
            specWin.Show();
        }
    }
 

   

}
