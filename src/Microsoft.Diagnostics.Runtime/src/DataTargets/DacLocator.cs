using System;
using System.IO;
using JetBrains.Annotations;
using Microsoft.Diagnostics.Runtime.Utilities;
using FileVersionInfo = System.Diagnostics.FileVersionInfo;

namespace Microsoft.Diagnostics.Runtime
{
    public class DacLocator
    {
        private readonly ISymbolLocator _symbolLocator;

        public DacLocator([NotNull] ISymbolLocator symbolLocator)
        {
            _symbolLocator = symbolLocator ?? throw new ArgumentNullException(nameof(symbolLocator));
        }

        public virtual string FindDac(ClrInfo clrInfo, Architecture architecture)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));
            return TryFindLocalDac(clrInfo) ?? TryFindRemoteDac(clrInfo, architecture);
        }
        
        protected string TryFindLocalDac(ClrInfo clrInfo)
        {
            if (!ClrInfoProvider.IsSupportedRuntime(clrInfo.ModuleInfo, out var flavor, out var platform))
                throw new ApplicationException("Unsupported runtime: " + clrInfo);

            var dacFileName = ClrInfoProvider.GetDacFileName(flavor, platform);
            var moduleDirectory = Path.GetDirectoryName(clrInfo.ModuleInfo.FileName) ?? string.Empty;
            var dacLocation = Path.Combine(moduleDirectory, dacFileName);
            if (!File.Exists(dacLocation))
                return null;
      
            var actualVersion = GetVersion(FileVersionInfo.GetVersionInfo(dacLocation));
            var expectedVersion = GetVersion(clrInfo.Version);

            if (actualVersion != expectedVersion)
            {
                LogMessage("There is a local dac '{0}', but in doesn't fit due to version mismatch (expected: {1}, actual: {2})", dacLocation, expectedVersion, actualVersion);
                return null;
            }

            LogMessage("Found a local dac '{0}'", dacLocation);
            return dacLocation;
        }

        protected string TryFindRemoteDac(ClrInfo clrInfo, Architecture architecture)
        {
            if (!ClrInfoProvider.IsSupportedRuntime(clrInfo.ModuleInfo, out var flavor, out var platform))
                throw new ApplicationException("Unsupported runtime: " + clrInfo);

            var dacRequestFileName = ClrInfoProvider.GetDacRequestFileName(flavor, architecture, architecture, clrInfo.Version, platform);
            var result = _symbolLocator.FindBinary(dacRequestFileName, (int)clrInfo.ModuleInfo.TimeStamp, (int)clrInfo.ModuleInfo.FileSize);
            if (result != null && File.Exists(result))
            {
                LogMessage("Got a dac from the remote server: '{0}'", result);
                return result;
            }

            return null;
        }
        
        private static Version GetVersion(FileVersionInfo versionInfo)
        {
            return new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
        }

        private static Version GetVersion(VersionInfo versionInfo)
        {
            return new Version(versionInfo.Major, versionInfo.Minor, versionInfo.Revision, versionInfo.Patch);
        }

        protected virtual void LogMessage(string format, params object[] parameters)
        {
        }
    }
}