using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public static class DataTargetTestExtensions
  {
    public static ClrRuntime CreateSingleRuntime(this DataTarget dt)
    {
      return dt.CreateRuntime(dt.ClrVersions.Single());
    }
  }
}