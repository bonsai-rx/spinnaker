using SpinnakerNET;
using SpinnakerNET.GenApi;
using System.Collections.Generic;
using System.ComponentModel;

namespace Bonsai.Spinnaker
{
    class SerialNumberConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var values = new List<string>();
            using (var system = new ManagedSystem())
            {
                var cameraList = system.GetCameras();
                for (int i = 0; i < cameraList.Count; i++)
                {
                    var camera = cameraList[i];
                    var nodeMap = camera.GetTLDeviceNodeMap();
                    var serialNumberNode = nodeMap.GetNode<StringReg>("Std::DeviceSerialNumber");
                    if (serialNumberNode != null)
                    {
                        values.Add(serialNumberNode.Value);
                    }
                }
            }

            return new StandardValuesCollection(values);
        }
    }
}
