using System;
using JetBrains.Annotations;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal static class FuncUtil<T>
  {
    /// <summary>
    /// Identity function that always returns the same value that was used as its argument.
    /// </summary>
    [NotNull] public static readonly Func<T, T> Identity = t => t;
    
  }
}