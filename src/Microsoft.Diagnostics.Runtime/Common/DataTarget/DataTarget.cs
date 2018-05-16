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

    public DataTarget(IDataReader dataReader, ISymbolLocator symbolLocator = null)
    {
      DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
      SymbolLocator = symbolLocator ?? new DefaultSymbolLocator();
      FileLoader = new FileLoader(this);

      IsMinidump = dataReader.IsMinidump;
      Architecture = dataReader.GetArchitecture();
      PointerSize = dataReader.GetPointerSize();
      Modules = InitModules(dataReader);
      ClrVersions = InitVersions(Modules);
    }

    public IDataReader DataReader { get; }
    public ISymbolLocator SymbolLocator { get; }
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
    
    private static IReadOnlyCollection<ClrInfo> InitVersions(IReadOnlyCollection<ModuleInfo> modules)
    {
      var versions = new List<ClrInfo>();
      foreach (var module in modules)
      {
        if (ClrInfo.IsClrRuntime(module, out var clrInfo))
          versions.Add(clrInfo);
      }

      versions.Sort();
      return versions;
    }

    public void Dispose()
    {
      DataReader.Close();
    }

    private DacLibrary GetDacLibrary(ClrInfo clrInfo)
    {
      if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));
      
      var moduleDirectory = Path.GetDirectoryName(clrInfo.ModuleInfo.FileName) ?? string.Empty;
      var dacFileName = DacInfo.GetDacFileName(clrInfo.Flavor, Architecture);
      var dacRequestFileName = DacInfo.GetDacRequestFileName(clrInfo.Flavor, Architecture, Architecture, clrInfo.Version);
      
      var dacLocation = Path.Combine(moduleDirectory, dacFileName);

      if (!File.Exists(dacLocation))
      {
        var downloadedDac = SymbolLocator.FindBinary(dacRequestFileName, (int)clrInfo.ModuleInfo.TimeStamp, (int)clrInfo.ModuleInfo.FileSize);
        if (!File.Exists(downloadedDac))
          throw new FileNotFoundException(dacRequestFileName);

        dacLocation = downloadedDac;
      }

      if (IntPtr.Size != PointerSize)
        throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

      return new DacLibrary(this, dacLocation);
    }
    
    /// <summary>
    ///   Creates a runtime from the given Dac file on disk.
    /// </summary>
    public ClrRuntime CreateRuntime(ClrInfo clrInfo)
    {
      if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

      var lib = GetDacLibrary(clrInfo);

      DesktopVersion ver;
      if (clrInfo.Flavor == ClrFlavor.Core)
        return new V45Runtime(clrInfo, this, lib);

      if (clrInfo.Flavor == ClrFlavor.Native)
        throw new NotSupportedException();

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