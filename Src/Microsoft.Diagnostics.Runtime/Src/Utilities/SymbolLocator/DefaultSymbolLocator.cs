﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public class DefaultSymbolLocator : ISymbolLocator
  {
    private static readonly IEnumerable<string> MicrosoftSymbolServers = new[]
    {
      "http://msdl.microsoft.com/download/symbols",
      "http://referencesource.microsoft.com/symbols"
    };

    private readonly object myLock = new object();
    private readonly IExternalLogger myLogger;
    private readonly string myCacheLocation;
    private readonly IEnumerable<SymPathElement> mySymPathElements;
    private readonly IDictionary<FileEntry, string> myCache = new Dictionary<FileEntry, string>();
    private readonly ICollection<FileEntry> myMissingEntries = new HashSet<FileEntry>();
    
    
    /// <summary>
    ///   The timeout (in milliseconds) used when contacting each individual server. This is not a total timeout for the entire symbol server operation.
    /// </summary>
    public int Timeout { get; set; } = 60000;
    
    public DefaultSymbolLocator(ITempPathProvider tempPathProvider, IExternalLogger logger)
    {
      if (tempPathProvider == null) throw new ArgumentNullException(nameof(tempPathProvider));
      if (logger == null) throw new ArgumentNullException(nameof(logger));

      myCacheLocation = tempPathProvider.GetFixedTempPath("Symbols");
      myLogger = logger;
      mySymPathElements = GetSymPathElements();
    }

    private static IEnumerable<SymPathElement> GetSymPathElements()
    {
      var result = new List<SymPathElement>();
      foreach (var entry in MicrosoftSymbolServers)
        result.Add(new SymPathElement(entry));
      
      var ntSymbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
      var entries = (ntSymbolPath ?? "").Split(';').Where(e => !string.IsNullOrEmpty(e)).ToArray();
      foreach (var entry in entries)
        result.Add(new SymPathElement(entry));

      return result;
    }

    private static FileEntry CreateEntry(string fileName, int buildTimeStamp, int imageSize)
    {
      fileName = Path.GetFileName(fileName);
      if (string.IsNullOrEmpty(fileName))
        return null;
      
      return new FileEntry(fileName.ToLower(), buildTimeStamp, imageSize);
    }
    
    public string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
    {
      var entry = CreateEntry(fileName, buildTimeStamp, imageSize);
      if (entry == null)
        return null;

      lock (myLock)
      {
        if (myCache.TryGetValue(entry, out var result))
          return result;

        if (myMissingEntries.Contains(entry))
          return null;

        if (ValidateBinary(fileName, buildTimeStamp, imageSize, checkProperties))
        {
          myCache[entry] = fileName;
          return fileName;
        }
      }

      // Finally, check the symbol paths.
      string indexPath = null;
      foreach (var element in mySymPathElements)
      {
        if (element.IsSymServer)
        {
          if (indexPath == null)
            indexPath = GetIndexPath(entry);

          var target = TryGetFileFromServer(element.Target, indexPath, element.Cache ?? myCacheLocation);
          if (target == null)
          {
            Trace($"Server '{element.Target}' did not have file '{Path.GetFileName(fileName)}' with timestamp={buildTimeStamp:x} and filesize={imageSize:x}.");
          }
          else if (ValidateBinary(target, buildTimeStamp, imageSize, checkProperties))
          {
            Trace($"Found '{fileName}' on server '{element.Target}'.  Copied to '{target}'.");
            lock (myLock)
              myCache[entry] = target;
            
            return target;
          }
        }
        else
        {
          var filePath = Path.Combine(element.Target, fileName);
          if (ValidateBinary(filePath, buildTimeStamp, imageSize, checkProperties))
          {
            Trace($"Found '{fileName}' at '{filePath}'.");
            lock (myLock)
              myCache[entry] = filePath;
            
            return filePath;
          }
        }
      }

      lock (myLock)
        myMissingEntries.Add(entry);
      
      return null;
    }
    
   
    /// <summary>
    ///   Validates whether a file on disk matches the properties we expect.
    /// </summary>
    /// <param name="fullPath">The full path on disk of a PEImage to inspect.</param>
    /// <param name="buildTimeStamp">The build timestamp we expect to match.</param>
    /// <param name="imageSize">The build image size we expect to match.</param>
    /// <param name="checkProperties">Whether we should actually validate the imagesize/timestamp or not.</param>
    /// <returns></returns>
    private bool ValidateBinary(string fullPath, int buildTimeStamp, int imageSize, bool checkProperties)
    {
      if (string.IsNullOrEmpty(fullPath))
        return false;

      if (!File.Exists(fullPath))
        return false;

      if (!checkProperties)
        return true;

      try
      {
        using (var pefile = new PEFile(fullPath))
        {
          var header = pefile.Header;
          if (header.TimeDateStampSec == buildTimeStamp && header.SizeOfImage == imageSize)
            return true;

          Trace("Rejected file '{0}' because file size and time stamp did not match.", fullPath);
          return false;
        }
      }
      catch (Exception e)
      {
        Trace("Encountered exception {0} while attempting to inspect file '{1}'.", e.GetType().Name, fullPath);
        return false;
      }
    }
    
    private void Trace(string format, params object[] parameters)
    {
      myLogger.Log("DefaultSymbolLocator", format, parameters);
    }
    
    private static string GetIndexPath(FileEntry entry)
    {
      return entry.FileName + @"\" + entry.TimeStamp.ToString("x") + entry.FileSize.ToString("x") + @"\" + entry.FileName;
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
    private void CopyStreamToFile(Stream input, string fullSrcPath, string fullDestPath, long size)
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
  }
}