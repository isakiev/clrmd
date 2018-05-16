using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public class DefaultSymbolLocator : ISymbolLocator
  {
    private static readonly string[] s_microsoftSymbolServers = {"http://msdl.microsoft.com/download/symbols", "http://referencesource.microsoft.com/symbols"};
    
    private static readonly Dictionary<FileEntry, Task<string>> s_files = new Dictionary<FileEntry, Task<string>>();

    /// <summary>
    ///   The raw symbol path.  You should probably use the SymbolPath property instead.
    /// </summary>
    protected volatile string _symbolPath;
    
    /// <summary>
    ///   The raw symbol cache.  You should probably use the SymbolCache property instead.
    /// </summary>
    protected volatile string _symbolCache;
    
    /// <summary>
    ///   The timeout (in milliseconds) used when contacting each individual server.  This is not a total timeout for the entire
    ///   symbol server operation.
    /// </summary>
    public int Timeout { get; set; } = 60000;
    
    /// <summary>
    ///   A set of files that we did not find when requested.  This set is SymbolLocator specific (not global
    ///   like successful downloads) and is cleared when we change the symbol path or cache.
    /// </summary>
    internal volatile HashSet<FileEntry> _missingFiles = new HashSet<FileEntry>();
    
    public DefaultSymbolLocator()
    {
      var sympath = _NT_SYMBOL_PATH;
      if (string.IsNullOrEmpty(sympath))
        sympath = MicrosoftSymbolServerPath;

      SymbolPath = sympath;
    }
    
    
    public string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
    {
      var fullPath = fileName;
      fileName = Path.GetFileName(fullPath).ToLower();

      // First see if we already have the result cached.
      var entry = new FileEntry(fileName, buildTimeStamp, imageSize);
      var result = GetFileEntry(entry);
      if (result != null)
        return result;

      var missingFiles = _missingFiles;
      if (IsMissing(missingFiles, entry))
        return null;

      // Test to see if the file is on disk.
      if (ValidateBinary(fullPath, buildTimeStamp, imageSize, checkProperties))
      {
        SetFileEntry(missingFiles, entry, fullPath);
        return fullPath;
      }

      // Finally, check the symbol paths.
      string exeIndexPath = null;
      foreach (var element in SymPathElement.GetElements(SymbolPath))
        if (element.IsSymServer)
        {
          if (exeIndexPath == null)
            exeIndexPath = GetIndexPath(fileName, buildTimeStamp, imageSize);

          var target = TryGetFileFromServer(element.Target, exeIndexPath, element.Cache ?? SymbolCache);
          if (target == null)
          {
            Trace($"Server '{element.Target}' did not have file '{Path.GetFileName(fileName)}' with timestamp={buildTimeStamp:x} and filesize={imageSize:x}.");
          }
          else if (ValidateBinary(target, buildTimeStamp, imageSize, checkProperties))
          {
            Trace($"Found '{fileName}' on server '{element.Target}'.  Copied to '{target}'.");
            SetFileEntry(missingFiles, entry, target);
            return target;
          }
        }
        else
        {
          var filePath = Path.Combine(element.Target, fileName);
          if (ValidateBinary(filePath, buildTimeStamp, imageSize, checkProperties))
          {
            Trace($"Found '{fileName}' at '{filePath}'.");
            SetFileEntry(missingFiles, entry, filePath);
            return filePath;
          }
        }

      SetFileEntry(missingFiles, entry, null);
      return null;
    }
    
    private string GetFileEntry(FileEntry entry)
    {
      lock (s_files)
      {
        if (s_files.TryGetValue(entry, out var task))
          return task.Result;
      }

      return null;
    }
    
    private static bool IsMissing<T>(HashSet<T> entries, T entry)
    {
      lock (entries)
      {
        return entries.Contains(entry);
      }
    }
    
    /// <summary>
    ///   Validates whether a file on disk matches the properties we expect.
    /// </summary>
    /// <param name="fullPath">The full path on disk of a PEImage to inspect.</param>
    /// <param name="buildTimeStamp">The build timestamp we expect to match.</param>
    /// <param name="imageSize">The build image size we expect to match.</param>
    /// <param name="checkProperties">Whether we should actually validate the imagesize/timestamp or not.</param>
    /// <returns></returns>
    protected virtual bool ValidateBinary(string fullPath, int buildTimeStamp, int imageSize, bool checkProperties)
    {
      if (string.IsNullOrEmpty(fullPath))
        return false;

      if (File.Exists(fullPath))
      {
        if (!checkProperties) return true;

        try
        {
          using (var pefile = new PEFile(fullPath))
          {
            var header = pefile.Header;
            if (!checkProperties || header.TimeDateStampSec == buildTimeStamp && header.SizeOfImage == imageSize)
              return true;

            Trace("Rejected file '{0}' because file size and time stamp did not match.", fullPath);
          }
        }
        catch (Exception e)
        {
          Trace("Encountered exception {0} while attempting to inspect file '{1}'.", e.GetType().Name, fullPath);
        }
      }

      return false;
    }
    
    /// <summary>
    ///   Writes diagnostic messages about symbol loading to System.Diagnostics.Trace.  Figuring out symbol issues can be tricky,
    ///   so if you override methods in SymbolLocator, be sure to trace the information here.
    /// </summary>
    /// <param name="fmt"></param>
    /// <param name="args"></param>
    protected virtual void Trace(string fmt, params object[] args)
    {
      if (args != null && args.Length > 0)
        fmt = string.Format(fmt, args);

      System.Diagnostics.Trace.WriteLine(fmt, "Microsoft.Diagnostics.Runtime.SymbolLocator");
    }
    
    private void SetFileEntry(HashSet<FileEntry> missingFiles, FileEntry entry, string value)
    {
      if (value != null)
        lock (s_files)
        {
          if (!s_files.ContainsKey(entry))
          {
            var task = new Task<string>(() => value);
            s_files[entry] = task;
            task.Start();
          }
        }
      else
        lock (missingFiles)
        {
          missingFiles.Add(entry);
        }
    }
    
    /// <summary>
    ///   Gets or sets the SymbolPath this object uses to attempt to find PDBs and binaries.
    /// </summary>
    public string SymbolPath
    {
      get => _symbolPath ?? "";

      set
      {
        _symbolPath = (value ?? "").Trim();

        SymbolPathOrCacheChanged();
      }
    }
    
    /// <summary>
    ///   Called when changing the symbol file path or cache.
    /// </summary>
    protected virtual void SymbolPathOrCacheChanged()
    {
      _missingFiles.Clear();
    }
    
    private static string GetIndexPath(string fileName, int buildTimeStamp, int imageSize)
    {
      return fileName + @"\" + buildTimeStamp.ToString("x") + imageSize.ToString("x") + @"\" + fileName;
    }
    
    private string TryGetFileFromServer(string urlForServer, string fileIndexPath, string cache)
    {
      Debug.Assert(!string.IsNullOrEmpty(cache));
      if (string.IsNullOrEmpty(urlForServer))
        return null;

      var targetPath = Path.Combine(cache, fileIndexPath);

      // See if it is a compressed file by replacing the last character of the name with an _
      var compressedSigPath = fileIndexPath.Substring(0, fileIndexPath.Length - 1) + "_";
      var compressedFilePath = GetPhysicalFileFromServer(urlForServer, compressedSigPath, cache);
      if (compressedFilePath != null)
        try
        {
          // Decompress it
          Command.Run("Expand " + Command.Quote(compressedFilePath) + " " + Command.Quote(targetPath));
          return targetPath;
        }
        catch (Exception e)
        {
          Trace("Exception encountered while expanding file '{0}': {1}", compressedFilePath, e.Message);
        }
        finally
        {
          if (File.Exists(compressedFilePath))
            File.Delete(compressedFilePath);
        }

      // Just try to fetch the file directly
      var ret = GetPhysicalFileFromServer(urlForServer, fileIndexPath, cache);
      if (ret != null)
        return ret;

      // See if we have a file that tells us to redirect elsewhere. 
      var filePtrSigPath = Path.Combine(Path.GetDirectoryName(fileIndexPath), "file.ptr");
      var filePtrData = GetPhysicalFileFromServer(urlForServer, filePtrSigPath, cache, true);
      if (filePtrData == null) return null;

      filePtrData = filePtrData.Trim();
      if (filePtrData.StartsWith("PATH:"))
        filePtrData = filePtrData.Substring(5);

      if (!filePtrData.StartsWith("MSG:") && File.Exists(filePtrData))
        using (var fs = File.OpenRead(filePtrData))
        {
          CopyStreamToFile(fs, filePtrData, targetPath, fs.Length);
          return targetPath;
        }

      Trace("Error resolving file.ptr: content '{0}' from '{1}.", filePtrData, filePtrSigPath);

      return null;
    }
    
    private string GetPhysicalFileFromServer(string serverPath, string pdbIndexPath, string symbolCacheDir, bool returnContents = false)
    {
      if (string.IsNullOrEmpty(serverPath))
        return null;

      var fullDestPath = Path.Combine(symbolCacheDir, pdbIndexPath);
      if (File.Exists(fullDestPath))
        return fullDestPath;

      if (serverPath.StartsWith("http:"))
      {
        var fullUri = serverPath + "/" + pdbIndexPath.Replace('\\', '/');
        try
        {
          var req = (HttpWebRequest)WebRequest.Create(fullUri);
          req.UserAgent = "Microsoft-Symbol-Server/6.13.0009.1140";
          req.Timeout = Timeout;
          var response = req.GetResponse();
          using (var fromStream = response.GetResponseStream())
          {
            if (returnContents)
            {
              TextReader reader = new StreamReader(fromStream);
              return reader.ReadToEnd();
            }

            CopyStreamToFile(fromStream, fullUri, fullDestPath, response.ContentLength);
            return fullDestPath;
          }
        }
        catch (WebException)
        {
          // A timeout or 404.
          return null;
        }
        catch (Exception e)
        {
          Trace("Probe of {0} failed: {1}", fullUri, e.Message);
          return null;
        }
      }

      var fullSrcPath = Path.Combine(serverPath, pdbIndexPath);
      if (!File.Exists(fullSrcPath))
        return null;

      if (returnContents)
        try
        {
          return File.ReadAllText(fullSrcPath);
        }
        catch
        {
          return "";
        }

      using (var fs = File.OpenRead(fullSrcPath))
      {
        CopyStreamToFile(fs, fullSrcPath, fullDestPath, fs.Length);
      }

      return fullDestPath;
    }
    
    /// <summary>
    ///   Copies a given stream to a file.
    /// </summary>
    /// <param name="input">The stream of data to copy.</param>
    /// <param name="fullSrcPath">The original source location of "stream".  This may be a URL or null.</param>
    /// <param name="fullDestPath">The full destination path to copy the file to.</param>
    /// <param name="size">A hint as to the length of the stream.  This may be 0 or negative if the length is unknown.</param>
    /// <returns>True if the method successfully copied the file, false otherwise.</returns>
    protected virtual void CopyStreamToFile(Stream input, string fullSrcPath, string fullDestPath, long size)
    {
      Debug.Assert(input != null);

      try
      {
        var fi = new FileInfo(fullDestPath);
        if (fi.Exists && fi.Length == size)
          return;

        var folder = Path.GetDirectoryName(fullDestPath);
        Directory.CreateDirectory(folder);

        FileStream file = null;
        try
        {
          file = new FileStream(fullDestPath, FileMode.OpenOrCreate);
          var buffer = new byte[2048];
          int read;
          while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            file.Write(buffer, 0, read);
        }
        finally
        {
          if (file != null)
            file.Dispose();
        }
      }
      catch (Exception e)
      {
        try
        {
          if (File.Exists(fullDestPath))
            File.Delete(fullDestPath);
        }
        catch
        {
          // We ignore errors of this nature.
        }

        Trace("Encountered an error while attempting to copy '{0} to '{1}': {2}", fullSrcPath, fullDestPath, e.Message);
      }
    }
    
    /// <summary>
    ///   Gets or sets the local symbol file cache.  This is the location that
    ///   all symbol files are downloaded to on your computer.
    /// </summary>
    public string SymbolCache
    {
      get
      {
        var cache = _symbolCache;
        if (!string.IsNullOrEmpty(cache))
          return cache;

        var tmp = Path.GetTempPath();
        if (string.IsNullOrEmpty(tmp))
          tmp = ".";

        return Path.Combine(tmp, "symbols");
      }
      set
      {
        _symbolCache = value;
        if (!string.IsNullOrEmpty(value))
          Directory.CreateDirectory(value);

        SymbolPathOrCacheChanged();
      }
    }
    
    /// <summary>
    ///   This property gets and sets the global _NT_SYMBOL_PATH environment variable.
    ///   This is the global setting for symbol paths on a computer.
    /// </summary>
    public static string _NT_SYMBOL_PATH
    {
      get
      {
        var ret = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
        return ret ?? "";
      }
      set => Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", value);
    }
    
    /// <summary>
    ///   Return the string representing a symbol path for the 'standard' microsoft symbol servers.
    ///   This returns the public msdl.microsoft.com server if outside Microsoft.
    /// </summary>
    public static string MicrosoftSymbolServerPath
    {
      get
      {
        var first = true;
        var result = new StringBuilder();

        foreach (var path in s_microsoftSymbolServers)
        {
          if (!first)
            result.Append(';');

          result.Append("SRV*");
          result.Append(path);
          first = false;
        }

        return result.ToString();
      }
    }

  }
}