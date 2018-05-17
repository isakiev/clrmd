using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class AppDomainTests
  {
    [TestMethod]
    public void ModuleDomainTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var appDomainExe = runtime.GetDomainByName("AppDomains.exe");
        var nestedDomain = runtime.GetDomainByName("Second AppDomain");

        var mscorlib = runtime.GetModule("mscorlib.dll");
        AssertModuleContainsDomains(mscorlib, runtime.SharedDomain, appDomainExe, nestedDomain);
        AssertModuleDoesntContainDomains(mscorlib, runtime.SystemDomain);

        // SharedLibrary.dll is loaded into both domains but not as shared library like mscorlib.
        // This means it will not be in the shared domain.
        var sharedLibrary = runtime.GetModule("sharedlibrary.dll");
        AssertModuleContainsDomains(sharedLibrary, appDomainExe, nestedDomain);
        AssertModuleDoesntContainDomains(sharedLibrary, runtime.SharedDomain, runtime.SystemDomain);

        var appDomainsExeModule = runtime.GetModule("AppDomains.exe");
        AssertModuleContainsDomains(appDomainsExeModule, appDomainExe);
        AssertModuleDoesntContainDomains(appDomainsExeModule, runtime.SystemDomain, runtime.SharedDomain, nestedDomain);

        var nestedExeModule = runtime.GetModule("NestedException.exe");
        AssertModuleContainsDomains(nestedExeModule, nestedDomain);
        AssertModuleDoesntContainDomains(nestedExeModule, runtime.SystemDomain, runtime.SharedDomain, appDomainExe);
      }
    }

    private void AssertModuleDoesntContainDomains(ClrModule module, params ClrAppDomain[] domainList)
    {
      var moduleDomains = module.AppDomains;

      foreach (var domain in domainList)
        Assert.IsFalse(moduleDomains.Contains(domain));
    }

    private void AssertModuleContainsDomains(ClrModule module, params ClrAppDomain[] domainList)
    {
      var moduleDomains = module.AppDomains;

      foreach (var domain in domainList)
        Assert.IsTrue(moduleDomains.Contains(domain));

      Assert.AreEqual(domainList.Length, moduleDomains.Count);
    }

    [TestMethod]
    public void AppDomainPropertyTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var systemDomain = runtime.SystemDomain;
        Assert.AreEqual("System Domain", systemDomain.Name);
        Assert.AreNotEqual(0, systemDomain.Address);

        var sharedDomain = runtime.SharedDomain;
        Assert.AreEqual("Shared Domain", sharedDomain.Name);
        Assert.AreNotEqual(0, sharedDomain.Address);

        Assert.AreNotEqual(systemDomain.Address, sharedDomain.Address);

        Assert.AreEqual(2, runtime.AppDomains.Count);
        var AppDomainsExe = runtime.AppDomains[0];
        Assert.AreEqual("AppDomains.exe", AppDomainsExe.Name);
        Assert.AreEqual(1, AppDomainsExe.Id);

        var NestedExceptionExe = runtime.AppDomains[1];
        Assert.AreEqual("Second AppDomain", NestedExceptionExe.Name);
        Assert.AreEqual(2, NestedExceptionExe.Id);
      }
    }

    [TestMethod]
    public void SystemAndSharedLibraryModulesTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var systemDomain = runtime.SystemDomain;
        Assert.AreEqual(0, systemDomain.Modules.Count);

        var sharedDomain = runtime.SharedDomain;
        Assert.AreEqual(1, sharedDomain.Modules.Count);

        var mscorlib = sharedDomain.Modules.Single();
        Assert.IsTrue(Path.GetFileName(mscorlib.FileName).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));
      }
    }

    [TestMethod]
    public void ModuleAppDomainEqualityTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var appDomainsExe = runtime.GetDomainByName("AppDomains.exe");
        var nestedExceptionExe = runtime.GetDomainByName("Second AppDomain");

        var appDomainsModules = GetDomainModuleDictionary(appDomainsExe);

        Assert.IsTrue(appDomainsModules.ContainsKey("appdomains.exe"));
        Assert.IsTrue(appDomainsModules.ContainsKey("mscorlib.dll"));
        Assert.IsTrue(appDomainsModules.ContainsKey("sharedlibrary.dll"));

        Assert.IsFalse(appDomainsModules.ContainsKey("nestedexception.exe"));

        var nestedExceptionModules = GetDomainModuleDictionary(nestedExceptionExe);

        Assert.IsTrue(nestedExceptionModules.ContainsKey("nestedexception.exe"));
        Assert.IsTrue(nestedExceptionModules.ContainsKey("mscorlib.dll"));
        Assert.IsTrue(nestedExceptionModules.ContainsKey("sharedlibrary.dll"));

        Assert.IsFalse(nestedExceptionModules.ContainsKey("appdomains.exe"));

        // Ensure that we use the same ClrModule in each AppDomain.
        Assert.AreEqual(appDomainsModules["mscorlib.dll"], nestedExceptionModules["mscorlib.dll"]);
        Assert.AreEqual(appDomainsModules["sharedlibrary.dll"], nestedExceptionModules["sharedlibrary.dll"]);
      }
    }

    private static Dictionary<string, ClrModule> GetDomainModuleDictionary(ClrAppDomain domain)
    {
      var result = new Dictionary<string, ClrModule>(StringComparer.OrdinalIgnoreCase);
      foreach (var module in domain.Modules)
        result.Add(Path.GetFileName(module.FileName), module);

      return result;
    }
  }
}