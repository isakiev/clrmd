﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.ComWrappers;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DacLibrary
    {
        private IntPtr _library;
        private SOSDac _sos;

        public DacDataTargetWrapper DacDataTarget { get; }

        public ClrDataProcess DacInterface { get; }

        public SOSDac SOSInterface
        {
            get
            {
                if (_sos == null)
                    _sos = DacInterface.GetSOSDacInterface();

                return _sos;
            }
        }

        public DacLibrary(DataTargetImpl dataTarget, object ix)
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

            DacInterface = new ClrDataProcess(pUnk);
        }

        public DacLibrary(DataTargetImpl dataTarget, string dacDll)
        {
            if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException(String.Format("Process is not a CLR process!"));

            _library = DataTarget.PlatformFunctions.LoadLibrary(dacDll);
            if (_library == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacDll);

            IntPtr initAddr = DataTarget.PlatformFunctions.GetProcAddress(_library, "DAC_PAL_InitializeDLL");
            if (initAddr != IntPtr.Zero)
            {
                IntPtr dllMain = DataTarget.PlatformFunctions.GetProcAddress(_library, "DllMain");
                DllMain main = (DllMain)Marshal.GetDelegateForFunctionPointer(dllMain, typeof(DllMain));
                int result = main(_library, 1, IntPtr.Zero);
            }


            IntPtr addr = DataTarget.PlatformFunctions.GetProcAddress(_library, "CLRDataCreateInstance");
            DacDataTarget = new DacDataTargetWrapper(dataTarget);

            CreateDacInstance func = (CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(CreateDacInstance));
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(ref guid, DacDataTarget.IDacDataTarget, out IntPtr iUnk);

            if (res != 0)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);


            DacInterface = new ClrDataProcess(iUnk);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DllMain(IntPtr instance, int reason, IntPtr reserved);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PAL_Initialize();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDacInstance(ref Guid riid,
                               IntPtr dacDataInterface,
                               out IntPtr ppObj);

        ~DacLibrary()
        {
            if (_library != IntPtr.Zero)
                DataTarget.PlatformFunctions.FreeLibrary(_library);
        }
    }
}