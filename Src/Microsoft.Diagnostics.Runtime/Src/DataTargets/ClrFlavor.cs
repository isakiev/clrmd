namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  /// Returns the "flavor" of CLR this module represents.
  /// </summary>
  public enum ClrFlavor
  {
    /// <summary>
    /// This is the full version of CLR included with windows.
    /// </summary>
    Desktop = 0,

    /// <summary>
    /// For .Net Core
    /// </summary>
    Core = 3
  }
}