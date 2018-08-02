namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public interface IExternalLogger
  {
    void Log(string category, string format, params object[] parameters);
  }
}