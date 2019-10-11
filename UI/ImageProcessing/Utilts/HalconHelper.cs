﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Xsl;
using HalconDotNet;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Random;

namespace UI.ImageProcessing.Utilts
{
    public static class HalconHelper
    {
        private static HDevelopExport HalconScripts = new HDevelopExport();

        public static HObject ConcateAll(params HObject[] objects)
        {
            var objectOut = objects[0];
            for (int i = 1; i < objects.Length; i++)
            {
                HOperatorSet.ConcatObj(objectOut, objects[i], out objectOut);
            }

            return objectOut;
        }

        public static Tuple<List<double>, List<double>> FindLineSubPixel(HImage image, double[] row, double[] col, double[] radian,
            double[] len1, double[] len2, string transition, int numSubRects, int threshold, string whichEdge, double ignoreFraction, int cannyLow, int cannyHigh, double sigma1, double sigma2,
            int newWidth, out HObject edges, out HObject findLineRects)
        {
            var length = row.Length;
            findLineRects = new HObject();
            findLineRects.GenEmptyObj();
            edges = new HObject();
            edges.GenEmptyObj();

            var outputXs = new List<double>();
            var outputYs = new List<double>();

            // For each find line rect
            for (int i = 0; i < length; i++)
            {
                HObject findLineRect, edge;
                HTuple xs, ys, x1, x2, y1, y2, _;
                HalconScripts.VisionProStyleFindLine(image, out findLineRect, transition, row[i], col[i], radian[i],
                    len1[i], len2[i], numSubRects, threshold, sigma1, whichEdge, "false", "first", 0, 0, out xs, out ys);

                HalconScripts.FitLine2D(xs, ys, ignoreFraction, out x1, out y1, out x2, out y2, out _, out _, out _,
                    out _);

                HalconScripts.GetEdgesInSubRect2(image, out findLineRect, out edge, x1, y1, x2, y2, radian[i], newWidth, sigma2, cannyLow, cannyHigh);
                edges.ConcatObj(edge);
                findLineRects.ConcatObj(findLineRect);

                List<double> contourXs, contourYs;
                GetContoursPoints(edge, out contourXs, out contourYs);
                outputXs.AddRange(contourXs);
                outputYs.AddRange(contourYs);
            }

            return new Tuple<List<double>, List<double>>(outputXs, outputYs);
        }

        public static void GetContoursPoints(HObject edges, out List<double> contourXs, out List<double> contourYs)
        {
            contourXs = new List<double>();
            contourYs = new List<double>();

            int edgeCount = edges.CountObj();

            for (int i = 0; i < edgeCount; i++)
            {
                HTuple ys, xs;
                HOperatorSet.GetContourXld(edges[i], out ys, out xs);
                contourXs.AddRange(xs.ToDArr());
                contourYs.AddRange(ys.ToDArr());
            }
        }



        /// <summary>
        /// 根据点坐标拟合一条直线
        /// </summary>
        public static Line leastSquareAdaptLine(List<double> xArray, List<double> yArray)
        {
            Trace.Assert(xArray.Count == yArray.Count);
            cylineParam lineValue = new cylineParam() { A = 0, B = 0, C = 0 };

            var xStart = xArray.Min();
            var xEnd = xArray.Max();

            int nums = xArray.Count;
            List<double> matrix = new List<double>() { 0, 0, 0, 0 };
            List<double> bias = new List<double>() { 0, 0 };
            double xx = 0, xy = 0, yy = 0, x = 0, y = 0;
            for (int i = 0; i < xArray.Count; i++)
            {
                xx += xArray[i] * xArray[i];
                yy += yArray[i] * yArray[i];
                x += xArray[i];
                xy += xArray[i] * yArray[i];
                y += yArray[i];
            }
            double dVAlue1 = xx - (x * x) / nums;
            double dValue2 = yy - (y * y) / nums;
            bool bFunX = true;
            // y方向的方差更小， 那证明， 应该采用 x= a*y +b;
            if (dVAlue1 < dValue2)
                bFunX = false;

            if (bFunX == true)
            {
                //y = ax+b;
                matrix[0] = xx; matrix[1] = x;
                matrix[2] = matrix[1]; matrix[3] = nums;
                bias[0] = xy; bias[1] = y;
            }
            else
            {
                //ay +b = x
                matrix[0] = yy; matrix[1] = y;
                matrix[2] = matrix[1]; matrix[3] = nums;
                bias[0] = xy; bias[1] = x;
            }

            List<double> Invmatrix = null;
            MatrixInv2X2(matrix, out Invmatrix);

            // AAd*y = a*x +c
            List<double> reusltMatrix = new List<double>();
            Matrix_Mult2X1(Invmatrix, bias, out reusltMatrix);
            double a = reusltMatrix[0];
            double c = reusltMatrix[1];

            //三次去掉最大的偏差点
            List<double> listDist = new List<double>();
    


            int nums2 = xArray.Count();
            for (int t = 0; t < 3; t++)
            {
                // 求平均距离， 和最大的偏置项，在置信度[-99.6, 99.6]内的值保留，其他的去掉
                double sValue = 0;
                double uValue = 0;
                if (xArray.Count() < nums2 * 2 / 3 || xArray.Count() <= 2)
                    break;
                for (int j = 0; j < xArray.Count; j++)
                {
                    double pValue = 0;
                    pValue = (bFunX == true) ? (xArray[j] * a + c - yArray[j]) / Math.Sqrt(a * a + 1) :
                                                (yArray[j] * a + c - xArray[j]) / Math.Sqrt(a * a + 1);
                    listDist.Add(pValue);
                    uValue += pValue;
                    sValue += pValue * pValue;
                }
                uValue /= xArray.Count;
                sValue = Math.Sqrt((sValue - (uValue * uValue) * xArray.Count) / (xArray.Count - 1));

                double distMin = uValue - 1.96 * sValue;
                double distMax = uValue + 1.96 * sValue;
                nums = xArray.Count;
                for (int j = 0; j < xArray.Count; j++)
                {
                    if (listDist[j] > distMin && listDist[j] < distMax)
                        continue;

                    matrix[0] -= ((bFunX == true) ? (xArray[j] * xArray[j]) : (yArray[j] * yArray[j]));
                    matrix[1] -= ((bFunX == true) ? xArray[j] : yArray[j]);
                    bias[0] -= xArray[j] * yArray[j];
                    bias[1] -= ((bFunX == true) ? yArray[j] : xArray[j]);

                    xArray.RemoveAt(j);
                    yArray.RemoveAt(j);
                    listDist.RemoveAt(j);

                    //double xValue = xArray[j];
                    //double yValue = yArray[j];
                    //moveXArray.Add(xValue);
                    //moveYArray.Add(yValue);
                    j--;
                }
                matrix[3] = xArray.Count;
                matrix[2] = matrix[1];
                MatrixInv2X2(matrix, out Invmatrix);
                Matrix_Mult2X1(Invmatrix, bias, out reusltMatrix);
                a = reusltMatrix[0];
                c = reusltMatrix[1];
                listDist.Clear();
                if (xArray.Count == nums)
                    break;
            }

            double div = Math.Sqrt(a * a + 1);
            if (bFunX == true)
            {
                // y=ax+c
                lineValue.A = a / div;
                lineValue.B = -1 / div;
                lineValue.C = -c / div;
            }
            else
            {
                //x = ay+c
                lineValue.A = 1 / div;
                lineValue.B = -a / div;
                lineValue.C = c / div;
            }

            var yStart = (lineValue.C - lineValue.A * xStart) / lineValue.B;
            var yEnd = (lineValue.C - lineValue.A * xEnd) / lineValue.B;

            return new Line(xStart, yStart, xEnd, yEnd);
        }

        public static void MatrixInv2X2(List<double> Rmatrix, out List<double> RInvmatrix)
        {
            RInvmatrix = new List<double>() { 0, 0, 0, 0 };
            double div = Rmatrix[3] * Rmatrix[0] - Rmatrix[1] * Rmatrix[2];
            if (Math.Abs(div) <= 1e-24)
                return;
            RInvmatrix[0] = Rmatrix[3] / div; RInvmatrix[1] = -Rmatrix[1] / div;
            RInvmatrix[2] = -Rmatrix[2] / div; RInvmatrix[3] = Rmatrix[0] / div;
        }

     
        public static void Matrix_Mult2X1(List<double> Rmatrix, List<double> input, out List<double> result)
        {
            result = new List<double>() {0, 0};
            if (Rmatrix.Count != 4)
                return;
            if (input.Count != 2)
                return;

            result[0] = Rmatrix[0] * input[0] + Rmatrix[1] * input[1];
            result[1] = Rmatrix[2] * input[0] + Rmatrix[3] * input[1];
        }
    }

    /// <summary>
    /// 直线参数
    /// </summary>
    public struct cylineParam
    {
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }

        public static cylineParam operator *(cylineParam a, double b)
        {
            return new cylineParam() { A = a.A * b, B = a.B * b, C = a.C * b };
        }
    }
}