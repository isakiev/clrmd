using System;
using System.CodeDom.Compiler;
using System.IO;
using Microsoft.CSharp;
using Microsoft.Diagnostics.Runtime.DataReaders.DbgEng;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    private static readonly Lazy<TestTarget> _appDomains = new Lazy<TestTarget>(() => new TestTarget("AppDomains.cs", NestedException));
    private static readonly Lazy<TestTarget> _finalizationQueue = new Lazy<TestTarget>(() => new TestTarget("FinalizationQueue.cs"));

    public static TestTarget GCRoot => _gcroot.Value;
    public static TestTarget NestedException => _nestedException.Value;
    public static ExceptionTestData NestedExceptionData = new ExceptionTestData();
    public static TestTarget GCHandles => _gcHandles.Value;
    public static TestTarget Types => _types.Value;
    public static TestTarget AppDomains => _appDomains.Value;
    public static TestTarget FinalizationQueue => _finalizationQueue.Value;
  }

  public class TestTarget
  {
    private static string _baseFolder;
    private static readonly TestTarget _sharedLibrary = new TestTarget("SharedLibrary.cs", true);

    private readonly bool _isLibrary;
    private readonly string _source;
    private string _executable;
    private object _sync = new object();
    private readonly string[] _miniDumpPath = new string[2];
    private readonly string[] _fullDumpPath = new string[2];

    private static string GetBaseFolder()
    {
      if (_baseFolder != null)
        return _baseFolder;

      var binFolder = Path.GetDirectoryName(typeof(TestTarget).Assembly.Location);
      var rootFolder = Path.GetDirectoryName(binFolder);
      if (rootFolder == null)
        throw new ApplicationException("Failed to detect project root folder");
      
      _baseFolder = Path.Combine(rootFolder, "Src", "Microsoft.Diagnostics.Runtime.Tests", "Data");
      return _baseFolder;
    }

    public string Executable
    {
      get
      {
        if (_executable == null)
          CompileSource();
        return _executable;
      }
    }

    public string Pdb => Path.ChangeExtension(Executable, "pdb");

    public string Source => _source;

    public TestTarget(string source, bool isLibrary = false)
    {
      _source = Path.Combine(GetBaseFolder(), source);
      _isLibrary = isLibrary;
    }

    public TestTarget(string source, params TestTarget[] required)
    {
      _source = Path.Combine(GetBaseFolder(), source);
      _isLibrary = false;

      foreach (var item in required)
        item.CompileSource();
    }

    public DataTarget LoadMiniDump(GCMode gc = GCMode.Workstation)
    {
      var path = GetMiniDumpName(Executable, gc);
      if (File.Exists(path))
        return LoadCrashDump(path);

      WriteCrashDumps(gc);

      return LoadCrashDump(_miniDumpPath[(int)gc]);
    }

    public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation)
    {
      var path = GetFullDumpName(Executable, gc);
      if (File.Exists(path))
        return LoadCrashDump(path);

      WriteCrashDumps(gc);

      return LoadCrashDump(_fullDumpPath[(int)gc]);
    }

    private void CompileSource()
    {
      if (_executable != null)
        return;

      // Don't recompile if it's there.
      var destination = GetOutputAssembly();
      if (!File.Exists(destination))
        _executable = CompileCSharp(_source, destination, _isLibrary);
      else
        _executable = destination;
    }

    private string GetOutputAssembly()
    {
      var extension = _isLibrary ? "dll" : "exe";
      return Path.Combine(Helpers.TestWorkingDirectory, Path.ChangeExtension(Path.GetFileNameWithoutExtension(_source), extension));
    }

    private static string CompileCSharp(string source, string destination, bool isLibrary)
    {
      var provider = new CSharpCodeProvider();
      var cp = new CompilerParameters();
      cp.ReferencedAssemblies.Add("system.dll");

      if (isLibrary)
      {
        cp.GenerateExecutable = false;
      }
      else
      {
        cp.GenerateExecutable = true;
        cp.ReferencedAssemblies.Add(_sharedLibrary.Executable);
      }

      cp.GenerateInMemory = false;
      cp.CompilerOptions = "/unsafe " + (IntPtr.Size == 4 ? "/platform:x86" : "/platform:x64");

      cp.IncludeDebugInformation = true;
      cp.OutputAssembly = destination;
      var cr = provider.CompileAssemblyFromFile(cp, source);

      if (cr.Errors.Count > 0 && System.Diagnostics.Debugger.IsAttached)
        System.Diagnostics.Debugger.Break();

      Assert.AreEqual(0, cr.Errors.Count);

      return cr.PathToAssembly;
    }

    private void WriteCrashDumps(GCMode gc)
    {
      if (_fullDumpPath[(int)gc] != null)
        return;

      var executable = Executable;
      var info = new DebuggerStartInfo();
      if (gc == GCMode.Server)
        info.SetEnvironmentVariable("COMPlus_BuildFlavor", "svr");

      using (var dbg = info.LaunchProcess(executable, Helpers.TestWorkingDirectory))
      {
        dbg.SecondChanceExceptionEvent += delegate(Debugger d, EXCEPTION_RECORD64 ex)
          {
            if (ex.ExceptionCode == (uint)ExceptionTypes.Clr)
              WriteDumps(dbg, executable, gc);
          };

        DEBUG_STATUS status;
        do
        {
          status = dbg.ProcessEvents(0xffffffff);
        } while (status != DEBUG_STATUS.NO_DEBUGGEE);

        Assert.IsNotNull(_miniDumpPath[(int)gc]);
        Assert.IsNotNull(_fullDumpPath[(int)gc]);
      }
    }

    private void WriteDumps(Debugger dbg, string exe, GCMode gc)
    {
      var dump = GetMiniDumpName(exe, gc);
      dbg.WriteDumpFile(dump, DEBUG_DUMP.SMALL);
      _miniDumpPath[(int)gc] = dump;

      dump = GetFullDumpName(exe, gc);
      dbg.WriteDumpFile(dump, DEBUG_DUMP.DEFAULT);
      _fullDumpPath[(int)gc] = dump;

      dbg.TerminateProcess();
    }

    private static string GetMiniDumpName(string executable, GCMode gc)
    {
      var basePath = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable));
      basePath += gc == GCMode.Workstation ? "_wks" : "_svr";
      basePath += "_mini.dmp";
      return basePath;
    }

    private static string GetFullDumpName(string executable, GCMode gc)
    {
      var basePath = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable));
      basePath += gc == GCMode.Workstation ? "_wks" : "_svr";
      basePath += "_full.dmp";
      return basePath;
    }

    private static DataTarget LoadCrashDump(string dumpPath)
    {
      return new DataTarget(new DbgEngDataReader(dumpPath));
    }
  }
}