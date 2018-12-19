using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public static class ModuleUtil
    {
        public static readonly Regex InvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidPathChars()))}]");
    }
}