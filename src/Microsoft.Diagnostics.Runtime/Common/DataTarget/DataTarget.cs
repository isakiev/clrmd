// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime.Utilities;

#pragma warning disable 0618

namespace Microsoft.Diagnostics.Runtime
{
  public class DataTarget : IDisposable
  {
    private static readonly Regex InvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");

    public DataTarget(IDataReader dataReader, SymbolLocator symbolLocator = null)
    {
      DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
      SymbolLocator = symbolLocator ?? new DefaultSymbolLocator();
      FileLoader = new FileLoader(this);

      IsMinidump = dataReader.IsMinidump;
      Architecture = dataReader.GetArchitecture();
      PointerSize = dataReader.GetPointerSize();
      Modules = InitModules(dataReader);
      ClrVersions = InitVersions(dataReader, Modules, Architecture, this);
    }

    public IDataReader DataReader { get; }
    public SymbolLocator SymbolLocator { get; }
    internal FileLoader FileLoader { get; }
    
    public bool IsMinidump { get; }
    public Architecture Architecture { get; }
    public uint PointerSize { get; }
    public IReadOnlyCollection<ModuleInfo> Modules { get; }
    public IReadOnlyCollection<ClrInfo> ClrVersions { get; }
    
    private static IReadOnlyCollection<ModuleInfo> InitModules(IDataReader dataReader)
    {
      var sortedModules = new List<ModuleInfo>(dataReader.EnumerateModules().Where(m => !InvalidChars.IsMatch(m.FileName)));
      sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
      return sortedModules.ToArray();
    }
    
    private static IReadOnlyCollection<ClrInfo> InitVersions(IDataReader dataReader, IReadOnlyCollection<ModuleInfo> modules, Architecture architecture, DataTarget dataTarget)
    {
      var versions = new List<ClrInfo>();
      foreach (var module in modules)
      {
        var moduleFileName = Path.GetFileNameWithoutExtension(module.FileName);
        if (moduleFileName == null)
          continue;
        
        var clrName = moduleFileName.ToLower();

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

        var moduleDirectory = Path.GetDirectoryName(module.FileName) ?? string.Empty;
        var dacLocation = Path.Combine(moduleDirectory, DacInfo.GetDacFileName(flavor, architecture));
        if (!File.Exists(dacLocation) || !NativeMethods.IsEqualFileVersion(dacLocation, module.Version))
          dacLocation = null;

        var version = module.Version;
        var dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, architecture, architecture, version);
        var dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, architecture, version);

        var dacInfo = new DacInfo(dataReader, dacAgnosticName, architecture)
        {
          FileSize = module.FileSize,
          TimeStamp = module.TimeStamp,
          FileName = dacFileName,
          Version = module.Version
        };

        versions.Add(new ClrInfo(dataTarget, flavor, module, dacInfo, dacLocation));
      }

      var result = versions.ToArray();
      Array.Sort(result);
      return result;
    }

    public void Dispose()
    {
      DataReader.Close();
    }
  }
}