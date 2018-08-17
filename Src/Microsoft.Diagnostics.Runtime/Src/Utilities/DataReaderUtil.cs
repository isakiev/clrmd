using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public static class DataReaderUtil
  {
    public static IReadOnlyCollection<ModuleInfo> GetSortedModules([NotNull] this IDataReader dataReader)
    {
      var sortedModules = new List<ModuleInfo>(dataReader.GetModulesWithoutInvalidChars());
      sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
      return sortedModules.ToArray();
    }

    private static IEnumerable<ModuleInfo> GetModulesWithoutInvalidChars(this IDataReader dataReader)
    {
      return dataReader.EnumerateModules().Where(m => !ModuleUtil.InvalidChars.IsMatch(m.FileName));
    }

    public static IReadOnlyCollection<ClrInfo> GetVersions([NotNull] this IDataReader reader)
    {
      var modules = reader.GetModulesWithoutInvalidChars();
      var architecture = reader.GetArchitecture();
      return modules.GetVersions(architecture, out _);
    }
  }
}