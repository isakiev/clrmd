namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public class DefaultLogger : IExternalLogger
  {
    public static readonly IExternalLogger Instance = new DefaultLogger();

    private DefaultLogger()
    {
    }

    public void Log(string category, string format, params object[] parameters)
    {
      if (parameters != null && parameters.Length > 0)
        format = string.Format(format, parameters);

      System.Diagnostics.Trace.WriteLine(format, category);
    }
  }
}