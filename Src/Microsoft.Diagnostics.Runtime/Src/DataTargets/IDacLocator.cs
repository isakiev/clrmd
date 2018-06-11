namespace Microsoft.Diagnostics.Runtime
{
  public interface IDacLocator
  {
    /// <returns>null if not found</returns>
    string FindDac(ClrInfo clrInfo);
  }
}