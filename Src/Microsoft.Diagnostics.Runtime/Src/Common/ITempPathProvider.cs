namespace Microsoft.Diagnostics.Runtime
{
  public interface ITempPathProvider
  {
    string GetUniqueTempPath();
    string GetFixedTempPath(string name);
  }
}