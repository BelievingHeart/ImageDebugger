﻿using System.Collections.Generic;
using HalconDotNet;

namespace UI.ImageProcessing
{
    public class Line
    {
        public double XStart { get; set; }

        public double YStart { get; set; }

        public double XEnd { get; set; }

        public double YEnd { get; set; }

        public static int ImageWidth { get; set; } = 5120;

        public static int ImageHeight { get; set; } = 5120;

        private static List<HObject>  LineRegions = new List<HObject>();

        private static HDevelopExport HalconScripts = new HDevelopExport();

        public Line(double xStart, double yStart, double xEnd, double yEnd, bool display = false)
        {
            XStart = xStart;
            YStart = yStart;
            XEnd = xEnd;
            YEnd = yEnd;

            if (!display) return;
            HObject lineRegion;
            HalconScripts.GenLineRegion(out lineRegion, xStart, yStart, xEnd, yEnd, ImageWidth, ImageHeight);
            LineRegions.Add(lineRegion);
        }


        public void Display(HWindow windowHandle)
        {
            foreach (var lineRegion in LineRegions)
            {
                lineRegion.DispObj(windowHandle);
            }
            LineRegions.Clear();
        }
    }
}