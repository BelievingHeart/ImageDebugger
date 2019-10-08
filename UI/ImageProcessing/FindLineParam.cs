﻿using System.Xml;
using System.Xml.Serialization;

namespace UI.ImageProcessing
{
    public class FindLineParam
    {
        [XmlAttribute] public string Name { get; set; } = "Null";
        [XmlAttribute] public FindLinePolarity Polarity { get; set; } = FindLinePolarity.Positive;
        [XmlAttribute] public EdgeSelection WhichEdge { get; set; } = EdgeSelection.First;
        [XmlAttribute] public PairSelection WhichPair { get; set; } = PairSelection.First;
        [XmlAttribute] public int Threshold { get; set; } = 20;
        [XmlAttribute] public double IgnoreFraction { get; set; } = 0.2;
        [XmlAttribute] public int NewWidth { get; set; } = 5;
        [XmlAttribute] public double Sigma1 { get; set; } = 1;
        [XmlAttribute] public double Sigma2 { get; set; } = 1;
        [XmlAttribute] public int CannyLow { get; set; } = 20;
        [XmlAttribute] public int CannyHigh { get; set; } = 40;
        [XmlAttribute] public bool FirstAttemptOnly { get; set; } = false;

        [XmlAttribute] public bool UsingPair { get; set; } = false;
        [XmlAttribute] public int MinWidth { get; set; }
        [XmlAttribute] public int MaxWidth { get; set; }

        /// <summary>
        /// Number of measure rectangle to generate
        /// </summary>
        [XmlAttribute]
        public int NumSubRects { get; set; } = 10;
    }


    public enum FindLinePolarity
    {
        Positive,
        Negative
    }

    public enum EdgeSelection
    {
        First,
        Last
    }

    public enum PairSelection
    {
        First,
        Last
    }
}