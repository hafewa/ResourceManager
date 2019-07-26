using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ResourceMoudle
{
    public interface IBuildResult
    {
        string[] GetAllBundles();
        string[] GetSceneBundles();

        string GetBundleHash(string bundleName);
        string GetBundleTag(string bundleName);
        string[] GetBundleDependencies(string bundleName, bool recursive);
        string[] GetAssetsInBundle(string bundleName);

        string[] GetAllPackages();
        string GetPackageHash(string packageName);

        bool isPackedBundle(string bundleName);
        bool GetBundlePackInfo(string bundleName, out string packageName, out long offset, out long size);

        string[] GetAllCustomFiles();
        string GetCustomFileHash(string fileName);
    }

}