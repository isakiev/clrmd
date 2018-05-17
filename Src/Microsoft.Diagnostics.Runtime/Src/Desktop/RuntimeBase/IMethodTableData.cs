namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal interface IMethodTableData
  {
    bool Shared { get; }
    bool Free { get; }
    bool ContainsPointers { get; }
    uint BaseSize { get; }
    uint ComponentSize { get; }
    ulong EEClass { get; }
    ulong Parent { get; }
    uint NumMethods { get; }
    ulong ElementTypeHandle { get; }
  }
}