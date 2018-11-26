namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  /// The type of work item this is.
  /// </summary>
  public enum WorkItemKind
  {
    /// <summary>
    /// Unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// Callback for an async timer.
    /// </summary>
    AsyncTimer,

    /// <summary>
    /// Async callback.
    /// </summary>
    AsyncCallback,

    /// <summary>
    /// From ThreadPool.QueueUserWorkItem.
    /// </summary>
    QueueUserWorkItem,

    /// <summary>
    /// Timer delete callback.
    /// </summary>
    TimerDelete
  }
}