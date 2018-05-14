using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  internal struct ImageFileHeader
  {
    public ushort Machine;
    public ushort NumberOfSections;
    public uint TimeDateStamp;
    public uint PointerToSymbolTable;
    public uint NumberOfSymbols;
    public ushort SizeOfOptionalHeader;
    public ushort Characteristics;
  }
}