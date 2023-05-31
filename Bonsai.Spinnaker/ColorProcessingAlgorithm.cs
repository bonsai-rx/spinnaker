namespace Bonsai.Spinnaker
{
    public enum ColorProcessingAlgorithm
    {
        /** No color processing. */
        NoColorProcessing,
        /**
         * Fastest but lowest quality. Equivalent to
         * FLYCAPTURE_NEAREST_NEIGHBOR_FAST in FlyCapture.
         */
        NearestNeighbor,
        /**
         * Nearest Neighbor with averaged green pixels. Higher quality but slower
         * compared to nearest neighbor without averaging.
         */
        NearestNeighborAvg,
        /** Weighted average of surrounding 4 pixels in a 2x2 neighborhood. */
        Bilinear,
        /** Weights surrounding pixels based on localized edge orientation. */
        EdgeSensing,
        /** Well-balanced speed and quality. */
        HqLinear,
        /** Multi-threaded with similar results to edge sensing. */
        Ipp,
        /** Best quality but much faster than rigorous. */
        DirectionalFilter,
        /** Slowest but produces good results. */
        Rigorous,
        /** Weighted pixel average from different directions. */
        WeightedDirectionalFilter
    }
}
