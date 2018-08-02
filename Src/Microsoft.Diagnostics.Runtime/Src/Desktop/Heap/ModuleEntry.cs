namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct ModuleEntry
  {
    public ClrModule Module;
    public uint Token;

    public ModuleEntry(ClrModule module, uint token)
    {
      Module = module;
      Token = token;
    }
  }
}