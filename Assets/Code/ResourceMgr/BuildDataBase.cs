using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ResourceMoudle
{
    public class BuildDatabase
    {
        IFileSystem fileSystem_;

        Dictionary<string, string> assetPathToBundleName_;
        Dictionary<string, string[]> bundleNameToDependencies_;
        Dictionary<string, string[]> bundleNameToAssets_;

        Dictionary<string, Triple<string, long, long>> bundleNameToPackInfo_;

        public BuildDatabase(IFileSystem fileSystem)
        {
            fileSystem_ = fileSystem;
        }

        public bool LocateBundle(string bundleName, out Location location, out string fileName, out long offset, out long size)
        {
            Triple<string, long, long> packInfo;

            if (bundleNameToPackInfo_.TryGetValue(bundleName, out packInfo))
            {
                if (fileSystem_.Exists(Location.Download, packInfo.first))
                {
                    location = Location.Download;
                    fileName = packInfo.first;
                    offset = packInfo.second;
                    size = packInfo.third;
                    return true;
                }

                if (fileSystem_.Exists(Location.Initial, packInfo.first))
                {
                    location = Location.Initial;
                    fileName = packInfo.first;
                    offset = packInfo.second;
                    size = packInfo.third;
                    return true;
                }
            }
            else
            {
                if (fileSystem_.Exists(Location.Download, bundleName))
                {
                    location = Location.Download;
                    fileName = bundleName;
                    offset = 0;
                    size = fileSystem_.GetLength(Location.Download, bundleName);
                    return true;
                }

                if (fileSystem_.Exists(Location.Initial, bundleName))
                {
                    location = Location.Initial;
                    fileName = bundleName;
                    offset = 0;
                    size = fileSystem_.GetLength(Location.Initial, bundleName);
                    return true;
                }
            }

            location = Location.Initial;
            fileName = null;
            offset = 0;
            size = 0;
            return false;
        }

        public string GetBundleName(string assetPath)
        {
            EnsureLoaded();

            return DataUtil.GetValueOrDefault(assetPathToBundleName_, assetPath.Replace('\\', '/'));
        }

        public string[] GetAssetPaths(string bundleName)
        {
            EnsureLoaded();

            return DataUtil.GetValueOrDefault(bundleNameToAssets_, bundleName,
                () => { return new string[0]; });
        }

        public string[] GetAllBundleNames()
        {
            EnsureLoaded();

            return bundleNameToDependencies_.Keys.ToArray();
        }

        public string[] GetBundleDependencies(string bundleName, bool recursive)
        {
            EnsureLoaded();

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
                return DataUtil.GetValueOrDefault(bundleNameToDependencies_, bundleName,
                    () => { return new string[0]; });
            }
        }

        void EnsureLoaded()
        {
            if (assetPathToBundleName_ == null)
            {
                LoadBuild();
            }
        }

        void LoadBuild()
        {
            FileLocator fl1 = new FileLocator(Location.Initial, Setting.buildFileName);
            FileLocator fl2 = new FileLocator(Location.Download, Setting.buildFileName);

            TextReader reader = null;

            if (IOUtility.Exists(fileSystem_, fl2))
            {
                reader = IOUtility.OpenText(fileSystem_, fl2);
            }
            else if (IOUtility.Exists(fileSystem_, fl1))
            {
                reader = IOUtility.OpenText(fileSystem_, fl1);
            }

            if (reader == null)
            {
                Debug.LogError("Failed to load build data file, please ensure the file exists and the path is correct.");
                return;
            }

            assetPathToBundleName_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bundleNameToDependencies_ = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            bundleNameToAssets_ = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            bundleNameToPackInfo_ = new Dictionary<string, Triple<string, long, long>>(StringComparer.OrdinalIgnoreCase);

            string name = null;
            List<string> assets = new List<string>();

            Triple<string, long, long> packInfo = null;

            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("//"))
                {
                    continue;
                }

                if (line.Length == 0)
                {
                    if (name != null)
                    {
                        bundleNameToAssets_.Add(name, assets.ToArray());

                        if (packInfo != null)
                        {
                            bundleNameToPackInfo_.Add(name, packInfo);
                            packInfo = null;
                        }

                        name = null;
                        assets.Clear();
                    }

                    continue;
                }

                if (line[0] != ' ')
                {
                    if (line[0] == '@')
                    {
                        string[] result = line.Substring(1).Split('/');

                        packInfo = DataUtil.MakeTriple(result[0], long.Parse(result[1]), long.Parse(result[2]));
                    }
                    else
                    {
                        string[] result = line.Split(':');

                        if (result.Length == 2)
                        {
                            bundleNameToDependencies_.Add(result[0], result[1].Split(','));
                        }

                        name = result[0];
                    }
                }
                else if (name != null)
                {
                    line = line.Replace('\\', '/').Trim();
                    assets.Add(line);
                    assetPathToBundleName_[line] = name;
                }
            }

            reader.Close();
        }

     
    }

}