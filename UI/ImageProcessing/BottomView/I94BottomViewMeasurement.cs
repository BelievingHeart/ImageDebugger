﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Accord.MachineLearning.VectorMachines.Learning;
using HalconDotNet;
using MaterialDesignThemes.Wpf;
using UI.ImageProcessing.Utilts;
using UI.Model;
using UI.ViewModels;

namespace UI.ImageProcessing.BottomView
{
    public partial class I94BottomViewMeasurement : IMeasurementProcedure
    {
        public event Action MeasurementResultReady;
        public string Name { get; } = "I94_BOTTOM";
        public event Action MeasurementResultPulled;

        private readonly HDevelopExport HalconScripts = new HDevelopExport();
        private HTuple _shapeModelHandle;

     

        public double Weight { get; set; } = 0.0076;


        public I94BottomViewMeasurement()
        {
            HOperatorSet.ReadShapeModel("./backViewModel", out _shapeModelHandle);
        }

        /// <summary>
        /// Get the longest contour with in a region
        /// </summary>
        /// <param name="image"></param>
        /// <param name="location"></param>
        /// <param name="cannyLow"></param>
        /// <param name="cannyHigh"></param>
        /// <returns></returns>
        private HObject GetContour(HImage image, FindLineLocation location, int cannyLow = 20, int cannyHigh = 40)
        {
            HObject region;
            HOperatorSet.GenRectangle2(out region, location.Y, location.X, MathUtils.ToRadian(location.Angle),
                location.Len1, location.Len2);
            var imageEdge = image.ReduceDomain(new HRegion(region));
            return imageEdge.EdgesSubPix("canny", 3, cannyLow, cannyHigh);
        }

        /// <summary>
        /// Return a single intersection point of a line and a contour
        /// </summary>
        /// <param name="line"></param>
        /// <param name="contour"></param>
        /// <returns></returns>
        private Point LineContourIntersection(Line line, HObject contour)
        {
            HTuple x, y, _, contourLength;
            HalconScripts.LongestXLD(contour, out contour, out contourLength);
            HOperatorSet.IntersectionLineContourXld(contour, line.YStart, line.XStart, line.YEnd, line.XEnd, out y,
                out x, out _);

            return new Point(x.D, y.D);
        }
    }
}