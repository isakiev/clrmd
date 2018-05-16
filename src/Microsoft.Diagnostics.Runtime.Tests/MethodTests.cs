using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class MethodTests
  {
    [TestMethod]
    public void MethodHandleMultiDomainTests()
    {
      ulong[] methodDescs;
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var module = runtime.GetModule("sharedlibrary.dll");
        var type = module.GetTypeByName("Foo");
        var method = type.GetMethod("Bar");
        methodDescs = method.EnumerateMethodDescs().ToArray();

        Assert.AreEqual(2, methodDescs.Length);
      }

      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var method = runtime.GetMethodByHandle(methodDescs[0]);

        Assert.IsNotNull(method);
        Assert.AreEqual("Bar", method.Name);
        Assert.AreEqual("Foo", method.Type.Name);
      }

      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var method = runtime.GetMethodByHandle(methodDescs[1]);

        Assert.IsNotNull(method);
        Assert.AreEqual("Bar", method.Name);
        Assert.AreEqual("Foo", method.Type.Name);
      }
    }

    [TestMethod]
    public void MethodHandleSingleDomainTests()
    {
      ulong methodDesc;
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var module = runtime.GetModule("sharedlibrary.dll");
        var type = module.GetTypeByName("Foo");
        var method = type.GetMethod("Bar");
        methodDesc = method.EnumerateMethodDescs().Single();

        Assert.AreNotEqual(0ul, methodDesc);
      }

      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var method = runtime.GetMethodByHandle(methodDesc);

        Assert.IsNotNull(method);
        Assert.AreEqual("Bar", method.Name);
        Assert.AreEqual("Foo", method.Type.Name);
      }

      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var module = runtime.GetModule("sharedlibrary.dll");
        var type = module.GetTypeByName("Foo");
        var method = type.GetMethod("Bar");
        Assert.AreEqual(methodDesc, method.EnumerateMethodDescs().Single());
      }
    }

    /// <summary>
    ///   This test tests a patch in v45runtime.GetNameForMD(ulong md) that
    ///   corrects an error from sos
    /// </summary>
    [TestMethod]
    public void CompleteSignatureIsRetrievedForMethodsWithGenericParameters()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var module = runtime.GetModule("sharedlibrary.dll");
        var type = module.GetTypeByName("Foo");

        var genericMethod = type.GetMethod("GenericBar");

        var methodName = genericMethod.GetFullSignature();

        Assert.AreEqual(')', methodName.Last());
      }
    }
  }
}