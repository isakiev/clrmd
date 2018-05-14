namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   The architecture of a process.
  /// </summary>
  public enum Architecture
  {
    /// <summary>
    ///   Unknown.  Should never be exposed except in case of error.
    /// </summary>
    Unknown,

    /// <summary>
    ///   x86.
    /// </summary>
    X86,

    /// <summary>
    ///   x64
    /// </summary>
    Amd64,

    /// <summary>
    ///   ARM
    /// </summary>
    Arm
  }
}