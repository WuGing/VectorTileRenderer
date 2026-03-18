#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    // Enables C# init-only setters when targeting netstandard2.0.
    internal static class IsExternalInit
    {
    }
}
#endif
