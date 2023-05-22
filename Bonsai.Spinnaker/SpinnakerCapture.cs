using OpenCV.Net;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Bonsai.Spinnaker
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OrderAttribute : Attribute
    {
        private readonly int _order;

        public OrderAttribute([CallerLineNumber]int order = 0)
        {
            this._order = order;
        }

         public int Order { get { return _order; } }

    }

    [XmlType(Namespace = Constants.XmlNamespace)]
    [TypeDescriptionProvider(typeof(SpinnakerCaptureTypeDescriptorProvider))]
    [Description("Acquires a sequence of images from a Spinnaker camera.")]
    public class SpinnakerCapture : Source<SpinnakerDataFrame>
    {
        static readonly object systemLock = new object();

        [Category("Camera")]
        [Order(0)]
        [Description("The optional index of the camera from which to acquire images.")]
        public int? Index { get; set; }

        [Category("Camera")]
        [Order(1)]
        [TypeConverter(typeof(SerialNumberConverter))]
        [Description("The optional serial number of the camera from which to acquire images.")]
        public string SerialNumber { get; set; }

        [Category("Camera")]
        [Order(2)]
        [Description("The method used to process bayer color images.")]
        public ColorProcessingAlgorithm ColorProcessing { get; set; }

        [Category("ROI AutoAlgorithmSelection")]
        [Order(0)]
        [DisplayName("AAS ROI")]
        [Description("Select algorithm for ROI")]
        public AutoAlgorithmSelector AutoAlgorithmSelector { get; set; }

        [Category("ROI AutoAlgorithmSelection")]
        [Order(1)]
        [DisplayName("AAS ROI Enable")]
        [Description("Enable user defined Auto Algorithm ROI selection")]
        public bool AasRoiEnable { get; set; }

        [Category("ROI AutoAlgorithmSelection")]
        [Order(2)]
        [DisplayName("AAS ROI OffsetX")]
        [Description("Auto algorithm selected ROI X offset")]
        public int AasRoiOffsetX { get; set; }

        [Category("ROI AutoAlgorithmSelection")]
        [Order(3)]
        [DisplayName("AAS ROI OffsetY")]
        [Description("Auto algorithm selected ROI Y offset")]
        public int AasRoiOffsetY { get; set; }

        [Category("ROI AutoAlgorithmSelection")]
        [Order(4)]
        [DisplayName("AAS ROI Width")]
        [Description("Auto algorithm selected ROI width")]
        public int AasRoiWidth { get; set; }

        [Category("ROI AutoAlgorithmSelection")]
        [Order(5)]
        [DisplayName("AAS ROI Height")]
        [Description("Auto algorithm selected ROI height")]
        public int AasRoiHeight { get; set; }

        [Category("ROI")]
        [Order(0)]
        [DisplayName("OffsetX")]
        [Description("X offset from the origin to the ROI")]
        public int OffsetX { get; set; }

        [Category("ROI")]
        [Order(1)]
        [DisplayName("OffsetY")]
        [Description("Y offset from the origin to the ROI")]
        public int OffsetY { get; set; }

        [Category("ROI")]
        [Order(2)]
        [DisplayName("Width")]
        [Description("Image width")]
        public int Width { get; set; }

        [Category("ROI")]
        [Order(3)]
        [DisplayName("Height")]
        [Description("Image height")]
        public int Height { get; set; }

        protected virtual void Configure(IManagedCamera camera)
        {
            INodeMap nodeMap = camera.GetNodeMap();
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

            setAasRoiSelector(nodeMap);
            setCameraAasRoi(camera);

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

        private void setAasRoiSelector(INodeMap nodeMap)
        {
            IEnum algorithmSelectorNode = nodeMap.GetNode<IEnum>("AutoAlgorithmSelector");
            if (algorithmSelectorNode != null && algorithmSelectorNode.IsWritable)
            {
                var algorithmSelectorValue = algorithmSelectorNode.GetEntryByName(Algorithm(AutoAlgorithmSelector).ToString());
                if (algorithmSelectorValue != null && algorithmSelectorValue.IsReadable)
                {
                    Console.WriteLine("Write algorithm {0}", AutoAlgorithmSelector);
                    algorithmSelectorNode.Value = algorithmSelectorValue.Symbolic;
                }
                else
                {
                    Console.WriteLine("Cannot Write algorithm selector");
                }
            }
            else
            {
                Console.WriteLine("Algorithm selector node invalid");
            }
        }

        private void setCameraAasRoi(IManagedCamera camera)
        {
            updateEnableROI(camera);
            if (AasRoiEnable)
            {
                setIntNodeValue(camera.AasRoiOffsetX, AasRoiOffsetX, "AAS ROI X offset");
                setIntNodeValue(camera.AasRoiOffsetY, AasRoiOffsetY, "AAS ROI Y offset");
                setIntNodeValue(camera.AasRoiWidth, AasRoiWidth, "AAS ROI width");
                setIntNodeValue(camera.AasRoiHeight, AasRoiHeight, "AAS ROI height");
            }
        }

        private void updateEnableROI(IManagedCamera camera)
        {
            IBool roiEnableNode = camera.AasRoiEnable;
            if (roiEnableNode == null)
            {
                throw new InvalidOperationException("ROI enable is not supported");
            }
            else if (roiEnableNode.IsWritable)
            {
                Console.WriteLine("Enable ROI");
                roiEnableNode.Value = AasRoiEnable;
            }
        }

        private void setCameraOffset(IManagedCamera camera)
        {
            setIntNodeValue(camera.OffsetX, OffsetX, "X offset");
            setIntNodeValue(camera.OffsetY, OffsetY, "Y offset");
        }

        private void setImageSize(IManagedCamera camera)
        {
            setIntNodeValue(camera.Width, Width, "Image width");
            setIntNodeValue(camera.Height, Height, "Image height");
        }

        private AutoAlgorithmSelectorEnums Algorithm(AutoAlgorithmSelector algorithmSelector)
        {
            switch (algorithmSelector)
            {
                case AutoAlgorithmSelector.AutoWhitebalance:
                    return AutoAlgorithmSelectorEnums.Awb;
                case AutoAlgorithmSelector.AutoExposure:
                    return AutoAlgorithmSelectorEnums.Ae;
                default:
                    return (AutoAlgorithmSelectorEnums)algorithmSelector;
            }
        }

        private void setIntNodeValue(IInteger n, int val, string nodeInfo)
        {
            if (n == null)
            {
                throw new InvalidOperationException(nodeInfo + " not supported");
            }
            else if (n.IsWritable)
            {
                Console.WriteLine("Set {0} = {1}", nodeInfo, val);
                n.Value = val;
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
                        image.ConvertToWriteAbleBitmap(outputFormat, destination, (SpinnakerNET.ColorProcessingAlgorithm)colorProcessing);
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
                        try
                        {
                            using (var system = new ManagedSystem())
                            {
                                var serialNumber = SerialNumber;
                                var cameraList = system.GetCameras();
                                if (!string.IsNullOrEmpty(serialNumber))
                                {
                                    camera = cameraList.GetBySerial(serialNumber);
                                    if (camera == null)
                                    {
                                        var message = string.Format("Spinnaker camera with serial number {0} was not found.", serialNumber);
                                        throw new InvalidOperationException(message);
                                    }
                                }
                                else
                                {
                                    var index = Index.GetValueOrDefault(0);
                                    if (index < 0 || index >= cameraList.Count)
                                    {
                                        var message = string.Format("No Spinnaker camera was found at index {0}.", index);
                                        throw new InvalidOperationException(message);
                                    }

                                    camera = cameraList.GetByIndex((uint)index);
                                }

                                cameraList.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            throw;
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
                        using (var cancellation = cancellationToken.Register(camera.EndAcquisition))
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                setCameraAasRoi(camera);
                                setCameraOffset(camera);
                                setImageSize(camera);
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
                    }
                    catch (Exception ex) { observer.OnError(ex); throw; }
                    finally
                    {
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

    public class SpinnakerCaptureTypeDescriptorProvider : TypeDescriptionProvider
    {
        private static TypeDescriptionProvider defaultTypeDescriptorProvider =
                       TypeDescriptor.GetProvider(typeof(SpinnakerCapture));

        public SpinnakerCaptureTypeDescriptorProvider() : base(defaultTypeDescriptorProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType,
                                                                object instance)
        {
            ICustomTypeDescriptor defaultDescriptor =
                                  base.GetTypeDescriptor(objectType, instance);
            return new SpinnakerCaptureTypeDescriptor(defaultDescriptor, (SpinnakerCapture)instance);
        }

    }

    public class SpinnakerCaptureTypeDescriptor : CustomTypeDescriptor
    {
        private SpinnakerCapture spinnakerCaptureInstance;

        public SpinnakerCaptureTypeDescriptor(ICustomTypeDescriptor parent, SpinnakerCapture instance) : base(parent)
        {
            this.spinnakerCaptureInstance = (SpinnakerCapture)instance;
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var properties = new PropertyDescriptorCollection(
                base.GetProperties(attributes).Cast<PropertyDescriptor>()
                .OrderBy(p => p.Category)
                .ThenBy(p => {
                    OrderAttribute a = p.Attributes.OfType<OrderAttribute>().SingleOrDefault();
                    Console.WriteLine("Attrbibute {0} -> {1}", p.DisplayName, a.Order);
                    return a.Order;
                 })
                .ToArray());
            var n = properties.Count;
            Console.WriteLine("!!!!! ALL: {0}", n);

            for (int i = 0; i < n; i++)
            {
                Console.WriteLine("{0}:{1}", properties[i].Category, properties[i].DisplayName);
            }
            return properties;
        }

    }

}
