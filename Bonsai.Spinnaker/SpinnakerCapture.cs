using OpenCV.Net;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Spinnaker
{
    [Description("Acquires a sequence of images from a Spinnaker camera.")]
    public class SpinnakerCapture : Source<SpinnakerDataFrame>
    {
        static readonly object systemLock = new object();

        [Description("The index of the camera from which to acquire images.")]
        public int Index { get; set; }

        [Description("The method used to process bayer color images.")]
        public ColorProcessingAlgorithm ColorProcessing { get; set; }

        protected virtual void Configure(IManagedCamera camera)
        {
            var nodeMap = camera.GetNodeMap();
            var chunkMode = nodeMap.GetNode<IBool>("ChunkModeActive");
            if (chunkMode != null && chunkMode.IsWritable)
            {
                chunkMode.Value = true;
                var chunkSelector = nodeMap.GetNode<IEnum>("ChunkSelector");
                if (chunkSelector != null && chunkSelector.IsReadable)
                {
                    var entries = chunkSelector.Entries;
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var chunkSelectorEntry = entries[i];
                        if (!chunkSelectorEntry.IsAvailable || !chunkSelectorEntry.IsReadable) continue;

                        chunkSelector.Value = chunkSelectorEntry.Value;
                        var chunkEnable = nodeMap.GetNode<IBool>("ChunkEnable");
                        if (chunkEnable == null || chunkEnable.Value || !chunkEnable.IsWritable) continue;
                        chunkEnable.Value = true;
                    }
                }
            }

            var acquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
            if (acquisitionMode == null || !acquisitionMode.IsWritable)
            {
                throw new InvalidOperationException("Unable to set acquisition mode to continuous.");
            }

            var continuousAcquisitionMode = acquisitionMode.GetEntryByName("Continuous");
            if (continuousAcquisitionMode == null || !continuousAcquisitionMode.IsReadable)
            {
                throw new InvalidOperationException("Unable to set acquisition mode to continuous.");
            }

            acquisitionMode.Value = continuousAcquisitionMode.Symbolic;
        }

        static Func<IManagedImage, IplImage> GetConverter(PixelFormatEnums pixelFormat, ColorProcessingAlgorithm colorProcessing)
        {
            ColorConversion? conversion;
            IplDepth sourceDepth, outputDepth;
            int sourceChannels, outputChannels;
            if (pixelFormat == PixelFormatEnums.Mono8 ||
                pixelFormat == PixelFormatEnums.BayerGR8 ||
                pixelFormat == PixelFormatEnums.BayerRG8 ||
                pixelFormat == PixelFormatEnums.BayerGB8 ||
                pixelFormat == PixelFormatEnums.BayerBG8 ||
                pixelFormat == PixelFormatEnums.BGR8)
            {
                sourceDepth = outputDepth = IplDepth.U8;
                if (pixelFormat == PixelFormatEnums.Mono8)
                {
                    sourceChannels = outputChannels = 1;
                    conversion = null;
                }
                else if (pixelFormat == PixelFormatEnums.BGR8)
                {
                    sourceChannels = outputChannels = 3;
                    conversion = null;
                }
                else
                {
                    sourceChannels = outputChannels = 1;
                    if (colorProcessing == ColorProcessingAlgorithm.NoColorProcessing) conversion = null;
                    else
                    {
                        outputChannels = 3;
                        var conversionOffset = (5 - (int)(pixelFormat - PixelFormatEnums.BayerGR8)) % 4;
                        conversion = ColorConversion.BayerRG2Rgb + conversionOffset;
                    }
                }
            }
            else if (pixelFormat == PixelFormatEnums.Mono16)
            {
                sourceDepth = outputDepth = IplDepth.U16;
                sourceChannels = outputChannels = 1;
                conversion = null;
            }
            else throw new InvalidOperationException(string.Format("Unable to convert pixel format {0}.", pixelFormat));

            return image =>
            {
                var width = (int)image.Width;
                var height = (int)image.Height;
                using (var bitmapHeader = new IplImage(new Size(width, height), sourceDepth, sourceChannels, image.DataPtr))
                {
                    var output = new IplImage(bitmapHeader.Size, outputDepth, outputChannels);
                    if (conversion.HasValue) CV.CvtColor(bitmapHeader, output, conversion.Value);
                    else CV.Copy(bitmapHeader, output);
                    return output;
                }
            };
        }

        public override IObservable<SpinnakerDataFrame> Generate()
        {
            return Generate(Observable.Return(Unit.Default));
        }

        public IObservable<SpinnakerDataFrame> Generate<TSource>(IObservable<TSource> start)
        {
            return Observable.Create<SpinnakerDataFrame>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(async () =>
                {
                    IManagedCamera camera;
                    lock (systemLock)
                    {
                        using (var system = new ManagedSystem())
                        {
                            var index = Index;
                            var cameraList = system.GetCameras();
                            if (index < 0 || index >= cameraList.Count)
                            {
                                throw new InvalidOperationException("No Spinnaker camera with the specified index was found.");
                            }

                            camera = cameraList[index];
                            cameraList.Clear();
                        }
                    }

                    try
                    {
                        camera.Init();
                        Configure(camera);
                        camera.BeginAcquisition();
                        await start;

                        var imageFormat = default(PixelFormatEnums);
                        var converter = default(Func<IManagedImage, IplImage>);
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            using (var image = camera.GetNextImage())
                            {
                                if (image.IsIncomplete)
                                {
                                    // drop incomplete frames
                                    continue;
                                }

                                if (converter == null || image.PixelFormat != imageFormat)
                                {
                                    converter = GetConverter(image.PixelFormat, ColorProcessing);
                                    imageFormat = image.PixelFormat;
                                }

                                var output = converter(image);
                                observer.OnNext(new SpinnakerDataFrame(output, image.ChunkData));
                            }
                        }
                    }
                    catch (SEHException ex) { observer.OnError(ex); throw; }
                    catch (InvalidOperationException ex) { observer.OnError(ex); throw; }
                    finally
                    {
                        camera.EndAcquisition();
                        camera.DeInit();
                        camera.Dispose();
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            });
        }
    }
}
