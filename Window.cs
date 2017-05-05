using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using IM.Imaging;
using IM.GlobalTools;
using IM.Plugin;
using IM.IO;
using IM.Library.Filtering;
using IM.Library.Mathematics;
using IM.Library;
using IM.Library.Descriptor;
using IM.Library.Morpho;

//////////////////////////////////////////////////////////////////////////
// If you want to change Menu & Name of plugin
// Go to "Properties->Resources" in Solution Explorer
// Change Menu & Name
//
// You can also use your own Painter & Mouse event handler
// 
//////////////////////////////////////////////////////////////////////////

namespace Thuy_BioFilm_Final
{
    public partial class Thuy_BioFilm_Final : Plugin
    {
        public Thuy_BioFilm_Final()
            : base()
        {
            InitializeComponent();
            Show();
        }

        /// <summary>
        /// Optional method to override. Init() is called before the loop of Process() call
        /// </summary>
        public override void Init()
        {

        }

        /// <summary>
        /// Process() is called in a loop when the user start a process.
        /// </summary>
        /// <param name="experiment"></param>
        public override void Process(Experiment experiment)
        {
            // ONLY process 1-band, 1-depth images

            process1Band1DepthImages(experiment);
        }

        /// <summary>
        /// Optional method to override. Conclude() is called after the loop of Process() call
        /// </summary>
        public override void Conclude()
        {

        }


        // ===================================================================


        public void process1Band1DepthImages(Experiment e)
        {
            List<float> dataC0 = new List<float>();
            List<float> dataDistC0 = new List<float>();
            List<float> listOfConnectedComponentAreasC0 = new List<float>();

            for (int i = 0; i < e.Sequences; i++)
            {
                if (this.checkBoxIgnorePart0.Checked && i == 0) continue;
                if (this.checkBoxIgnorePart1.Checked && i == 1) continue;
                if (this.checkBoxIgnorePart2.Checked && i == 2) continue;
                if (this.checkBoxIgnorePart3.Checked && i == 3) continue;
                if (this.checkBoxIgnorePart4.Checked && i == 4) continue;

                Image3D image = e.Sequence[i][0];
                Image3D imgDepth0 = new Image3D(image.Width, image.Height, 1, image.NumBands);
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int b = 0; b < image.NumBands; b++)
                        {
                            float val = image.Get(x, y, 0, b); 
                            imgDepth0.Set(x, y, 0, b, val);
                        }
                    }
                }
                process1Band1Depth(e, i, imgDepth0, dataC0, dataDistC0, listOfConnectedComponentAreasC0);
            }
        }


        public void process1Band1Depth(Experiment e, int partNo, Image3D image00,
            List<float> dataC0, List<float> dataDistC0, List<float> listOfConnectedComponentAreasC0)
        {
            float meanC0 = 0;
            float meanDistC0 = 0;


            // ---------- there are black (non-value) regions in the new data image (called 'badImage'), eliminate them here ----------
            int position;
            List<float> lineScan = new List<float>();
            float lineMean = 0;
            List<float> candidiate0 = new List<float>();

            int nNonBlackLines = 0;
            for (int y = 0; y < image00.Height; y++)
            {
                lineScan.Clear();
                for (int x = 0; x < image00.Width; x++)
                {
                    position = x + y * image00.Width;
                    candidiate0.Add(image00.Data[0][position]);
                    lineScan.Add(image00.Data[0][position]);
                }
                nNonBlackLines++;

                lineMean = new IM.Library.Mathematics.MathTools().Mean(lineScan.ToArray());
                if (lineMean < 10f) break; //50f // if the average intensity of a line is smaller than a threshold, the current image is the 'badImage'                                            
            }


            // ----------
            Image3D imageTmp0;
            List<Point> testPoints0 = new List<Point>();
            Image3D distanceMap0;

            if (nNonBlackLines <= image00.Height / 2)
            {
                return;
            }
            else
            { // if the current image is not the 'badImage'
                for (int c = 0; c < candidiate0.Count; c++)
                    dataC0.Add(candidiate0[c]); // the current image is not the 'badImage', keep every lineScan/candidate to store to dataC0 (channel 0)

                Image3D image = new Image3D(image00.Width, nNonBlackLines, image00.Depth, image00.NumBands);
                new IM.Library.Transforms.Geometrical().Crop(image00, image, 0, 0, 0);

                int nx = image.Width;
                int ny = image.Height;

                // ---------- median filter ----------
                imageTmp0 = new Image3D(image.Width, image.Height, image.Depth, 2);
                Median mf = new IM.Library.Filtering.Median();
                Image3D imageTmpMe0 = new Image3D(image.Width, image.Height, image.Depth, 2);
                mf.Median2D_Huang(image, 0, imageTmpMe0, 0, 3, 256, false);

                // ---------- convolution ----------
                new IM.Library.Mathematics.Convolution().ConvolveFast(imageTmpMe0, 0, imageTmp0, 0, new KernelMaker().MakeGaussianKernel(3.0f), BoundaryConditions.Mirror);

                // ---------- morphology local max ----------
                new IM.Library.Morpho.Morphology2D().LocalMax(imageTmp0, 0, imageTmp0, 1, 3);
                float[] data0 = imageTmp0.Data[0];
                float[] data1 = imageTmp0.Data[1];

                // ----------
                int offset = 0;
                int x, y;

                for (y = 0; y < imageTmp0.Height; y++)
                {
                    for (x = 0; x < imageTmp0.Width; x++)
                    {
                        if (data0[offset] > 0 && data1[offset] > 0)
                        {
                            if (!Util.IsBoundary(x, y, nx, ny)) testPoints0.Add(new Point(x, y));
                            else data1[offset] = 0f;
                        }
                        offset++;
                    }
                }

                // ---------- distance map (calculate this, then separately display the result) ----------
                distanceMap0 = new Image3D(nx, ny, 1, 2);
                foreach (Point v in testPoints0) distanceMap0.Data[0][v.X + v.Y * nx] = 1f;
                new IM.Library.Transforms.Distance().UnsignedDistanceMap(distanceMap0, 0, distanceMap0, 1, 0.5f, IM.Library.Transforms.Distance.Algorithm.Chamfer);
                for (int j = 0; j < distanceMap0.ImageSize; j++) dataDistC0.Add(distanceMap0.Data[1][j]);

                //Util.ShowImage(distanceMap0, "");
            }


            // ============================== calculate sum, mean, median, max, std distances ==============================

            float meanDist0OfCurrentPart = new IM.Library.Mathematics.MathTools().Mean(distanceMap0.Data[1]);

            if (dataC0.Count == 0)
            {
                meanC0 = 0;
                meanDistC0 = 0;
            }
            else
            {
                // check the mean distance, to ignore problem-parts or not (dataDistC0 will be ignored as well)                
                if (this.checkBoxWellProblemBasedOnMeanDist.Checked // is check the well's problem                    
                    && (float)this.numericUpDownWellProblemBasedOnMeanDistThr.Value < meanDist0OfCurrentPart)
                {
                    if (this.checkBoxIgnorePartWithProblem.Checked)
                    {
                        int dataDistC0_count = dataDistC0.Count;
                        for (int i = 0; i < distanceMap0.ImageSize; i++)
                        {
                            int idxToRemove = dataDistC0_count - i - 1;
                            dataDistC0.RemoveAt(idxToRemove);
                            dataC0.RemoveAt(idxToRemove); // ignore this part in other lists as well
                        }
                    }
                }

                meanC0 = new IM.Library.Mathematics.MathTools().Mean(dataC0.ToArray());
                if (dataDistC0.Count > 0)
                    meanDistC0 = new IM.Library.Mathematics.MathTools().Mean(dataDistC0.ToArray());
            }

            e.AddData("mean intensity", (meanC0 == 0 ? float.NaN : meanC0));


            // -- using FULL mean dist
            e.AddData("mean distance", (meanDistC0 == 0 ? float.NaN : meanDistC0));

            if (this.checkBoxWellProblemBasedOnMeanDist.Checked && (float)this.numericUpDownWellProblemBasedOnMeanDistThr.Value < meanDist0OfCurrentPart)
            {
                string detectProblemString = string.Format("part" + partNo + " (" + meanDist0OfCurrentPart + ")");
                if (this.checkBoxIgnorePartWithProblem.Checked)
                    detectProblemString = detectProblemString + string.Format(" ignored");
                e.AddData("FOCUS PROBLEM (using mean distance)", (meanDistC0 == 0 ? "NaN" : detectProblemString));
            }

            float invMeanDistC0 = 0;
            if (meanDistC0 == 0)
                invMeanDistC0 = 0;
            else
                invMeanDistC0 = 1.0f / meanDistC0;
            e.AddData("inversed mean distance", (invMeanDistC0 == 0 ? float.NaN : invMeanDistC0));


            // ============================== CONSIDER the size(area) of detected aggregation ==============================

            considerSizeOfAggregation(e, image00, 0, listOfConnectedComponentAreasC0, meanDistC0);
        }


        private void considerSizeOfAggregation(Experiment e, Image3D image00, int band, List<float> listOfConnectedComponentAreas, float meanDist)
        {
            Image3D otsuImg = new Image3D(image00.Width, image00.Height, 1, 1);
            Util.OtsuThreshold(image00, band, otsuImg, 0);
            //Util.ShowImage(otsuImg, "otsu"); 

            Image3D bwImg = new Image3D(image00.Width, image00.Height, 1, 1);
            Util.convertGrayImgToBW(otsuImg, bwImg);
            //Util.ShowImage(bwImg, "bw");

            ConnectedComponentSet connComponentSet = new ConnectedComponentSet(bwImg, 0, Connectivity.TwoD_4, 10, bwImg.ImageSize / 50);

            float mean = 0;
            float median95 = 0;

            if (connComponentSet.Count > 0)
            {
                foreach (ConnectedComponent cc in connComponentSet)
                    listOfConnectedComponentAreas.Add(cc.Indices.Count);

                // mean area
                mean = new MathTools().Sum(listOfConnectedComponentAreas.ToArray()) / listOfConnectedComponentAreas.Count;

                // 95%
                listOfConnectedComponentAreas.Sort();
                median95 = listOfConnectedComponentAreas[(int)(listOfConnectedComponentAreas.Count * 0.95)];
            }

            e.AddData("mean area", (meanDist == 0 ? float.NaN : mean));
            e.AddData("95% area", (meanDist == 0 ? float.NaN : median95));
            e.AddData("# particles", (meanDist == 0 ? float.NaN : connComponentSet.Count));
        }


        private List<float> copyList(List<float> src)
        {
            List<float> result = new List<float>();
            for (int i = 0; i < src.Count; i++)
                result.Add(src[i]);
            return result;
        }


        private void checkBoxWellProblemBasedOnMeanDist_CheckedChanged_1(object sender, EventArgs e)
        {
            if (this.checkBoxWellProblemBasedOnMeanDist.Checked == false)
            {
                this.labelWellProblemBasedOnMeanDist.Enabled = false;
                this.numericUpDownWellProblemBasedOnMeanDistThr.Enabled = false;

                this.checkBoxIgnorePartWithProblem.Enabled = false;
            }
            else
            {
                this.labelWellProblemBasedOnMeanDist.Enabled = true;
                this.numericUpDownWellProblemBasedOnMeanDistThr.Enabled = true;

                this.checkBoxIgnorePartWithProblem.Enabled = true;
            }
        }
    }


    /// <summary>
    /// If you want keep your version information,
    /// Put your version information in the AssemblyInfo.cs file
    /// [assembly: AssemblyVersion("1.0.*")]
    /// [assembly: AssemblyFileVersion("1.0.0.0")]
    /// </summary>
    public static class PluginVersion
    {
        public static string Info
        {
            get
            {
                System.Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                bool isDaylightSavingsTime = TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now);
                DateTime MyTime = new DateTime(2000, 1, 1).AddDays(v.Build).AddSeconds(v.Revision * 2).AddHours(isDaylightSavingsTime ? 1 : 0);

                return string.Format("Version:{0}.{1} - Compiled:{2:s}", v.Major, v.MajorRevision, MyTime);
            }
        }
    }
}