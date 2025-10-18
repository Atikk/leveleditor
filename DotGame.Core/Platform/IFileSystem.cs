using System.Collections.Generic;
using System.IO;

namespace DotGame.Core.Platform;

public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    Stream OpenRead(string path, FileShare share = FileShare.Read);

    Stream OpenWrite(string path, bool overwrite = true, FileShare share = FileShare.None);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

    string GetAbsolutePath(string path);
}
