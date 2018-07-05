using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public class SimpleTempPathProvider : ITempPathProvider, IDisposable
  {
    private readonly string myRootPath;

    public SimpleTempPathProvider()
    {
      do
      {
        myRootPath = Path.Combine(Path.GetTempPath(), "clrmd_removeme_" + DateTime.Now.Ticks);
      } while (Directory.Exists(myRootPath));

      Directory.CreateDirectory(myRootPath);
    }

    public string GetUniqueTempPath()
    {
      string result;
      do
      {
        result = Path.Combine(myRootPath, Guid.NewGuid().ToString());
      } while (Directory.Exists(result));

      Directory.CreateDirectory(result);
      return result;
    }

    public string GetFixedTempPath(string name)
    {
      var result = Path.Combine(myRootPath, name);
      if (!Directory.Exists(result))
        Directory.CreateDirectory(result);

      return result;
    }

    public void Dispose()
    {
      Directory.Delete(myRootPath, true);
    }
  }
}