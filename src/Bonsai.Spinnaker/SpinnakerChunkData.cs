using SpinnakerNET;

namespace Bonsai.Spinnaker
{
    /// <summary>
    /// Represents the chunk data containing additional information about an image
    /// acquired from a Spinnaker camera.
    /// </summary>
    public class SpinnakerChunkData
    {
        readonly ManagedChunkData chunkData;

        internal SpinnakerChunkData(ManagedChunkData chunkData)
        {
            this.chunkData = chunkData;
        }

        /// <summary>
        /// Gets the sequential identifier of the acquired image frame.
        /// </summary>
        public long FrameID => chunkData.FrameID;

        /// <summary>
        /// Gets the timestamp at which the image frame was acquired.
        /// </summary>
        public long Timestamp => chunkData.Timestamp;

        /// <summary>
        /// Gets the value of the timestamp latch at the time the image frame was acquired.
        /// </summary>
        public long TimestampLatchValue => chunkData.TimestampLatchValue;

        /// <summary>
        /// Gets the value of the selected counter at the time the image frame was acquired.
        /// </summary>
        public long CounterValue => chunkData.CounterValue;

        /// <summary>
        /// Gets the value of the selected timer at the time the image frame was acquired,
        /// in microseconds.
        /// </summary>
        public double TimerValue => chunkData.TimerValue;

        /// <summary>
        /// Gets the value of the selected encoder at the time the image frame was acquired.
        /// </summary>
        public long EncoderValue => chunkData.EncoderValue;

        /// <summary>
        /// Gets the exposure time used to acquire the image frame, in microseconds.
        /// </summary>
        public double ExposureTime => chunkData.ExposureTime;

        /// <summary>
        /// Gets the gain applied when acquiring the image frame.
        /// </summary>
        public double Gain => chunkData.Gain;

        /// <summary>
        /// Gets the black level applied when acquiring the image frame.
        /// </summary>
        public double BlackLevel => chunkData.BlackLevel;

        /// <summary>
        /// Gets the width of the acquired image frame, in pixels.
        /// </summary>
        public long Width => chunkData.Width;

        /// <summary>
        /// Gets the height of the acquired image frame, in pixels.
        /// </summary>
        public long Height => chunkData.Height;

        /// <summary>
        /// Gets the horizontal offset of the acquired image frame from the sensor origin, in pixels.
        /// </summary>
        public long OffsetX => chunkData.OffsetX;

        /// <summary>
        /// Gets the vertical offset of the acquired image frame from the sensor origin, in pixels.
        /// </summary>
        public long OffsetY => chunkData.OffsetY;

        /// <summary>
        /// Gets the minimum value of the dynamic range of the acquired image frame.
        /// </summary>
        public long PixelDynamicRangeMin => chunkData.PixelDynamicRangeMin;

        /// <summary>
        /// Gets the maximum value of the dynamic range of the acquired image frame.
        /// </summary>
        public long PixelDynamicRangeMax => chunkData.PixelDynamicRangeMax;

        /// <summary>
        /// Gets the status of all the input and output lines at the end of exposure of the image frame.
        /// </summary>
        public long ExposureEndLineStatusAll => chunkData.ExposureEndLineStatusAll;

        /// <summary>
        /// Gets the status of all the input and output lines at the time the image frame was acquired.
        /// </summary>
        public long LineStatusAll => chunkData.LineStatusAll;

        /// <summary>
        /// Gets the cyclic redundancy check value used to validate the integrity of the image frame.
        /// </summary>
        public long CRC => chunkData.CRC;
    }
}
