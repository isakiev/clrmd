using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
  internal class DacLibrary
  {
    private readonly IntPtr _library;
    private readonly DacDataTarget _dacDataTarget;
    private readonly IXCLRDataProcess _dac;
    private ISOSDac _sos;
    private readonly HashSet<object> _release = new HashSet<object>();

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

    public DacLibrary(DataTarget dataTarget, object ix)
    {
      _dac = ix as IXCLRDataProcess;
      if (_dac == null)
        throw new ArgumentException("clrDataProcess not an instance of IXCLRDataProcess");
    }

    public DacLibrary(DataTarget dataTarget, string dacDll)
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
}