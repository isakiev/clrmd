using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct HandleData
  {
    public ulong AppDomain;
    public ulong Handle;
    public ulong Secondary;
    public uint Type;
    public uint StrongReference;

    // For RefCounted Handles
    public uint RefCount;
    public uint JupiterRefCount;
    public uint IsPegged;
  }
}