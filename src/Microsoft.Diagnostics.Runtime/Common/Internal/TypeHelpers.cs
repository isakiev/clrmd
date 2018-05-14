using System;

namespace Microsoft.Diagnostics.Runtime
{
  internal static class TypeHelpers
  {
    internal static Guid GetGuid(this Type self)
    {
      return self.GUID;
    }
  }
}