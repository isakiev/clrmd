using System;

namespace Microsoft.Diagnostics.Runtime
{
    [Serializable]
    public enum ClrDiagnosticsExceptionKind
    {
        Unknown,
        CorruptedFileOrUnknownFormat,
        RevisionMismatch,
        DebuggerError,
        CrashDumpError,
        DataRequestError,
        DacError,
        RuntimeUninitialized
    }
}