using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Bonsai.Spinnaker
{
    /// <summary>
    /// Resolves the SpinnakerNET managed assembly against the Spinnaker SDK installed
    /// on the local machine, rather than a version bundled with this package.
    /// </summary>
    /// <remarks>
    /// The resolver is registered from a module initializer so it is in place before any
    /// type or member of this assembly is used, including any types derived from them
    /// that reference SpinnakerNET. This type references no SpinnakerNET types itself, so
    /// registering the resolver does not itself trigger a load.
    /// </remarks>
    static class SpinnakerRuntime
    {
        // The "_v140" suffix identifies the MSVC v140 (Visual Studio 2015) C++ runtime
        // the mixed-mode assembly was built against, not the SDK version.
        const string AssemblyName = "SpinnakerNET_v140";
        const string AssemblyFileName = AssemblyName + ".dll";
        const string PathEnvironmentVariable = "PATH";

        static Assembly spinnakerAssembly;

        [ModuleInitializer]
        internal static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSpinnakerAssembly;
        }

        static Assembly ResolveSpinnakerAssembly(object sender, ResolveEventArgs args)
        {
            var requestedName = new AssemblyName(args.Name).Name;
            if (!string.Equals(requestedName, AssemblyName, StringComparison.OrdinalIgnoreCase))
                return null;

            if (spinnakerAssembly != null)
                return spinnakerAssembly;

            var directory = GetBinDirectory();
            if (directory == null)
                return null;

            AddLibraryPath(directory);
            var assemblyPath = Path.Combine(directory, AssemblyFileName);
            if (!File.Exists(assemblyPath))
                return null;

            return spinnakerAssembly = Assembly.LoadFrom(assemblyPath);
        }

        static string GetBinDirectory()
        {
            var installDirectory = GetInstallDirectory();
            if (installDirectory == null)
                return null;

            var subFolder = Environment.Is64BitProcess
                ? Path.Combine("bin64", "vs2015")
                : Path.Combine("bin", "vs2015");
            var candidate = Path.Combine(installDirectory, subFolder);
            return File.Exists(Path.Combine(candidate, AssemblyFileName)) ? candidate : null;
        }

        static string GetInstallDirectory()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            // The install path is recorded under the registry view matching the process
            // bitness. Probe the current Teledyne vendor key, then the legacy FLIR Systems
            // key used by older Spinnaker installations.
            var view = Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32;
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            return ReadInstallDirectory(baseKey, @"SOFTWARE\Teledyne\Spinnaker")
                ?? ReadInstallDirectory(baseKey, @"SOFTWARE\FLIR Systems\Spinnaker");
        }

        static string ReadInstallDirectory(RegistryKey baseKey, string subKeyName)
        {
            using var key = baseKey.OpenSubKey(subKeyName);
            var installDirectory = key?.GetValue("InstallDir") as string;
            return string.IsNullOrEmpty(installDirectory) ? null : installDirectory;
        }

        static void AddLibraryPath(string path)
        {
            var currentPath = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
            if (currentPath == null || !currentPath.Contains(path))
            {
                currentPath = string.Join(Path.PathSeparator.ToString(), path, currentPath);
                Environment.SetEnvironmentVariable(PathEnvironmentVariable, currentPath);
            }

            NativeMethods.AddDllDirectory(path);
        }
    }
}
