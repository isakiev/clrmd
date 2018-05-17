namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   A managed threadpool object.
  /// </summary>
  public abstract class ManagedWorkItem
  {
    /// <summary>
    ///   The object address of this entry.
    /// </summary>
    public abstract ulong Object { get; }

    /// <summary>
    ///   The type of Object.
    /// </summary>
    public abstract ClrType Type { get; }
  }
}