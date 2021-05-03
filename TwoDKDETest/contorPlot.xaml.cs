using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;
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

namespace TwoDKDETest
{
    /// <summary>
    /// Interaction logic for contorPlot.xaml
    /// </summary>
    public partial class contorPlot : Window
    {

        public MainDbContex viewModel;
        public contorPlot()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(contorPlot_Loaded);
        }

        private void contorPlot_Loaded(object sender, RoutedEventArgs e)
        {
            this.viewModel = this.DataContext as MainDbContex;
            loadContorPlot();
    }

        public void loadContorPlot()
        {
            var dataseries = new UniformHeatmapDataSeries<double,double,double>(viewModel._matrix, viewModel.minSlope,viewModel.slopeSampleRate, viewModel.minMz, viewModel.mzSampleRate);

            contourSeries.DataSeries = dataseries;
            contourSeries.ZMax = viewModel.Max;
            contourSeries.ZMin = viewModel.Max*0.01;
            contourSeries.ZStep = viewModel.Max / 5;
            contourSeries.ColorMap.Maximum = viewModel.Max;

        }

    }
}
