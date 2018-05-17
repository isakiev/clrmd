using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class PdbTests
  {
    [TestMethod]
    public void PdbEqualityTest()
    {
      // Ensure all methods in our source file is in the pdb.
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var allPdbs = runtime.Modules.Where(m => m.Pdb != null).Select(m => m.Pdb).ToArray();
        Assert.IsTrue(allPdbs.Length > 1);

        for (var i = 0; i < allPdbs.Length; i++)
        {
          Assert.IsTrue(allPdbs[i].Equals(allPdbs[i]));
          for (var j = i + 1; j < allPdbs.Length; j++)
          {
            Assert.IsFalse(allPdbs[i].Equals(allPdbs[j]));
            Assert.IsFalse(allPdbs[j].Equals(allPdbs[i]));
          }
        }
      }
    }

    [TestMethod]
    public void PdbGuidAgeTest()
    {
      PdbReader.GetPdbProperties(TestTargets.NestedException.Pdb, out var pdbSignature, out var pdbAge);

      // Ensure we get the same answer a different way.
      using (var pdbReader = new PdbReader(TestTargets.NestedException.Pdb))
      {
        Assert.AreEqual(pdbAge, pdbReader.Age);
        Assert.AreEqual(pdbSignature, pdbReader.Signature);
      }

      // Ensure the PEFile has the same signature/age.
      using (var peFile = new PEFile(TestTargets.NestedException.Executable))
      {
        Assert.AreEqual(peFile.PdbInfo.Guid, pdbSignature);
        Assert.AreEqual(peFile.PdbInfo.Revision, pdbAge);
      }
    }

    [TestMethod]
    public void PdbSourceLineTest()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var thread = runtime.GetMainThread();

        var sourceLines = new HashSet<int>();
        using (var reader = new PdbReader(TestTargets.NestedException.Pdb))
        {
          Assert.IsTrue(TestTargets.NestedException.Source.Equals(reader.Sources.Single().Name, StringComparison.OrdinalIgnoreCase));

          var functions = from frame in thread.StackTrace
                          where frame.Kind != ClrStackFrameType.Runtime
                          select reader.GetFunctionFromToken(frame.Method.MetadataToken);

          foreach (var function in functions)
          {
            var sourceFile = function.SequencePoints.Single();

            foreach (int line in sourceFile.Lines.Select(l => l.LineBegin))
              sourceLines.Add(line);
          }
        }

        var curr = 0;
        foreach (var line in File.ReadLines(TestTargets.NestedException.Source))
        {
          curr++;
          if (line.Contains("/* seq */"))
            Assert.IsTrue(sourceLines.Contains(curr));
        }
      }
    }

    [TestMethod]
    public void PdbMethodTest()
    {
      // Ensure all methods in our source file is in the pdb.
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var module = runtime.Modules.Where(m => m.Name.Equals(TestTargets.NestedException.Executable, StringComparison.OrdinalIgnoreCase)).Single();
        var type = module.GetTypeByName("Program");

        using (var pdb = new PdbReader(TestTargets.NestedException.Pdb))
        {
          foreach (var method in type.Methods)
          {
            // ignore inherited methods and constructors
            if (method.Type != type || method.IsConstructor || method.IsClassConstructor)
              continue;

            Assert.IsNotNull(pdb.GetFunctionFromToken(method.MetadataToken));
          }
        }
      }
    }
  }
}