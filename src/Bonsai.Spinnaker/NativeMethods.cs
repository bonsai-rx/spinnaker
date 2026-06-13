using System;
using System.Runtime.InteropServices;

namespace Bonsai.Spinnaker
{
    static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr AddDllDirectory(string newDirectory);
    }
}
