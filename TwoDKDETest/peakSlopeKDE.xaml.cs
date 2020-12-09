using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Annotations;
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
using TopDown.Cadmium.Kernel;
using TopDown.Tools;

namespace TwoDKDETest
{
    /// <summary>
    /// Interaction logic for peakSlopeKDE.xaml
    /// </summary>
    public partial class peakSlopeKDE : Window
    { 
        public MainDbContex viewModel;

        public peakSlopeKDE()
        {
            InitializeComponent();
            
            this.Loaded += new RoutedEventHandler(peakSlopeKDE_Loaded);

        }

        void peakSlopeKDE_Loaded(object sender, RoutedEventArgs e)
        {

            ShowSpectra();
        }


        public void ShowSpectra()
        {
            this.viewModel = this.DataContext as MainDbContex;
            var series = new XyDataSeries<double>();

            var peakSlopes = this.viewModel._peaks;

            var maxSlope = peakSlopes.Max(x=>x.Slope);
            var minSlope = peakSlopes.Min(x => x.Slope);
            var slopeSampleRate = 500;
            var cutoff = 0.01;

            var slopeBandwidth = 3000;

            var slopesampleCount = (int)((maxSlope - minSlope) / slopeSampleRate);

            var slopeArray = new double[slopesampleCount];
            var intensityArray = new double[slopesampleCount];


            for (int i = 0; i < slopesampleCount -1; i++)
            {
                slopeArray[i] = minSlope + slopeSampleRate * i;
            }



            Parallel.ForEach(peakSlopes, (peak) =>
            {
                //Do a KDE
                int slopeIndex = (int)((peak.Slope - minSlope) / slopeSampleRate);
                var slopeDist = new NormalKDE(peak.Slope, slopeBandwidth);

                var thisCutoff = cutoff * slopeDist.Density(peak.Slope);
                int currentSlopeIndex = slopeIndex;

                while (currentSlopeIndex >= 0 && currentSlopeIndex < slopesampleCount - 1)
                {

                    var slopeToCheck = peak.Slope + (currentSlopeIndex - slopeIndex) * slopeSampleRate;
                    double density = slopeDist.Density(slopeToCheck,peak.intensity);

                    //if (density < thisCutoff) break;

                    Utility.AtomicDoubleAdd(ref intensityArray[currentSlopeIndex--], density);

                }
                currentSlopeIndex = slopeIndex + 1;

                while (currentSlopeIndex >= 0 && currentSlopeIndex < slopesampleCount - 1)
                {
                    var slopeToCheck = peak.Slope + (currentSlopeIndex - slopeIndex) * slopeSampleRate;
                    double density = slopeDist.Density(slopeToCheck, peak.intensity);

                    //if (density < thisCutoff) break;

                    Utility.AtomicDoubleAdd(ref intensityArray[currentSlopeIndex++], density);
                }


            });


            for (int i = 0; i < slopesampleCount - 1; i++)
            {
                series.Append(slopeArray[i], intensityArray[i]);
            }
            var lineplot = new FastLineRenderableSeries() { DataSeries=series};


            ObservableCollection<IRenderableSeries> tempcollection = new ObservableCollection<IRenderableSeries> { lineplot };

            sciChartSurface.RenderableSeries = tempcollection;

            foreach( var chargedata in viewModel._chargeData.SingleChargeDatas)
            {
                sciChartSurface.Annotations.Add(
                    new VerticalLineAnnotation()
                    {
                        X1 = chargedata.Mean,
                        LabelValue = chargedata.Charge,
                        ShowLabel = true,
                        LabelPlacement = LabelPlacement.Axis


                    }

                    ); ;
             
            }


        }


    }
}
