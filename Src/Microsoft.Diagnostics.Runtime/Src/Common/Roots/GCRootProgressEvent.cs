namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  /// A delegate for reporting GCRoot progress.
  /// </summary>
  /// <param name="source">The GCRoot sending the event.</param>
  /// <param name="current">The total number of objects processed.</param>
  /// <param name="total">The total number of objects in the heap, if that number is known, otherwise -1.</param>
  public delegate void GCRootProgressEvent(GCRoot source, long current, long total);
}