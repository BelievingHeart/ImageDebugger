﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using HalconDotNet;
using MaterialDesignThemes.Wpf;
using PropertyChanged;
using UI.Model;

namespace UI.ViewModels
{
    public partial class HalconWindowPageViewModel
    {
                #region Image Providing Logic

                public string CurrentImageName
                {
                    get
                    {
                        if (ImageMegaList == null || ImageMegaList.Count == 0 || ImageMegaList[0].Count == 0 || CurrentImageIndex < 0 || CurrentImageIndex >= TotalImages) return "";
                        return GetImageName(ImageMegaList[0][CurrentImageIndex]);
                    }
                }
                
                [AlsoNotifyFor(nameof(CurrentImageName))]
                public int CurrentImageIndex
                {
                    get { return _currentImageIndex; }
                    set
                    {
                        _currentImageIndex = value;
                        if (_currentImageIndex == TotalImages)
                        {
                            CurrentImageIndex = 0;
                            PromptUserThreadUnsafe("Current image index reaches the end, start over!");
                            return;
                        }

                        if (_currentImageIndex < 0)
                        {
                            CurrentImageIndex = TotalImages - 1;
                            PromptUserThreadUnsafe("Jump to the end of image list!");
                        }
                    }
                }


                public ICommand ImageNameSelectionChangedCommand { get; }
        private void PromptUserThreadUnsafe(string message)
        {
            RunStatusMessageQueue.Enqueue(message);
        }


        public int TotalImages { get; set; }

        private string GetImageName(string imagePath)
        {
            return Path.GetFileName(imagePath);
        }

        private List<List<string>> ImageMegaList { get; set; } = new List<List<string>>();



        /// <summary>
        /// Known list of image extensions to filter non-image files
        /// </summary>
        private static readonly List<string> ImageExtensions = new List<string>
            {".JPG", ".JPE", ".BMP", ".TIF", ".PNG"};

        private string _imageDirectory;
        private int _currentImageIndex = -1;

        /// <summary>
        /// Directory to images
        /// 1. If folder not contains any images do not update any state
        /// 2. If image names incorrect or image lists not equal, do not update any state
        /// 3. If naming correct and list all have the same count, update image lists, current image index and total images
        /// </summary>
        private string ImageDirectory
        {
            get { return _imageDirectory; }
            set
            {
                _imageDirectory = value;

                string[] filePaths = Directory.GetFiles(_imageDirectory);
                var imagePaths = new List<string>();

                foreach (var imagePath in filePaths)
                {
                    if (IsImageFile(imagePath))
                    {
                        imagePaths.Add(imagePath);
                    }
                }

                if(imagePaths.Count == 0)
                {
                    PromptUserThreadUnsafe("This folder does not contains any supported images");
                    return;
                }

             bool updateImageListsSuccess =   TryAssignImageLists(imagePaths);
             
             // set it to empty so that user can reopen the same directory
             _imageDirectory = string.Empty;
    
             
             if (!updateImageListsSuccess) return;
             // Generate image names
             ImageNames = GenImageNames();
             TotalImages = ImageMegaList[0].Count;
             ImageToShowSelectionList = GenImageToShowSelectionList(ImageMegaList.Count);

             _currentImageIndex = -1;
             OnPropertyChanged(nameof(CurrentImageIndex));
             OnPropertyChanged(nameof(CurrentImageName));
            }
        }

        private List<int> GenImageToShowSelectionList(int numImagesOneGo)
        {
            var output = new List<int>();
            for (int i = 0; i < numImagesOneGo; i++)
            {
                output.Add(i);
            }

            return output;
        }

        private List<string> GenImageNames()
        {
            var output = new List<string>();
            foreach (var path in ImageMegaList[0])
            {
                output.Add(GetImageName(path));
            }

            return output;
        }

        private string Separator { get; set; } = "-";
        
        /// <summary>
        /// Assign and return true only if all named correctly and all lists have the same count
        /// </summary>
        /// <param name="imagePaths"></param>
        /// <returns></returns>
        private bool TryAssignImageLists(List<string> imagePaths)
        {
            int numImagesInOneGo = GetNumImagesInOneGo(imagePaths);
           List<List<string>> tempMegaList = MakeTempMegaList(numImagesInOneGo);


            foreach (var path in imagePaths)
            {
                int imageIndex = 0;

                if (numImagesInOneGo > 1)
                {
                    var imageName = Path.GetFileName(path);
                    var start = imageName.IndexOf(Separator, StringComparison.Ordinal) + 1;
                    var length = 1;
                    var imageIndexString = imageName.Substring(start, length);
                    try
                    {
                        imageIndex = int.Parse(imageIndexString) - 1;
                    }
                    catch (Exception e)
                    {
                        PromptUserThreadUnsafe($"Incorrect image name: {imageName}");
                        return false;
                    }
                }

                tempMegaList[imageIndex].Add(path);
            }

            var numImagesInFirstList = tempMegaList[0].Count;
            if (tempMegaList.Any(l => l.Count != numImagesInFirstList))
            {
                PromptUserThreadUnsafe("Count of image lists not equal");
                return false;
            }
            
            var sortedImageMegaList = new List<List<string>>();
            foreach (var queue in tempMegaList)
            {
                var orderedQueue = new List<string>(queue.OrderBy(Path.GetFileName));
                sortedImageMegaList.Add(orderedQueue);
            }

            ImageMegaList = sortedImageMegaList;
            return true;
        }

        private List<List<string>> MakeTempMegaList(int numImagesInOneGo)
        {
            var output = new List<List<string>>();
            for (int i = 0; i < numImagesInOneGo; i++)
            {
                output.Add(new List<string>());
            }

            return output;
        }

        private List<HImage> GrabImageInputs(int index)
        {
            var output = new List<HImage>();
            foreach (var list in ImageMegaList)
            {
                output.Add(new HImage(list[index]));
            }

            return output;
        }
       

        /// <summary>
        /// Determine how many images should be provided within one button hit
        /// </summary>
        /// <param name="imagePaths"></param>
        /// <returns></returns>
        private int GetNumImagesInOneGo(List<string> imagePaths)
        {
            var allImageNames = imagePaths.Select(Path.GetFileName);
            var nameToTest = Path.GetFileName(imagePaths[0]);

            // Naming convention: images belong to the same group will have the same prefix
            // for example: 02_1 and 02_2 have the same prefix 02_
            if (!nameToTest.Contains(Separator)) return 1;

            var testPrefix = nameToTest.Substring(0, nameToTest.IndexOf(Separator, StringComparison.Ordinal) + 1);

            return allImageNames.Count(ele => ele.StartsWith(testPrefix));
        }

        /// <summary>
        /// Filter image files based on file extension
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        private bool IsImageFile(string imagePath)
        {
            return ImageExtensions.Contains(Path.GetExtension(imagePath)?.ToUpper());
        }

        public ICommand SelectImageDirCommand { get; private set; }

        #endregion
        
    }
}