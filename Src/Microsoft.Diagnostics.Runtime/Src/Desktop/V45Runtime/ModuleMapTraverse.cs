using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]
  internal delegate void ModuleMapTraverse(uint index, ulong methodTable, IntPtr token);
}