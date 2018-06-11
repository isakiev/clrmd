﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.DataReaders.DbgEng;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  internal enum ExceptionTypes : uint
  {
    AV = 0xC0000005,
    StackOverflow = 0xC00000FD,
    Cpp = 0xe06d7363,
    Clr = 0xe0434352,
    Break = 0x80000003
  }

  internal class DebuggerStartInfo
  {
    private readonly Dictionary<string, string> m_environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void SetEnvironmentVariable(string key, string value)
    {
      m_environment[key] = value;
    }

    public Debugger LaunchProcess(string commandLine, string workingDirectory)
    {
      var client = CreateIDebugClient();
      var control = (IDebugControl)client;

      if (string.IsNullOrEmpty(workingDirectory))
        workingDirectory = Environment.CurrentDirectory;

      var env = GetEnvironment();
      var options = new DEBUG_CREATE_PROCESS_OPTIONS();
      options.CreateFlags = (DEBUG_CREATE_PROCESS)1;
      var hr = client.CreateProcessAndAttach2(
        0,
        commandLine,
        ref options,
        (uint)Marshal.SizeOf(typeof(DEBUG_CREATE_PROCESS_OPTIONS)),
        workingDirectory,
        env,
        0,
        DEBUG_ATTACH.DEFAULT);

      if (hr < 0)
        throw new Exception(Debugger.GetExceptionString("IDebugClient::CreateProcessAndAttach2", hr));

      var debugger = new Debugger(client, control);
      hr = client.SetEventCallbacks(debugger);
      if (hr < 0)
        throw new Exception(Debugger.GetExceptionString("IDebugClient::SetEventCallbacks", hr));

      hr = client.SetOutputCallbacks(debugger);
      if (hr < 0)
        throw new Exception(Debugger.GetExceptionString("IDebugClient::SetOutputCallbacks", hr));

      return debugger;
    }

    [DllImport("dbgeng.dll")]
    private static extern int DebugCreate(ref Guid InterfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object Interface);

    private static IDebugClient5 CreateIDebugClient()
    {
      var guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
      object obj;
      var hr = DebugCreate(ref guid, out obj);
      if (hr < 0)
        throw new Exception(Debugger.GetExceptionString("DebugCreate", hr));

      return (IDebugClient5)obj;
    }

    private string GetEnvironment()
    {
      if (m_environment.Count == 0)
        return null;

      var sb = new StringBuilder();
      foreach (var key in m_environment.Keys)
      {
        sb.Append(key);
        sb.Append("=");

        var value = m_environment[key];
        if (value.Length > 0 && value.Contains(' ') && value[0] != '"' && value[value.Length - 1] != '"')
          value = '"' + value + '"';

        sb.Append(value);
        sb.Append((char)0);
      }

      sb.Append((char)0);
      return sb.ToString();
    }
  }

  internal class Debugger : IDebugOutputCallbacks, IDebugEventCallbacks, IDisposable
  {
    private DEBUG_OUTPUT _outputMask;
    private readonly StringBuilder _output = new StringBuilder();
    private bool m_exited, _processing;

    private readonly IDebugClient5 _client;
    private readonly IDebugControl _control;
    private DataTarget _dataTarget;

    public delegate void ModuleEventHandler(Debugger dbg, ModuleEventArgs args);

    public event ModuleEventHandler ModuleLoadEvent;
    public event ModuleEventHandler ModuleUnloadEvent;

    public delegate void CreateThreadEventHandler(Debugger dbg, CreateThreadArgs args);

    public event CreateThreadEventHandler ThreadCreateEvent;

    public delegate void ExitThreadEventHandler(Debugger dbg, int exitCode);

    public event ExitThreadEventHandler ExitThreadEvent;

    public delegate void ExceptionEventHandler(Debugger dbg, EXCEPTION_RECORD64 ex);

    public event ExceptionEventHandler FirstChanceExceptionEvent;
    public event ExceptionEventHandler SecondChanceExceptionEvent;

    public delegate void CreateProcessEventHandler(Debugger dbg, CreateProcessArgs args);

    public event CreateProcessEventHandler CreateProcessEvent;

    public delegate void ExitProcessEventHandler(Debugger dbg, int exitCode);

    public event ExitProcessEventHandler ExitProcessEvent;

    public IDebugClient5 Client => _client;

    public DataTarget DataTarget
    {
      get
      {
        if (_dataTarget == null)
        {
          var dataReader = new DbgEngDataReader(_client);
          _dataTarget = new DataTarget(dataReader);
        }

        return _dataTarget;
      }
    }

    public Debugger(IDebugClient5 client, IDebugControl control)
    {
      _client = client;
      _control = control;

      client.SetOutputCallbacks(this);
    }

    public DEBUG_STATUS ProcessEvents(uint timeout)
    {
      if (_processing)
        throw new InvalidOperationException("Cannot call ProcessEvents reentrantly.");

      if (m_exited)
        return DEBUG_STATUS.NO_DEBUGGEE;

      _processing = true;
      var hr = _control.WaitForEvent(0, timeout);
      _processing = false;

      if (hr < 0 && (uint)hr != 0x8000000A)
        throw new Exception(GetExceptionString("IDebugControl::WaitForEvent", hr));

      return GetDebugStatus();
    }

    public void TerminateProcess()
    {
      m_exited = true;
      _client.EndSession(DEBUG_END.ACTIVE_TERMINATE);
    }

    public string Execute(ulong handle, string command, string args)
    {
      var mask = _outputMask;
      _output.Clear();
      _outputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS
      | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING | DEBUG_OUTPUT.DEBUGGEE;

      string result = null;
      try
      {
        var hr = _control.CallExtension(handle, command, args);
        if (hr < 0)
          _output.Append(string.Format("Command encountered an error.  HRESULT={0:X8}", hr));

        result = _output.ToString();
      }
      finally
      {
        _outputMask = mask;
        _output.Clear();
      }

      return result;
    }

    public string Execute(string cmd)
    {
      var mask = _outputMask;
      _output.Clear();
      _outputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING | DEBUG_OUTPUT.DEBUGGEE;

      string result = null;
      try
      {
        var hr = _control.Execute(DEBUG_OUTCTL.ALL_CLIENTS, cmd, DEBUG_EXECUTE.DEFAULT);
        if (hr < 0)
          _output.Append(string.Format("Command encountered an error.  HRESULT={0:X8}", hr));

        result = _output.ToString();
      }
      finally
      {
        _outputMask = mask;
        _output.Clear();
      }

      return result;
    }

    public string ExecuteScript(string script)
    {
      var mask = _outputMask;
      _output.Clear();
      _outputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING | DEBUG_OUTPUT.DEBUGGEE;

      string result = null;
      try
      {
        var hr = _control.ExecuteCommandFile(DEBUG_OUTCTL.ALL_CLIENTS, script, DEBUG_EXECUTE.DEFAULT);
        if (hr < 0)
          _output.Append(string.Format("Script encountered an error.  HRESULT={0:X8}", hr));

        result = _output.ToString();
      }
      finally
      {
        _outputMask = mask;
        _output.Clear();
      }

      return result;
    }

    public int WriteDumpFile(string dump, DEBUG_DUMP type)
    {
      return _client.WriteDumpFile(dump, type);
    }

    private void SetDebugStatus(DEBUG_STATUS status)
    {
      var hr = _control.SetExecutionStatus(status);
      if (hr < 0)
        throw new Exception(GetExceptionString("IDebugControl::SetExecutionStatus", hr));
    }

    private DEBUG_STATUS GetDebugStatus()
    {
      DEBUG_STATUS status;
      var hr = _control.GetExecutionStatus(out status);

      if (hr < 0)
        throw new Exception(GetExceptionString("IDebugControl::GetExecutionStatus", hr));

      return status;
    }

    internal static string GetExceptionString(string name, int hr)
    {
      return string.Format("{0} failed with hresult={1:X8}", name, hr);
    }

    public int Output(DEBUG_OUTPUT Mask, string Text)
    {
      if (_output != null && (_outputMask & Mask) != 0)
        _output.Append(Text);

      return 0;
    }

    public int GetInterestMask(out DEBUG_EVENT Mask)
    {
      Mask = DEBUG_EVENT.BREAKPOINT | DEBUG_EVENT.CREATE_PROCESS
      | DEBUG_EVENT.EXCEPTION | DEBUG_EVENT.EXIT_PROCESS
      | DEBUG_EVENT.CREATE_THREAD | DEBUG_EVENT.EXIT_THREAD
      | DEBUG_EVENT.LOAD_MODULE | DEBUG_EVENT.UNLOAD_MODULE;
      return 0;
    }

    public int Breakpoint(IDebugBreakpoint Bp)
    {
      return (int)DEBUG_STATUS.GO;
    }

    public int CreateProcess(
      ulong ImageFileHandle,
      ulong Handle,
      ulong BaseOffset,
      uint ModuleSize,
      string ModuleName,
      string ImageName,
      uint CheckSum,
      uint TimeDateStamp,
      ulong InitialThreadHandle,
      ulong ThreadDataOffset,
      ulong StartOffset)
    {
      CreateProcessEvent?.Invoke(
        this,
        new CreateProcessArgs(ImageFileHandle, Handle, BaseOffset, ModuleSize, ModuleName, ImageName, CheckSum, TimeDateStamp, InitialThreadHandle, ThreadDataOffset, StartOffset));

      return 0;
    }

    public int ExitProcess(uint ExitCode)
    {
      ExitProcessEvent?.Invoke(this, (int)ExitCode);

      m_exited = true;
      return (int)DEBUG_STATUS.BREAK;
    }

    public int CreateThread(ulong Handle, ulong DataOffset, ulong StartOffset)
    {
      ThreadCreateEvent?.Invoke(this, new CreateThreadArgs(Handle, DataOffset, StartOffset));

      return 0;
    }

    public int ExitThread(uint ExitCode)
    {
      ExitThreadEvent?.Invoke(this, (int)ExitCode);

      return 0;
    }

    public int Exception(ref EXCEPTION_RECORD64 Exception, uint FirstChance)
    {
      (FirstChance == 1 ? FirstChanceExceptionEvent : SecondChanceExceptionEvent)?.Invoke(this, Exception);

      return (int)DEBUG_STATUS.BREAK;
    }

    public int LoadModule(ulong ImageFileHandle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp)
    {
      ModuleLoadEvent?.Invoke(this, new ModuleEventArgs(ImageFileHandle, BaseOffset, ModuleSize, ModuleName, ImageName, CheckSum, TimeDateStamp));

      return 0;
    }

    public int UnloadModule(string ImageBaseName, ulong BaseOffset)
    {
      ModuleUnloadEvent?.Invoke(this, new ModuleEventArgs(ImageBaseName, BaseOffset));

      return 0;
    }

    public int SessionStatus(DEBUG_SESSION Status)
    {
      throw new NotImplementedException();
    }

    public int SystemError(uint Error, uint Level)
    {
      throw new NotImplementedException();
    }

    public int ChangeDebuggeeState(DEBUG_CDS Flags, ulong Argument)
    {
      throw new NotImplementedException();
    }

    public int ChangeEngineState(DEBUG_CES Flags, ulong Argument)
    {
      throw new NotImplementedException();
    }

    public int ChangeSymbolState(DEBUG_CSS Flags, ulong Argument)
    {
      throw new NotImplementedException();
    }

    public void Dispose()
    {
      _client.SetEventCallbacks(null);
      _client.SetOutputCallbacks(null);
    }
  }

  internal class ModuleEventArgs
  {
    public ulong ImageFileHandle;
    public ulong BaseOffset;
    public uint ModuleSize;
    public string ModuleName;
    public string ImageName;
    public uint CheckSum;
    public uint TimeDateStamp;

    public ModuleEventArgs(string imageBaseName, ulong baseOffset)
    {
      ImageName = imageBaseName;
      BaseOffset = baseOffset;
    }

    public ModuleEventArgs(ulong ImageFileHandle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName, uint CheckSum, uint TimeDateStamp)
    {
      this.ImageFileHandle = ImageFileHandle;
      this.BaseOffset = BaseOffset;
      this.ModuleSize = ModuleSize;
      this.ModuleName = ModuleName;
      this.ImageName = ImageName;
      this.CheckSum = CheckSum;
      this.TimeDateStamp = TimeDateStamp;
    }
  }

  internal class CreateThreadArgs
  {
    public ulong Handle;
    public ulong DataOffset;
    public ulong StartOffset;

    public CreateThreadArgs(ulong handle, ulong data, ulong start)
    {
      Handle = handle;
      DataOffset = data;
      StartOffset = start;
    }
  }

  internal class CreateProcessArgs
  {
    public ulong ImageFileHandle;
    public ulong Handle;
    public ulong BaseOffset;
    public uint ModuleSize;
    public string ModuleName;
    public string ImageName;
    public uint CheckSum;
    public uint TimeDateStamp;
    public ulong InitialThreadHandle;
    public ulong ThreadDataOffset;
    public ulong StartOffset;

    public CreateProcessArgs(
      ulong ImageFileHandle,
      ulong Handle,
      ulong BaseOffset,
      uint ModuleSize,
      string ModuleName,
      string ImageName,
      uint CheckSum,
      uint TimeDateStamp,
      ulong InitialThreadHandle,
      ulong ThreadDataOffset,
      ulong StartOffset)
    {
      this.ImageFileHandle = ImageFileHandle;
      this.Handle = Handle;
      this.BaseOffset = BaseOffset;
      this.ModuleSize = ModuleSize;
      this.ModuleName = ModuleName;
      this.ImageName = ImageName;
      this.CheckSum = CheckSum;
      this.TimeDateStamp = TimeDateStamp;
      this.InitialThreadHandle = InitialThreadHandle;
      this.ThreadDataOffset = ThreadDataOffset;
      this.StartOffset = StartOffset;
    }
  }
}