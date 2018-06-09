// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
  public class DataTarget : IDisposable
  {
    private static readonly Regex InvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");

    public DataTarget(IDataReader dataReader, IDacLocator dacLocator = null, ISymbolLocator symbolLocator = null)
    {
      DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
      SymbolLocator = symbolLocator ?? new DefaultSymbolLocator();
      DacLocator = dacLocator ?? new DefaultDacLocator(SymbolLocator);
      FileLoader = new FileLoader(this);

      IsMinidump = dataReader.IsMinidump;
      Architecture = dataReader.GetArchitecture();
      PointerSize = dataReader.GetPointerSize();
      Modules = InitModules(dataReader);
      ClrVersions = InitVersions(Modules, Architecture, out var hasNativeRuntimes);
      HasNativeRuntimes = hasNativeRuntimes;
    }

    public IDataReader DataReader { get; }
    public ISymbolLocator SymbolLocator { get; }
    public IDacLocator DacLocator { get; }
    internal FileLoader FileLoader { get; }
    
    public bool IsMinidump { get; }
    public Architecture Architecture { get; }
    public uint PointerSize { get; }
    public IReadOnlyCollection<ModuleInfo> Modules { get; }
    public IReadOnlyCollection<ClrInfo> ClrVersions { get; }
    public bool HasNativeRuntimes { get; }
    
    
    private static IReadOnlyCollection<ModuleInfo> InitModules(IDataReader dataReader)
    {
      var sortedModules = new List<ModuleInfo>(dataReader.EnumerateModules().Where(m => !InvalidChars.IsMatch(m.FileName)));
      sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
      return sortedModules.ToArray();
    }
    
    private static IReadOnlyCollection<ClrInfo> InitVersions(IReadOnlyCollection<ModuleInfo> modules, Architecture architecture, out bool hasNativeRuntimes)
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

    public void Dispose()
    {
      DataReader.Close();
    }
   
    /// <summary>
    ///   Creates a runtime from the given Dac file on disk.
    /// </summary>
    public ClrRuntime CreateRuntime(ClrInfo clrInfo)
    {
      if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));
      
      if (IntPtr.Size != PointerSize)
        throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

      var dacLocation = DacLocator.FindDac(clrInfo);
      if (dacLocation == null || !File.Exists(dacLocation))
        throw new FileNotFoundException("Failed to find matching dac file");

      var lib = new DacLibrary(this, dacLocation);

      DesktopVersion ver;
      if (clrInfo.Flavor == ClrFlavor.Core)
        return new V45Runtime(clrInfo, this, lib);

      if (clrInfo.Version.Major == 2)
        ver = DesktopVersion.v2;
      else if (clrInfo.Version.Major == 4 && clrInfo.Version.Minor == 0 && clrInfo.Version.Patch < 10000)
        ver = DesktopVersion.v4;
      else
        return new V45Runtime(clrInfo, this, lib);

      return new LegacyRuntime(clrInfo, this, lib, ver, clrInfo.Version.Patch);
    }
  }
}