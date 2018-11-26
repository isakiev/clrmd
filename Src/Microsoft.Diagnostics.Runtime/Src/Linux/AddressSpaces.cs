using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Linux
{
  internal interface IAddressSpace
  {
    int Read(long position, byte[] buffer, int bufferOffset, int count);
    long Length { get; }
    string Name { get; }
  }

  internal class StreamAddressSpace : IAddressSpace
  {
    private readonly Stream _stream;

    public StreamAddressSpace(Stream stream)
    {
      _stream = stream;
    }

    public long Length => _stream.Length;

    public string Name => _stream.GetFilename() ?? _stream.GetType().Name;

    public int Read(long position, byte[] buffer, int bufferOffset, int count)
    {
      _stream.Seek(position, SeekOrigin.Begin);
      return _stream.Read(buffer, bufferOffset, count);
    }
  }

  internal class RelativeAddressSpace : IAddressSpace
  {
    private readonly IAddressSpace _baseAddressSpace;
    private readonly long _baseStart;
    private readonly long _length;
    private readonly long _baseToRelativeShift;
    private readonly string _name;

    public string Name => _name == null ? _baseAddressSpace.Name : $"{_baseAddressSpace.Name}:{_name}";

    public RelativeAddressSpace(IAddressSpace baseAddressSpace, string name, long startOffset, long length) :
      this(baseAddressSpace, name, startOffset, length, -startOffset)
    {
    }

    public RelativeAddressSpace(IAddressSpace baseAddressSpace, string name, long startOffset, long length, long baseToRelativeShift)
    {
      _baseAddressSpace = baseAddressSpace;
      _baseStart = startOffset;
      _length = length;
      _baseToRelativeShift = baseToRelativeShift;
      _name = name;
    }

    public int Read(long position, byte[] buffer, int bufferOffset, int count)
    {
      var basePosition = position - _baseToRelativeShift;
      if (basePosition < _baseStart)
        return 0;

      count = (int)Math.Min(count, _length);
      return _baseAddressSpace.Read(basePosition, buffer, bufferOffset, count);
    }

    public long Length => _baseStart + _length + _baseToRelativeShift;
  }

  internal class ELFVirtualAddressSpace : IAddressSpace
  {
    private readonly IReadOnlyList<ElfProgramHeader> _segments;
    private readonly IAddressSpace _addressSpace;

    public string Name => _addressSpace.Name;

    public ELFVirtualAddressSpace(IReadOnlyList<ElfProgramHeader> segments, IAddressSpace addressSpace)
    {
      _segments = segments;
      Length = _segments.Max(s => s.Header.VirtualAddress + s.Header.VirtualSize);
      _addressSpace = addressSpace;
    }

    public long Length { get; }

    public int Read(long position, byte[] buffer, int bufferOffset, int count)
    {
      for (var i = 0; i < _segments.Count; i++)
      {
        ref var header = ref _segments[i].RefHeader;
        // FileSize == 0 means the segment isn't backed by any data
        if (header.FileSize > 0 && header.VirtualAddress <= position && position + count <= header.VirtualAddress + header.VirtualSize)
        {
          var segmentOffset = position - header.VirtualAddress;
          var fileBytes = (int)Math.Min(count, header.FileSize);

          var fileOffset = header.FileOffset + segmentOffset;
          var bytesRead = _addressSpace.Read(fileOffset, buffer, bufferOffset, fileBytes);

          //zero the rest of the buffer if it is in the virtual address space but not the physical address space
          if (bytesRead == fileBytes && fileBytes != count)
          {
            Array.Clear(buffer, bufferOffset + fileBytes, count - fileBytes);
            bytesRead = count;
          }

          return bytesRead;
        }
      }

      return 0;
    }
  }
}