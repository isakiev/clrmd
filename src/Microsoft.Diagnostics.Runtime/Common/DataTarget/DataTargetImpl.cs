using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
  internal class DataTargetImpl : DataTarget
  {
    private readonly IDataReader _dataReader;
    private readonly IDebugClient _client;
    private readonly Architecture _architecture;
    private readonly Lazy<ClrInfo[]> _versions;
    private readonly Lazy<ModuleInfo[]> _modules;

    public DataTargetImpl(IDataReader dataReader, IDebugClient client)
    {
      _dataReader = dataReader ?? throw new ArgumentNullException("dataReader");
      _client = client;
      _architecture = _dataReader.GetArchitecture();
      _modules = new Lazy<ModuleInfo[]>(InitModules);
      _versions = new Lazy<ClrInfo[]>(InitVersions);
    }

    public override IDataReader DataReader => _dataReader;

    public override bool IsMinidump => _dataReader.IsMinidump;

    public override Architecture Architecture => _architecture;

    public override uint PointerSize => _dataReader.GetPointerSize();

    public override IList<ClrInfo> ClrVersions => _versions.Value;

    public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
    {
      return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
    }

    public override IDebugClient DebuggerInterface => _client;

    public override IEnumerable<ModuleInfo> EnumerateModules()
    {
      return _modules.Value;
    }

    private ModuleInfo FindModule(ulong addr)
    {
      // TODO: Make binary search.
      foreach (var module in _modules.Value)
        if (module.ImageBase <= addr && addr < module.ImageBase + module.FileSize)
          return module;

      return null;
    }

    private static readonly Regex s_invalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");

    private ModuleInfo[] InitModules()
    {
      var sortedModules = new List<ModuleInfo>(_dataReader.EnumerateModules().Where(m => !s_invalidChars.IsMatch(m.FileName)));
      sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
      return sortedModules.ToArray();
    }

#pragma warning disable 0618
    
    private ClrInfo[] InitVersions()
    {
      var versions = new List<ClrInfo>();
      foreach (var module in EnumerateModules())
      {
        var clrName = Path.GetFileNameWithoutExtension(module.FileName).ToLower();

        if (clrName != "clr" && clrName != "mscorwks" && clrName != "coreclr" && clrName != "mrt100_app")
          continue;

        ClrFlavor flavor;
        switch (clrName)
        {
          case "mrt100_app":
            flavor = ClrFlavor.Native;
            break;

          case "coreclr":
            flavor = ClrFlavor.Core;
            break;

          default:
            flavor = ClrFlavor.Desktop;
            break;
        }

        var dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), DacInfo.GetDacFileName(flavor, Architecture));
        if (!File.Exists(dacLocation) || !NativeMethods.IsEqualFileVersion(dacLocation, module.Version))
          dacLocation = null;

        var version = module.Version;
        var dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, Architecture, Architecture, version);
        var dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version);

        var dacInfo = new DacInfo(_dataReader, dacAgnosticName, Architecture)
        {
          FileSize = module.FileSize,
          TimeStamp = module.TimeStamp,
          FileName = dacFileName,
          Version = module.Version
        };

        versions.Add(new ClrInfo(this, flavor, module, dacInfo, dacLocation));
      }

      var result = versions.ToArray();
      Array.Sort(result);
      return result;
    }

#pragma warning restore 0618

    public override void Dispose()
    {
      _dataReader.Close();
    }
  }
}