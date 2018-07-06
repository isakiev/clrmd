using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public static class Helpers
  {
    private const string TempPathPrefix = "clrmd_tests_removeme_";
    private static string _tempPath;
    
    public static string GetTempPath()
    {
      if (_tempPath == null)
      {
        _tempPath = Path.Combine(Path.GetTempPath(), TempPathPrefix + DateTime.Now.Ticks);
        Directory.CreateDirectory(_tempPath);
      }

      return _tempPath;
    }

    public static void CleanupTempPaths()
    {
      foreach (var tempPath in Directory.GetDirectories(Path.GetTempPath(), TempPathPrefix + "*"))
        Directory.Delete(tempPath, true);
    }
    
    public static IEnumerable<ulong> GetObjectsOfType(this ClrHeap heap, string name)
    {
      return from obj in heap.EnumerateObjectAddresses()
             let type = heap.GetObjectType(obj)
             where type?.Name == name
             select obj;
    }

    public static ClrObject GetStaticObjectValue(this ClrType mainType, string fieldName)
    {
      var field = mainType.GetStaticFieldByName(fieldName);
      var obj = (ulong)field.GetValue(field.Type.Heap.Runtime.AppDomains.Single());
      return new ClrObject(obj, mainType.Heap.GetObjectType(obj));
    }

    public static ClrModule GetMainModule(this ClrRuntime runtime)
    {
      return runtime.Modules.Single(m => m.FileName.EndsWith(".exe"));
    }

    public static ClrMethod GetMethod(this ClrType type, string name)
    {
      return GetMethods(type, name).Single();
    }

    public static IEnumerable<ClrMethod> GetMethods(this ClrType type, string name)
    {
      return type.Methods.Where(m => m.Name == name);
    }

    public static HashSet<T> Unique<T>(this IEnumerable<T> self)
    {
      var set = new HashSet<T>();
      foreach (var t in self)
        set.Add(t);

      return set;
    }

    public static ClrAppDomain GetDomainByName(this ClrRuntime runtime, string domainName)
    {
      return runtime.AppDomains.Single(ad => ad.Name == domainName);
    }

    public static ClrModule GetModule(this ClrRuntime runtime, string filename)
    {
      return (from module in runtime.Modules
              let file = Path.GetFileName(module.FileName)
              where file.Equals(filename, StringComparison.OrdinalIgnoreCase)
              select module).Single();
    }

    public static ClrThread GetMainThread(this ClrRuntime runtime)
    {
      var thread = runtime.Threads.Single(t => !t.IsFinalizer);
      return thread;
    }

    public static ClrStackFrame GetFrame(this ClrThread thread, string functionName)
    {
      return thread.StackTrace.Single(sf => sf.Method != null && sf.Method.Name == functionName);
    }
  }

  [TestClass]
  public class GlobalCleanup
  {
    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();

      Helpers.CleanupTempPaths();
    }
  }
}