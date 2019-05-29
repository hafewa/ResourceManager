using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ResourceMoudle
{
    class BundleDatabase
    {
        private string prefix_ = "";
        private int currentBundleId_ = 1;
        private int currentPackageId_ = 1;

        private HashSet<string> sceneAssetSet_ = new HashSet<string>();
        private HashSet<int> leafBundleIdSet_ = new HashSet<int>();

        private Dictionary<string, string> assetGuidToHash_ = new Dictionary<string, string>();
        private Dictionary<string, int> assetGuidToBundleId_ = new Dictionary<string, int>();
        private Dictionary<string, string> bundleNameToHash_ = new Dictionary<string, string>();
        private Dictionary<int, string> bundleIdToTag_ = new Dictionary<int, string>();
        private Dictionary<string, int> bundleNameToPackageId_ = new Dictionary<string, int>();
        private Dictionary<string, Triple<string, long, long>> bundleNameToPackInfo_ = new Dictionary<string, Triple<string, long, long>>();

        private List<Pair<string, List<string>>> build_ = new List<Pair<string, List<string>>>();

        public string prefix
        {
            get { return prefix_; }
            set { prefix_ = value; }
        }

        public int currentBundleId
        {
            get { return currentBundleId_; }
            set { currentBundleId_ = value; }
        }

        public int currentPackageId
        {
            get { return currentPackageId_; }
            set { currentPackageId_ = value; }
        }

        public void AddSceneAsset(string path)
        {
            sceneAssetSet_.Add(AssetDatabase.AssetPathToGUID(path));
        }

        public bool HasSceneAsset(string path)
        {
            return sceneAssetSet_.Contains(AssetDatabase.AssetPathToGUID(path));
        }

        public void AddAsset(string path, int bundleId)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();

            assetGuidToHash_.Add(guid, hash);
            assetGuidToBundleId_.Add(guid, bundleId);
        }

        public bool CompareAssetHash(string path)
        {
            return CompareAssetHashByGuid(AssetDatabase.AssetPathToGUID(path));
        }

        public bool CompareAssetHashByGuid(string guid)
        {
            string oldHash;

            if (assetGuidToHash_.TryGetValue(guid, out oldHash))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
                return (hash == oldHash);
            }

            return false;
        }

        public int GetBundleIdOfAsset(string path)
        {
            return GetBundleIdOfAssetByGuid(AssetDatabase.AssetPathToGUID(path));
        }

        public int GetBundleIdOfAssetByGuid(string guid)
        {
            return DataUtil.GetValueOrDefault(assetGuidToBundleId_, guid, 0);
        }

        public void SetLeafBundle(int bundleId)
        {
            leafBundleIdSet_.Add(bundleId);
        }

        public bool IsLeafBundle(int bundleId)
        {
            return leafBundleIdSet_.Contains(bundleId);
        }

        public void SetBundleHash(string bundle, string hash)
        {
            bundleNameToHash_[bundle] = hash;
        }

        public string GetBundleHash(string bundle)
        {
            return DataUtil.GetValueOrDefault(bundleNameToHash_, bundle, "");
        }

        public Dictionary<string, string> GetBundles()
        {
            return bundleNameToHash_;
        }

        public void SetBundleTag(int bundleId, string tag)
        {
            bundleIdToTag_[bundleId] = tag;
        }

        public string GetBundleTag(int bundleId)
        {
            return DataUtil.GetValueOrDefault(bundleIdToTag_, bundleId, "");
        }

        public List<Pair<string, List<string>>> GetBuild()
        {
            return build_;
        }

        public void SetBuild(AssetBundleBuild[] builds)
        {
            List<Pair<string, List<string>>> build = new List<Pair<string, List<string>>>();

            for (int i = 0; i < builds.Length; ++i)
            {
                List<string> assetList = new List<string>();

                string[] assets = builds[i].assetNames;

                for (int j = 0; j < assets.Length; ++j)
                {
                    assetList.Add(AssetDatabase.AssetPathToGUID(assets[j]));
                }

                build.Add(DataUtil.MakePair(builds[i].assetBundleName, assetList));
            }

            SetBuild(build);
        }

        public void SetBuild(List<Pair<string, List<string>>> build)
        {
            build_ = build;
        }

        public void SetBundlePackageId(string bundleName, int packageId)
        {
            bundleNameToPackageId_[bundleName] = packageId;
        }

        public int GetBundlePackageId(string bundleName)
        {
            return DataUtil.GetValueOrDefault(bundleNameToPackageId_, bundleName, 0);
        }

        public void SetBundlePackInfo(string bundleName, string packageName, long offset, long size)
        {
            bundleNameToPackInfo_[bundleName] = DataUtil.MakeTriple(packageName, offset, size);
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

            packageName = null;
            offset = 0;
            size = 0;
            return false;
        }

        public static void SaveToFile(string filename, BundleDatabase bd)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Create))
            {
                byte[] fileMark = new byte[3]
                {
                    Convert.ToByte('L'),
                    Convert.ToByte('B'),
                    Convert.ToByte('B'),
                };

                IOUtility.WriteFile(stream, fileMark);

                int version = 4;

                Serialization.WriteToStream(stream, version);

                Serialization.WriteToStream(stream, bd.prefix_);
                Serialization.WriteToStream(stream, bd.currentBundleId_);
                Serialization.WriteToStream(stream, bd.currentPackageId_);

                Serialization.WriteToStream(stream, bd.sceneAssetSet_);

                Serialization.WriteToStream(stream, bd.assetGuidToHash_);
                Serialization.WriteToStream(stream, bd.assetGuidToBundleId_);
                Serialization.WriteToStream(stream, bd.leafBundleIdSet_);

                Serialization.WriteToStream(stream, bd.bundleNameToHash_);

                Serialization.WriteToStream(stream, bd.bundleIdToTag_);

                Serialization.WriteToStream(stream, bd.bundleNameToPackageId_);
                Serialization.WriteToStream(stream, bd.bundleNameToPackInfo_);

                Serialization.WriteToStream(stream, bd.build_);

                stream.Close();
            }
        }

        public static void LoadFromFile(string filename, out BundleDatabase bd)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Open))
            {
                byte[] buffer = new byte[3];

                byte[] fileMark = new byte[3]
                {
                    Convert.ToByte('L'),
                    Convert.ToByte('B'),
                    Convert.ToByte('B'),
                };

                if (IOUtility.ReadFile(stream, buffer, 3) != 3)
                {
                    throw new System.Exception("Bad file");
                }

                for (int i = 0; i < 3; ++i)
                {
                    if (buffer[i] != fileMark[i])
                    {
                        throw new System.Exception("Bad file");
                    }
                }

                int version;

                Serialization.ReadFromStream(stream, out version);

                if (version < 4)
                {
                    throw new System.Exception("BuildDatabase version is too low");
                }

                bd = new BundleDatabase();

                Serialization.ReadFromStream(stream, out bd.prefix_);
                Serialization.ReadFromStream(stream, out bd.currentBundleId_);
                Serialization.ReadFromStream(stream, out bd.currentPackageId_);

                Serialization.ReadFromStream(stream, out bd.sceneAssetSet_);

                Serialization.ReadFromStream(stream, out bd.assetGuidToHash_);
                Serialization.ReadFromStream(stream, out bd.assetGuidToBundleId_);
                Serialization.ReadFromStream(stream, out bd.leafBundleIdSet_);

                Serialization.ReadFromStream(stream, out bd.bundleNameToHash_);

                Serialization.ReadFromStream(stream, out bd.bundleIdToTag_);

                Serialization.ReadFromStream(stream, out bd.bundleNameToPackageId_);
                Serialization.ReadFromStream(stream, out bd.bundleNameToPackInfo_);

                Serialization.ReadFromStream(stream, out bd.build_);

                stream.Close();
            }
        }
    }

}