using MathNet.Numerics.Statistics;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.PaletteProviders;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting3D;
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

        
        public MainDbContex()
        {

            minMz = 500;
            maxMz = 1500;
            minSlope = 900_000;
            maxSlope = 2000_000;
            mzSampleRate = 0.01;
            slopeSampleRate = 5000;



            MikesSelectionWindow = new MikesSelectionObject() { MinMz = this.minMz, MaxMz=this.maxMz, MaxSlope = this.maxSlope, MinSlope=this.minSlope, Charge=0, ForceCharge = false};
            mikesSelectionWindow.PropertyChanged += MikesSelectionWindow_PropertyChanged;
        }

        private void MikesSelectionWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.UpdatePlot?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler UpdatePlot;

        public double[,] _matrix { get; set; }

        public ICollection<MainWindow.TwoDKDEChargeAssignment.Peak> _peaks { get; set; }

        public double minMz { get; set; }
        public double maxMz { get; set; }
        public double maxSlope { get; set; }
        public double minSlope { get; set; }
        public double mzSampleRate { get; set; }
        public double slopeSampleRate { get; set; }
        public double Max { get; set; }


        public IChargeData _chargeData { get; set; }

        MikesSelectionObject mikesSelectionWindow;

        public MikesSelectionObject MikesSelectionWindow
        {
            get { return mikesSelectionWindow; }
            set { mikesSelectionWindow = value; }
        }

     

    }

    public class MikesSelectionObject : INotifyPropertyChanged 
    {
        private bool forceCharge;

        public bool ForceCharge
        {
            get { return forceCharge; }
            set { forceCharge = value; this.OnPropertyChanged(nameof(ForceCharge)); }
        }


        private int charge;

        public int Charge
        {
            get { return charge; }
            set { charge = value; this.OnPropertyChanged(nameof(Charge)); }
        }


        private double _minMZ;

        public double MinMz
        {
            get { return _minMZ; }
            set { _minMZ = value; this.OnPropertyChanged(nameof(MinMz)); }
        }

        private double _maxMZ;

        public double MaxMz
        {
            get { return _maxMZ; }
            set { _maxMZ = value; this.OnPropertyChanged(nameof(MaxMz)); }
        }


        private double _minSlope;

        public double MinSlope
        {
            get { return _minSlope; }
            set { _minSlope = value; this.OnPropertyChanged(nameof(MinSlope)); }
        }

        private double _maxSlope;

        public double MaxSlope
        {
            get { return _maxSlope; }
            set { _maxSlope = value; this.OnPropertyChanged(nameof(MaxSlope)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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
            

            double rSquared = 0.996;
            double minToD = 0.2;
         
            //double sampleRate = 0.02;
            double resolutionReference = 140_000;
            double resolutionMz = 200;

            string calfile = @"C:\Data\BOB\othercalibrationfolders\boltcal.txt";
            IChargeData chargeData = EmpiricalChargeData.CreateFromCalibrationFile(calfile);
            theViewModel._chargeData = GaussianInterpolatedChargeData.InterpolateMissingCharges(chargeData, 250);


            IInstrumentI2MSResolutionCalculator resolutionCalculator = new InstrumentI2MSResolutionCalculator(140000, 200);

            var po = new ParallelOptions();
            po.MaxDegreeOfParallelism = 7;



            var ChargeAssinger = new TwoDKDEChargeAssignment(theViewModel._chargeData, resolutionCalculator, po);

            string input = @"C:\Data\I2MS\new IgG data\1877withstandard_HCLC_I2MS_1kV_IT0.05_001_data";

            var pingIons =  new PingOutputParser().ParseDirectory(new DirectoryInfo(input), rSquared, minToD, theViewModel.minSlope);
            var singleIons = pingIons.Select(x => new SingleIon(x.ScanNumber, x.Mz, x.Slope)).ToArray();
            Array.Sort(singleIons, new Comparison<ISingleIon>((x, y) => x.Mz.CompareTo(y.Mz)));

            theViewModel._matrix =  ChargeAssinger.Assign(singleIons, theViewModel.minMz, theViewModel.maxMz, theViewModel.minSlope, theViewModel.maxSlope, theViewModel.mzSampleRate, theViewModel.slopeSampleRate);

            theViewModel._peaks = ChargeAssinger.FindPeaks(theViewModel._matrix, theViewModel.minMz, theViewModel.minSlope, theViewModel.mzSampleRate, theViewModel.slopeSampleRate);

            var max = 0.0;
            var mzLen = theViewModel._matrix.GetLength(0);
            var slopeLen = theViewModel._matrix.GetLength(1);

            for (int i = 0; i < mzLen; i++)
            {
                for (int j = 0; j < slopeLen; j++)
                {
                    if (theViewModel._matrix[i, j] > max)
                    {
                        max = theViewModel._matrix[i, j];
                    }
                    ;
                }
            }

            theViewModel.Max = max;


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
            theViewModel.Max = max;

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
                        
                       

                        var slopeDist = new NormalKDE(ion.Slope, slopeBandwidth/2);

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



            foreach (var charge in theViewModel._chargeData.SingleChargeDatas)
            { 
                if (charge.Mean >= theViewModel.minSlope && charge.Mean <= theViewModel.maxSlope)
                {
CreatePlaneAtSlope(charge.Mean, Color.FromArgb(128, 255, 0, 0));
                }


                

            }
           
           


        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var specWin = new _2DSpec();
            specWin.Owner = this;
            specWin.DataContext = this.theViewModel;
            specWin.Show();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var peakkdeWin = new peakSlopeKDE();
            peakkdeWin.Owner = this;
            peakkdeWin.DataContext = this.theViewModel;
            peakkdeWin.Show();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var peakkdeWin = new contorPlot();
            peakkdeWin.Owner = this;
            peakkdeWin.DataContext = this.theViewModel;
            peakkdeWin.Show();
        }


        public void CreatePlaneAtSlope(double slope,Color color, double width = 0.5 )
        {
            //Turns data values into percentages for world coordiantes

            float maxZ = (float)theViewModel.maxSlope;
            float minZ = (float)theViewModel.minSlope;
            float halfLen = (maxZ - minZ) / 2;


            float vetorvalue = ((((halfLen + minZ) - (float)slope) / (halfLen)) * 100) - ((float)width/2);

            var plane = new CubeGeometry(new Vector3(-100.0f, 0.0f, -vetorvalue), new Vector3(100.0f, 100.0f, -vetorvalue + 0.5f), color);

            SciChart3DSurface.Viewport3D.RootEntity.Children.Add(plane);
        }
    }


    public class CubeGeometry : BaseSceneEntity<SCRTSceneEntity>
    {
        private readonly Vector3 bottomRight;
        private readonly Vector3 topLeft;

        private readonly Color cubeColor;


        public CubeGeometry(Vector3 topLeft, Vector3 bottomRight, Color cubeColor) : base(new SCRTSceneEntity())
        {
            // Shady : Setting the position of scene entities will be used back when sorting them from camera perspective back to front
            using (TSRVector3 centerPosition = new TSRVector3(
                    0.5f * (topLeft.x + bottomRight.x),
                    0.5f * (topLeft.y + bottomRight.y),
                    0.5f * (topLeft.z + bottomRight.z)))
            {
                SetPosition(centerPosition);
            }

            this.topLeft = topLeft;
            this.bottomRight = bottomRight;
            this.cubeColor = cubeColor;
        }

        /// <summary>
        /// Determines a kind of the entity. If SCRT_SCENE_ENTITY_KIND_TRANSPARENT then the 3D Engine must make some internal adjustments to allow order independent transparency
        /// </summary>
        public override eSCRTSceneEntityKind GetKind()
        {
            return cubeColor.A == 255 ? eSCRTSceneEntityKind.SCRT_SCENE_ENTITY_KIND_OPAQUE : eSCRTSceneEntityKind.SCRT_SCENE_ENTITY_KIND_TRANSPARENT;
        }

        /// <summary>
        ///     Called when the 3D Engine wishes to render this element. This is where geometry must be drawn to the 3D scene
        /// </summary>
        /// <param name="rpi">The <see cref="IRenderPassInfo3D" /> containing parameters for the current render pass.</param>
        public override void RenderScene(IRenderPassInfo3D rpi)
        {
            float bottomRightCoordX = bottomRight.X;
            float bottomRightCoordY = bottomRight.Y;
            float bottomRightCoordZ = bottomRight.Z;
            float topLeftCoordX = topLeft.X;
            float topLeftCoordY = topLeft.Y;
            float topLeftCoordZ = topLeft.Z;

            // Commented code below is the example of treating the Location value
            // as 3D point in Data Coordinates Space but not in World Coordinates Space
            //bottomRightCoordX = (float)e.XCalc.GetCoordinate(bottomRight.X) - e.WorldDimensions.X / 2.0f;
            //bottomRightCoordY = (float)e.YCalc.GetCoordinate(bottomRight.Y);
            //bottomRightCoordZ = (float)e.ZCalc.GetCoordinate(bottomRight.Z) - e.WorldDimensions.Z / 2.0f;
            //topLeftCoordX = (float)e.XCalc.GetCoordinate(topLeft.X) - e.WorldDimensions.X / 2.0f;
            //topLeftCoordY = (float)e.YCalc.GetCoordinate(topLeft.Y);
            //topLeftCoordZ = (float)e.ZCalc.GetCoordinate(topLeft.Z) - e.WorldDimensions.Z / 2.0f;

            // y          1--------0
            // |         /|       /|
            // |       5--------4  |
            // |       |  |     |  |
            // |       |  |     |  |
            // |       |  2--------3
            // |  z    | /      |/    
            // | /     6--------7        
            // |/
            // ----------- X
            Vector3[] corners = {
                new Vector3(topLeftCoordX, topLeftCoordY, topLeftCoordZ), //0
                new Vector3(bottomRightCoordX, topLeftCoordY, topLeftCoordZ), //1
                new Vector3(bottomRightCoordX, bottomRightCoordY, topLeftCoordZ), //2
                new Vector3(topLeftCoordX, bottomRightCoordY, topLeftCoordZ), //3
                new Vector3(topLeftCoordX, topLeftCoordY, bottomRightCoordZ), //4
                new Vector3(bottomRightCoordX, topLeftCoordY, bottomRightCoordZ), //5
                new Vector3(bottomRightCoordX, bottomRightCoordY, bottomRightCoordZ), //6
                new Vector3(topLeftCoordX, bottomRightCoordY, bottomRightCoordZ), //7
            };

            Vector3[] normals = {
                new Vector3(+0.0f, +0.0f, -1.0f), //front
                new Vector3(+0.0f, +0.0f, +1.0f), //back
                new Vector3(+1.0f, +0.0f, +0.0f), //right
                new Vector3(-1.0f, +0.0f, +0.0f), //left
                new Vector3(+0.0f, +1.0f, +0.0f), //top
                new Vector3(+0.0f, -1.0f, +0.0f), //bottom
            };

            // We create a mesh context. There are various mesh render modes. The simplest is Triangles
            // For this mode we have to draw a single triangle (three vertices) for each corner of the cube
            // You can see 
            using (var meshContext = base.BeginLitMesh(TSRRenderMode.TRIANGLES))
            {
                // Set the Rasterizer State for this entity 
                VXccelEngine3D.PushRasterizerState(RasterizerStates.CullBackFacesState.TSRRasterizerState);

                // Set the color before drawing vertices
                meshContext.SetVertexColor(cubeColor);

                // Pass Entity ID value for a hit test purpose
                ulong selectionColor = VXccelEngine3D.EncodeSelectionId(EntityId, 0);
                meshContext.SetSelectionId(selectionColor);

                // Now draw the triangles. Each face of the cube is made up of two triangles
                // Front face
                SetNormal(meshContext, normals[0]);
                SetVertex(meshContext, corners[0]);
                SetVertex(meshContext, corners[2]);
                SetVertex(meshContext, corners[1]);
                SetVertex(meshContext, corners[2]);
                SetVertex(meshContext, corners[0]);
                SetVertex(meshContext, corners[3]);

                // Right side face
                SetNormal(meshContext, normals[2]);
                SetVertex(meshContext, corners[1]);
                SetVertex(meshContext, corners[2]);
                SetVertex(meshContext, corners[6]);
                SetVertex(meshContext, corners[1]);
                SetVertex(meshContext, corners[6]);
                SetVertex(meshContext, corners[5]);

                // Top face
                SetNormal(meshContext, normals[4]);
                SetVertex(meshContext, corners[2]);
                SetVertex(meshContext, corners[7]);
                SetVertex(meshContext, corners[6]);
                SetVertex(meshContext, corners[7]);
                SetVertex(meshContext, corners[2]);
                SetVertex(meshContext, corners[3]);

                // Left side face
                SetNormal(meshContext, normals[3]);
                SetVertex(meshContext, corners[3]);
                SetVertex(meshContext, corners[0]);
                SetVertex(meshContext, corners[4]);
                SetVertex(meshContext, corners[3]);
                SetVertex(meshContext, corners[4]);
                SetVertex(meshContext, corners[7]);

                // Back face
                SetNormal(meshContext, normals[1]);
                SetVertex(meshContext, corners[7]);
                SetVertex(meshContext, corners[5]);
                SetVertex(meshContext, corners[6]);
                SetVertex(meshContext, corners[7]);
                SetVertex(meshContext, corners[4]);
                SetVertex(meshContext, corners[5]);

                // Bottom face 
                SetNormal(meshContext, normals[5]);
                SetVertex(meshContext, corners[0]);
                SetVertex(meshContext, corners[1]);
                SetVertex(meshContext, corners[5]);
                SetVertex(meshContext, corners[0]);
                SetVertex(meshContext, corners[5]);
                SetVertex(meshContext, corners[4]);
            }

            // Revert raster state
            VXccelEngine3D.PopRasterizerState();

            // Set the Rasterizer State for wireframe 
            VXccelEngine3D.PushRasterizerState(RasterizerStates.WireframeState.TSRRasterizerState);

            // Create a Line Context for a continuous line and draw the outline of the cube 
            var lineColor = Color.FromArgb(0xFF, cubeColor.R, cubeColor.G, cubeColor.B);

            CreateSquare(2.0f, true, lineColor, new[] { corners[0], corners[1], corners[2], corners[3] });
            CreateSquare(2.0f, true, lineColor, new[] { corners[4], corners[5], corners[6], corners[7] });
            CreateSquare(2.0f, true, lineColor, new[] { corners[0], corners[4], corners[7], corners[3] });
            CreateSquare(2.0f, true, lineColor, new[] { corners[5], corners[1], corners[2], corners[6] });

            // Revert raster state
            VXccelEngine3D.PopRasterizerState();
        }

        private void CreateSquare(float lineThickness, bool isAntiAlias, Color lineColor, Vector3[] vertices)
        {
            using (var lineContext = base.BeginLineStrips(lineThickness, isAntiAlias))
            {
                lineContext.SetVertexColor(lineColor);

                // Pass Entity ID value for a hit test purpose
                ulong selectionColor = VXccelEngine3D.EncodeSelectionId(EntityId, 0);
                lineContext.SetSelectionId(selectionColor);

                foreach (var v in vertices)
                {
                    SetVertex(lineContext, v);
                }
                SetVertex(lineContext, vertices.First());
                lineContext.Freeze();
                lineContext.Draw();
            }
        }

        private void SetVertex(IImmediateLitMeshContext meshContext, Vector3 vector3)
        {
            meshContext.SetVertex3(vector3.X, vector3.Y, vector3.Z);
        }

        private void SetVertex(ILinesMesh linesContext, Vector3 vector3)
        {
            linesContext.SetVertex3(vector3.X, vector3.Y, vector3.Z);
        }

        private void SetNormal(IImmediateLitMeshContext meshContext, Vector3 vector3)
        {
            meshContext.Normal3(vector3.X, vector3.Y, vector3.Z);
        }
    }

}
