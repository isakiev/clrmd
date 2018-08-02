using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal class FileEntry : IEquatable<FileEntry>
  {
    public readonly string FileName;
    public readonly int TimeStamp;
    public readonly int FileSize;

    public FileEntry(string filename, int timestamp, int filesize)
    {
      FileName = filename ?? throw new ArgumentNullException(nameof(filename));
      TimeStamp = timestamp;
      FileSize = filesize;
    }

    public bool Equals(FileEntry other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return string.Equals(FileName, other.FileName) && TimeStamp == other.TimeStamp && FileSize == other.FileSize;
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != GetType()) return false;
      return Equals((FileEntry) obj);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        var hashCode = FileName.GetHashCode();
        hashCode = (hashCode * 397) ^ TimeStamp;
        hashCode = (hashCode * 397) ^ FileSize;
        return hashCode;
      }
    }
  }
}