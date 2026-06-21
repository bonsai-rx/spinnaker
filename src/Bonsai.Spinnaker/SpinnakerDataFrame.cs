using OpenCV.Net;

namespace Bonsai.Spinnaker
{
    /// <summary>
    /// Represents a data object containing a decoded image frame and the chunk data
    /// which contains additional information about the image.
    /// </summary>
    public class SpinnakerDataFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpinnakerDataFrame"/> class
        /// with the specified image frame and chunk data.
        /// </summary>
        /// <param name="image">The decoded image frame.</param>
        /// <param name="chunkData">
        /// The chunk data which contains additional information about the image.
        /// </param>
        public SpinnakerDataFrame(IplImage image, SpinnakerChunkData chunkData)
        {
            Image = image;
            ChunkData = chunkData;
        }

        /// <summary>
        /// Gets the decoded image frame.
        /// </summary>
        public IplImage Image { get; private set; }

        /// <summary>
        /// Gets the chunk data which contains additional information about the image.
        /// </summary>
        public SpinnakerChunkData ChunkData { get; private set; }
    }
}
