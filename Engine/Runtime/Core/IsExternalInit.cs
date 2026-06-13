#if !NET5_0_OR_GREATER
// Polyfill for C# 9 init-only setters — Unity's Mono compiler doesn't provide this.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
