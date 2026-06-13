#if NETFRAMEWORK
using System;

namespace System.Runtime.CompilerServices
{
    // Defined to enable module initializers on this target.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
