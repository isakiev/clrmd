// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Microsoft.Diagnostics.Runtime
{
    internal enum DumpProcessorArchitecture : ushort
    {
        INTEL = 0,
        MIPS = 1,
        ALPHA = 2,
        PPC = 3,
        SHX = 4,
        ARM = 5,
        IA64 = 6,
        ALPHA64 = 7,
        MSIL = 8,
        AMD64 = 9,
        IA32_ON_WIN64 = 10
    }
}