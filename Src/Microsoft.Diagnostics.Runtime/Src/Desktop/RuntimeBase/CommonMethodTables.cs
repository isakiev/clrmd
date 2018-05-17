#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  // This is consistent across all dac versions.  No need for interface.
  internal struct CommonMethodTables
  {
    public ulong ArrayMethodTable;
    public ulong StringMethodTable;
    public ulong ObjectMethodTable;
    public ulong ExceptionMethodTable;
    public ulong FreeMethodTable;

    public bool Validate()
    {
      return ArrayMethodTable != 0 &&
        StringMethodTable != 0 &&
        ObjectMethodTable != 0 &&
        ExceptionMethodTable != 0 &&
        FreeMethodTable != 0;
    }
  }
}