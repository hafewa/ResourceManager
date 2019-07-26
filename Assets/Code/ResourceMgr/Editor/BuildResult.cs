using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ResourceMoudle
{
    class BuildResultData : IBuildResult
    {
        private List<string> bundleNameList_;
        private HashSet<string> sceneBundleNameSet_;
        private Dictionary<string, string> bundleNameToHash_;
        private Dictionary<string, string> bundleNameToTag_;
        private Dictionary<string, string[]> bundleNameToDependencies_;
        private Dictionary<string, string[]> bundleNameToAssets_;

        private List<string> packageNameList_;
        private Dictionary<string, string> packageNameToHash_;

        private Dictionary<string, Triple<string, long, long>> bundleNameToPackInfo_;

        private List<string> customFileNameList_;
        private Dictionary<string, string> customFileNameToHash_;
        public BuildResultData()
        {
            bundleNameList_ = new List<string>();
            sceneBundleNameSet_ = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bundleNameToHash_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bundleNameToTag_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bundleNameToDependencies_ = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            bundleNameToAssets_ = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            packageNameList_ = new List<string>();
            packageNameToHash_ = new Dictionary<string, string>();

            bundleNameToPackInfo_ = new Dictionary<string, Triple<string, long, long>>();

            customFileNameList_ = new List<string>();
            customFileNameToHash_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string[] GetAllBundles()
        {
            return bundleNameList_.ToArray();
        }

        public string[] GetSceneBundles()
        {
            return bundleNameList_.Where(x => sceneBundleNameSet_.Contains(x)).ToArray();
        }

        public string GetBundleHash(string bundleName)
        {
            return DataUtil.GetValueOrDefault(bundleNameToHash_, bundleName, "");
        }

        public string GetBundleTag(string bundleName)
        {
            return DataUtil.GetValueOrDefault(bundleNameToTag_, bundleName, "");
        }

        public string[] GetBundleDependencies(string bundleName, bool recursive)
        {
            if (recursive)
            {
                List<string> list = new List<string>();
                HashSet<string> set = new HashSet<string>();

                list.Add(bundleName);
                set.Add(bundleName);

                for (int i = 0; i < list.Count; ++i)
                {
                    foreach (string p in GetBundleDependencies(list[i], false))
                    {
                        if (set.Add(p))
                        {
                            list.Add(p);
                        }
                    }
                }

                list.RemoveAt(0);

                return list.ToArray();
            }
            else
            {
                return DataUtil.GetValueOrDefault(bundleNameToDependencies_, bundleName);
            }
        }

        public string[] GetAssetsInBundle(string bundleName)
        {
            return DataUtil.GetValueOrDefault(bundleNameToAssets_, bundleName);
        }

        public string[] GetAllPackages()
        {
            return packageNameList_.ToArray();
        }

        public string GetPackageHash(string packageName)
        {
            return DataUtil.GetValueOrDefault(packageNameToHash_, packageName, "");
        }

        public bool isPackedBundle(string bundleName)
        {
            return bundleNameToPackInfo_.ContainsKey(bundleName);
        }

        public bool GetBundlePackInfo(string bundleName, out string packageName, out long offset, out long size)
        {
            Triple<string, long, long> packInfo;

            if (bundleNameToPackInfo_.TryGetValue(bundleName, out packInfo))
            {
                packageName = packInfo.first;
                offset = packInfo.second;
                size = packInfo.third;

                return true;
            }
            else
            {
                packageName = null;
                offset = 0;
                size = 0;

                return false;
            }
        }

        public string[] GetAllCustomFiles()
        {
            return customFileNameList_.ToArray();
        }

        public string GetCustomFileHash(string fileName)
        {
            return DataUtil.GetValueOrDefault(customFileNameToHash_, fileName, "");
        }

        public void AddBundle(string bundleName, bool isSceneBundle = false)
        {
            bundleNameList_.Add(bundleName);

            if (isSceneBundle)
            {
                sceneBundleNameSet_.Add(bundleName);
            }
        }

        public void SetBundleHash(string bundleName, string hash)
        {
            bundleNameToHash_.Add(bundleName, hash);
        }

        public void SetBundleTag(string bundleName, string tag)
        {
            bundleNameToTag_.Add(bundleName, tag);
        }

        public void SetBundleDependencies(string bundleName, string[] dependencies)
        {
            bundleNameToDependencies_.Add(bundleName, dependencies);
        }

        public void SetAssetsInBundle(string bundleName, string[] assets)
        {
            bundleNameToAssets_.Add(bundleName, assets);
        }

        public void AddPackage(string packageName)
        {
            packageNameList_.Add(packageName);
        }

        public void SetPackageHash(string packageName, string hash)
        {
            packageNameToHash_[packageName] = hash;
        }

        public void SetBundlePackInfo(string bundleName, string packageName, long offset, long size)
        {
            bundleNameToPackInfo_[bundleName] = DataUtil.MakeTriple(packageName, offset, size);
        }

        public void AddCustomFile(string fileName)
        {
            customFileNameList_.Add(fileName);
        }

        public void SetCustomFileHash(string fileName, string hash)
        {
            customFileNameToHash_[fileName] = hash;
        }

    }

}