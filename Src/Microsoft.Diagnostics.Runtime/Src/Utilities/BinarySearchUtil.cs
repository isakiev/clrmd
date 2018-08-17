using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal static class BinarySearchUtil
  {
    public static int BinarySearch<TSource, TValue>(
      [NotNull] this IList<TSource> sources,
      [NotNull] TValue value,
      [NotNull] Func<TSource, TValue> selector,
      [CanBeNull] IComparer<TValue> comparer = null)
    {
      var min = 0;
      var max = sources.Count - 1;
      var localComparer = comparer ?? Comparer<TValue>.Default;
      while (min <= max)
      {
        var medianIndex = (min + max) >> 1; // (min + max) / 2
        var median = sources[medianIndex];
        var res = localComparer.Compare(selector(median), value);

        if (res < 0)
          min = medianIndex + 1;
        else if (res > 0)
          max = medianIndex - 1;
        else return medianIndex;
      }

      return ~min;
    }

    public static int BinarySearch<T>([NotNull] this IList<T> source, [NotNull] T value)
      where T : IComparable<T>
    {
      return source.BinarySearch(value, FuncUtil<T>.Identity);
    }

    public static int BinarySearch<T>([NotNull] this IList<T> source, [NotNull] T value, IComparer<T> comparer)
    {
      return source.BinarySearch(value, FuncUtil<T>.Identity, comparer);
    }
  }
}