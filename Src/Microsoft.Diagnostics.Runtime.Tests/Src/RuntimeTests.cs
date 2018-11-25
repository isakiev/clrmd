using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public class RuntimeTests
  {
    [Fact]
    public void RuntimeClrInfo()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var info = dt.ClrVersions.Single();
        var runtime = dt.CreateRuntime(info);

        Assert.Equal(info, runtime.ClrInfo);
      }
    }

    [Fact]
    public void ModuleEnumerationTest()
    {
      // This test ensures that we enumerate all modules in the process exactly once.

      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var expected = new HashSet<string>(new[] {"mscorlib.dll", "sharedlibrary.dll", "nestedexception.exe", "appdomains.exe"}, StringComparer.OrdinalIgnoreCase);
        var modules = new HashSet<ClrModule>();

        foreach (var module in runtime.Modules)
        {
          Assert.Contains(Path.GetFileName(module.FileName), expected);
          Assert.DoesNotContain(module, modules);
          modules.Add(module);
        }
      }
    }
  }
}