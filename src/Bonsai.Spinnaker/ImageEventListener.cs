using OpenCV.Net;
using SpinnakerNET;
using System;

namespace Bonsai.Spinnaker
{
    class ImageEventListener(IObserver<SpinnakerDataFrame> observer, ColorProcessingAlgorithm colorProcessing) : ManagedImageEventHandler
    {
        readonly IObserver<SpinnakerDataFrame> observer = observer ?? throw new ArgumentNullException(nameof(observer));
        readonly ColorProcessingAlgorithm colorProcessing = colorProcessing;
        Func<IManagedImage, IplImage> converter;
        PixelFormatEnums pixelFormat;

        protected override void OnImageEvent(ManagedImage image)
        {
            try
            {
                if (image.IsIncomplete)
                    return;

                if (converter == null || image.PixelFormat != pixelFormat)
                {
                    converter = GetConverter(image.PixelFormat, colorProcessing);
                    pixelFormat = image.PixelFormat;
                }

                var output = converter(image);
                observer.OnNext(new SpinnakerDataFrame(output, new SpinnakerChunkData(image.ChunkData)));
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                throw;
            }
            finally
            {
                image.Release();
            }
        }

        static Func<IManagedImage, IplImage> GetConverter(PixelFormatEnums pixelFormat, ColorProcessingAlgorithm colorProcessing)
        {
            int outputChannels;
            IplDepth outputDepth;
            if (pixelFormat < PixelFormatEnums.BayerGR8 || pixelFormat == PixelFormatEnums.BGR8 ||
                pixelFormat <= PixelFormatEnums.BayerBG16 && colorProcessing == ColorProcessingAlgorithm.NoColorProcessing)
            {
                if (pixelFormat == PixelFormatEnums.BGR8)
                {
                    outputChannels = 3;
                    outputDepth = IplDepth.U8;
                }
                else
                {
                    outputChannels = 1;
                    var depthFactor = (int)pixelFormat;
                    if (pixelFormat > PixelFormatEnums.Mono16) depthFactor = (depthFactor - 3) / 4;
                    outputDepth = (IplDepth)(8 * (depthFactor + 1));
                }

                return image =>
                {
                    var width = (int)image.Width;
                    var height = (int)image.Height;

                    using var bitmapHeader = new IplImage(new Size(width, height), outputDepth, outputChannels, image.DataPtr);
                    var output = new IplImage(bitmapHeader.Size, outputDepth, outputChannels);
                    CV.Copy(bitmapHeader, output);
                    return output;
                };
            }

            PixelFormatEnums outputFormat;
            if (pixelFormat == PixelFormatEnums.Mono10p ||
                pixelFormat == PixelFormatEnums.Mono10Packed ||
                pixelFormat == PixelFormatEnums.Mono12p ||
                pixelFormat == PixelFormatEnums.Mono12Packed)
            {
                outputFormat = PixelFormatEnums.Mono16;
                outputDepth = IplDepth.U16;
                outputChannels = 1;
            }
            else if (pixelFormat >= PixelFormatEnums.BayerGR8 && pixelFormat <= PixelFormatEnums.BayerBG16)
            {
                outputFormat = PixelFormatEnums.BGR8;
                outputDepth = IplDepth.U8;
                outputChannels = 3;
            }
            else throw new InvalidOperationException(string.Format("Unable to convert pixel format {0}.", pixelFormat));

            return image =>
            {
                var width = (int)image.Width;
                var height = (int)image.Height;
                var output = new IplImage(new Size(width, height), outputDepth, outputChannels);
                unsafe
                {
                    using var destination = new ManagedImage((uint)width, (uint)height, 0, 0, outputFormat, output.ImageData.ToPointer());
                    image.ConvertToBitmapSource(outputFormat, destination, (SpinnakerNET.ColorProcessingAlgorithm)colorProcessing);
                    return output;
                }
            };
        }
    }
}
