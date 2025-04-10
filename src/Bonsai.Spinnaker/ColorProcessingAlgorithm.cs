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
        NoColorProcessing
    }
}
