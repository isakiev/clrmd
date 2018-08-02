namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   The type of frame the ClrStackFrame represents.
  /// </summary>
  public enum ClrStackFrameType
  {
    /// <summary>
    ///   Indicates this stack frame is unknown
    /// </summary>
    Unknown = -1,

    /// <summary>
    ///   Indicates this stack frame is a standard managed method.
    /// </summary>
    ManagedMethod = 0,

    /// <summary>
    ///   Indicates this stack frame is a special stack marker that the Clr runtime leaves on the stack.
    ///   Note that the ClrStackFrame may still have a ClrMethod associated with the marker.
    /// </summary>
    Runtime = 1
  }
}