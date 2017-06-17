using OpenCV.Net;
using SpinnakerNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
