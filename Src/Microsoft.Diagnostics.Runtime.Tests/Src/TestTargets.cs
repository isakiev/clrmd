﻿using System;
using System.IO;
using Microsoft.Diagnostics.Runtime.DataReaders.DbgEng;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public enum GCMode
  {
    Workstation,
    Server
  }

  public class ExceptionTestData
  {
    public readonly string OuterExceptionMessage = "IOE Message";
    public readonly string OuterExceptionType = "System.InvalidOperationException";
  }

  public class TestTargets
  {
    private static readonly Lazy<TestTarget> _gcroot = new Lazy<TestTarget>(() => new TestTarget("GCRoot.cs"));
    private static readonly Lazy<TestTarget> _nestedException = new Lazy<TestTarget>(() => new TestTarget("NestedException.cs"));
    private static readonly Lazy<TestTarget> _gcHandles = new Lazy<TestTarget>(() => new TestTarget("GCHandles.cs"));
    private static readonly Lazy<TestTarget> _types = new Lazy<TestTarget>(() => new TestTarget("Types.cs"));
    private static readonly Lazy<TestTarget> _appDomains = new Lazy<TestTarget>(() => new TestTarget("AppDomains.cs"));
    private static readonly Lazy<TestTarget> _finalizableObjects = new Lazy<TestTarget>(() => new TestTarget("FinalizableObjects.cs"));
    private static readonly Lazy<TestTarget> _dumpDebugTests = new Lazy<TestTarget>(() => new TestTarget("DumpDebug.cs"));

    public static TestTarget GCRoot => _gcroot.Value;
    public static TestTarget NestedException => _nestedException.Value;
    public static ExceptionTestData NestedExceptionData => new ExceptionTestData();
    public static TestTarget GCHandles => _gcHandles.Value;
    public static TestTarget Types => _types.Value;
    public static TestTarget AppDomains => _appDomains.Value;
    public static TestTarget FinalizableObjects => _finalizableObjects.Value;
    public static TestTarget DumpDebug => _dumpDebugTests.Value;
  }

  public class TestTarget
  {
    public string Executable { get; }

    public string Pdb { get; }

    public string Source { get; }

    private static string Architecture { get; }
    private static string TestRoot { get; }

    static TestTarget()
    {
      Architecture = IntPtr.Size == 4 ? "x86" : "x64";

      var info = new DirectoryInfo(Environment.CurrentDirectory);
      while (info.GetFiles(".gitignore").Length != 1)
      {
        info = info.Parent;
        if (info == null)
          throw new ApplicationException("Base directory not found");
      }

      TestRoot = Path.Combine(info.FullName, "Src", "Microsoft.Diagnostics.Runtime.Tests", "Data");
    }

    public TestTarget(string source)
    {
      Source = Path.Combine(TestRoot, source);
      if (!File.Exists(Source))
        throw new FileNotFoundException($"Could not find source file: {source}");

      Executable = Path.Combine(TestRoot, "Bin", Architecture, Path.ChangeExtension(source, ".exe"));
      Pdb = Path.ChangeExtension(Executable, ".pdb");

      if (!File.Exists(Executable) || !File.Exists(Pdb))
      {
        var buildTestAssets = Path.Combine(TestRoot, "build_test_assets.cmd");
        throw new InvalidOperationException($"You must first generate test binaries and crash dumps using by running: {buildTestAssets}");
      }
    }

    private string BuildDumpName(GCMode gcmode, bool full)
    {
      var filename = Path.Combine(Path.GetDirectoryName(Executable), Path.GetFileNameWithoutExtension(Executable));

      var gc = gcmode == GCMode.Server ? "svr" : "wks";
      var dumpType = full ? "" : "_mini";
      filename = $"{filename}_{gc}{dumpType}.dmp";
      return filename;
    }

    public DataTarget LoadMiniDump(GCMode gc = GCMode.Workstation)
    {
      var path = BuildDumpName(gc, false);
      return LoadCrashDump(path);
    }

    public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation)
    {
      var path = BuildDumpName(gc, true);
      return LoadCrashDump(path);
    }

    private static DataTarget LoadCrashDump(string dumpPath)
    {
      var cacheLocation = Path.Combine(Helpers.GetTempPath(), "Cache");
      Directory.CreateDirectory(cacheLocation);

      var symbolLocator = new DefaultSymbolLocator(DefaultLogger.Instance, cacheLocation);
      return new DataTarget(new DbgEngDataReader(dumpPath), new DefaultDacLocator(symbolLocator), symbolLocator);
    }
  }
}