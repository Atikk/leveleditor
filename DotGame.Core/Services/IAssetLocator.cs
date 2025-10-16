using System.IO;

namespace DotGame.Core.Services;

public interface IAssetLocator
{
    Stream? OpenRead(string assetPath);
}
