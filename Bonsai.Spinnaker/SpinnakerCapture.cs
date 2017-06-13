using OpenCV.Net;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Spinnaker
{
    public class SpinnakerCapture : Source<IplImage>
    {
        IObservable<IplImage> source;
        readonly object captureLock = new object();

        public SpinnakerCapture()
        {
            source = Observable.Create<IplImage>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    lock (captureLock)
                    {
                        IManagedCamera camera;
                        using (var system = new ManagedSystem())
                        {
                            var index = Index;
                            var cameraList = system.GetCameras();
                            if (index < 0 || index >= cameraList.Count)
                            {
                                throw new InvalidOperationException("No Spinnaker camera with the specified index was found.");
                            }

                            camera = cameraList[index];
                        }

                        try
                        {
                            camera.Init();

                            var nodeMap = camera.GetNodeMap();
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
                            camera.BeginAcquisition();

                            while (!cancellationToken.IsCancellationRequested)
                            {
                                using (var image = camera.GetNextImage())
                                {
                                    if (image.IsIncomplete)
                                    {
                                        var message = string.Format("Acquired incomplete image with image status {0}", image.ImageStatus);
                                        throw new InvalidOperationException(message);
                                    }

                                    IplImage output;
                                    var width = (int)image.Width;
                                    var height = (int)image.Height;
                                    if (image.PixelFormat == PixelFormatEnums.Mono8 ||
                                        image.PixelFormat == PixelFormatEnums.Mono16)
                                    {
                                        unsafe
                                        {
                                            var depth = image.PixelFormat == PixelFormatEnums.Mono16 ? IplDepth.U16 : IplDepth.U8;
                                            var bitmapHeader = new IplImage(new Size(width, height), depth, 1, image.DataPtr);
                                            output = new IplImage(bitmapHeader.Size, bitmapHeader.Depth, bitmapHeader.Channels);
                                            CV.Copy(bitmapHeader, output);
                                        }
                                    }
                                    else
                                    {
                                        unsafe
                                        {
                                            output = new IplImage(new Size(width, height), IplDepth.U8, 3);
                                            using (var bitmapHeader = new ManagedImage(image.Width, image.Height, 0, 0, image.PixelFormat, output.ImageData.ToPointer()))
                                            {
                                                image.ConvertToBitmapSource(PixelFormatEnums.BGR8, bitmapHeader);
                                            }
                                        }
                                    }

                                    observer.OnNext(output);
                                }
                            }
                        }
                        finally
                        {
                            camera.EndAcquisition();
                            camera.DeInit();
                            camera.Dispose();
                        }
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            })
            .PublishReconnectable()
            .RefCount();
        }

        public int Index { get; set; }

        public override IObservable<IplImage> Generate()
        {
            return source;
        }
    }
}
