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

    public DataTarget(IDataReader dataReader, SymbolLocator symbolLocator = null)
    {
      DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
      SymbolLocator = symbolLocator ?? new DefaultSymbolLocator();
      FileLoader = new FileLoader(this);

      IsMinidump = dataReader.IsMinidump;
      Architecture = dataReader.GetArchitecture();
      PointerSize = dataReader.GetPointerSize();
      Modules = InitModules(dataReader);
      ClrVersions = InitVersions(dataReader, Modules, Architecture);
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
    
    private static IReadOnlyCollection<ClrInfo> InitVersions(IDataReader dataReader, IReadOnlyCollection<ModuleInfo> modules, Architecture architecture)
    {
      var versions = new List<ClrInfo>();
      foreach (var module in modules)
      {
        if (ClrInfo.IsClrModule(module))
          versions.Add(new ClrInfo(module, architecture, dataReader));
      }

      var result = versions.ToArray();
      Array.Sort(result);
      return result;
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

      var dac = clrInfo.DacLocation;
      if (dac != null && !File.Exists(dac))
        dac = null;

      if (dac == null)
        dac = SymbolLocator.FindBinary(clrInfo.DacInfo);

      if (!File.Exists(dac))
        throw new FileNotFoundException(clrInfo.DacInfo.FileName);

      if (IntPtr.Size != PointerSize)
        throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

      return ConstructRuntime(clrInfo, dac);
    }

    /// <summary>
    ///   Creates a runtime from the given Dac file on disk.
    /// </summary>
    /// <param name="clrInfo">CLR info</param>
    /// <param name="dacFilename">A full path to the matching mscordacwks for this process.</param>
    /// <param name="ignoreMismatch">Whether or not to ignore mismatches between </param>
    /// <returns></returns>
    public ClrRuntime CreateRuntime(ClrInfo clrInfo, string dacFilename, bool ignoreMismatch = false)
    {
      if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));
      if (string.IsNullOrEmpty(dacFilename)) throw new ArgumentNullException(nameof(dacFilename));
      if (!File.Exists(dacFilename)) throw new FileNotFoundException(dacFilename);

      if (!ignoreMismatch)
      {
        NativeMethods.GetFileVersion(dacFilename, out var major, out var minor, out var revision, out var patch);
        if (major != clrInfo.Version.Major || minor != clrInfo.Version.Minor || revision != clrInfo.Version.Revision || patch != clrInfo.Version.Patch)
          throw new InvalidOperationException($"Mismatched dac. Version: {major}.{minor}.{revision}.{patch}");
      }

      return ConstructRuntime(clrInfo, dacFilename);
    }

    private ClrRuntime ConstructRuntime(ClrInfo clrInfo, string dac)
    {
      if (IntPtr.Size != (int)DataReader.GetPointerSize())
        throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

      if (IsMinidump)
        SymbolLocator.PrefetchBinary(clrInfo.ModuleInfo.FileName, (int)clrInfo.ModuleInfo.TimeStamp, (int)clrInfo.ModuleInfo.FileSize);

      var lib = new DacLibrary(this, dac);

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