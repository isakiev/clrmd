using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class TypeTests
  {
    [TestMethod]
    public void IntegerObjectClrType()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var field = runtime.GetModule("types.exe").GetTypeByName("Types").GetStaticFieldByName("s_i");

        var addr = (ulong)field.GetValue(runtime.AppDomains.Single());
        var type = heap.GetObjectType(addr);
        Assert.IsTrue(type.IsPrimitive);
        Assert.IsFalse(type.IsObjectReference);
        Assert.IsFalse(type.IsValueClass);

        var value = type.GetValue(addr);
        Assert.AreEqual("42", value.ToString());
        Assert.IsInstanceOfType(value, typeof(int));
        Assert.AreEqual(42, (int)value);

        Assert.IsTrue(heap.EnumerateObjectAddresses().Contains(addr));
      }
    }

    [TestMethod]
    public void ArrayComponentTypeTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        // Ensure that we always have a component for every array type.
        foreach (var obj in heap.EnumerateObjectAddresses())
        {
          var type = heap.GetObjectType(obj);
          Assert.IsTrue(!type.IsArray || type.ComponentType != null);

          foreach (var field in type.Fields)
          {
            Assert.IsNotNull(field.Type);
            Assert.IsTrue(!field.Type.IsArray || field.Type.ComponentType != null);
            Assert.AreSame(heap, field.Type.Heap);
          }
        }

        foreach (var module in runtime.Modules)
        {
          foreach (var type in module.EnumerateTypes())
          {
            Assert.IsTrue(!type.IsArray || type.ComponentType != null);
            Assert.AreSame(heap, type.Heap);
          }
        }
      }
    }

    [TestMethod]
    public void ComponentType()
    {
      // Simply test that we can enumerate the heap.

      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        foreach (var obj in heap.EnumerateObjectAddresses())
        {
          var type = heap.GetObjectType(obj);
          Assert.IsNotNull(type);

          if (type.IsArray || type.IsPointer)
            Assert.IsNotNull(type.ComponentType);
          else
            Assert.IsNull(type.ComponentType);
        }
      }
    }

    [TestMethod]
    public void TypeEqualityTest()
    {
      // This test ensures that only one ClrType is created when we have a type loaded into two different AppDomains with two different
      // method tables.

      const string TypeName = "Foo";
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var types = (from obj in heap.EnumerateObjectAddresses()
                     let t = heap.GetObjectType(obj)
                     where t.Name == TypeName
                     select t).ToArray();

        Assert.AreEqual(2, types.Length);
        Assert.AreNotSame(types[0], types[1]);

        var module = runtime.Modules.Where(m => Path.GetFileName(m.FileName).Equals("sharedlibrary.dll", StringComparison.OrdinalIgnoreCase)).Single();
        var typeFromModule = module.GetTypeByName(TypeName);

        Assert.AreEqual(TypeName, typeFromModule.Name);
        Assert.AreEqual(types[0], typeFromModule);
      }
    }

    [TestMethod]
    public void VariableRootTest()
    {
      // Test to make sure that a specific static and local variable exist.

      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;
        heap.StackwalkPolicy = ClrRootStackwalkPolicy.Exact;

        var fooRoots = from root in heap.EnumerateRoots()
                       where root.Type.Name == "Foo"
                       select root;

        var staticRoot = fooRoots.Where(r => r.Kind == GCRootKind.StaticVar).Single();
        Assert.IsTrue(staticRoot.Name.Contains("s_foo"));

        var arr = fooRoots.Where(r => r.Kind == GCRootKind.LocalVar).ToArray();
        var localVarRoot = fooRoots.Where(r => r.Kind == GCRootKind.LocalVar).Single();

        var thread = runtime.GetMainThread();
        var main = thread.GetFrame("Main");
        var inner = thread.GetFrame("Inner");

        var low = thread.StackBase;
        var high = thread.StackLimit;

        // Account for different platform stack direction.
        if (low > high)
        {
          var tmp = low;
          low = high;
          high = tmp;
        }

        Assert.IsTrue(low <= localVarRoot.Address && localVarRoot.Address <= high);
      }
    }

    [TestMethod]
    public void EETypeTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var methodTables = (from obj in heap.EnumerateObjectAddresses()
                            let type = heap.GetObjectType(obj)
                            where !type.IsFree
                            select heap.GetMethodTable(obj)).Unique();

        Assert.IsFalse(methodTables.Contains(0));

        foreach (var mt in methodTables)
        {
          var type = heap.GetTypeByMethodTable(mt);
          var eeclass = heap.GetEEClassByMethodTable(mt);
          Assert.AreNotEqual(0ul, eeclass);

          Assert.AreNotEqual(0ul, heap.GetMethodTableByEEClass(eeclass));
        }
      }
    }

    [TestMethod]
    public void MethodTableHeapEnumeration()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        foreach (var type in heap.EnumerateObjectAddresses().Select(obj => heap.GetObjectType(obj)).Unique())
        {
          Assert.AreNotEqual(0ul, type.MethodTable);

          ClrType typeFromHeap;

          if (type.IsArray)
          {
            var componentType = type.ComponentType;
            Assert.IsNotNull(componentType);

            typeFromHeap = heap.GetTypeByMethodTable(type.MethodTable, componentType.MethodTable);
          }
          else
          {
            typeFromHeap = heap.GetTypeByMethodTable(type.MethodTable);
          }

          Assert.AreEqual(type.MethodTable, typeFromHeap.MethodTable);
          Assert.AreSame(type, typeFromHeap);
        }
      }
    }

    [TestMethod]
    public void GetObjectMethodTableTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var i = 0;
        foreach (var obj in heap.EnumerateObjectAddresses())
        {
          i++;
          var type = heap.GetObjectType(obj);

          if (type.IsArray)
          {
            ulong mt, cmt;
            var result = heap.TryGetMethodTable(obj, out mt, out cmt);

            Assert.IsTrue(result);
            Assert.AreNotEqual(0ul, mt);
            Assert.AreEqual(type.MethodTable, mt);

            Assert.AreSame(type, heap.GetTypeByMethodTable(mt, cmt));
          }
          else
          {
            var mt = heap.GetMethodTable(obj);

            Assert.AreNotEqual(0ul, mt);
            Assert.IsTrue(type.EnumerateMethodTables().Contains(mt));

            Assert.AreSame(type, heap.GetTypeByMethodTable(mt));
            Assert.AreSame(type, heap.GetTypeByMethodTable(mt, 0));

            ulong mt2, cmt;
            var res = heap.TryGetMethodTable(obj, out mt2, out cmt);

            Assert.IsTrue(res);
            Assert.AreEqual(mt, mt2);
            Assert.AreEqual(0ul, cmt);
          }
        }
      }
    }

    [TestMethod]
    public void EnumerateMethodTableTest()
    {
      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var fooObjects = (from obj in heap.EnumerateObjectAddresses()
                          let t = heap.GetObjectType(obj)
                          where t.Name == "Foo"
                          select obj).ToArray();

        // There are exactly two Foo objects in the process, one in each app domain.
        // They will have different method tables.
        Assert.AreEqual(2, fooObjects.Length);

        var fooType = heap.GetObjectType(fooObjects[0]);
        Assert.AreNotSame(fooType, heap.GetObjectType(fooObjects[1]));

        var appDomainsFoo = (from root in heap.EnumerateRoots(true)
                             where root.Kind == GCRootKind.StaticVar && root.Type == fooType
                             select root).Single();

        var nestedExceptionFoo = fooObjects.Where(obj => obj != appDomainsFoo.Object).Single();
        var nestedExceptionFooType = heap.GetObjectType(nestedExceptionFoo);

        Assert.AreNotSame(nestedExceptionFooType, appDomainsFoo.Type);

        var nestedExceptionFooMethodTable = dt.DataReader.ReadPointerUnsafe(nestedExceptionFoo);
        var appDomainsFooMethodTable = dt.DataReader.ReadPointerUnsafe(appDomainsFoo.Object);

        // These are in different domains and should have different type handles:
        Assert.AreNotEqual(nestedExceptionFooMethodTable, appDomainsFooMethodTable);

        // The MethodTable returned by ClrType should always be the method table that lives in the "first"
        // AppDomain (in order of ClrAppDomain.Id).
        Assert.AreEqual(appDomainsFooMethodTable, fooType.MethodTable);

        // Ensure that we enumerate two type handles and that they match the method tables we have above.
        var methodTableEnumeration = fooType.EnumerateMethodTables().ToArray();
        Assert.AreEqual(2, methodTableEnumeration.Length);

        // These also need to be enumerated in ClrAppDomain.Id order
        Assert.AreEqual(appDomainsFooMethodTable, methodTableEnumeration[0]);
        Assert.AreEqual(nestedExceptionFooMethodTable, methodTableEnumeration[1]);
      }
    }

    [TestMethod]
    public void FieldNameAndValueTests()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var domain = runtime.AppDomains.Single();

        var fooType = runtime.GetModule("sharedlibrary.dll").GetTypeByName("Foo");
        var obj = (ulong)runtime.GetModule("types.exe").GetTypeByName("Types").GetStaticFieldByName("s_foo").GetValue(runtime.AppDomains.Single());

        Assert.AreSame(fooType, heap.GetObjectType(obj));

        TestFieldNameAndValue(fooType, obj, "i", 42);
        TestFieldNameAndValue(fooType, obj, "s", "string");
        TestFieldNameAndValue(fooType, obj, "b", true);
        TestFieldNameAndValue(fooType, obj, "f", 4.2f);
        TestFieldNameAndValue(fooType, obj, "d", 8.4);
      }
    }

    public ClrInstanceField TestFieldNameAndValue<T>(ClrType type, ulong obj, string name, T value)
    {
      var field = type.GetFieldByName(name);
      Assert.IsNotNull(field);
      Assert.AreEqual(name, field.Name);

      var v = field.GetValue(obj);
      Assert.IsNotNull(v);
      Assert.IsInstanceOfType(v, typeof(T));

      Assert.AreEqual(value, (T)v);

      return field;
    }
  }

  [TestClass]
  public class ArrayTests
  {
    [TestMethod]
    public void ArrayOffsetsTest()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var domain = runtime.AppDomains.Single();

        var typesModule = runtime.GetModule("types.exe");
        var type = heap.GetTypeByName("Types");

        var s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
        var s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
        var s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
        var s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

        ulong[] expected = {s_one, s_two, s_three};

        var arrayType = heap.GetObjectType(s_array);

        for (var i = 0; i < expected.Length; i++)
        {
          Assert.AreEqual(expected[i], (ulong)arrayType.GetArrayElementValue(s_array, i));

          var address = arrayType.GetArrayElementAddress(s_array, i);
          var value = dt.DataReader.ReadPointerUnsafe(address);

          Assert.IsNotNull(address);
          Assert.AreEqual(expected[i], value);
        }
      }
    }

    [TestMethod]
    public void ArrayLengthTest()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var domain = runtime.AppDomains.Single();

        var typesModule = runtime.GetModule("types.exe");
        var type = heap.GetTypeByName("Types");

        var s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
        var arrayType = heap.GetObjectType(s_array);

        Assert.AreEqual(3, arrayType.GetArrayLength(s_array));
      }
    }

    [TestMethod]
    public void ArrayReferenceEnumeration()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var heap = runtime.Heap;

        var domain = runtime.AppDomains.Single();

        var typesModule = runtime.GetModule("types.exe");
        var type = heap.GetTypeByName("Types");

        var s_array = (ulong)type.GetStaticFieldByName("s_array").GetValue(domain);
        var s_one = (ulong)type.GetStaticFieldByName("s_one").GetValue(domain);
        var s_two = (ulong)type.GetStaticFieldByName("s_two").GetValue(domain);
        var s_three = (ulong)type.GetStaticFieldByName("s_three").GetValue(domain);

        var arrayType = heap.GetObjectType(s_array);

        var objs = new List<ulong>();
        arrayType.EnumerateRefsOfObject(s_array, (obj, offs) => objs.Add(obj));

        // We do not guarantee the order in which these are enumerated.
        Assert.AreEqual(3, objs.Count);
        Assert.IsTrue(objs.Contains(s_one));
        Assert.IsTrue(objs.Contains(s_two));
        Assert.IsTrue(objs.Contains(s_three));
      }
    }
  }
}