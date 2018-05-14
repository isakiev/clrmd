#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal unsafe struct IMAGE_RESOURCE_DIRECTORY_ENTRY
  {
    public bool IsStringName => _nameOffsetAndFlag < 0;
    public int NameOffset => _nameOffsetAndFlag & 0x7FFFFFFF;

    public bool IsLeaf => (0x80000000 & _dataOffsetAndFlag) == 0;
    public int DataOffset => _dataOffsetAndFlag & 0x7FFFFFFF;
    public int Id => 0xFFFF & _nameOffsetAndFlag;

    private int _nameOffsetAndFlag;
    private int _dataOffsetAndFlag;

    internal string GetName(PEBuffer buff, int resourceStartFileOffset)
    {
      if (IsStringName)
      {
        int nameLen = *((ushort*)buff.Fetch(NameOffset + resourceStartFileOffset, 2));
        var namePtr = (char*)buff.Fetch(NameOffset + resourceStartFileOffset + 2, nameLen);
        return new string(namePtr);
      }

      return Id.ToString();
    }

    internal static string GetTypeNameForTypeId(int typeId)
    {
      switch (typeId)
      {
        case 1:
          return "Cursor";
        case 2:
          return "BitMap";
        case 3:
          return "Icon";
        case 4:
          return "Menu";
        case 5:
          return "Dialog";
        case 6:
          return "String";
        case 7:
          return "FontDir";
        case 8:
          return "Font";
        case 9:
          return "Accelerator";
        case 10:
          return "RCData";
        case 11:
          return "MessageTable";
        case 12:
          return "GroupCursor";
        case 14:
          return "GroupIcon";
        case 16:
          return "Version";
        case 19:
          return "PlugPlay";
        case 20:
          return "Vxd";
        case 21:
          return "Aniicursor";
        case 22:
          return "Aniicon";
        case 23:
          return "Html";
        case 24:
          return "RT_MANIFEST";
      }

      return typeId.ToString();
    }
  }
}