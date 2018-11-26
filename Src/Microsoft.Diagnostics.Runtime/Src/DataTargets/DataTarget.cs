// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
  public class DataTarget : IDisposable
  {
    internal static PlatformFunctions PlatformFunctions { get; }

    static DataTarget()
    {
      //TODO: revisit for Linux
//#if !NET45
//      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
//        PlatformFunctions = new LinuxFunctions();
//      else
//#endif
      PlatformFunctions = new WindowsFunctions();
    }

    private List<DacLibrary> _dacLibraries = new List<DacLibrary>(2);

    public DataTarget(IDataReader dataReader, IDacLocator dacLocator, ISymbolLocator symbolLocator)
    {
      DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
      DacLocator = dacLocator ?? throw new ArgumentNullException(nameof(dacLocator));
      SymbolLocator = symbolLocator ?? throw new ArgumentNullException(nameof(symbolLocator));
      FileLoader = new FileLoader(this);

      IsMinidump = dataReader.IsMinidump;
      Architecture = dataReader.GetArchitecture();
      PointerSize = dataReader.GetPointerSize();
      Modules = dataReader.GetSortedModules();
      ClrVersions = Modules.GetVersions(Architecture, out var hasNativeRuntimes);
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

    internal void AddDacLibrary(DacLibrary dacLibrary) => _dacLibraries.Add(dacLibrary);

    public void Dispose()
    {
      DataReader.Close();
      foreach (DacLibrary library in _dacLibraries)
        library.Dispose();
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