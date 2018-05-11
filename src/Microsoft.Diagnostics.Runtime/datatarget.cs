// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Represents the version of a DLL.
  /// </summary>
  [Serializable]
  public struct VersionInfo
  {
    /// <summary>
    ///   In a version 'A.B.C.D', this field represents 'A'.
    /// </summary>
    public int Major;

    /// <summary>
    ///   In a version 'A.B.C.D', this field represents 'B'.
    /// </summary>
    public int Minor;

    /// <summary>
    ///   In a version 'A.B.C.D', this field represents 'C'.
    /// </summary>
    public int Revision;

    /// <summary>
    ///   In a version 'A.B.C.D', this field represents 'D'.
    /// </summary>
    public int Patch;

    internal VersionInfo(int major, int minor, int revision, int patch)
    {
      Major = major;
      Minor = minor;
      Revision = revision;
      Patch = patch;
    }

    /// <summary>
    ///   To string.
    /// </summary>
    /// <returns>The A.B.C.D version prepended with 'v'.</returns>
    public override string ToString()
    {
      return string.Format("v{0}.{1}.{2}.{3:D2}", Major, Minor, Revision, Patch);
    }
  }

  /// <summary>
  ///   Returns the "flavor" of CLR this module represents.
  /// </summary>
  public enum ClrFlavor
  {
    /// <summary>
    ///   This is the full version of CLR included with windows.
    /// </summary>
    Desktop = 0,

    /// <summary>
    ///   This originally was for Silverlight and other uses of "coreclr", but now
    ///   there are several flavors of coreclr, some of which are no longer supported.
    /// </summary>
    [Obsolete]
    CoreCLR = 1,

    /// <summary>
    ///   Used for .Net Native.
    /// </summary>
    [Obsolete(".Net Native support is being split out of this library into a different one.")]
    Native = 2,

    /// <summary>
    ///   For .Net Core
    /// </summary>
    Core = 3
  }

  /// <summary>
  ///   Represents information about a single Clr runtime in a process.
  /// </summary>
  [Serializable]
  public class ClrInfo : IComparable
  {
    /// <summary>
    ///   The version number of this runtime.
    /// </summary>
    public VersionInfo Version => ModuleInfo.Version;

    /// <summary>
    ///   The type of CLR this module represents.
    /// </summary>
    public ClrFlavor Flavor { get; }

    /// <summary>
    ///   Returns module information about the Dac needed create a ClrRuntime instance for this runtime.
    /// </summary>
    public DacInfo DacInfo { get; }

    /// <summary>
    ///   Returns module information about the ClrInstance.
    /// </summary>
    public ModuleInfo ModuleInfo { get; }

    /// <summary>
    ///   Returns the location of the local dac on your machine which matches this version of Clr, or null
    ///   if one could not be found.
    /// </summary>
    public string LocalMatchingDac => _dacLocation;

    /// <summary>
    ///   Creates a runtime from the given Dac file on disk.
    /// </summary>
    public ClrRuntime CreateRuntime()
    {
      var dac = _dacLocation;
      if (dac != null && !File.Exists(dac))
        dac = null;

      if (dac == null)
        dac = _dataTarget.SymbolLocator.FindBinary(DacInfo);

      if (!File.Exists(dac))
        throw new FileNotFoundException(DacInfo.FileName);

      if (IntPtr.Size != (int)_dataTarget.DataReader.GetPointerSize())
        throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

      return ConstructRuntime(dac);
    }

    /// <summary>
    ///   Creates a runtime from a given IXClrDataProcess interface.  Used for debugger plugins.
    /// </summary>
    public ClrRuntime CreateRuntime(object clrDataProcess)
    {
      var lib = new DacLibrary(_dataTarget, (IXCLRDataProcess)clrDataProcess);

      // Figure out what version we are on.
      if (clrDataProcess is ISOSDac) return new V45Runtime(this, _dataTarget, lib);

      var buffer = new byte[Marshal.SizeOf(typeof(V2HeapDetails))];

      var val = lib.DacInterface.Request(DacRequests.GCHEAPDETAILS_STATIC_DATA, 0, null, (uint)buffer.Length, buffer);
      if ((uint)val == 0x80070057)
        return new LegacyRuntime(this, _dataTarget, lib, DesktopVersion.v4, 10000);

      return new LegacyRuntime(this, _dataTarget, lib, DesktopVersion.v2, 3054);
    }

    /// <summary>
    ///   Creates a runtime from the given Dac file on disk.
    /// </summary>
    /// <param name="dacFilename">A full path to the matching mscordacwks for this process.</param>
    /// <param name="ignoreMismatch">Whether or not to ignore mismatches between </param>
    /// <returns></returns>
    public ClrRuntime CreateRuntime(string dacFilename, bool ignoreMismatch = false)
    {
      if (string.IsNullOrEmpty(dacFilename))
        throw new ArgumentNullException("dacFilename");

      if (!File.Exists(dacFilename))
        throw new FileNotFoundException(dacFilename);

      if (!ignoreMismatch)
      {
        NativeMethods.GetFileVersion(dacFilename, out var major, out var minor, out var revision, out var patch);
        if (major != Version.Major || minor != Version.Minor || revision != Version.Revision || patch != Version.Patch)
          throw new InvalidOperationException(string.Format("Mismatched dac. Version: {0}.{1}.{2}.{3}", major, minor, revision, patch));
      }

      return ConstructRuntime(dacFilename);
    }

#pragma warning disable 0618
    private ClrRuntime ConstructRuntime(string dac)
    {
      if (IntPtr.Size != (int)_dataTarget.DataReader.GetPointerSize())
        throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

      if (_dataTarget.IsMinidump)
        _dataTarget.SymbolLocator.PrefetchBinary(ModuleInfo.FileName, (int)ModuleInfo.TimeStamp, (int)ModuleInfo.FileSize);

      var lib = new DacLibrary(_dataTarget, dac);

      DesktopVersion ver;
      if (Flavor == ClrFlavor.Core)
        return new V45Runtime(this, _dataTarget, lib);

      if (Flavor == ClrFlavor.Native)
        throw new NotSupportedException();

      if (Version.Major == 2)
        ver = DesktopVersion.v2;
      else if (Version.Major == 4 && Version.Minor == 0 && Version.Patch < 10000)
        ver = DesktopVersion.v4;
      else
        return new V45Runtime(this, _dataTarget, lib);

      return new LegacyRuntime(this, _dataTarget, lib, ver, Version.Patch);
    }

    /// <summary>
    ///   To string.
    /// </summary>
    /// <returns>A version string for this Clr runtime.</returns>
    public override string ToString()
    {
      return Version.ToString();
    }

    internal ClrInfo(DataTargetImpl dt, ClrFlavor flavor, ModuleInfo module, DacInfo dacInfo, string dacLocation)
    {
      Debug.Assert(dacInfo != null);

      Flavor = flavor;
      DacInfo = dacInfo;
      ModuleInfo = module;
      module.IsRuntime = true;
      _dataTarget = dt;
      _dacLocation = dacLocation;
    }

    internal ClrInfo()
    {
    }

    private readonly string _dacLocation;
    private readonly DataTargetImpl _dataTarget;

    /// <summary>
    ///   IComparable.  Sorts the object by version.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
    public int CompareTo(object obj)
    {
      if (obj == null)
        return 1;

      if (!(obj is ClrInfo))
        throw new InvalidOperationException("Object not ClrInfo.");

      var flv = ((ClrInfo)obj).Flavor;
      if (flv != Flavor)
        return flv.CompareTo(Flavor); // Intentionally reversed.

      var rhs = ((ClrInfo)obj).Version;
      if (Version.Major != rhs.Major)
        return Version.Major.CompareTo(rhs.Major);

      if (Version.Minor != rhs.Minor)
        return Version.Minor.CompareTo(rhs.Minor);

      if (Version.Revision != rhs.Revision)
        return Version.Revision.CompareTo(rhs.Revision);

      return Version.Patch.CompareTo(rhs.Patch);
    }
  }

  /// <summary>
  ///   Specifies how to attach to a live process.
  /// </summary>
  public enum AttachFlag
  {
    /// <summary>
    ///   Performs an invasive debugger attach.  Allows the consumer of this API to control the target
    ///   process through normal IDebug function calls.  The process will be paused.
    /// </summary>
    Invasive,

    /// <summary>
    ///   Performs a non-invasive debugger attach.  The process will be paused by this attached (and
    ///   for the duration of the attach) but the caller cannot control the target process.  This is
    ///   useful when there's already a debugger attached to the process.
    /// </summary>
    NonInvasive,

    /// <summary>
    ///   Performs a "passive" attach, meaning no debugger is actually attached to the target process.
    ///   The process is not paused, so queries for quickly changing data (such as the contents of the
    ///   GC heap or callstacks) will be highly inconsistent unless the user pauses the process through
    ///   other means.  Useful when attaching with ICorDebug (managed debugger), as you cannot use a
    ///   non-invasive attach with ICorDebug.
    /// </summary>
    Passive
  }

  /// <summary>
  ///   Information about a specific PDB instance obtained from a PE image.
  /// </summary>
  [Serializable]
  public class PdbInfo
  {
    /// <summary>
    ///   The Guid of the PDB.
    /// </summary>
    public Guid Guid { get; set; }

    /// <summary>
    ///   The pdb revision.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    ///   The filename of the pdb.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    ///   Creates an instance of the PdbInfo class
    /// </summary>
    public PdbInfo()
    {
    }

    /// <summary>
    ///   Creates an instance of the PdbInfo class with the corresponding properties initialized
    /// </summary>
    public PdbInfo(string fileName, Guid guid, int rev)
    {
      FileName = fileName;
      Guid = guid;
      Revision = rev;
    }

    /// <summary>
    ///   GetHashCode implementation.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return Guid.GetHashCode() ^ Revision;
    }

    /// <summary>
    ///   Override for Equals.  Returns true if the guid, age, and filenames equal.  Note that this compares only the
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>True if the objects match, false otherwise.</returns>
    public override bool Equals(object obj)
    {
      if (obj == null)
        return false;

      if (ReferenceEquals(this, obj))
        return true;

      if (obj is PdbInfo rhs)
        if (Revision == rhs.Revision && Guid == rhs.Guid)
        {
          var lhsFilename = Path.GetFileName(FileName);
          var rhsFilename = Path.GetFileName(rhs.FileName);
          return lhsFilename.Equals(rhsFilename, StringComparison.OrdinalIgnoreCase);
        }

      return false;
    }

    /// <summary>
    ///   To string implementation.
    /// </summary>
    /// <returns>Printing friendly version.</returns>
    public override string ToString()
    {
      return $"{Guid} {Revision} {FileName}";
    }
  }

  /// <summary>
  ///   Provides information about loaded modules in a DataTarget
  /// </summary>
  [Serializable]
  public class ModuleInfo
  {
    /// <summary>
    ///   The base address of the object.
    /// </summary>
    public virtual ulong ImageBase { get; set; }

    /// <summary>
    ///   The filesize of the image.
    /// </summary>
    public virtual uint FileSize { get; set; }

    /// <summary>
    ///   The build timestamp of the image.
    /// </summary>
    public virtual uint TimeStamp { get; set; }

    /// <summary>
    ///   The filename of the module on disk.
    /// </summary>
    public virtual string FileName { get; set; }

    /// <summary>
    ///   Returns true if this module is a native (non-managed) .Net runtime module.
    /// </summary>
    public bool IsRuntime { get; internal set; }

    /// <summary>
    ///   Returns a PEFile from a stream constructed using instance fields of this object.
    ///   If the PEFile cannot be constructed correctly, null is returned
    /// </summary>
    /// <returns></returns>
    public PEFile GetPEFile()
    {
      return PEFile.TryLoad(new ReadVirtualStream(_dataReader, (long)ImageBase, FileSize), true);
    }

    /// <summary>
    ///   Whether the module is managed or not.
    /// </summary>
    public virtual bool IsManaged
    {
      get
      {
        InitData();
        return _managed ?? false;
      }
    }

    /// <summary>
    ///   To string.
    /// </summary>
    /// <returns>The filename of the module.</returns>
    public override string ToString()
    {
      return FileName;
    }

    /// <summary>
    ///   The PDB associated with this module.
    /// </summary>
    public PdbInfo Pdb
    {
      get
      {
        if (_pdb != null || _dataReader == null)
          return _pdb;

        InitData();
        return _pdb;
      }

      set => _pdb = value;
    }

    private void InitData()
    {
      if (_dataReader == null)
        return;

      if (_pdb != null && _managed != null)
        return;

      PEFile file = null;
      try
      {
        file = PEFile.TryLoad(new ReadVirtualStream(_dataReader, (long)ImageBase, FileSize), true);
        if (file == null)
          return;

        _managed = file.Header.ComDescriptorDirectory.VirtualAddress != 0;

        if (file.GetPdbSignature(out var pdbName, out var guid, out var age))
          _pdb = new PdbInfo
          {
            FileName = pdbName,
            Guid = guid,
            Revision = age
          };
      }
      catch
      {
      }
      finally
      {
        if (file != null)
          file.Dispose();
      }
    }

    /// <summary>
    ///   The version information for this file.
    /// </summary>
    public VersionInfo Version
    {
      get
      {
        if (_versionInit || _dataReader == null)
          return _version;

        _dataReader.GetVersionInfo(ImageBase, out _version);
        _versionInit = true;
        return _version;
      }

      set
      {
        _version = value;
        _versionInit = true;
      }
    }

    /// <summary>
    ///   Empty constructor for serialization.
    /// </summary>
    public ModuleInfo()
    {
    }

    /// <summary>
    ///   Creates a ModuleInfo object with an IDataReader instance.  This is used when
    ///   lazily evaluating VersionInfo.
    /// </summary>
    /// <param name="reader"></param>
    public ModuleInfo(IDataReader reader)
    {
      _dataReader = reader;
    }

    [NonSerialized]
    private readonly IDataReader _dataReader;
    private PdbInfo _pdb;
    private bool? _managed;
    private VersionInfo _version;
    private bool _versionInit;
  }

  /// <summary>
  ///   Represents the dac dll
  /// </summary>
  [Serializable]
  public class DacInfo : ModuleInfo
  {
    /// <summary>
    ///   Returns the filename of the dac dll according to the specified parameters
    /// </summary>
    public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo clrVersion)
    {
      if (flavor == ClrFlavor.Native)
        return targetArchitecture == Architecture.Amd64 ? "mrt100dac_winamd64.dll" : "mrt100dac_winx86.dll";

      var dacName = flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
      return string.Format(
        "{0}_{1}_{2}_{3}.{4}.{5}.{6:D2}.dll",
        dacName,
        currentArchitecture,
        targetArchitecture,
        clrVersion.Major,
        clrVersion.Minor,
        clrVersion.Revision,
        clrVersion.Patch);
    }

    internal static string GetDacFileName(ClrFlavor flavor, Architecture targetArchitecture)
    {
      if (flavor == ClrFlavor.Native)
        return targetArchitecture == Architecture.Amd64 ? "mrt100dac_winamd64.dll" : "mrt100dac_winx86.dll";

      return flavor == ClrFlavor.Core ? "mscordaccore.dll" : "mscordacwks.dll";
    }

    /// <summary>
    ///   The platform-agnostice filename of the dac dll
    /// </summary>
    public string PlatformAgnosticFileName { get; set; }

    /// <summary>
    ///   The architecture (x86 or amd64) being targeted
    /// </summary>
    public Architecture TargetArchitecture { get; set; }

    /// <summary>
    ///   Constructs a DacInfo object with the appropriate properties initialized
    /// </summary>
    public DacInfo(IDataReader reader, string agnosticName, Architecture targetArch)
      : base(reader)
    {
      PlatformAgnosticFileName = agnosticName;
      TargetArchitecture = targetArch;
    }
  }

  /// <summary>
  ///   The result of a VirtualQuery.
  /// </summary>
  [Serializable]
  public struct VirtualQueryData
  {
    /// <summary>
    ///   The base address of the allocation.
    /// </summary>
    public ulong BaseAddress;

    /// <summary>
    ///   The size of the allocation.
    /// </summary>
    public ulong Size;

    /// <summary>
    ///   Constructor.
    /// </summary>
    /// <param name="addr">Base address of the memory range.</param>
    /// <param name="size">The size of the memory range.</param>
    public VirtualQueryData(ulong addr, ulong size)
    {
      BaseAddress = addr;
      Size = size;
    }
  }

  /// <summary>
  ///   An interface for reading data out of the target process.
  /// </summary>
  public interface IDataReader
  {
    /// <summary>
    ///   Called when the DataTarget is closing (Disposing).  Used to clean up resources.
    /// </summary>
    void Close();

    /// <summary>
    ///   Informs the data reader that the user has requested all data be flushed.
    /// </summary>
    void Flush();

    /// <summary>
    ///   Gets the architecture of the target.
    /// </summary>
    /// <returns>The architecture of the target.</returns>
    Architecture GetArchitecture();

    /// <summary>
    ///   Gets the size of a pointer in the target process.
    /// </summary>
    /// <returns>The pointer size of the target process.</returns>
    uint GetPointerSize();

    /// <summary>
    ///   Enumerates modules in the target process.
    /// </summary>
    /// <returns>A list of the modules in the target process.</returns>
    IList<ModuleInfo> EnumerateModules();

    /// <summary>
    ///   Gets the version information for a given module (given by the base address of the module).
    /// </summary>
    /// <param name="baseAddress">The base address of the module to look up.</param>
    /// <param name="version">The version info for the given module.</param>
    void GetVersionInfo(ulong baseAddress, out VersionInfo version);

    /// <summary>
    ///   Read memory out of the target process.
    /// </summary>
    /// <param name="address">The address of memory to read.</param>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="bytesRequested">The number of bytes to read.</param>
    /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
    /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
    bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);

    /// <summary>
    ///   Read memory out of the target process.
    /// </summary>
    /// <param name="address">The address of memory to read.</param>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="bytesRequested">The number of bytes to read.</param>
    /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
    /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
    bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead);

    /// <summary>
    ///   Returns true if the data target is a minidump (or otherwise may not contain full heap data).
    /// </summary>
    /// <returns>True if the data target is a minidump (or otherwise may not contain full heap data).</returns>
    bool IsMinidump { get; }

    /// <summary>
    ///   Gets the TEB of the specified thread.
    /// </summary>
    /// <param name="thread">The OS thread ID to get the TEB for.</param>
    /// <returns>The address of the thread's teb.</returns>
    ulong GetThreadTeb(uint thread);

    /// <summary>
    ///   Enumerates the OS thread ID of all threads in the process.
    /// </summary>
    /// <returns>An enumeration of all threads in the target process.</returns>
    IEnumerable<uint> EnumerateAllThreads();

    /// <summary>
    ///   Gets information about the given memory range.
    /// </summary>
    /// <param name="addr">An arbitrary address in the target process.</param>
    /// <param name="vq">The base address and size of the allocation.</param>
    /// <returns>True if the address was found and vq was filled, false if the address is not valid memory.</returns>
    bool VirtualQuery(ulong addr, out VirtualQueryData vq);

    /// <summary>
    ///   Gets the thread context for the given thread.
    /// </summary>
    /// <param name="threadID">The OS thread ID to read the context from.</param>
    /// <param name="contextFlags">The requested context flags, or 0 for default flags.</param>
    /// <param name="contextSize">The size (in bytes) of the context parameter.</param>
    /// <param name="context">A pointer to the buffer to write to.</param>
    bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context);

    /// <summary>
    ///   Gets the thread context for the given thread.
    /// </summary>
    /// <param name="threadID">The OS thread ID to read the context from.</param>
    /// <param name="contextFlags">The requested context flags, or 0 for default flags.</param>
    /// <param name="contextSize">The size (in bytes) of the context parameter.</param>
    /// <param name="context">A pointer to the buffer to write to.</param>
    bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context);

    /// <summary>
    ///   Read a pointer out of the target process.
    /// </summary>
    /// <returns>
    ///   The pointer at the give address, or 0 if that pointer doesn't exist in
    ///   the data target.
    /// </returns>
    ulong ReadPointerUnsafe(ulong addr);

    /// <summary>
    ///   Read an int out of the target process.
    /// </summary>
    /// <returns>
    ///   The int at the give address, or 0 if that pointer doesn't exist in
    ///   the data target.
    /// </returns>
    uint ReadDwordUnsafe(ulong addr);
  }

  /// <summary>
  ///   The type of crash dump reader to use.
  /// </summary>
  public enum CrashDumpReader
  {
    /// <summary>
    ///   Use DbgEng.  This allows the user to obtain an instance of IDebugClient through the
    ///   DataTarget.DebuggerInterface property, at the cost of strict threading requirements.
    /// </summary>
    DbgEng,

    /// <summary>
    ///   Use a simple dump reader to read data out of the crash dump.  This allows processing
    ///   multiple dumps (using separate DataTargets) on multiple threads, but the
    ///   DataTarget.DebuggerInterface property will return null.
    /// </summary>
    ClrMD
  }

  /// <summary>
  ///   A crash dump or live process to read out of.
  /// </summary>
  public abstract class DataTarget : IDisposable
  {
    /// <summary>
    ///   Creates a DataTarget from a crash dump.
    /// </summary>
    /// <param name="fileName">The crash dump's filename.</param>
    /// <returns>A DataTarget instance.</returns>
    public static DataTarget LoadCrashDump(string fileName)
    {
      var reader = new DbgEngDataReader(fileName);
      return CreateFromReader(reader, reader.DebuggerInterface);
    }

    /// <summary>
    ///   Creates a DataTarget from a crash dump, specifying the dump reader to use.
    /// </summary>
    /// <param name="fileName">The crash dump's filename.</param>
    /// <param name="dumpReader">The type of dump reader to use.</param>
    /// <returns>A DataTarget instance.</returns>
    public static DataTarget LoadCrashDump(string fileName, CrashDumpReader dumpReader)
    {
      if (dumpReader == CrashDumpReader.DbgEng)
      {
        var reader = new DbgEngDataReader(fileName);
        return CreateFromReader(reader, reader.DebuggerInterface);
      }
      else
      {
        var reader = new DumpDataReader(fileName);
        return CreateFromReader(reader, null);
      }
    }

    /// <summary>
    ///   Create an instance of DataTarget from a user defined DataReader
    /// </summary>
    /// <param name="reader">A user defined DataReader.</param>
    /// <returns>A new DataTarget instance.</returns>
    public static DataTarget CreateFromDataReader(IDataReader reader)
    {
      return CreateFromReader(reader, null);
    }

    private static DataTarget CreateFromReader(IDataReader reader, IDebugClient client)
    {
#if _TRACING
            reader = new TraceDataReader(reader);
#endif
      return new DataTargetImpl(reader, client);
    }

    /// <summary>
    ///   Creates a data target from an existing IDebugClient interface.  If you created and attached
    ///   a dbgeng based debugger to a process you may pass the IDebugClient RCW object to this function
    ///   to create the DataTarget.
    /// </summary>
    /// <param name="client">The dbgeng IDebugClient object.  We will query interface on this for IDebugClient.</param>
    /// <returns>A DataTarget instance.</returns>
    public static DataTarget CreateFromDebuggerInterface(IDebugClient client)
    {
      var reader = new DbgEngDataReader(client);
      var dataTarget = new DataTargetImpl(reader, reader.DebuggerInterface);

      return dataTarget;
    }

    /// <summary>
    ///   Invasively attaches to a live process.
    /// </summary>
    /// <param name="pid">The process ID of the process to attach to.</param>
    /// <param name="msecTimeout">Timeout in milliseconds.</param>
    /// <returns>A DataTarget instance.</returns>
    public static DataTarget AttachToProcess(int pid, uint msecTimeout)
    {
      return AttachToProcess(pid, msecTimeout, AttachFlag.Invasive);
    }

    /// <summary>
    ///   Attaches to a live process.
    /// </summary>
    /// <param name="pid">The process ID of the process to attach to.</param>
    /// <param name="msecTimeout">Timeout in milliseconds.</param>
    /// <param name="attachFlag">The type of attach requested for the target process.</param>
    /// <returns>A DataTarget instance.</returns>
    public static DataTarget AttachToProcess(int pid, uint msecTimeout, AttachFlag attachFlag)
    {
      IDebugClient client = null;
      IDataReader reader;
      if (attachFlag == AttachFlag.Passive)
      {
        reader = new LiveDataReader(pid, false);
      }
      else
      {
        var dbgeng = new DbgEngDataReader(pid, attachFlag, msecTimeout);
        reader = dbgeng;
        client = dbgeng.DebuggerInterface;
      }

      var dataTarget = new DataTargetImpl(reader, client);
      return dataTarget;
    }

    /// <summary>
    ///   Attaches to a snapshot process (see https://msdn.microsoft.com/en-us/library/dn457825(v=vs.85).aspx).
    /// </summary>
    /// <param name="pid">The process ID of the process to attach to.</param>
    /// <returns>A DataTarget instance.</returns>
    public static DataTarget CreateSnapshotAndAttach(int pid)
    {
      IDataReader reader = new LiveDataReader(pid, true);
      var dataTarget = new DataTargetImpl(reader, null);
      return dataTarget;
    }

    /// <summary>
    ///   The data reader for this instance.
    /// </summary>
    public abstract IDataReader DataReader { get; }

    private SymbolLocator _symbolLocator;
    /// <summary>
    ///   Instance to manage the symbol path(s)
    /// </summary>
    public SymbolLocator SymbolLocator
    {
      get
      {
        if (_symbolLocator == null)
          _symbolLocator = new DefaultSymbolLocator();

        return _symbolLocator;
      }
      set => _symbolLocator = value;
    }

    /// <summary>
    ///   A symbol provider which loads PDBs on behalf of ClrMD.  This should be set so that when ClrMD needs to
    ///   resolve names which can only come from PDBs.  If this is not set, you may have a degraded experience.
    /// </summary>
    public ISymbolProvider SymbolProvider { get; set; }

    private FileLoader _fileLoader;
    internal FileLoader FileLoader
    {
      get
      {
        if (_fileLoader == null)
          _fileLoader = new FileLoader(this);

        return _fileLoader;
      }
    }

    /// <summary>
    ///   Returns true if the target process is a minidump, or otherwise might have limited memory.  If IsMinidump
    ///   returns true, a greater range of functions may fail to return data due to the data not being present in
    ///   the application/crash dump you are debugging.
    /// </summary>
    public abstract bool IsMinidump { get; }

    /// <summary>
    ///   Returns the architecture of the target process or crash dump.
    /// </summary>
    public abstract Architecture Architecture { get; }

    /// <summary>
    ///   Returns the list of Clr versions loaded into the process.
    /// </summary>
    public abstract IList<ClrInfo> ClrVersions { get; }

    /// <summary>
    ///   Returns the pointer size for the target process.
    /// </summary>
    public abstract uint PointerSize { get; }

    /// <summary>
    ///   Reads memory from the target.
    /// </summary>
    /// <param name="address">The address to read from.</param>
    /// <param name="buffer">
    ///   The buffer to store the data in.  Size must be greator or equal to
    ///   bytesRequested.
    /// </param>
    /// <param name="bytesRequested">The amount of bytes to read from the target process.</param>
    /// <param name="bytesRead">The actual number of bytes read.</param>
    /// <returns>
    ///   True if any bytes were read out of the process (including a partial read).  False
    ///   if no bytes could be read from the address.
    /// </returns>
    public abstract bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);

    /// <summary>
    ///   Returns the IDebugClient interface associated with this datatarget.  (Will return null if the
    ///   user attached passively.)
    /// </summary>
    public abstract IDebugClient DebuggerInterface { get; }

    /// <summary>
    ///   Enumerates information about the loaded modules in the process (both managed and unmanaged).
    /// </summary>
    public abstract IEnumerable<ModuleInfo> EnumerateModules();

    /// <summary>
    ///   IDisposable implementation.
    /// </summary>
    public abstract void Dispose();
  }

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

  internal class DacLibrary
  {
    #region Variables
    private readonly IntPtr _library;
    private readonly DacDataTarget _dacDataTarget;
    private readonly IXCLRDataProcess _dac;
    private ISOSDac _sos;
    private readonly HashSet<object> _release = new HashSet<object>();
    #endregion

    public DacDataTarget DacDataTarget => _dacDataTarget;

    public IXCLRDataProcess DacInterface => _dac;

    public ISOSDac SOSInterface
    {
      get
      {
        if (_sos == null)
          _sos = (ISOSDac)_dac;

        return _sos;
      }
    }

    public DacLibrary(DataTargetImpl dataTarget, object ix)
    {
      _dac = ix as IXCLRDataProcess;
      if (_dac == null)
        throw new ArgumentException("clrDataProcess not an instance of IXCLRDataProcess");
    }

    public DacLibrary(DataTargetImpl dataTarget, string dacDll)
    {
      if (dataTarget.ClrVersions.Count == 0)
        throw new ClrDiagnosticsException("Process is not a CLR process!");

      _library = NativeMethods.LoadLibrary(dacDll);
      if (_library == IntPtr.Zero)
        throw new ClrDiagnosticsException("Failed to load dac: " + dacDll);

      var addr = NativeMethods.GetProcAddress(_library, "CLRDataCreateInstance");
      _dacDataTarget = new DacDataTarget(dataTarget);

      var func = (NativeMethods.CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(NativeMethods.CreateDacInstance));
      var guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
      var res = func(ref guid, _dacDataTarget, out var obj);

      if (res == 0)
        _dac = obj as IXCLRDataProcess;

      if (_dac == null)
        throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);
    }

    ~DacLibrary()
    {
      foreach (var obj in _release)
        Marshal.FinalReleaseComObject(obj);

      if (_dac != null)
        Marshal.FinalReleaseComObject(_dac);

      if (_library != IntPtr.Zero)
        NativeMethods.FreeLibrary(_library);
    }

    internal void AddToReleaseList(object obj)
    {
      Debug.Assert(Marshal.IsComObject(obj));
      _release.Add(obj);
    }
  }

  internal class DacDataTarget : IDacDataTarget, IMetadataLocator, ICorDebugDataTarget
  {
    private readonly DataTargetImpl _dataTarget;
    private readonly IDataReader _dataReader;
    private readonly ModuleInfo[] _modules;

    public DacDataTarget(DataTargetImpl dataTarget)
    {
      _dataTarget = dataTarget;
      _dataReader = _dataTarget.DataReader;
      _modules = dataTarget.EnumerateModules().ToArray();
      Array.Sort(_modules, delegate(ModuleInfo a, ModuleInfo b) { return a.ImageBase.CompareTo(b.ImageBase); });
    }

    public CorDebugPlatform GetPlatform()
    {
      var arch = _dataReader.GetArchitecture();

      switch (arch)
      {
        case Architecture.Amd64:
          return CorDebugPlatform.CORDB_PLATFORM_WINDOWS_AMD64;

        case Architecture.X86:
          return CorDebugPlatform.CORDB_PLATFORM_WINDOWS_X86;

        case Architecture.Arm:
          return CorDebugPlatform.CORDB_PLATFORM_WINDOWS_ARM;

        default:
          throw new Exception();
      }
    }

    public uint ReadVirtual(ulong address, IntPtr buffer, uint bytesRequested)
    {
      if (ReadVirtual(address, buffer, (int)bytesRequested, out var read) >= 0)
        return (uint)read;

      throw new Exception();
    }

    void ICorDebugDataTarget.GetThreadContext(uint threadId, uint contextFlags, uint contextSize, IntPtr context)
    {
      if (!_dataReader.GetThreadContext(threadId, contextFlags, contextSize, context))
        throw new Exception();
    }

    public void GetMachineType(out IMAGE_FILE_MACHINE machineType)
    {
      var arch = _dataReader.GetArchitecture();

      switch (arch)
      {
        case Architecture.Amd64:
          machineType = IMAGE_FILE_MACHINE.AMD64;
          break;

        case Architecture.X86:
          machineType = IMAGE_FILE_MACHINE.I386;
          break;

        case Architecture.Arm:
          machineType = IMAGE_FILE_MACHINE.THUMB2;
          break;

        default:
          machineType = IMAGE_FILE_MACHINE.UNKNOWN;
          break;
      }
    }

    private ModuleInfo GetModule(ulong address)
    {
      int min = 0, max = _modules.Length - 1;

      while (min <= max)
      {
        var i = (min + max) / 2;
        var curr = _modules[i];

        if (curr.ImageBase <= address && address < curr.ImageBase + curr.FileSize)
          return curr;

        if (curr.ImageBase < address)
          min = i + 1;
        else
          max = i - 1;
      }

      return null;
    }

    public void GetPointerSize(out uint pointerSize)
    {
      pointerSize = _dataReader.GetPointerSize();
    }

    public void GetImageBase(string imagePath, out ulong baseAddress)
    {
      imagePath = Path.GetFileNameWithoutExtension(imagePath);

      foreach (var module in _modules)
      {
        var moduleName = Path.GetFileNameWithoutExtension(module.FileName);
        if (imagePath.Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
        {
          baseAddress = module.ImageBase;
          return;
        }
      }

      throw new Exception();
    }

    public unsafe int ReadVirtual(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
    {
      if (_dataReader.ReadMemory(address, buffer, bytesRequested, out var read))
      {
        bytesRead = read;
        return 0;
      }

      var info = GetModule(address);
      if (info != null)
      {
        var filePath = _dataTarget.SymbolLocator.FindBinary(info.FileName, info.TimeStamp, info.FileSize, true);
        if (filePath == null)
        {
          bytesRead = 0;
          return -1;
        }

        // We do not put a using statement here to prevent needing to load/unload the binary over and over.
        var file = _dataTarget.FileLoader.LoadPEFile(filePath);
        if (file?.Header != null)
        {
          var peBuffer = file.AllocBuff();

          var rva = checked((int)(address - info.ImageBase));

          if (file.Header.TryGetFileOffsetFromRva(rva, out rva))
          {
            var dst = (byte*)buffer.ToPointer();
            var src = peBuffer.Fetch(rva, bytesRequested);

            for (var i = 0; i < bytesRequested; i++)
              dst[i] = src[i];

            bytesRead = bytesRequested;
            return 0;
          }

          file.FreeBuff(peBuffer);
        }
      }

      bytesRead = 0;
      return -1;
    }

    public int ReadMemory(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
    {
      if (_dataReader.ReadMemory(address, buffer, (int)bytesRequested, out var read))
      {
        bytesRead = (uint)read;
        return 0;
      }

      bytesRead = 0;
      return -1;
    }

    public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
    {
      return ReadMemory(address, buffer, bytesRequested, out bytesRead);
    }

    public void WriteVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesWritten)
    {
      // This gets used by MemoryBarrier() calls in the dac, which really shouldn't matter what we do here.
      bytesWritten = bytesRequested;
    }

    public void GetTLSValue(uint threadID, uint index, out ulong value)
    {
      // TODO:  Validate this is not used?
      value = 0;
    }

    public void SetTLSValue(uint threadID, uint index, ulong value)
    {
      throw new NotImplementedException();
    }

    public void GetCurrentThreadID(out uint threadID)
    {
      threadID = 0;
    }

    public void GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
    {
      _dataReader.GetThreadContext(threadID, contextFlags, contextSize, context);
    }

    public void SetThreadContext(uint threadID, uint contextSize, IntPtr context)
    {
      throw new NotImplementedException();
    }

    public void Request(uint reqCode, uint inBufferSize, IntPtr inBuffer, IntPtr outBufferSize, out IntPtr outBuffer)
    {
      throw new NotImplementedException();
    }

    public int GetMetadata(string filename, uint imageTimestamp, uint imageSize, IntPtr mvid, uint mdRva, uint flags, uint bufferSize, byte[] buffer, IntPtr dataSize)
    {
      var filePath = _dataTarget.SymbolLocator.FindBinary(filename, imageTimestamp, imageSize, true);
      if (filePath == null)
        return -1;

      // We do not put a using statement here to prevent needing to load/unload the binary over and over.
      var file = _dataTarget.FileLoader.LoadPEFile(filePath);
      if (file == null)
        return -1;

      var comDescriptor = file.Header.ComDescriptorDirectory;
      if (comDescriptor.VirtualAddress == 0)
        return -1;

      var peBuffer = file.AllocBuff();
      if (mdRva == 0)
      {
        var hdr = file.SafeFetchRVA(comDescriptor.VirtualAddress, comDescriptor.Size, peBuffer);

        var corhdr = (IMAGE_COR20_HEADER)Marshal.PtrToStructure(hdr, typeof(IMAGE_COR20_HEADER));
        if (bufferSize < corhdr.MetaData.Size)
        {
          file.FreeBuff(peBuffer);
          return -1;
        }

        mdRva = corhdr.MetaData.VirtualAddress;
        bufferSize = corhdr.MetaData.Size;
      }

      var ptr = file.SafeFetchRVA((int)mdRva, (int)bufferSize, peBuffer);
      Marshal.Copy(ptr, buffer, 0, (int)bufferSize);

      file.FreeBuff(peBuffer);
      return 0;
    }
  }

  internal unsafe class DbgEngDataReader : IDisposable, IDataReader
  {
    private static int s_totalInstanceCount;
    private static bool s_needRelease = true;

    private IDebugClient _client;
    private IDebugDataSpaces _spaces;
    private IDebugDataSpaces2 _spaces2;
    private IDebugDataSpacesPtr _spacesPtr;
    private IDebugSymbols _symbols;
    private IDebugSymbols3 _symbols3;
    private IDebugControl2 _control;
    private IDebugAdvanced _advanced;
    private IDebugSystemObjects _systemObjects;
    private IDebugSystemObjects3 _systemObjects3;

    private uint _instance;
    private bool _disposed;

    private readonly byte[] _ptrBuffer = new byte[IntPtr.Size];
    private List<ModuleInfo> _modules;
    private bool? _minidump;

    ~DbgEngDataReader()
    {
      Dispose(false);
    }

    private void SetClientInstance()
    {
      Debug.Assert(s_totalInstanceCount > 0);

      if (_systemObjects3 != null && s_totalInstanceCount > 1)
        _systemObjects3.SetCurrentSystemId(_instance);
    }

    public DbgEngDataReader(string dumpFile)
    {
      if (!File.Exists(dumpFile))
        throw new FileNotFoundException(dumpFile);

      var client = CreateIDebugClient();
      var hr = client.OpenDumpFile(dumpFile);

      if (hr != 0)
        throw new ClrDiagnosticsException(string.Format("Could not load crash dump '{0}', HRESULT: 0x{1:x8}", dumpFile, hr), ClrDiagnosticsException.HR.DebuggerError);

      CreateClient(client);

      // This actually "attaches" to the crash dump.
      _control.WaitForEvent(0, 0xffffffff);
    }

    public DbgEngDataReader(IDebugClient client)
    {
      //* We need to be very careful to not cleanup the IDebugClient interfaces
      // * (that is, detach from the target process) if we created this wrapper
      // * from a pre-existing IDebugClient interface.  Setting s_needRelease to
      // * false will keep us from *ever* explicitly detaching from any IDebug
      // * interface (even ones legitimately attached with other constructors),
      // * but this is the best we can do with DbgEng's design.  Better to leak
      // * a small amount of memory (and file locks) than detatch someone else's
      // * IDebug interface unexpectedly.
      // 
      CreateClient(client);
      s_needRelease = false;
    }

    public DbgEngDataReader(int pid, AttachFlag flags, uint msecTimeout)
    {
      var client = CreateIDebugClient();
      CreateClient(client);

      var attach = flags == AttachFlag.Invasive ? DEBUG_ATTACH.DEFAULT : DEBUG_ATTACH.NONINVASIVE;
      var hr = _control.AddEngineOptions(DEBUG_ENGOPT.INITIAL_BREAK);

      if (hr == 0)
        hr = client.AttachProcess(0, (uint)pid, attach);

      if (hr == 0)
        hr = _control.WaitForEvent(0, msecTimeout);

      if (hr == 1) throw new TimeoutException("Break in did not occur within the allotted timeout.");

      if (hr != 0)
      {
        if ((uint)hr == 0xd00000bb)
          throw new InvalidOperationException("Mismatched architecture between this process and the target process.");

        throw new ClrDiagnosticsException(string.Format("Could not attach to pid {0:X}, HRESULT: 0x{1:x8}", pid, hr), ClrDiagnosticsException.HR.DebuggerError);
      }
    }

    public bool IsMinidump
    {
      get
      {
        if (_minidump != null)
          return (bool)_minidump;

        SetClientInstance();

        _control.GetDebuggeeType(out var cls, out var qual);

        if (qual == DEBUG_CLASS_QUALIFIER.USER_WINDOWS_SMALL_DUMP)
        {
          _control.GetDumpFormatFlags(out var flags);
          _minidump = (flags & DEBUG_FORMAT.USER_SMALL_FULL_MEMORY) == 0;
          return _minidump.Value;
        }

        _minidump = false;
        return false;
      }
    }

    public Architecture GetArchitecture()
    {
      SetClientInstance();

      var hr = _control.GetExecutingProcessorType(out var machineType);
      if (0 != hr)
        throw new ClrDiagnosticsException(string.Format("Failed to get proessor type, HRESULT: {0:x8}", hr), ClrDiagnosticsException.HR.DebuggerError);

      switch (machineType)
      {
        case IMAGE_FILE_MACHINE.I386:
          return Architecture.X86;

        case IMAGE_FILE_MACHINE.AMD64:
          return Architecture.Amd64;

        case IMAGE_FILE_MACHINE.ARM:
        case IMAGE_FILE_MACHINE.THUMB:
        case IMAGE_FILE_MACHINE.THUMB2:
          return Architecture.Arm;

        default:
          return Architecture.Unknown;
      }
    }

    private static IDebugClient CreateIDebugClient()
    {
      var guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
      NativeMethods.DebugCreate(ref guid, out var obj);

      var client = (IDebugClient)obj;
      return client;
    }

    public void Close()
    {
      Dispose();
    }

    internal IDebugClient DebuggerInterface => _client;

    public uint GetPointerSize()
    {
      SetClientInstance();
      var hr = _control.IsPointer64Bit();
      if (hr == 0)
        return 8;
      if (hr == 1)
        return 4;

      throw new ClrDiagnosticsException(string.Format("IsPointer64Bit failed: {0:x8}", hr), ClrDiagnosticsException.HR.DebuggerError);
    }

    public void Flush()
    {
      _modules = null;
    }

    public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
    {
      GetThreadIdBySystemId(threadID, out var id);

      SetCurrentThreadId(id);
      GetThreadContext(context, contextSize);

      return true;
    }

    private void GetThreadContext(IntPtr context, uint contextSize)
    {
      SetClientInstance();
      _advanced.GetThreadContext(context, contextSize);
    }

    internal int ReadVirtual(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
    {
      SetClientInstance();
      if (buffer == null)
        throw new ArgumentNullException("buffer");

      if (buffer.Length < bytesRequested)
        bytesRequested = buffer.Length;

      var res = _spaces.ReadVirtual(address, buffer, (uint)bytesRequested, out var read);
      bytesRead = (int)read;
      return res;
    }

    private ulong[] GetImageBases()
    {
      if (GetNumberModules(out var count, out var unloadedCount) < 0)
        return null;

      var bases = new List<ulong>((int)count);
      for (uint i = 0; i < count; ++i)
      {
        if (GetModuleByIndex(i, out var image) < 0)
          continue;

        bases.Add(image);
      }

      return bases.ToArray();
    }

    public IList<ModuleInfo> EnumerateModules()
    {
      if (_modules != null)
        return _modules;

      var bases = GetImageBases();
      if (bases == null || bases.Length == 0)
        return new ModuleInfo[0];

      var mods = new DEBUG_MODULE_PARAMETERS[bases.Length];
      var modules = new List<ModuleInfo>();
      var encounteredBases = new HashSet<ulong>();

      if (bases != null && CanEnumerateModules)
      {
        var hr = GetModuleParameters(bases.Length, bases, 0, mods);
        if (hr >= 0)
          for (var i = 0; i < bases.Length; ++i)
          {
            var info = new ModuleInfo(this)
            {
              TimeStamp = mods[i].TimeDateStamp,
              FileSize = mods[i].Size,
              ImageBase = bases[i]
            };

            var sbpath = new StringBuilder();
            if (GetModuleNameString(DEBUG_MODNAME.IMAGE, i, bases[i], null, 0, out var needed) >= 0 && needed > 1)
            {
              sbpath.EnsureCapacity((int)needed);
              if (GetModuleNameString(DEBUG_MODNAME.IMAGE, i, bases[i], sbpath, needed, out needed) >= 0)
                info.FileName = sbpath.ToString();
            }

            modules.Add(info);
          }
      }

      _modules = modules;
      return modules;
    }

    public bool CanEnumerateModules => _symbols3 != null;

    internal int GetModuleParameters(int count, ulong[] bases, int start, DEBUG_MODULE_PARAMETERS[] mods)
    {
      SetClientInstance();
      return _symbols.GetModuleParameters((uint)count, bases, (uint)start, mods);
    }

    private void CreateClient(IDebugClient client)
    {
      _client = client;

      _spaces = (IDebugDataSpaces)_client;
      _spacesPtr = (IDebugDataSpacesPtr)_client;
      _symbols = (IDebugSymbols)_client;
      _control = (IDebugControl2)_client;

      // These interfaces may not be present in older DbgEng dlls.
      _spaces2 = _client as IDebugDataSpaces2;
      _symbols3 = _client as IDebugSymbols3;
      _advanced = _client as IDebugAdvanced;
      _systemObjects = _client as IDebugSystemObjects;
      _systemObjects3 = _client as IDebugSystemObjects3;

      Interlocked.Increment(ref s_totalInstanceCount);

      if (_systemObjects3 == null && s_totalInstanceCount > 1)
        throw new ClrDiagnosticsException("This version of DbgEng is too old to create multiple instances of DataTarget.", ClrDiagnosticsException.HR.DebuggerError);

      if (_systemObjects3 != null)
        _systemObjects3.GetCurrentSystemId(out _instance);
    }

    internal int GetModuleNameString(DEBUG_MODNAME Which, int Index, ulong Base, StringBuilder Buffer, uint BufferSize, out uint NameSize)
    {
      if (_symbols3 == null)
      {
        NameSize = 0;
        return -1;
      }

      SetClientInstance();
      return _symbols3.GetModuleNameString(Which, (uint)Index, Base, Buffer, BufferSize, out NameSize);
    }

    internal int GetNumberModules(out uint count, out uint unloadedCount)
    {
      if (_symbols3 == null)
      {
        count = 0;
        unloadedCount = 0;
        return -1;
      }

      SetClientInstance();
      return _symbols3.GetNumberModules(out count, out unloadedCount);
    }

    internal int GetModuleByIndex(uint i, out ulong image)
    {
      if (_symbols3 == null)
      {
        image = 0;
        return -1;
      }

      SetClientInstance();
      return _symbols3.GetModuleByIndex(i, out image);
    }

    internal int GetNameByOffsetWide(ulong offset, StringBuilder sb, int p, out uint size, out ulong disp)
    {
      SetClientInstance();
      return _symbols3.GetNameByOffsetWide(offset, sb, p, out size, out disp);
    }

    public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
    {
      vq = new VirtualQueryData();
      if (_spaces2 == null)
        return false;

      SetClientInstance();
      var hr = _spaces2.QueryVirtual(addr, out var mem);
      vq.BaseAddress = mem.BaseAddress;
      vq.Size = mem.RegionSize;

      return hr == 0;
    }

    public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
    {
      return ReadVirtual(address, buffer, bytesRequested, out bytesRead) >= 0;
    }

    public ulong ReadPointerUnsafe(ulong addr)
    {
      if (ReadVirtual(addr, _ptrBuffer, IntPtr.Size, out var read) != 0)
        return 0;

      fixed (byte* r = _ptrBuffer)
      {
        if (IntPtr.Size == 4)
          return *((uint*)r);

        return *((ulong*)r);
      }
    }

    public uint ReadDwordUnsafe(ulong addr)
    {
      if (ReadVirtual(addr, _ptrBuffer, 4, out var read) != 0)
        return 0;

      fixed (byte* r = _ptrBuffer)
      {
        return *((uint*)r);
      }
    }

    internal void SetSymbolPath(string path)
    {
      SetClientInstance();
      _symbols.SetSymbolPath(path);
      _control.Execute(DEBUG_OUTCTL.NOT_LOGGED, ".reload", DEBUG_EXECUTE.NOT_LOGGED);
    }

    internal int QueryVirtual(ulong addr, out MEMORY_BASIC_INFORMATION64 mem)
    {
      if (_spaces2 == null)
      {
        mem = new MEMORY_BASIC_INFORMATION64();
        return -1;
      }

      SetClientInstance();
      return _spaces2.QueryVirtual(addr, out mem);
    }

    internal int GetModuleByModuleName(string image, int start, out uint index, out ulong baseAddress)
    {
      SetClientInstance();
      return _symbols.GetModuleByModuleName(image, (uint)start, out index, out baseAddress);
    }

    public void GetVersionInfo(ulong addr, out VersionInfo version)
    {
      version = new VersionInfo();

      var hr = _symbols.GetModuleByOffset(addr, 0, out var index, out var baseAddr);
      if (hr != 0)
        return;

      hr = GetModuleVersionInformation(index, baseAddr, "\\", null, 0, out var needed);
      if (hr != 0)
        return;

      var buffer = new byte[needed];
      hr = GetModuleVersionInformation(index, baseAddr, "\\", buffer, needed, out needed);
      if (hr != 0)
        return;

      version.Minor = (ushort)Marshal.ReadInt16(buffer, 8);
      version.Major = (ushort)Marshal.ReadInt16(buffer, 10);
      version.Patch = (ushort)Marshal.ReadInt16(buffer, 12);
      version.Revision = (ushort)Marshal.ReadInt16(buffer, 14);
    }

    internal int GetModuleVersionInformation(uint index, ulong baseAddress, string p, byte[] buffer, uint needed1, out uint needed2)
    {
      if (_symbols3 == null)
      {
        needed2 = 0;
        return -1;
      }

      SetClientInstance();
      return _symbols3.GetModuleVersionInformation(index, baseAddress, "\\", buffer, needed1, out needed2);
    }

    internal int GetModuleNameString(DEBUG_MODNAME requestType, uint index, ulong baseAddress, StringBuilder sbpath, uint needed1, out uint needed2)
    {
      if (_symbols3 == null)
      {
        needed2 = 0;
        return -1;
      }

      SetClientInstance();
      return _symbols3.GetModuleNameString(requestType, index, baseAddress, sbpath, needed1, out needed2);
    }

    internal int GetModuleParameters(uint Count, ulong[] Bases, uint Start, DEBUG_MODULE_PARAMETERS[] Params)
    {
      SetClientInstance();
      return _symbols.GetModuleParameters(Count, Bases, Start, Params);
    }

    internal void GetThreadIdBySystemId(uint threadID, out uint id)
    {
      SetClientInstance();
      _systemObjects.GetThreadIdBySystemId(threadID, out id);
    }

    internal void SetCurrentThreadId(uint id)
    {
      SetClientInstance();
      _systemObjects.SetCurrentThreadId(id);
    }

    internal void GetExecutingProcessorType(out IMAGE_FILE_MACHINE machineType)
    {
      SetClientInstance();
      _control.GetEffectiveProcessorType(out machineType);
    }

    public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
    {
      SetClientInstance();

      var res = _spacesPtr.ReadVirtual(address, buffer, (uint)bytesRequested, out var read) >= 0;
      bytesRead = res ? (int)read : 0;
      return res;
    }

    public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
    {
      SetClientInstance();
      return _spaces.ReadVirtual(address, buffer, bytesRequested, out bytesRead);
    }

    public IEnumerable<uint> EnumerateAllThreads()
    {
      SetClientInstance();

      var hr = _systemObjects.GetNumberThreads(out var count);
      if (hr == 0)
      {
        var sysIds = new uint[count];

        hr = _systemObjects.GetThreadIdsByIndex(0, count, null, sysIds);
        if (hr == 0)
          return sysIds;
      }

      return new uint[0];
    }

    public ulong GetThreadTeb(uint thread)
    {
      SetClientInstance();

      ulong teb = 0;
      var hr = _systemObjects.GetCurrentThreadId(out var id);
      var haveId = hr == 0;

      if (_systemObjects.GetThreadIdBySystemId(thread, out id) == 0 && _systemObjects.SetCurrentThreadId(id) == 0)
        _systemObjects.GetCurrentThreadTeb(out teb);

      if (haveId)
        _systemObjects.SetCurrentThreadId(id);

      return teb;
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (_disposed)
        return;

      _disposed = true;

      var count = Interlocked.Decrement(ref s_totalInstanceCount);
      if (count == 0 && s_needRelease && disposing)
      {
        if (_systemObjects3 != null)
          _systemObjects3.SetCurrentSystemId(_instance);

        _client.EndSession(DEBUG_END.ACTIVE_DETACH);
        _client.DetachProcesses();
      }

      // If there are no more debug instances, we can safely reset this variable
      // and start releasing newly created IDebug objects.
      if (count == 0)
        s_needRelease = true;
    }

    public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
    {
      GetThreadIdBySystemId(threadID, out var id);

      SetCurrentThreadId(id);
      fixed (byte* pContext = &context[0])
      {
        GetThreadContext(new IntPtr(pContext), contextSize);
      }

      return true;
    }
  }

  internal unsafe class LiveDataReader : IDataReader
  {
    #region Variables
    private readonly int _originalPid;
    private readonly IntPtr _snapshotHandle;
    private readonly IntPtr _cloneHandle;
    private IntPtr _process;
    private readonly int _pid;
    #endregion

    private const int PROCESS_VM_READ = 0x10;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;

    public LiveDataReader(int pid, bool createSnapshot)
    {
      if (createSnapshot)
      {
        _originalPid = pid;
        var process = Process.GetProcessById(pid);
        var hr = PssCaptureSnapshot(process.Handle, PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE, IntPtr.Size == 8 ? 0x0010001F : 0x0001003F, out _snapshotHandle);
        if (hr != 0) throw new ClrDiagnosticsException(string.Format("Could not create snapshot to process. Error {0}.", hr));

        hr = PssQuerySnapshot(_snapshotHandle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_VA_CLONE_INFORMATION, out _cloneHandle, IntPtr.Size);
        if (hr != 0) throw new ClrDiagnosticsException(string.Format("Could not query the snapshot. Error {0}.", hr));

        _pid = GetProcessId(_cloneHandle);
      }
      else
      {
        _pid = pid;
      }

      _process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _pid);

      if (_process == IntPtr.Zero)
        throw new ClrDiagnosticsException(string.Format("Could not attach to process. Error {0}.", Marshal.GetLastWin32Error()));

      using (var p = Process.GetCurrentProcess())
      {
        if (NativeMethods.TryGetWow64(p.Handle, out var wow64) &&
          NativeMethods.TryGetWow64(_process, out var targetWow64) &&
          wow64 != targetWow64)
          throw new ClrDiagnosticsException("Dac architecture mismatch!");
      }
    }

    public bool IsMinidump => false;

    public void Close()
    {
      if (_originalPid != 0)
      {
        CloseHandle(_cloneHandle);
        var hr = PssFreeSnapshot(Process.GetCurrentProcess().Handle, _snapshotHandle);
        if (hr != 0) throw new ClrDiagnosticsException(string.Format("Could not free the snapshot. Error {0}.", hr));

        try
        {
          Process.GetProcessById(_pid).Kill();
        }
        catch (Win32Exception)
        {
        }
      }

      if (_process != IntPtr.Zero)
      {
        CloseHandle(_process);
        _process = IntPtr.Zero;
      }
    }

    public void Flush()
    {
    }

    public Architecture GetArchitecture()
    {
      if (IntPtr.Size == 4)
        return Architecture.X86;

      return Architecture.Amd64;
    }

    public uint GetPointerSize()
    {
      return (uint)IntPtr.Size;
    }

    public IList<ModuleInfo> EnumerateModules()
    {
      var result = new List<ModuleInfo>();

      EnumProcessModules(_process, null, 0, out var needed);

      var modules = new IntPtr[needed / 4];
      var size = (uint)modules.Length * sizeof(uint);

      if (!EnumProcessModules(_process, modules, size, out needed))
        throw new ClrDiagnosticsException("Unable to get process modules.", ClrDiagnosticsException.HR.DataRequestError);

      for (var i = 0; i < modules.Length; i++)
      {
        var ptr = modules[i];

        if (ptr == IntPtr.Zero) break;

        var sb = new StringBuilder(1024);
        GetModuleFileNameExA(_process, ptr, sb, sb.Capacity);

        var baseAddr = (ulong)ptr.ToInt64();
        GetFileProperties(baseAddr, out var filesize, out var timestamp);

        var filename = sb.ToString();
        var module = new ModuleInfo(this)
        {
          ImageBase = baseAddr,
          FileName = filename,
          FileSize = filesize,
          TimeStamp = timestamp
        };

        result.Add(module);
      }

      return result;
    }

    public void GetVersionInfo(ulong addr, out VersionInfo version)
    {
      var filename = new StringBuilder(1024);
      GetModuleFileNameExA(_process, new IntPtr((long)addr), filename, filename.Capacity);

      if (NativeMethods.GetFileVersion(filename.ToString(), out var major, out var minor, out var revision, out var patch))
        version = new VersionInfo(major, minor, revision, patch);
      else
        version = new VersionInfo();
    }

    public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
    {
      try
      {
        var res = ReadProcessMemory(_process, new IntPtr((long)address), buffer, bytesRequested, out bytesRead);
        return res != 0;
      }
      catch
      {
        bytesRead = 0;
        return false;
      }
    }

    public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
    {
      try
      {
        var res = RawPinvokes.ReadProcessMemory(_process, new IntPtr((long)address), buffer, bytesRequested, out bytesRead);
        return res != 0;
      }
      catch
      {
        bytesRead = 0;
        return false;
      }
    }

    private readonly byte[] _ptrBuffer = new byte[IntPtr.Size];

    public ulong ReadPointerUnsafe(ulong addr)
    {
      if (!ReadMemory(addr, _ptrBuffer, IntPtr.Size, out var read))
        return 0;

      fixed (byte* r = _ptrBuffer)
      {
        if (IntPtr.Size == 4)
          return *((uint*)r);

        return *((ulong*)r);
      }
    }

    public uint ReadDwordUnsafe(ulong addr)
    {
      if (!ReadMemory(addr, _ptrBuffer, 4, out var read))
        return 0;

      fixed (byte* r = _ptrBuffer)
      {
        return *((uint*)r);
      }
    }

    public ulong GetThreadTeb(uint thread)
    {
      // todo
      throw new NotImplementedException();
    }

    public IEnumerable<uint> EnumerateAllThreads()
    {
      var p = Process.GetProcessById(_pid);
      foreach (ProcessThread thread in p.Threads)
        yield return (uint)thread.Id;
    }

    public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
    {
      vq = new VirtualQueryData();

      var mem = new MEMORY_BASIC_INFORMATION();
      var ptr = new IntPtr((long)addr);

      var res = VirtualQueryEx(_process, ptr, ref mem, new IntPtr(Marshal.SizeOf(mem)));
      if (res == 0)
        return false;

      vq.BaseAddress = mem.BaseAddress;
      vq.Size = mem.Size;
      return true;
    }

    public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
    {
      using (var thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
      {
        if (thread.IsInvalid)
          return false;

        var res = GetThreadContext(thread.DangerousGetHandle(), context);
        return res;
      }
    }

    public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
    {
      using (var thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
      {
        if (thread.IsInvalid)
          return false;

        fixed (byte* b = context)
        {
          var res = GetThreadContext(thread.DangerousGetHandle(), new IntPtr(b));
          return res;
        }
      }
    }

    private void GetFileProperties(ulong moduleBase, out uint filesize, out uint timestamp)
    {
      filesize = 0;
      timestamp = 0;
      var buffer = new byte[4];

      if (ReadMemory(moduleBase + 0x3c, buffer, buffer.Length, out var read) && read == buffer.Length)
      {
        var sigOffset = (uint)BitConverter.ToInt32(buffer, 0);
        var sigLength = 4;

        if (ReadMemory(moduleBase + sigOffset, buffer, buffer.Length, out read) && read == buffer.Length)
        {
          var header = (uint)BitConverter.ToInt32(buffer, 0);

          // Ensure the module contains the magic "PE" value at the offset it says it does.  This check should
          // never fail unless we have the wrong base address for CLR.
          Debug.Assert(header == 0x4550);
          if (header == 0x4550)
          {
            const int timeDataOffset = 4;
            const int imageSizeOffset = 0x4c;
            if (ReadMemory(moduleBase + sigOffset + (ulong)sigLength + timeDataOffset, buffer, buffer.Length, out read) && read == buffer.Length)
              timestamp = (uint)BitConverter.ToInt32(buffer, 0);

            if (ReadMemory(moduleBase + sigOffset + (ulong)sigLength + imageSizeOffset, buffer, buffer.Length, out read) && read == buffer.Length)
              filesize = (uint)BitConverter.ToInt32(buffer, 0);
          }
        }
      }
    }

    #region PInvoke Enums
    [Flags]
    private enum PSS_CAPTURE_FLAGS : uint
    {
      PSS_CAPTURE_NONE = 0x00000000,
      PSS_CAPTURE_VA_CLONE = 0x00000001,
      PSS_CAPTURE_RESERVED_00000002 = 0x00000002,
      PSS_CAPTURE_HANDLES = 0x00000004,
      PSS_CAPTURE_HANDLE_NAME_INFORMATION = 0x00000008,
      PSS_CAPTURE_HANDLE_BASIC_INFORMATION = 0x00000010,
      PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION = 0x00000020,
      PSS_CAPTURE_HANDLE_TRACE = 0x00000040,
      PSS_CAPTURE_THREADS = 0x00000080,
      PSS_CAPTURE_THREAD_CONTEXT = 0x00000100,
      PSS_CAPTURE_THREAD_CONTEXT_EXTENDED = 0x00000200,
      PSS_CAPTURE_RESERVED_00000400 = 0x00000400,
      PSS_CAPTURE_VA_SPACE = 0x00000800,
      PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION = 0x00001000,
      PSS_CREATE_BREAKAWAY_OPTIONAL = 0x04000000,
      PSS_CREATE_BREAKAWAY = 0x08000000,
      PSS_CREATE_FORCE_BREAKAWAY = 0x10000000,
      PSS_CREATE_USE_VM_ALLOCATIONS = 0x20000000,
      PSS_CREATE_MEASURE_PERFORMANCE = 0x40000000,
      PSS_CREATE_RELEASE_SECTION = 0x80000000
    }

    private enum PSS_QUERY_INFORMATION_CLASS
    {
      PSS_QUERY_PROCESS_INFORMATION = 0,
      PSS_QUERY_VA_CLONE_INFORMATION = 1,
      PSS_QUERY_AUXILIARY_PAGES_INFORMATION = 2,
      PSS_QUERY_VA_SPACE_INFORMATION = 3,
      PSS_QUERY_HANDLE_INFORMATION = 4,
      PSS_QUERY_THREAD_INFORMATION = 5,
      PSS_QUERY_HANDLE_TRACE_INFORMATION = 6,
      PSS_QUERY_PERFORMANCE_COUNTERS = 7
    }
    #endregion

    #region PInvoke Structs
    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORY_BASIC_INFORMATION
    {
      public IntPtr Address;
      public IntPtr AllocationBase;
      public uint AllocationProtect;
      public IntPtr RegionSize;
      public uint State;
      public uint Protect;
      public uint Type;

      public ulong BaseAddress => (ulong)Address;

      public ulong Size => (ulong)RegionSize;
    }
    #endregion

    #region PInvokes
    [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true)]
    [PreserveSig]
    public static extern uint GetModuleFileNameExA([In] IntPtr hProcess, [In] IntPtr hModule, [Out] StringBuilder lpFilename, [In][MarshalAs(UnmanagedType.U4)] int nSize);

    [DllImport("kernel32.dll")]
    private static extern int ReadProcessMemory(
      IntPtr hProcess,
      IntPtr lpBaseAddress,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
      byte[] lpBuffer,
      int dwSize,
      out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

    [DllImport("kernel32.dll")]
    private static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWin32Handle OpenThread(ThreadAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32")]
    private static extern int PssCaptureSnapshot(IntPtr ProcessHandle, PSS_CAPTURE_FLAGS CaptureFlags, int ThreadContextFlags, out IntPtr SnapshotHandle);

    [DllImport("kernel32")]
    private static extern int PssFreeSnapshot(IntPtr ProcessHandle, IntPtr SnapshotHandle);

    [DllImport("kernel32")]
    private static extern int PssQuerySnapshot(IntPtr SnapshotHandle, PSS_QUERY_INFORMATION_CLASS InformationClass, out IntPtr Buffer, int BufferLength);

    [DllImport("kernel32")]
    private static extern int GetProcessId(IntPtr hObject);
    #endregion

    private enum ThreadAccess
    {
      THREAD_ALL_ACCESS = 0x1F03FF
    }
  }

  internal class RawPinvokes
  {
    [DllImport("kernel32.dll")]
    internal static extern int ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out int lpNumberOfBytesRead);
  }
}