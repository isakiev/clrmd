using System;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Simple
{
  internal struct ContentPosition : IEquatable<ContentPosition>
  {
    public readonly long Value;

    public ContentPosition(long value)
    {
      if (value < 0)
        throw new ApplicationException("Position can't be negative");

      Value = value;
    }

    public bool Equals(ContentPosition other)
    {
      return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;

      return obj is ContentPosition position && Equals(position);
    }

    public override int GetHashCode()
    {
      return Value.GetHashCode();
    }

    public static bool operator ==(ContentPosition left, ContentPosition right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(ContentPosition left, ContentPosition right)
    {
      return !left.Equals(right);
    }
  }
}