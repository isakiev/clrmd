#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal struct IMAGE_RESOURCE_DATA_ENTRY
  {
    public int RvaToData;
    public int Size;
    public int CodePage;
    public int Reserved;
  }
}