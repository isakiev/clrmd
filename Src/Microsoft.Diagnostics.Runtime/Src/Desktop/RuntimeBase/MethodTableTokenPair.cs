namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct MethodTableTokenPair
  {
    public ulong MethodTable { get; set; }
    public uint Token { get; set; }

    public MethodTableTokenPair(ulong methodTable, uint token)
    {
      MethodTable = methodTable;
      Token = token;
    }
  }
}