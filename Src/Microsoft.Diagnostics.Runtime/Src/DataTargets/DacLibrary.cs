﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DacLibrary : IDisposable
    {
        private volatile RefCountedFreeLibrary _library;
        private SOSDac _sos;

        internal DacDataTargetWrapper DacDataTarget { get; }
        
        internal RefCountedFreeLibrary OwningLibrary => _library;

        public ClrDataProcess DacPrivateInterface { get; }

        public SOSDac SOSDacInterface
        {
            get
            {
                if (_sos == null)
                    _sos = DacPrivateInterface.GetSOSDacInterface();

                return _sos;
            }
        }

        internal static IntPtr TryGetDacPtr(object ix)
        {
            if (!(ix is IntPtr pUnk))
            {
                if (Marshal.IsComObject(ix))
                    pUnk = Marshal.GetIUnknownForObject(ix);
                else
                    pUnk = IntPtr.Zero;
            }

            if (pUnk == IntPtr.Zero)
                throw new ArgumentException("clrDataProcess not an instance of IXCLRDataProcess");

            return pUnk;
        }
        
        internal DacLibrary(DataTarget dataTarget, IntPtr pUnk)
        {
          DacPrivateInterface = new ClrDataProcess(this, pUnk);
        }

        public DacLibrary(DataTarget dataTarget, string dacDll)
        {
            if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException(String.Format("Process is not a CLR process!"));

            IntPtr dacLibrary = DataTarget.PlatformFunctions.LoadLibrary(dacDll);
            if (dacLibrary == IntPtr.Zero)
              throw new ClrDiagnosticsException("Failed to load dac: " + dacLibrary);

            _library = new RefCountedFreeLibrary(dacLibrary);
            dataTarget.AddDacLibrary(this);

            IntPtr initAddr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "DAC_PAL_InitializeDLL");
            if (initAddr != IntPtr.Zero)
            {
                IntPtr dllMain = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "DllMain");
                DllMain main = (DllMain)Marshal.GetDelegateForFunctionPointer(dllMain, typeof(DllMain));
                int result = main(dacLibrary, 1, IntPtr.Zero);
            }

            IntPtr addr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "CLRDataCreateInstance");
            DacDataTarget = new DacDataTargetWrapper(dataTarget);

            CreateDacInstance func = (CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(CreateDacInstance));
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(ref guid, DacDataTarget.IDacDataTarget, out IntPtr iUnk);

            if (res != 0)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsExceptionKind.DacError);


            DacPrivateInterface = new ClrDataProcess(this, iUnk);
        }
        
        public void Dispose()
        {
          lock (this)
          {
            if (_library != null)
            {
              _library.Release();
              _library = null;
            }
          }
        }

        ~DacLibrary() => Dispose();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DllMain(IntPtr instance, int reason, IntPtr reserved);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PAL_Initialize();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDacInstance(ref Guid riid,
                               IntPtr dacDataInterface,
                               out IntPtr ppObj);
    }
}