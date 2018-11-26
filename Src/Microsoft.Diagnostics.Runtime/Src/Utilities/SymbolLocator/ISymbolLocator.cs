namespace Microsoft.Diagnostics.Runtime.Utilities
{
  public interface ISymbolLocator
  {
    /// <summary>
    /// Attempts to locate a binary via the symbol server. This function will then copy the file
    /// locally to the symbol cache and return the location of the local file on disk.
    /// </summary>
    /// <param name="fileName">The filename that the binary is indexed under.</param>
    /// <param name="buildTimeStamp">The build timestamp the binary is indexed under.</param>
    /// <param name="imageSize">The image size the binary is indexed under.</param>
    /// <param name="checkProperties">Whether or not to validate the properties of the binary after download.</param>
    /// <returns>A full path on disk (local) of where the binary was copied to, null if it was not found.</returns>
    string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true);
  }
}