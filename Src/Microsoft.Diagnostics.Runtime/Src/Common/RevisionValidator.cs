namespace Microsoft.Diagnostics.Runtime
{
  internal static class RevisionValidator
  {
    public static void Validate(int revision, int runtimeRevision)
    {
      if (revision != runtimeRevision)
        throw new ClrDiagnosticsException(
          $"You must not reuse any object other than ClrRuntime after calling flush!\nClrModule revision ({revision}) != ClrRuntime revision ({runtimeRevision}).",
          ClrDiagnosticsExceptionKind.RevisionMismatch);
    }
  }
}