using OpenCV.Net;
using SpinnakerNET;

namespace Bonsai.Spinnaker
{
    public class SpinnakerDataFrame
    {
        public SpinnakerDataFrame(IplImage image, ManagedChunkData chunkData)
        {
            Image = image;
            ChunkData = chunkData;
        }

        public IplImage Image { get; private set; }

        public ManagedChunkData ChunkData { get; private set; }
    }
}
