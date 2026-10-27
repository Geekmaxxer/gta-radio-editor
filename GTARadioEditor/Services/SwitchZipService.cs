namespace GTARadioEditor.Services;

public static class SwitchZipService
{
    public const string TitleId = "0100B00B51230000";

    public static string BuildLayeredFsZip(string builtRpfPath, System.IProgress<string>? progress)
    {
        if (string.IsNullOrWhiteSpace(builtRpfPath) || !System.IO.File.Exists(builtRpfPath))
            throw new System.IO.FileNotFoundException("Build an output RPF first. Missing: " + builtRpfPath);
        string fileName = System.IO.Path.GetFileName(builtRpfPath);
        string zipPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(builtRpfPath) ?? ".", System.IO.Path.GetFileNameWithoutExtension(fileName) + "-layeredfs.zip");
        if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);
        string entryPath = "atmosphere/contents/" + TitleId + "/romfs/switch/audio/sfx/" + fileName;
        if (progress != null) progress.Report("Zipping LayeredFS: " + entryPath);
#if NET48
        using (var zipStream = System.IO.File.Open(zipPath, System.IO.FileMode.Create))
        using (var zip = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(entryPath, System.IO.Compression.CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            using (var source = System.IO.File.OpenRead(builtRpfPath))
            {
                source.CopyTo(entryStream);
            }
        }
#else
        using (var zipStream = System.IO.File.Open(zipPath, System.IO.FileMode.Create))
        using (var zip = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(entryPath, System.IO.Compression.CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            using (var source = System.IO.File.OpenRead(builtRpfPath))
            {
                source.CopyTo(entryStream);
            }
        }
#endif
        return zipPath;
    }

    public static string LayeredFsHint(string fileName)
    {
        return "atmosphere/contents/" + TitleId + "/romfs/switch/audio/sfx/" + fileName;
    }
}
