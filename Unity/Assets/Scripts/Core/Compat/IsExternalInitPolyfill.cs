// Polyfill IsExternalInit: core records use init accessors. Unity's runtime
// profile doesn't provide this type; it exists in the BCL of net8.0
// (MonoGame build), so this file is only compiled by Unity
// (excluded from Aethelgard.csproj along with all of Compat/).
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
