using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  internal class RawPinvokes
  {
    [DllImport("kernel32.dll")]
    internal static extern int ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out int lpNumberOfBytesRead);
  }
}