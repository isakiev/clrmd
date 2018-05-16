using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
  internal class DacDataTarget : IDacDataTarget, IMetadataLocator, ICorDebugDataTarget
  {
    private readonly DataTarget _dataTarget;
    private readonly IDataReader _dataReader;
    private readonly ModuleInfo[] _modules;

    public DacDataTarget(DataTarget dataTarget)
    {
      _dataTarget = dataTarget;
      _dataReader = _dataTarget.DataReader;
      _modules = dataTarget.Modules.ToArray();
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
        var filePath = _dataTarget.SymbolLocator.FindBinary(info.FileName, (int)info.TimeStamp, (int)info.FileSize);
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
      var filePath = _dataTarget.SymbolLocator.FindBinary(filename, (int)imageTimestamp, (int)imageSize);
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
}