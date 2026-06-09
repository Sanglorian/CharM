using System.IO.Compression;
using System.Security.Cryptography;
using LibArchive.Net;

namespace CharM.RulesDb.Import;
public static class XMLExtractor
{
    // Extract rules XML from a character builder update
    public static string ExtractXML(string exePath)
    {
        string destPath = Path.GetTempFileName();
        string datFile = "RegPatcher.dat";
        string xmlFile = "combined.dnd40.encrypted";
        byte[]? datCode = null;
        byte[]? xml = null;

        using var exeArc = new LibArchiveReader(exePath);
        foreach (var entry in exeArc.Entries())
        {
            if(entry.Name.Equals(xmlFile, StringComparison.OrdinalIgnoreCase))
            {
                xml = entry.ReadAllBytes().ToArray();
                continue;
            }
            if (entry.Name.Equals(datFile, StringComparison.OrdinalIgnoreCase))
            {
                var base64 = entry.ReadAllText();
                datCode = Convert.FromBase64String(base64);
                continue;
            }
        }
        if(xml is null || datCode is null || (!datCode?.Any() ?? true) || (!xml?.Any() ?? true) )
        {
            return string.Empty;
        }
        // if we're here than we're set. Lets decrypt it.
        // We need the AppId as the IV
        var iv = Guid.Parse("2a1ddbc4-4503-4392-9548-d0010d1ba9b1");
        using var aes = Aes.Create();
        aes.Key = datCode ?? Array.Empty<Byte>();
        var decrypted = aes.DecryptCbc(xml?.Skip(16).ToArray() ?? Array.Empty<Byte>(), iv.ToByteArray());
        using var gzStream = new GZipStream(new MemoryStream(decrypted), CompressionMode.Decompress);
        using var fileStream = File.Open(destPath, FileMode.Create);
        gzStream.CopyTo(fileStream);
        fileStream.Flush();
        return destPath;
    }
}
