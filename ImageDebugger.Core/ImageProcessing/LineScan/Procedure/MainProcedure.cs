﻿using System.Collections.Generic;
using System.Windows.Media;
using HalconDotNet;
using ImageDebugger.Core.ImageProcessing.Utilts;
using ImageDebugger.Core.Models;
using ImageDebugger.Core.ViewModels.LineScan;
using ImageDebugger.Core.ViewModels.LineScan.PointSetting;
using MaterialDesignThemes.Wpf;

namespace ImageDebugger.Core.ImageProcessing.LineScan.Procedure
{
    public partial class I40LineScanMeasurement
    {
        public ImageProcessingResults3D Process(List<HImage> images, List<PointSettingViewModel> pointSettings,
            ISnackbarMessageQueue messageQueue)
        {
            var bottomImage = images[0];
            var leftImage = images[1];
            var rightImage = images[2];
            HTuple imageWidth, imageHeight;
            rightImage.GetImageSize(out imageWidth, out imageHeight);


            HObject leftImageAligned, bottomImageAligned, rightImageAligned, contours, imageComposed, regionB;
            HTuple rowB, colB, rowC, colC;


       
            _halconScripts.get_location(leftImage, rightImage, bottomImage, out leftImageAligned, out rightImageAligned, out bottomImageAligned, out contours, out imageComposed, out regionB, _shapeModelHandleRight, out rowB, out colB, out rowC, out colC);
            

         
            // Translate base
           var lineB = new Line(colB.DArr[0], rowB.DArr[0], colB.DArr[1], rowB.DArr[1], true).SortLeftRight();
           var lineC  = new Line(colC.DArr[0], rowC.DArr[0], colC.DArr[1], rowC.DArr[1], true).SortUpDown().InvertDirection();
           
            var xAxis = lineB.Translate(1.0 / _yCoeff * -6.788);
            xAxis.IsVisible = true;
            var yAxis = lineC.Translate(1.0 / _xCoeff * -19.605);
            yAxis.IsVisible = true;

            // Record debugging data
            var recordings = new List<ICsvColumnElement>();
            recordings.Add(new AngleItem()
            {
                Name = "BC",
                Value = lineB.AngleWithLine(lineC)
            });
            recordings.Add(new AngleItem()
            {
                Name = "C",
                Value = lineC.AngleWithLine(new Line(1,0,2,0))
            });



            var imageB = bottomImageAligned.HobjectToHimage().ConvertImageType("real").ScaleImage(0.001, 0);
            var imageL = leftImageAligned.HobjectToHimage().ConvertImageType("real").ScaleImage(0.001, 0);
            var imageR = rightImageAligned.HobjectToHimage().ConvertImageType("real").ScaleImage(0.001, 0);
            
            imageB.GetImageSize(out imageWidth, out imageHeight);
            imageL.GetImageSize(out imageWidth, out imageHeight);
            imageR.GetImageSize(out imageWidth, out imageHeight);
          
            
            var visualBR = VisualizeAlignment(imageR, imageB);
            var visualLR = VisualizeAlignment(imageR, imageL);

            
            var pointLocator = new PointLocator(xAxis, yAxis, _xCoeff, -_yCoeff);
            var pointMarkers = pointLocator.LocatePoints(pointSettings, new List<HImage>()
            {
                imageB, imageL, imageR
            });
            


            var output = new ImageProcessingResults3D()
            {
                Images = new List<HImage>()
                {
                     imageB, imageL, imageR, visualBR, visualLR
                },
                PointMarkers = pointMarkers,
                RecordingElements = recordings,
                ChangeOfBaseInv = MathUtils.GetChangeOfBaseInv(xAxis, yAxis, 1/_xCoeff, 1/_yCoeff),
                Edges = regionB
            };
            output.AddLineRegion(contours);
            

            return output;
        }
    }
}