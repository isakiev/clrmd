namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  /// The way a method was JIT'ed.
  /// </summary>
  public enum MethodCompilationType
  {
    /// <summary>
    /// Method is not yet JITed and no NGEN image exists.
    /// </summary>
    None,

    /// <summary>
    /// Method was JITed.
    /// </summary>
    Jit,

    /// <summary>
    /// Method was NGEN'ed (pre-JITed).
    /// </summary>
    Ngen
  }
}