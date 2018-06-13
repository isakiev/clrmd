using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Simple
{
  internal static class DumpUtil
  {
    public static BinaryReader Seek(this BinaryReader reader, ContentPosition position)
    {
      reader.BaseStream.Seek(position.Value, SeekOrigin.Begin);
      return reader;
    }

    public static ContentPosition GetPosition(this BinaryReader reader)
    {
      return new ContentPosition(reader.BaseStream.Position);
    }
    
    public static T ReadStructure<T>(this BinaryReader reader)
    {
      var bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
      var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      try
      {
        return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
      }
      finally
      {
        handle.Free();
      }
    }
  }
}