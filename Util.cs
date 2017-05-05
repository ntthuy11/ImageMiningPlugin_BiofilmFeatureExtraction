using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IM.Imaging;
using IM.GlobalTools;
using IM.Library.Filtering;
using IM.Library;
using IM.Library.Morpho;
using IM.Library.Mathematics;
using IM.Library.Mathematics.Matrix;
using System.Drawing;
using System.IO;
using IM.Library.Descriptor;

namespace Thuy_BioFilm_Final
{
    public class Util
    {

        public static void ShowImage(Image3D image, string imageName) {
            Sequence s = new Sequence();
            s.Name = imageName;
            s.Add(image);
            IMGlobal.AddSequence(s);
        }


        public static bool IsBoundary(int x, int y, int nx, int ny) {
            return x <= 0f || y <= 0f || x >= nx - 1 || y >= ny - 1;
        }


        public static bool isInside(Point p, ConnectedComponent cc)
        {
            int startX = cc.MinX;
            int endX = startX + cc.Width;

            int startY = cc.MinY;
            int endY = startY + cc.Height;

            return (startX <= p.X && p.X <= endX) && (startY <= p.Y && p.Y <= endY);
        }


        public static void convertGrayImgToBW(Image3D grayImg, Image3D bwImg)
        {
            for (int y = 0; y < grayImg.Height; y++)
            {
                for (int x = 0; x < grayImg.Width; x++)
                {
                    if (grayImg.Get(x, y, 0, 0) > 10.0f)
                        bwImg.Set(x, y, 0, 0, 255.0f);
                    else
                        bwImg.Set(x, y, 0, 0, 0);
                }
            }
        }


        /// <summary> Otsu's Threshold Method - 2D Only
        /// Input/Output - Image3D Object
        /// </summary>
        /// <param name="input">Input image</param>
        /// <param name="inputBand">Input channel</param>
        /// <param name="output">Output image</param>
        /// <param name="outputBand">Output channel</param>        
        public static void OtsuThreshold(Image3D input, int inputBand, Image3D output, int outputBand)
        {
            float min = 0f;
            float max = 0f;

            Util.ComputeMinMaxValue(input, inputBand, ref min, ref max);

            int stepNum = (int)(max - min) + 1;
            float[] hist = new float[stepNum];

            for (int i = 0; i < stepNum; i++) hist[i] = 0;
            for (int i = 0; i < input.ImageSize; i++) hist[(int)(input.Data[inputBand][i] - min)] += 1f;

            float numB = hist[0];
            float numO = (float)input.ImageSize;
            float muB = min;
            float muO = 0f;
            for (int i = 1; i < stepNum; i++) muO += hist[i] * (min + (float)i);
            muO /= numO;

            float[] sigSq = new float[stepNum];
            sigSq[0] = (float)Math.Sqrt((double)(numB * numO * (muB - muO) * (muB - muO)));

            float numBN = 0f;
            float numON = 0f;
            float muBN = 0f;
            float muON = 0f;

            for (int i = 1; i < stepNum; i++)
            {
                numBN = numB + hist[i];
                numON = numO - hist[i];
                muBN = (muB * numB + hist[i] * (min + (float)i)) / numBN;
                muON = (muO * numO - hist[i] * (min + (float)i)) / numON;
                sigSq[i] = (float)Math.Sqrt((double)(numBN * numON * (muBN - muON) * (muBN - muON)));

                numB = numBN;
                numO = numON;
                muB = muBN;
                muO = muON;
            }

            int thresholdValue = 0;
            float tmp = float.MinValue;

            for (int i = 0; i < stepNum; i++)
            {
                if (tmp < sigSq[i])
                {
                    tmp = sigSq[i];
                    thresholdValue = i;
                }
            }

            thresholdValue += (int)min;

            for (int i = 0; i < output.ImageSize; i++) if (input.Data[inputBand][i] >= (float)thresholdValue) output.Data[outputBand][i] = input.Data[inputBand][i];
        }


        /// <summary> Compute Minimum and Maximum Intensity of Image - 2D Only
        /// Input - Image3D Object
        /// </summary>
        /// <param name="input">Input image</param>
        /// <param name="band">Input channel</param>
        /// <param name="min">Output - minimum value</param>
        /// <param name="max">Output - maximum value</param>
        public static void ComputeMinMaxValue(Image3D input, int band, ref float min, ref float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int i = 0; i < input.ImageSize; i++)
            {
                if (min > input.Data[band][i]) min = input.Data[band][i];
                if (max < input.Data[band][i]) max = input.Data[band][i];
            }
        }
    }
}
