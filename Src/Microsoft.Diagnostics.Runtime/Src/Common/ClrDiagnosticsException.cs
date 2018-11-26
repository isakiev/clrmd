using System;
using System.Runtime.Serialization;

namespace Microsoft.Diagnostics.Runtime
{
  [Serializable]
  public class ClrDiagnosticsException : Exception
  {
    public ClrDiagnosticsExceptionKind Kind { get; }

    public ClrDiagnosticsException(string message, ClrDiagnosticsExceptionKind kind = ClrDiagnosticsExceptionKind.Unknown)
      : base(message)
    {
      Kind = kind;
    }

    protected ClrDiagnosticsException(SerializationInfo info, StreamingContext context)
      : base(info, context)
    {
    }
  }
}