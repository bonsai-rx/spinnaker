using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Bonsai.Spinnaker
{
    /// <summary>
    /// Represents an operator that generates a sequence of images acquired from
    /// the specified FLIR Spinnaker camera.
    /// </summary>
    [XmlType(Namespace = Constants.XmlNamespace)]
    [Description("Acquires a sequence of images from a Spinnaker camera.")]
    public class SpinnakerCapture : Source<SpinnakerDataFrame>
    {
        static readonly object systemLock = new object();

        /// <summary>
        /// Gets or sets the optional index of the camera from which to acquire images.
        /// If no serial number and no index is specified, the first detected camera
        /// will be used.
        /// </summary>
        [Description("The optional index of the camera from which to acquire images.")]
        public int? Index { get; set; }

        /// <summary>
        /// Gets or sets the optional serial number of the camera from which to acquire images.
        /// If no serial number is specified, the index will be used for camera selection.
        /// </summary>
        [TypeConverter(typeof(SerialNumberConverter))]
        [Description("The optional serial number of the camera from which to acquire images.")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets a value specifying the method used to process bayer pattern encoded
        /// color images.
        /// </summary>
        [Description("The method used to process bayer pattern encoded color images.")]
        public ColorProcessingAlgorithm ColorProcessing { get; set; }

        /// <summary>
        /// Configures the acquisition mode on the selected camera.
        /// </summary>
        /// <param name="camera">The camera to configure.</param>
        /// <exception cref="InvalidOperationException">
        /// Unable to configure the camera acquisition mode.
        /// </exception>
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

        /// <summary>
        /// Generates an observable sequence of images acquired from the specified FLIR
        /// Spinnaker camera.
        /// </summary>
        /// <returns>
        /// A sequence of <see cref="SpinnakerDataFrame"/> objects representing each data
        /// frame acquired from the camera and the managed chunk data containing additional
        /// information about the image.
        /// </returns>
        public override IObservable<SpinnakerDataFrame> Generate()
        {
            return Generate(Observable.Return(Unit.Default));
        }

        /// <summary>
        /// Generates an observable sequence of images acquired from the specified FLIR
        /// Spinnaker camera, where the start of acquisition is controlled by the input
        /// sequence.
        /// </summary>
        /// <typeparam name="TSource">
        /// The type of the elements in the <paramref name="start"/> sequence.
        /// </typeparam>
        /// <param name="start">
        /// The sequence containing the notification used to start reading images
        /// from the Spinnaker camera.
        /// </param>
        /// <returns>
        /// A sequence of <see cref="SpinnakerDataFrame"/> objects representing each data
        /// frame acquired from the camera and the managed chunk data containing additional
        /// information about the image.
        /// </returns>
        public IObservable<SpinnakerDataFrame> Generate<TSource>(IObservable<TSource> start)
        {
            var captureInstance = (SpinnakerCapture)MemberwiseClone();
            return Observable.Create<SpinnakerDataFrame>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(async () =>
                {
                    IManagedCamera camera;
                    lock (systemLock)
                    {
                        try
                        {
                            using var system = new ManagedSystem();
                            var serialNumber = captureInstance.SerialNumber;
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
                                var index = captureInstance.Index.GetValueOrDefault(0);
                                if (index < 0 || index >= cameraList.Count)
                                {
                                    var message = string.Format("No Spinnaker camera was found at index {0}.", index);
                                    throw new InvalidOperationException(message);
                                }

                                camera = cameraList.GetByIndex((uint)index);
                            }

                            cameraList.Clear();
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            throw;
                        }
                    }

                    ImageEventListener imageListener = null;
                    try
                    {
                        camera.Init();
                        captureInstance.Configure(camera);

                        imageListener = new ImageEventListener(observer, captureInstance.ColorProcessing);
                        camera.RegisterEventHandler(imageListener);
                        await start.ToTask(cancellationToken);

                        camera.BeginAcquisition();
                        cancellationToken.WaitHandle.WaitOne();
                        camera.EndAcquisition();
                    }
                    catch (Exception ex) { observer.OnError(ex); throw; }
                    finally
                    {
                        if (imageListener is not null)
                            camera.UnregisterEventHandler(imageListener);
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
