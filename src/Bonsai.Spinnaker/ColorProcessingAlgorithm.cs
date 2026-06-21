namespace Bonsai.Spinnaker
{
    /// <summary>
    /// Specifies the method used to convert bayer pattern encoded color images.
    /// </summary>
    public enum ColorProcessingAlgorithm
    {
        /// <summary>
        /// Specifies that the default debayering algorithm should be used.
        /// </summary>
        Default,

        /// <summary>
        /// Specifies that no debayering algorithm should be used and only the
        /// raw frame data is returned.
        /// </summary>
        NoColorProcessing,

        /// <summary>
        /// Specifies the fastest debayering algorithm, at the lowest quality.
        /// </summary>
        NearestNeighbor,

        /// <summary>
        /// Specifies nearest neighbor interpolation with averaged green pixels, for
        /// higher quality than nearest neighbor at the expense of speed.
        /// </summary>
        NearestNeighborAverage,

        /// <summary>
        /// Specifies debayering by a weighted average of the surrounding pixels in a
        /// 2x2 neighborhood.
        /// </summary>
        Bilinear,

        /// <summary>
        /// Specifies debayering by weighting surrounding pixels based on localized edge
        /// orientation.
        /// </summary>
        EdgeSensing,

        /// <summary>
        /// Specifies a debayering algorithm well balanced between speed and quality.
        /// </summary>
        HQLinear,

        /// <summary>
        /// Specifies a multi-threaded debayering algorithm producing similar results to
        /// edge sensing.
        /// </summary>
        Ipp,

        /// <summary>
        /// Specifies a high quality debayering algorithm that is more memory intensive
        /// than the alternatives.
        /// </summary>
        DirectionalFilter,

        /// <summary>
        /// Specifies the slowest debayering algorithm, producing good quality results.
        /// </summary>
        Rigorous,

        /// <summary>
        /// Specifies debayering by a weighted pixel average from different directions.
        /// </summary>
        WeightedDirectionalFilter
    }
}
