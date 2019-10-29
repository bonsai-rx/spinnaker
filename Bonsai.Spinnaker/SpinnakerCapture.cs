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
                    using (var bitmapHeader = new IplImage(new Size(width, height), outputDepth, outputChannels, image.DataPtr))
                    {
                        var output = new IplImage(bitmapHeader.Size, outputDepth, outputChannels);
                        CV.Copy(bitmapHeader, output);
                        return output;
                    }
                };
            }

            PixelFormatEnums outputFormat;
            if (pixelFormat == PixelFormatEnums.Mono12p ||
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
                    using (var destination = new ManagedImage((uint)width, (uint)height, 0, 0, outputFormat, output.ImageData.ToPointer()))
                    {
                        image.Convert(destination, outputFormat, (SpinnakerNET.ColorProcessingAlgorithm)colorProcessing);
                        return output;
                    }
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
