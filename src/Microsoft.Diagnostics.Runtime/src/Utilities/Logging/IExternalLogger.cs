namespace Microsoft.Diagnostics.Runtime.Utilities.Logging
{
    public interface IExternalLogger
    {
        void Info(string format, params object[] parameters);
    }
}