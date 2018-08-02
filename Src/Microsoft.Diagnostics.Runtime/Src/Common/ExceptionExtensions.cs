using System;

namespace Microsoft.Diagnostics.Runtime
{
  internal static class ExceptionExtensions
  {
    public static Exception AddData(this Exception exception, string name, object value)
    {
      if (exception == null) throw new ArgumentNullException(nameof(exception));

      exception.Data[name] = value;
      return exception;
    }
  }
}