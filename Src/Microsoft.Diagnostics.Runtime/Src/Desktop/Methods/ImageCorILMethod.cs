using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct ImageCorILMethod
  {
    public uint FlagsSizeStack;
    public uint CodeSize;
    public uint LocalVarSignatureToken;

    public const uint FormatShift = 3;
    public const uint FormatMask = (uint)(1 << (int)FormatShift) - 1;
    public const uint TinyFormat = 0x2;
    public const uint mdSignatureNil = 0x11000000;
  }
}