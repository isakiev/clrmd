using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public static class ModuleUtil
  {
    public static readonly Regex InvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");
    
    public static IReadOnlyCollection<ClrInfo> GetVersions([NotNull] this IEnumerable<ModuleInfo> modules, Architecture architecture, out bool hasNativeRuntimes)
    {
      var versions = new List<ClrInfo>();
      hasNativeRuntimes = false;

      foreach (var module in modules)
      {
        if (ClrInfoProvider.IsSupportedRuntime(module, out var flavor))
          versions.Add(new ClrInfo(flavor, module, architecture));
        else if (ClrInfoProvider.IsNativeRuntime(module))
          hasNativeRuntimes = true;
      }

      return versions;
    }
  }
}