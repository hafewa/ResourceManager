using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace ResourceMoudle
{
    public class BundleBuilder
    {
        private StreamWriter dbgOut_;

        private BuildArguments args_;

        private BundleDatabase bd_;

        private List<Triple<string, List<string>, GroupMode>> groupList_;

        private List<string[]> packageList_;

        private Dictionary<string, long> assetSizeHint_;

        ////////////////////////////////
        private HashSet<string> restoreIgnoredSet_;

        private Dictionary<string, int> assetPathToBundleId_;

        private Dictionary<int, HashSet<string>> bundleIdToAssetSet_;
        private Dictionary<int, string> bundleIdToTag_;
        private Dictionary<int, int> bundleIdToOrder_;
        private Dictionary<int, long> bundleIdToBundleSize_;

        private List<Pair<int, int>> relationship_;

        private HashSet<int> sceneBundleSet_;
        private List<int> sceneBundleList_;

        private Dictionary<string, int> bundleNameToPackageId_;
        private Dictionary<int, HashSet<string>> packageIdToBundleSet_;
        private Dictionary<int, long> packageIdToPackageSize_;

        private int currentBundleId_;
        private int currentPackageId_;
        ////////////////////////////////
        public BundleBuilder(BuildArguments args)
        {
            args_ = args;
        }

        public bool Build(bool silent = false, bool debugOutput = true)
        {
            try
            {
                if (debugOutput)
                {
                    string dbgOutDir = GetDebugOutputDirectory(args_.buildTarget);

                    DirectoryInfo di = Directory.CreateDirectory(dbgOutDir);

                    if (!di.Exists)
                    {
                        Debug.LogError("Failed to create directory");
                        return false;
                    }

                    string dbgFile = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss(fff)") + ".txt";

                    dbgOut_ = new StreamWriter(Path.Combine(dbgOutDir, dbgFile), false);
                }

                foreach (var customScript in args_.customScripts)
                {
                    if (!customScript.PreBuildEvent(args_))
                    {
                        return false;
                    }
                }

                if (string.IsNullOrEmpty(args_.location))
                {
                    Debug.LogError("Location cannot be left empty");
                    return false;
                }
                else if (Directory.Exists(args_.location) == false)
                {
                    Debug.LogError("Location does not exist");
                    return false;
                }

                if (string.IsNullOrEmpty(args_.version))
                {
                    Debug.LogError("Version cannot be left empty");
                    return false;
                }
                else if (Directory.Exists(GetVersionPath()))
                {
                    if (silent || EditorUtility.DisplayDialog("", "The specified version already exists, do you want to overwrite it ?", "Overwrite", "Cancel"))
                    {
                        ClearDirectory(GetVersionPath());
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    DirectoryInfo di = Directory.CreateDirectory(GetVersionPath());

                    if (!di.Exists)
                    {
                        Debug.LogError("Failed to create version directory");
                        return false;
                    }
                }

                if (args_.mode == Mode.FullBuild)
                {
                    bd_ = new BundleDatabase();

                    RestrictPrefix();
                }
                else
                {
                    if (string.IsNullOrEmpty(args_.parentVersion))
                    {
                        Debug.LogError("Parent version cannot be left empty in incremental mode");
                        return false;
                    }

                    if (args_.version == args_.parentVersion)
                    {
                        Debug.LogError("Version cannot be the same as the parent version");
                        return false;
                    }

                    BundleDatabase.LoadFromFile(Path.Combine(GetParentVersionPath(), ".LycheeBundleBuilder"), out bd_);

                    if (!CopyBundlesFromParentVersion())
                    {
                        return false;
                    }

                    args_.prefix = bd_.prefix;
                }

                if (!CollectGroups())
                {
                    return false;
                }

                if (!CollectPackages())
                {
                    return false;
                }

                RemovesDuplicateScenes();

                if (!PrebuildAssetBundles())
                {
                    return false;
                }

                BuildResultData buildResult = new BuildResultData();

                InitializeBuildIntermediateVars();

                RestoreBundlesOfGroups();
                RestoreBundlesOfScenes();

                AddAllGroups();

                AddAllScenes();

                MergeDependentBundles(sceneBundleList_);

                BundleDatabase newBd = new BundleDatabase();

                if (!BuildAssetBundles(newBd, buildResult))
                {
                    return false;
                }

                if (!BuildPackages(newBd, buildResult))
                {
                    return false;
                }

                BundleDatabase.SaveToFile(Path.Combine(GetVersionPath(), ".LycheeBundleBuilder"), newBd);

                AddAllCustomFiles(buildResult);

                WriteBuildFile(buildResult);
                WriteListFile(buildResult);

                foreach (var customScript in args_.customScripts)
                {
                    customScript.PostBuildEvent(buildResult);
                }

                if (args_.mode == Mode.IncrementalBuild && args_.reportVersionComparision)
                {
                    Comparer c = new Comparer(GetParentVersionPath(), GetVersionPath());
                    CompareResult result = c.Compare();
                    Debug.Log(result.ToString());
                }

                dbgOut_.Close();

                return true;
            }
            catch (System.Exception)
            {
                if (dbgOut_ != null)
                {
                    dbgOut_.Close();
                }

                throw;
            }
        }

        private void AddAllCustomFiles(BuildResultData buildResult)
        {
            try
            {
                int i = 1;
                int guessMax = 1;

                string[] files;

                foreach (var customScript in args_.customScripts)
                {
                    while (customScript.AddCustomFiles(out files))
                    {
                        foreach (string file in files)
                        {
                            string destFileName = Path.GetFileName(file).ToLower();
                            string destFileFullPath = Path.Combine(GetVersionPath(), destFileName);

                            if (destFileName == Setting.buildFileName || destFileName == Setting.listFileName)
                            {
                                throw new System.Exception("\"" + destFileName + "\" is a reserved file name");
                            }

                            File.Copy(file, destFileFullPath);

                            byte[] buffer = File.ReadAllBytes(destFileFullPath);

                            CreateSignatureFile(destFileFullPath, buffer);

                            string hash = VerifyUtility.HashToString(MD5.Create().ComputeHash(buffer));

                            buildResult.AddCustomFile(destFileName);
                            buildResult.SetCustomFileHash(destFileName, hash);

                            if (i > guessMax)
                            {
                                guessMax = i + i / 2;
                            }

                            EditorUtility.DisplayProgressBar("Lychee Bundle Builder",
                                "Add custom file " + Path.GetFileName(file),
                                MathUtility.CalculateProgress(0.0f, 1.0f, i++, guessMax));
                        }
                    }
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }
        }

        private void WriteListFile(IBuildResult buildResult)
        {
            using (StreamWriter writer = new StreamWriter(Path.Combine(GetVersionPath(), Setting.listFileName), false))
            {
                writer.Write(Setting.buildFileName + "/" + VerifyUtility.HashToString(
                    MD5.Create().ComputeHash(File.ReadAllBytes(
                        Path.Combine(GetVersionPath(), Setting.buildFileName)))) + "\r\n");

                foreach (string bundle in buildResult.GetAllBundles())
                {
                    if (buildResult.isPackedBundle(bundle))
                    {
                        continue;
                    }

                    string hash = buildResult.GetBundleHash(bundle);

                    writer.Write(bundle + "/" + hash + "\r\n");
                }

                foreach (string package in buildResult.GetAllPackages())
                {
                    string hash = buildResult.GetPackageHash(package);

                    writer.Write(package + "/" + hash + "\r\n");
                }

                foreach (string file in buildResult.GetAllCustomFiles())
                {
                    string hash = buildResult.GetCustomFileHash(file);

                    writer.Write(file + "/" + hash + "\r\n");
                }

                writer.Close();
            }
        }

        private void WriteBuildFile(IBuildResult buildResult)
        {
            string buildFilePath = Path.Combine(GetVersionPath(), Setting.buildFileName);

            using (StreamWriter writer = new StreamWriter(buildFilePath, false))
            {
                foreach (string bundle in buildResult.GetAllBundles())
                {
                    string tag = buildResult.GetBundleTag(bundle);

                    if (!string.IsNullOrEmpty(tag))
                    {
                        writer.Write("// " + tag + "\r\n");
                    }

                    if (buildResult.isPackedBundle(bundle))
                    {
                        string package;
                        long offset;
                        long size;

                        buildResult.GetBundlePackInfo(bundle, out package, out offset, out size);

                        writer.Write("@" + package + "/" + offset.ToString() + "/" + size.ToString() + "\r\n");
                    }

                    writer.Write(bundle);

                    string[] dependencies = buildResult.GetBundleDependencies(bundle, false);

                    if (dependencies.Length > 0)
                    {
                        writer.Write(":");

                        for (int i = 0; i < dependencies.Length; ++i)
                        {
                            if (i > 0)
                            {
                                writer.Write(",");
                            }

                            writer.Write(dependencies[i]);
                        }
                    }

                    writer.Write("\r\n");

                    foreach (string path in buildResult.GetAssetsInBundle(bundle))
                    {
                        writer.Write("    " + path + "\r\n");
                    }

                    writer.Write("\r\n");
                }

                writer.Close();
            }

            CreateSignatureFile(buildFilePath, File.ReadAllBytes(buildFilePath));
        }

        private void RestrictPrefix()
        {
            if (args_.prefix == null)
            {
                args_.prefix = "";
            }
            else if (args_.prefix.Length > 0)
            {
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < args_.prefix.Length; ++i)
                {
                    char c = char.ToLower(args_.prefix[i]);

                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    {
                        sb.Append(c);
                    }
                }

                args_.prefix = sb.ToString();
            }
        }

        private void RemovesDuplicateScenes()
        {
            HashSet<string> set = new HashSet<string>();
            List<string> list = new List<string>();

            foreach (string scene in args_.scenesInBuild)
            {
                string guid = AssetDatabase.AssetPathToGUID(scene);

                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning("Scene does not exist: " + scene);
                }
                else if (set.Add(guid))
                {
                    list.Add(NormalizeAssetPath(scene));
                }
            }

            args_.scenesInBuild = list.ToArray();
        }

        private bool PrebuildAssetBundles()
        {
            Dictionary<string, string> assetBundleMap = new Dictionary<string, string>();

            try
            {
                //资源和依赖
                HashSet<string> assetSet = new HashSet<string>();
                //循环所有组 
                for (int i = 0; i < groupList_.Count; ++i)
                {
                    //每组的资源列表
                    foreach (string path in groupList_[i].second)
                    {
                        assetSet.Add(path);
                        //获取资源的依赖
                        foreach (string path2 in GetDependencies(path, true))
                        {
                            assetSet.Add(path2);
                        }
                    }
                }
                //全部的资源数组
                List<string> assetList = assetSet.ToList();

                for (int i = 0; i < assetList.Count; ++i)
                {
                    string path = assetList[i];
                    string bundleName = AssetDatabase.AssetPathToGUID(path);
                    //过滤掉已经添加的 脚本 编辑器资源
                    if (assetBundleMap.ContainsValue(bundleName) || IsExcludedAsset(path) || IsEditorOnlyAsset(path))
                    {
                        continue;
                    }

                    assetBundleMap.Add(path, bundleName);

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder", "Prepare to build asset bundles",
                        MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, groupList_.Count));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }

            try
            {
                //添加场景相关资源到assetBundleMap
                for (int i = 0; i < args_.scenesInBuild.Length; ++i)
                {
                    foreach (string path in GetDependencies(args_.scenesInBuild[i], true))
                    {
                        string bundleName = AssetDatabase.AssetPathToGUID(path);

                        if (assetBundleMap.ContainsValue(bundleName) || IsExcludedAsset(path) || IsEditorOnlyAsset(path))
                        {
                            continue;
                        }

                        assetBundleMap.Add(path, bundleName);
                    }

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder", "Prepare to build asset bundles",
                        MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, args_.scenesInBuild.Length));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }

            AssetBundleBuild[] builds = MakeBuildingMap(assetBundleMap);

            string outPath = GetAssetSizeHintDirectory(args_.buildTarget);

            DirectoryInfo di = Directory.CreateDirectory(outPath);

            if (!di.Exists)
            {
                Debug.LogError("Failed to create directory");
                return false;
            }

            BuildAssetBundleOptions options = BuildAssetBundleOptions.None;

            if (args_.compressAssetBundles)
            {
                options = BuildAssetBundleOptions.ChunkBasedCompression;
            }
            else
            {
                options = BuildAssetBundleOptions.UncompressedAssetBundle;
            }

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outPath, builds, options, args_.buildTarget);

            if (manifest == null)
            {
                Debug.LogError("Failed to build asset bundles");
                return false;
            }

            HashSet<string> validAssetBundles = new HashSet<string>();

            foreach (string bundleName in manifest.GetAllAssetBundles())
            {
                validAssetBundles.Add(bundleName.ToLower());
            }

            assetSizeHint_ = new Dictionary<string, long>();

            foreach (KeyValuePair<string, string> p in assetBundleMap)
            {
                if (validAssetBundles.Contains(p.Value))
                {
                    FileInfo fileInfo = new FileInfo(Path.GetFullPath(Path.Combine(outPath, p.Value)));

                    if (!fileInfo.Exists)
                    {
                        Debug.LogError("File error");
                        return false;
                    }

                    if (fileInfo.Length > 4096)
                    {
                        assetSizeHint_.Add(p.Key, fileInfo.Length - 4096);
                    }
                    else
                    {
                        assetSizeHint_.Add(p.Key, 64);
                    }
                }
                else
                {
                    assetSizeHint_.Add(p.Key, 0);
                }
            }

            return true;
        }

        private bool CopyBundlesFromParentVersion()
        {
            try
            {
                Dictionary<string, string> bundles = bd_.GetBundles();

                string[] list = bundles.Keys.ToArray();

                for (int j = 0; j < list.Length; ++j)
                {
                    string path = Path.Combine(GetVersionPath(), list[j]);

                    if (bd_.GetBundlePackageId(list[j]) > 0)
                    {
                        string packageName;
                        long offset;
                        long size;

                        bd_.GetBundlePackInfo(list[j], out packageName, out offset, out size);

                        string path2 = Path.Combine(GetParentVersionPath(), packageName);

                        using (FileStream stream = new FileStream(path2, FileMode.Open))
                        {
                            stream.Seek(offset, SeekOrigin.Begin);

                            byte[] buffer = new byte[size];

                            Debug.Assert(size < int.MaxValue);

                            if (IOUtility.ReadFile(stream, buffer, (int)size) != size)
                            {
                                Debug.LogError("Unable to read package: " + path2);
                                return false;
                            }

                            File.WriteAllBytes(path, buffer);
                        }
                    }
                    else
                    {
                        File.Copy(Path.Combine(GetParentVersionPath(), list[j]), path);
                    }

                    string hashOld = bundles[list[j]];
                    string hash = VerifyUtility.HashToString(MD5.Create().ComputeHash(File.ReadAllBytes(path)));

                    if (hash != hashOld)
                    {
                        Debug.LogError("Bundle verification failed: " + path);
                        return false;
                    }

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder", "Copy bundles from parent version",
                        MathUtility.CalculateProgress(0.0f, 1.0f, j + 1, list.Length));
                }

                EditorUtility.ClearProgressBar();

                return true;
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }
        }

        private bool CollectGroups()
        {
            var groupList1 = new List<Triple<string, List<string>, GroupMode>>();
            var groupList2 = new List<Triple<string, List<string>, GroupMode>>();

            HashSet<string> groupIdSet = new HashSet<string>();

            foreach (var customScript in args_.customScripts)
            {
                string name = customScript.Name();

                string groupId;
                string[] assets;
                GroupMode mode;

                while (customScript.AddGroup(out groupId, out assets, out mode))
                {
                    string uniqueGroupId = name + "#" + groupId;

                    if (groupIdSet.Add(uniqueGroupId))
                    {
                        HashSet<string> set = new HashSet<string>();

                        for (int i = 0; i < assets.Length; ++i)
                        {
                            if (AssetExists(assets[i]))
                            {
                                set.Add(NormalizeAssetPath(assets[i]));
                            }
                            else
                            {
                                Debug.LogWarning("Asset does not exist: " + assets[i]);
                            }
                        }

                        if (mode == GroupMode.Standalone)
                        {
                            groupList1.Add(DataUtil.MakeTriple(uniqueGroupId, set.ToList(), mode));
                        }
                        else if (mode == GroupMode.Exclusive)
                        {
                            groupList2.Add(DataUtil.MakeTriple(uniqueGroupId, set.ToList(), mode));
                        }
                    }
                    else
                    {
                        Debug.LogError("Asset group with same ID already exists: " + uniqueGroupId);
                        for (int i = 0; i < assets.Length; i++)
                            Debug.LogError(" ###################### Asset group with same ID already exists: " + assets[i]);

                        return false;
                    }
                }
            }

            groupList_ = new List<Triple<string, List<string>, GroupMode>>();
            groupList_.AddRange(groupList1);
            groupList_.AddRange(groupList2);

            return true;
        }

        private bool CollectPackages()
        {
            packageList_ = new List<string[]>();

            foreach (var customScript in args_.customScripts)
            {
                string name = customScript.Name();

                string[] groupIds;

                while (customScript.AddPackage(out groupIds))
                {
                    for (int i = 0; i < groupIds.Length; ++i)
                    {
                        string uniqueGroupId = name + "#" + groupIds[i];

                        if (groupList_.FindIndex(x => x.first == uniqueGroupId) < 0)
                        {
                            Debug.LogError("Specified asset group ID was not found: " + groupIds[i]);
                            return false;
                        }

                        groupIds[i] = uniqueGroupId;
                    }

                    packageList_.Add(groupIds);
                }
            }

            return true;
        }

        private void InitializeBuildIntermediateVars()
        {
            restoreIgnoredSet_ = new HashSet<string>();

            assetPathToBundleId_ = new Dictionary<string, int>();

            bundleIdToAssetSet_ = new Dictionary<int, HashSet<string>>();
            bundleIdToTag_ = new Dictionary<int, string>();
            bundleIdToOrder_ = new Dictionary<int, int>();
            bundleIdToBundleSize_ = new Dictionary<int, long>();

            relationship_ = new List<Pair<int, int>>();

            sceneBundleSet_ = new HashSet<int>();
            sceneBundleList_ = new List<int>();

            bundleNameToPackageId_ = new Dictionary<string, int>();
            packageIdToBundleSet_ = new Dictionary<int, HashSet<string>>();
            packageIdToPackageSize_ = new Dictionary<int, long>();

            currentBundleId_ = bd_.currentBundleId;
            currentPackageId_ = bd_.currentPackageId;
        }

        private void RestoreBundlesOfGroups()
        {
            foreach (var group in groupList_)
            {
                string groupId = group.first;
                string tag = "";

                if (group.third == GroupMode.Standalone)
                {
                    tag = "[Standalone]" + groupId;
                }
                else if (group.third == GroupMode.Exclusive)
                {
                    tag = "[Exclusive]" + groupId;
                }

                foreach (string path in group.second)
                {
                    RestoreBundleOfAsset(path, tag);

                    foreach (string path2 in GetDependencies(path, true))
                    {
                        RestoreBundleOfAsset(path2, tag);
                    }
                }
            }
        }

        private void RestoreBundlesOfScenes()
        {
            foreach (string sceneAsset in args_.scenesInBuild)
            {
                RestoreBundleOfAsset(sceneAsset, "[Scene]");

                foreach (string path in GetDependencies(sceneAsset))
                {
                    string tag = "";

                    if (IsLightingDataAsset(path))
                    {
                        tag = "[LightingData]";
                    }

                    RestoreBundleOfAsset(path, tag);

                    foreach (string path2 in GetDependencies(path, true))
                    {
                        RestoreBundleOfAsset(path2, tag);
                    }
                }
            }
        }

        private void RestoreBundleOfAsset(string path, string tag)
        {
            if (restoreIgnoredSet_.Contains(path))
            {
                return;
            }

            int bundleId = GetBundleFromAsset(path);

            if (bundleId > 0)
            {
                return;
            }

            if ((bundleId = bd_.GetBundleIdOfAsset(path)) == 0)
            {
                return;
            }

            string oldTag = bd_.GetBundleTag(bundleId);

            if (oldTag == tag)
            {
                if (bd_.CompareAssetHash(path))
                {
                    MoveAssetToBundle(path, bundleId);

                    DebugOutput("AddAsset(Restore) " + path + ", unchanged, bundle " + bundleId);

                    if (string.IsNullOrEmpty(GetBundleTag(bundleId)) && !string.IsNullOrEmpty(tag))
                    {
                        SetBundleTag(bundleId, tag);
                    }
                }
                else
                {
                    string[] dependencies = GetDependencies(path);

                    bool isLeafAsset = (dependencies.Length == 0);
                    bool isLeafBundle = bd_.IsLeafBundle(bundleId);

                    if (isLeafAsset == isLeafBundle)
                    {
                        MoveAssetToBundle(path, bundleId);

                        DebugOutput("AddAsset(Restore) " + path + ", changed, leaf " + isLeafAsset + ", bundle " + bundleId);

                        if (string.IsNullOrEmpty(GetBundleTag(bundleId)) && !string.IsNullOrEmpty(tag))
                        {
                            SetBundleTag(bundleId, tag);
                        }
                    }
                    else
                    {
                        restoreIgnoredSet_.Add(path);

                        DebugOutput("AddAsset(Restore) " + path + ", ignored, leaf " + isLeafBundle + " -> " + isLeafAsset);
                    }
                }
            }
            else
            {
                restoreIgnoredSet_.Add(path);

                DebugOutput("AddAsset(Restore) " + path + ", ignored, tag '" + oldTag + "' -> '" + tag + "'");
            }
        }

        private void AddAllScenes()
        {
            try
            {
                for (int i = 0; i < args_.scenesInBuild.Length; ++i)
                {
                    AddScene(args_.scenesInBuild[i]);

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder",
                        "Add scene " + (i + 1) + "/" + args_.scenesInBuild.Length,
                        MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, args_.scenesInBuild.Length));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }
        }

        private void AddAllGroups()
        {
            try
            {
                for (int i = 0; i < groupList_.Count; ++i)
                {
                    string groupId = groupList_[i].first;
                    string[] assets = groupList_[i].second.ToArray();
                    GroupMode mode = groupList_[i].third;

                    if (mode == GroupMode.Standalone)
                    {
                        string tag = "[Standalone]" + groupId;

                        AddGroupStandalone(assets, tag, i);
                    }
                    else if (mode == GroupMode.Exclusive)
                    {
                        string tag = "[Exclusive]" + groupId;

                        AddGroupExclusive(assets, tag, i);
                    }

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder", "Add group assets",
                        MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, groupList_.Count));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }
        }

        private bool BuildAssetBundles(BundleDatabase newBd, BuildResultData buildResultData)
        {
            int denseBundleId = bd_.currentBundleId;

            Dictionary<int, int> denseBundleIdMap = new Dictionary<int, int>();
            {
                foreach (int bundleId in bundleIdToAssetSet_.Keys.OrderBy(x => x))
                {
                    if (bundleId < bd_.currentBundleId)
                    {
                        denseBundleIdMap.Add(bundleId, bundleId);
                    }
                    else
                    {
                        denseBundleIdMap.Add(bundleId, denseBundleId);

                        DebugOutput("BundleId " + bundleId + " -> " + denseBundleId);

                        ++denseBundleId;
                    }
                }
            }

            newBd.prefix = args_.prefix;
            newBd.currentBundleId = denseBundleId;

            for (int i = 0; i < args_.scenesInBuild.Length; ++i)
            {
                newBd.AddSceneAsset(args_.scenesInBuild[i]);
            }

            Dictionary<string, string> assetPathToBundleName = new Dictionary<string, string>();
            Dictionary<string, string> bundleNameToTag = new Dictionary<string, string>();
            Dictionary<string, int> bundleNameToBundleId = new Dictionary<string, int>();
            HashSet<string> sceneBundleSet = new HashSet<string>();

            try
            {
                int vi = 0;

                foreach (KeyValuePair<string, int> pair in assetPathToBundleId_)
                {
                    if (IsExcludedAsset(pair.Key))
                    {
                        DebugOutput("SkipAsset " + pair.Key + ", excluded asset");
                        continue;
                    }
                    else if (IsEditorOnlyAsset(pair.Key))
                    {
                        DebugOutput("SkipAsset " + pair.Key + ", editor-only asset");
                        continue;
                    }

                    int bundleId = denseBundleIdMap[pair.Value];
                    string tag = GetBundleTag(pair.Value);

                    string p = string.IsNullOrEmpty(args_.prefix) ? "" : args_.prefix + "_";
                    string p2 = bundleId.ToString("x");

                    string bundleName;

                    if (IsSceneBundle(pair.Value))
                    {
                        bundleName = p + "scene_" + p2 + Setting.bundleFileExtName;
                        sceneBundleSet.Add(bundleName);
                    }
                    else
                    {
                        bundleName = p + "bundle_" + p2 + Setting.bundleFileExtName;
                    }

                    assetPathToBundleName.Add(pair.Key, bundleName);

                    if (!string.IsNullOrEmpty(tag))
                    {
                        bundleNameToTag[bundleName] = tag;
                    }

                    bundleNameToBundleId[bundleName] = bundleId;

                    newBd.AddAsset(pair.Key, bundleId);

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder", "Prepare to build asset bundles",
                        MathUtility.CalculateProgress(0.0f, 1.0f, vi + 1, assetPathToBundleId_.Count));

                    ++vi;
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }

            foreach (var p in bundleNameToBundleId)
            {
                DebugOutput("BundleName " + p.Value + " : " + p.Key);
            }

            foreach (int bundleId in bundleIdToAssetSet_.Keys)
            {
                string tag = GetBundleTag(bundleId);

                if (!string.IsNullOrEmpty(tag))
                {
                    newBd.SetBundleTag(denseBundleIdMap[bundleId], tag);
                }
            }

            AssetBundleBuild[] builds = MakeBuildingMap(assetPathToBundleName, bd_.GetBuild());

            BuildAssetBundleOptions options = BuildAssetBundleOptions.None;

            if (args_.compressAssetBundles)
            {
                options = BuildAssetBundleOptions.ChunkBasedCompression;
            }
            else
            {
                options = BuildAssetBundleOptions.UncompressedAssetBundle;
            }

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                GetVersionPath(), builds, options, args_.buildTarget);

            if (manifest == null)
            {
                Debug.LogError("Failed to build asset bundles");
                return false;
            }

            newBd.SetBuild(builds);

            foreach (string bundle in manifest.GetAllAssetBundles().OrderBy(n => bundleNameToBundleId[n]))
            {
                string path = Path.Combine(GetVersionPath(), bundle);

                byte[] buffer = File.ReadAllBytes(path);

                CreateSignatureFile(path, buffer);

                string hash = VerifyUtility.HashToString(MD5.Create().ComputeHash(buffer));

                buildResultData.AddBundle(bundle, sceneBundleSet.Contains(bundle));
                buildResultData.SetBundleHash(bundle, hash);

                if (bundleNameToTag.ContainsKey(bundle))
                {
                    buildResultData.SetBundleTag(bundle, bundleNameToTag[bundle]);
                }

                newBd.SetBundleHash(bundle, hash);

                DebugOutput("BundleHash " + bundle + " : " + hash);
            }

            foreach (int bundleId in GetLeafBundles())
            {
                newBd.SetLeafBundle(bundleId);
            }

            string[] oldList = bd_.GetBundles().Keys.ToArray();

            foreach (string bundle in oldList)
            {
                if (!newBd.GetBundles().ContainsKey(bundle))
                {
                    File.Delete(Path.Combine(GetVersionPath(), bundle));
                }
            }

            foreach (AssetBundleBuild build in builds)
            {
                buildResultData.SetBundleDependencies(build.assetBundleName,
                    manifest.GetDirectDependencies(build.assetBundleName));

                buildResultData.SetAssetsInBundle(build.assetBundleName, build.assetNames);
            }

            return true;
        }

        private bool BuildPackages(BundleDatabase newBd, BuildResultData buildResultData)
        {
            var groupIdToBundleNameList = new Dictionary<string, List<string>>();

            foreach (string bundleName in buildResultData.GetAllBundles())
            {
                string tag = buildResultData.GetBundleTag(bundleName);

                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                if (!tag.StartsWith("[Standalone]") && !tag.StartsWith("[Exclusive]"))
                {
                    continue;
                }

                string groupId = tag.Substring(tag.IndexOf(']') + 1);

                List<string> bundleNameList;

                if (!groupIdToBundleNameList.TryGetValue(groupId, out bundleNameList))
                {
                    bundleNameList = new List<string>();
                    groupIdToBundleNameList.Add(groupId, bundleNameList);
                }

                bundleNameList.Add(bundleName);
            }

            var packageList = new List<string[]>();

            foreach (string[] groupIds in packageList_)
            {
                List<string> bundleNameList = new List<string>();

                foreach (string groupId in groupIds)
                {
                    bundleNameList.AddRange(DataUtil.GetValueOrDefault(groupIdToBundleNameList, groupId));
                }

                packageList.Add(bundleNameList.ToArray());
            }

            foreach (string[] bundleNames in packageList)
            {
                foreach (string bundleName in bundleNames)
                {
                    int packageId = GetPackageFromBundleName(bundleName);

                    if (packageId == 0)
                    {
                        packageId = bd_.GetBundlePackageId(bundleName);

                        if (packageId > 0)
                        {
                            if (buildResultData.GetBundleHash(bundleName) == bd_.GetBundleHash(bundleName))
                            {
                                MoveBundleToPackage(bundleName, packageId);

                                DebugOutput("AddBundle(Restore) " + bundleName + ", unchanged, package " + packageId);
                            }
                            else
                            {
                                DebugOutput("AddBundle(Restore) " + bundleName + ", ignored");
                            }
                        }
                    }
                }
            }

            foreach (string[] bundleNames in packageList)
            {
                List<int> packageIdList = new List<int>();

                foreach (string bundleName in bundleNames)
                {
                    string tag = buildResultData.GetBundleTag(bundleName);

                    int packageId = GetPackageFromBundleName(bundleName);

                    if (packageId > 0)
                    {
                        DebugOutput("AddBundle " + bundleName + ", already exists, tag '" + tag + "', package " + packageId);
                    }

                    if (packageId == 0)
                    {
                        packageId = NewPackageId();
                        MoveBundleToPackage(bundleName, packageId);

                        DebugOutput("AddBundle " + bundleName + ", new, tag '" + tag + "', package " + packageId);
                    }

                    packageIdList.Add(packageId);
                }

                TryMergePackages(packageIdList);
            }

            int densePackageId = bd_.currentPackageId;

            Dictionary<int, int> densePackageIdMap = new Dictionary<int, int>();
            {
                foreach (int packageId in packageIdToBundleSet_.Keys.OrderBy(x => x))
                {
                    if (packageId < bd_.currentPackageId)
                    {
                        densePackageIdMap.Add(packageId, packageId);
                    }
                    else
                    {
                        densePackageIdMap.Add(packageId, densePackageId);

                        DebugOutput("PackageId " + packageId + " -> " + densePackageId);

                        ++densePackageId;
                    }
                }
            }

            newBd.currentPackageId = densePackageId;

            try
            {
                int vi = 0;

                foreach (KeyValuePair<int, HashSet<string>> pair in packageIdToBundleSet_)
                {
                    int packageId = densePackageIdMap[pair.Key];

                    string p = string.IsNullOrEmpty(args_.prefix) ? "" : args_.prefix + "_";
                    string p2 = packageId.ToString("x");

                    string packageName = p + "package_" + p2 + Setting.bundleFileExtName;

                    DebugOutput("PackageName " + packageId + " : " + packageName);

                    buildResultData.AddPackage(packageName);

                    string packageFilePath = Path.Combine(GetVersionPath(), packageName);

                    using (FileStream stream = new FileStream(packageFilePath, FileMode.Create))
                    {
                        long offset = 0;

                        foreach (string bundleName in pair.Value.OrderBy(x => x))
                        {
                            byte[] data = File.ReadAllBytes(Path.Combine(GetVersionPath(), bundleName));

                            stream.Write(data, 0, data.Length);

                            newBd.SetBundlePackageId(bundleName, packageId);
                            newBd.SetBundlePackInfo(bundleName, packageName, offset, data.Length);

                            buildResultData.SetBundlePackInfo(bundleName, packageName, offset, data.Length);

                            offset += data.Length;

                            File.Delete(Path.Combine(GetVersionPath(), bundleName));
                            File.Delete(Path.Combine(GetVersionPath(), bundleName + Setting.signFileExtName));
                        }

                        stream.Close();
                    }

                    byte[] buffer = File.ReadAllBytes(packageFilePath);

                    string hash = VerifyUtility.HashToString(MD5.Create().ComputeHash(buffer));

                    buildResultData.SetPackageHash(packageName, hash);

                    CreateSignatureFile(packageFilePath, buffer);

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder", "Build packages",
                        MathUtility.CalculateProgress(0.0f, 1.0f, vi + 1, packageIdToBundleSet_.Count));

                    ++vi;
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }

            return true;
        }

        private void CreateSignatureFile(string sourceFile, byte[] sourceFileData)
        {
            using (FileStream stream = new FileStream(sourceFile + Setting.signFileExtName, FileMode.CreateNew))
            {
                Serialization.WriteToStream(stream, sourceFileData.Length);

                for (int i = 0; i < sourceFileData.Length; i += Setting.chunkSize)
                {
                    int length = Math.Min(sourceFileData.Length - i, Setting.chunkSize);

                    uint hash = XXHash32.CalculateHash(DataUtil.SubArray(sourceFileData, i, length), length);

                    Serialization.WriteToStream(stream, hash);
                }

                stream.Close();
            }
        }

        private void AddScene(string sceneAsset)
        {
            int sceneBundleId = AddAssetNonExclusive(sceneAsset, "[Scene]");

            AddSceneBundle(sceneBundleId);

            string[] toplevelAssets = GetDependencies(sceneAsset);

            for (int i = 0; i < toplevelAssets.Length; ++i)
            {
                if (IsLightingDataAsset(toplevelAssets[i]))
                {
                    AddGroupNonExclusive(new string[] { toplevelAssets[i] }, sceneBundleId, "[LightingData]");
                }
                else
                {
                    AddGroupNonExclusive(new string[] { toplevelAssets[i] }, sceneBundleId);
                }
            }
        }

        private void AddGroupStandalone(string[] assets, string tag, int order)
        {
            List<int> bundleList = new List<int>();
            HashSet<int> bundleSet = new HashSet<int>();

            List<string> assetList = assets.ToList();
            HashSet<string> assetSet = new HashSet<string>(assets);

            int oldBundleId = 0;

            for (int i = 0; i < assetList.Count; ++i)
            {
                string path = assetList[i];

                Debug.Assert(!IsSceneAsset(path));

                int bundleId = AddAssetStandalone(path, tag, order, ref oldBundleId);

                if (bundleSet.Add(bundleId))
                {
                    bundleList.Add(bundleId);
                }

                foreach (string p in GetDependencies(path))
                {
                    if (assetSet.Add(p))
                    {
                        assetList.Add(p);
                    }
                }
            }

            if (oldBundleId > 0)
            {
                int index = bundleList.IndexOf(oldBundleId);

                Debug.Assert(index >= 0);

                if (index > 0)
                {
                    bundleList.RemoveAt(index);
                    bundleList.Insert(0, oldBundleId);
                }
            }

            for (int i = 1; i < bundleList.Count; ++i)
            {
                MergeBundle(bundleList[i], bundleList[0]);
            }
        }

        private int AddAssetStandalone(string path, string tag, int order, ref int oldBundleId)
        {
            int bundleId = GetBundleFromAsset(path);

            if (bundleId > 0)
            {
                string tag2 = GetBundleTag(bundleId);
                int order2 = GetBundleOrder(bundleId);

                if (order2 < order)
                {
                    throw new System.Exception("Conflicting bundle assignments: " + path + ", " + tag2 + " -> " + tag);
                }

                if (tag2 == tag)
                {
                    Debug.Assert(oldBundleId == 0 || oldBundleId == bundleId);

                    if (order2 == int.MaxValue)
                    {
                        SetBundleOrder(bundleId, order);
                        order2 = order;
                    }

                    oldBundleId = bundleId;

                    DebugOutput("AddAsset[Standalone] " + path + ", already exists, tag '" + tag2 + "', order " + order2 + ", bundle " + bundleId);
                }
                else
                {
                    bundleId = 0;
                }
            }

            if (bundleId == 0)
            {
                bundleId = NewBundleId();
                MoveAssetToBundle(path, bundleId);

                SetBundleTag(bundleId, tag);
                SetBundleOrder(bundleId, order);

                DebugOutput("AddAsset[Standalone] " + path + ", new, tag '" + tag + "', order " + order + ", bundle " + bundleId);
            }

            return bundleId;
        }

        private void AddGroupExclusive(string[] assets, string tag, int order)
        {
            HashSet<int> nonleafBundles = new HashSet<int>();
            HashSet<int> leafBundles = new HashSet<int>();

            List<string> assetList = assets.ToList();
            HashSet<string> assetSet = new HashSet<string>(assets);

            for (int i = 0; i < assetList.Count; ++i)
            {
                string path = assetList[i];

                Debug.Assert(!IsSceneAsset(path));

                int bundleId = AddAssetExclusive(path, tag, order);

                string[] deps = GetDependencies(path);

                if (deps.Length > 0)
                {
                    nonleafBundles.Add(bundleId);

                    foreach (string p in deps)
                    {
                        int bundleId2 = AddAssetExclusive(p, tag, order);

                        AddRelationship(bundleId, bundleId2);

                        if (assetSet.Add(p))
                        {
                            assetList.Add(p);
                        }
                    }
                }
                else
                {
                    leafBundles.Add(bundleId);
                }
            }

            TryMergeBundles(leafBundles.ToList());
            TryMergeBundles(nonleafBundles.ToList());
        }

        private int AddAssetExclusive(string path, string tag, int order)
        {
            int bundleId = GetBundleFromAsset(path);

            if (bundleId > 0)
            {
                string tag2 = GetBundleTag(bundleId);
                int order2 = GetBundleOrder(bundleId);

                if (order2 == int.MaxValue)
                {
                    SetBundleOrder(bundleId, order);
                    order2 = order;
                }

                DebugOutput("AddAsset[Exclusive] " + path + ", already exists, tag '" + tag2 + "', order " + order2 + ", bundle " + bundleId);
            }

            if (bundleId == 0)
            {
                bundleId = NewBundleId();
                MoveAssetToBundle(path, bundleId);

                SetBundleTag(bundleId, tag);
                SetBundleOrder(bundleId, order);

                DebugOutput("AddAsset[Exclusive] " + path + ", new, tag '" + tag + "', order " + order + ", bundle " + bundleId);
            }

            return bundleId;
        }

        private void AddGroupNonExclusive(string[] assets, int rootBundleId = 0, string tag = "")
        {
            HashSet<int> nonleafBundles = new HashSet<int>();
            HashSet<int> leafBundles = new HashSet<int>();

            List<string> assetList = assets.ToList();
            HashSet<string> assetSet = new HashSet<string>(assets);

            for (int i = 0; i < assetList.Count; ++i)
            {
                string path = assetList[i];

                Debug.Assert(!IsSceneAsset(path));

                int bundleId = AddAssetNonExclusive(path, tag);

                if (i < assets.Length && rootBundleId > 0)
                {
                    AddRelationship(rootBundleId, bundleId);
                }

                string[] deps = GetDependencies(path);

                if (deps.Length > 0)
                {
                    nonleafBundles.Add(bundleId);

                    foreach (string p in deps)
                    {
                        int bundleId2 = AddAssetNonExclusive(p, tag);

                        AddRelationship(bundleId, bundleId2);

                        if (assetSet.Add(p))
                        {
                            assetList.Add(p);
                        }
                    }
                }
                else
                {
                    leafBundles.Add(bundleId);
                }
            }

            TryMergeBundles(leafBundles.ToList());
            TryMergeBundles(nonleafBundles.ToList());
        }

        private int AddAssetNonExclusive(string path, string tag = "")
        {
            int bundleId = GetBundleFromAsset(path);

            if (bundleId > 0)
            {
                DebugOutput("AddAsset[NonExclusive] " + path + ", already exists, tag '" + tag + "', bundle " + bundleId);
            }

            if (bundleId == 0)
            {
                bundleId = NewBundleId();
                MoveAssetToBundle(path, bundleId);

                if (!string.IsNullOrEmpty(tag))
                {
                    SetBundleTag(bundleId, tag);
                }

                DebugOutput("AddAsset[NonExclusive] " + path + ", new, tag '" + tag + "', bundle " + bundleId);
            }

            return bundleId;
        }

        // Add a relationship: left bundle depends on right bundle.
        private void AddRelationship(int leftBundleId, int rightBundleId)
        {
            if (leftBundleId != rightBundleId)
            {
                relationship_.Add(DataUtil.MakePair(leftBundleId, rightBundleId));
            }
        }

        private void RedirectRelationship(int oldBundleId, int newBundleId)
        {
            Debug.Assert(oldBundleId != newBundleId);

            List<Pair<int, int>> relationship = new List<Pair<int, int>>();
            HashSet<Pair<int, int>> set = new HashSet<Pair<int, int>>();

            foreach (var p in relationship_)
            {
                if (p.first == oldBundleId)
                {
                    p.first = newBundleId;
                }

                if (p.second == oldBundleId)
                {
                    p.second = newBundleId;
                }

                if (p.first == p.second)
                {
                    continue;
                }

                if (set.Add(p))
                {
                    relationship.Add(p);
                }
            }

            relationship_ = relationship;
        }

        private Pair<int, int> GetRelationships(int bundleId, List<Pair<int, int>> relationship)
        {
            int lower = 0;
            int upper = relationship.Count;
            int index = -1;

            while (lower < upper)
            {
                int mid = lower + (upper - lower) / 2;

                if (relationship[mid].first == bundleId)
                {
                    index = mid;
                    break;
                }

                if (relationship[mid].first < bundleId)
                {
                    lower = mid + 1;
                }
                else
                {
                    upper = mid;
                }
            }

            if (index >= 0)
            {
                lower = index;
                upper = index + 1;

                while (lower > 0 && relationship[lower - 1].first == bundleId)
                {
                    --lower;
                }

                while (upper < relationship.Count && relationship[upper].first == bundleId)
                {
                    ++upper;
                }

                return DataUtil.MakePair(lower, upper);
            }

            return DataUtil.MakePair(-1, -1);
        }

        private void MergeDependentBundles(List<int> rootBundleIds)
        {
            try
            {
                for (int i = 0; i < rootBundleIds.Count; ++i)
                {
                    List<Pair<int, int>> relationship = relationship_.OrderBy(p => p.first).ToList();

                    List<int> list = new List<int>();
                    HashSet<int> set = new HashSet<int>();

                    list.Add(rootBundleIds[i]);

                    for (int j = 0; j < list.Count; ++j)
                    {
                        Pair<int, int> p = GetRelationships(list[j], relationship);

                        for (int k = p.first; k < p.second; ++k)
                        {
                            int bundleId = relationship[k].second;

                            Debug.Assert(bundleId != list[0]);

                            if (IsSceneBundle(bundleId))
                            {
                                continue;
                            }

                            if (set.Add(bundleId))
                            {
                                list.Add(bundleId);
                            }
                        }
                    }

                    list.RemoveAt(0);

                    HashSet<int> leafBundleSet = GetLeafBundles();

                    List<int> leafBundles = new List<int>();
                    List<int> nonleafBundles = new List<int>();

                    foreach (int bundleId in list)
                    {
                        if (leafBundleSet.Contains(bundleId))
                        {
                            leafBundles.Add(bundleId);
                        }
                        else
                        {
                            nonleafBundles.Add(bundleId);
                        }
                    }

                    TryMergeBundles(leafBundles);
                    TryMergeBundles(nonleafBundles);

                    float progress = MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, rootBundleIds.Count);

                    EditorUtility.DisplayProgressBar("Lychee Bundle Builder",
                        "Merge bundles", progress);
                }

                EditorUtility.ClearProgressBar();
            }
            catch (Exception)
            {
                EditorUtility.ClearProgressBar();
                throw;
            }
        }

        private HashSet<int> GetLeafBundles()
        {
            HashSet<int> set = new HashSet<int>();

            foreach (var p in relationship_)
            {
                if (IsSceneBundle(p.second))
                {
                    continue;
                }

                set.Add(p.second);
            }

            foreach (var p in relationship_)
            {
                set.Remove(p.first);
            }

            return set;
        }

        private void TryMergeBundles(List<int> bundles)
        {
            long bundleSizeHint = (long)(args_.bundleSizeHint * 1024.0f * 1024.0f);

            List<Pair<int, long>> newBundleList = new List<Pair<int, long>>();

            foreach (int newBundleId in bundles.FindAll(p => p >= bd_.currentBundleId).Distinct())
            {
                newBundleList.Add(DataUtil.MakePair(newBundleId, EstimateBundleSize(newBundleId)));
            }

            if (newBundleList.Count == 0)
            {
                return;
            }

            // Merge new bundles into old bundles.
            if (true)
            {
                List<Pair<int, long>> bundleList = new List<Pair<int, long>>();
                List<Pair<int, long>> oldBundleList = new List<Pair<int, long>>();

                foreach (int oldBundleId in bundles.FindAll(p => p < bd_.currentBundleId).Distinct())
                {
                    oldBundleList.Add(DataUtil.MakePair(oldBundleId, EstimateBundleSize(oldBundleId)));
                }

                SortBundleListByTagAndSize(newBundleList);
                SortBundleListByTagAndSize(oldBundleList);

                while (newBundleList.Count > 0 && oldBundleList.Count > 0)
                {
                    string tag1 = GetBundleTag(newBundleList[0].first);
                    string tag2 = GetBundleTag(oldBundleList[0].first);

                    int n = CompareTag(tag1, tag2);

                    if (n < 0)
                    {
                        bundleList.Add(newBundleList[0]);
                        newBundleList.RemoveAt(0);
                    }
                    else if (n > 0)
                    {
                        oldBundleList.RemoveAt(0);
                    }
                    else
                    {
                        if (newBundleList[0].second + oldBundleList[0].second <= bundleSizeHint)
                        {
                            MergeBundle(newBundleList[0].first, oldBundleList[0].first);
                            oldBundleList[0].second = EstimateBundleSize(oldBundleList[0].first);

                            newBundleList.RemoveAt(0);
                        }
                        else
                        {
                            oldBundleList.RemoveAt(0);
                        }
                    }
                }

                newBundleList.AddRange(bundleList);
            }

            SortBundleListByTagAndSize(newBundleList);

            // Merge new bundles.
            while (newBundleList.Count > 1)
            {
                string tag1 = GetBundleTag(newBundleList[0].first);
                string tag2 = GetBundleTag(newBundleList[1].first);

                int n = CompareTag(tag1, tag2);

                Debug.Assert(n <= 0);

                if (n == 0)
                {
                    if (newBundleList[0].second + newBundleList[1].second <= bundleSizeHint)
                    {
                        MergeBundle(newBundleList[0].first, newBundleList[1].first);
                        newBundleList[1].second = EstimateBundleSize(newBundleList[1].first);
                    }
                }

                newBundleList.RemoveAt(0);
            }
        }

        private void SortBundleListByTagAndSize(List<Pair<int, long>> bundleList)
        {
            bundleList.Sort((bundle1, bundle2) =>
            {
                int n = CompareTag(GetBundleTag(bundle1.first), GetBundleTag(bundle2.first));

                if (n != 0)
                {
                    return n;
                }
                else
                {
                    return bundle1.second.CompareTo(bundle2.second);
                }
            });
        }

        private void MergeBundle(int sourceBundleId, int targetBundleId)
        {
            Debug.Assert(sourceBundleId != targetBundleId);
            Debug.Assert(sourceBundleId >= bd_.currentBundleId);
            Debug.Assert(CompareTag(GetBundleTag(sourceBundleId), GetBundleTag(targetBundleId)) == 0);
            Debug.Assert(GetBundleOrder(sourceBundleId) == GetBundleOrder(targetBundleId));

            HashSet<string> assets;

            if (bundleIdToAssetSet_.TryGetValue(sourceBundleId, out assets))
            {
                foreach (string path in assets.ToList())
                {
                    MoveAssetToBundle(path, targetBundleId);
                }

                RedirectRelationship(sourceBundleId, targetBundleId);
            }

            DebugOutput("MergeBundle " + sourceBundleId + " -> " + targetBundleId);
        }

        private void MoveAssetToBundle(string path, int bundleId)
        {
            int oldBundleId = GetBundleFromAsset(path);

            Debug.Assert(oldBundleId != bundleId);

            if (oldBundleId > 0)
            {
                bundleIdToAssetSet_[oldBundleId].Remove(path);

                if (bundleIdToAssetSet_[oldBundleId].Count == 0)
                {
                    bundleIdToAssetSet_.Remove(oldBundleId);
                    bundleIdToTag_.Remove(oldBundleId);
                    bundleIdToOrder_.Remove(oldBundleId);
                }

                bundleIdToBundleSize_.Remove(oldBundleId);
            }

            HashSet<string> assets;

            if (!bundleIdToAssetSet_.TryGetValue(bundleId, out assets))
            {
                assets = new HashSet<string>();
                bundleIdToAssetSet_.Add(bundleId, assets);
            }

            assets.Add(path);

            assetPathToBundleId_[path] = bundleId;

            bundleIdToBundleSize_.Remove(bundleId);
        }

        private int GetBundleFromAsset(string path)
        {
            return DataUtil.GetValueOrDefault(assetPathToBundleId_, path, 0);
        }

        private long EstimateBundleSize(int bundleId)
        {
            long size = 0;

            if (bundleIdToBundleSize_.TryGetValue(bundleId, out size))
            {
                return size;
            }

            HashSet<string> assetSet;

            if (bundleIdToAssetSet_.TryGetValue(bundleId, out assetSet))
            {
                foreach (string path in assetSet)
                {
                    size += GetAssetSize(path);
                }
            }

            bundleIdToBundleSize_.Add(bundleId, size);

            return size;
        }

        private void SetBundleTag(int bundleId, string tag)
        {
            bundleIdToTag_.Add(bundleId, tag);
        }

        private string GetBundleTag(int bundleId)
        {
            return DataUtil.GetValueOrDefault(bundleIdToTag_, bundleId, "");
        }

        private void SetBundleOrder(int bundleId, int order)
        {
            bundleIdToOrder_.Add(bundleId, order);
        }

        private int GetBundleOrder(int bundleId)
        {
            return DataUtil.GetValueOrDefault(bundleIdToOrder_, bundleId, int.MaxValue);
        }

        private void AddSceneBundle(int bundleId)
        {
            if (sceneBundleSet_.Add(bundleId))
            {
                sceneBundleList_.Add(bundleId);
            }
        }

        private bool IsSceneBundle(int bundleId)
        {
            return sceneBundleSet_.Contains(bundleId);
        }

        private long GetAssetSize(string path, bool recursive)
        {
            if (recursive)
            {
                long size = GetAssetSize(path);

                foreach (string p in GetDependencies(path, true))
                {
                    size += GetAssetSize(p);
                }

                return size;
            }
            else
            {
                return GetAssetSize(path);
            }
        }

        private long GetAssetSize(string path)
        {
            long size;

            if (!assetSizeHint_.TryGetValue(path, out size))
            {
                size = IOUtility.GetFileSize(path);
            }

            return size;
        }

        private int NewBundleId()
        {
            return currentBundleId_++;
        }

        private void TryMergePackages(List<int> packages)
        {
            long packageSizeHint = (long)(args_.packageSizeHint * 1024.0f * 1024.0f);

            List<Pair<int, long>> newPackageList = new List<Pair<int, long>>();

            foreach (int newPackageId in packages.FindAll(p => p >= bd_.currentPackageId).Distinct())
            {
                newPackageList.Add(DataUtil.MakePair(newPackageId, EstimatePackageSize(newPackageId)));
            }

            if (newPackageList.Count == 0)
            {
                return;
            }

            // Merge new packages into old packages.
            if (true)
            {
                List<Pair<int, long>> oldPackageList = new List<Pair<int, long>>();

                foreach (int oldPackageId in packages.FindAll(p => p < bd_.currentPackageId).Distinct())
                {
                    oldPackageList.Add(DataUtil.MakePair(oldPackageId, EstimatePackageSize(oldPackageId)));
                }

                SortPackageListBySize(newPackageList);
                SortPackageListBySize(oldPackageList);

                while (newPackageList.Count > 0 && oldPackageList.Count > 0)
                {
                    if (newPackageList[0].second + oldPackageList[0].second <= packageSizeHint)
                    {
                        MergePackage(newPackageList[0].first, oldPackageList[0].first);
                        oldPackageList[0].second = EstimatePackageSize(oldPackageList[0].first);

                        newPackageList.RemoveAt(0);
                    }
                    else
                    {
                        oldPackageList.RemoveAt(0);
                    }
                }
            }

            SortPackageListBySize(newPackageList);

            // Merge new packages.
            while (newPackageList.Count > 1)
            {
                if (newPackageList[0].second + newPackageList[1].second <= packageSizeHint)
                {
                    MergePackage(newPackageList[0].first, newPackageList[1].first);
                    newPackageList[1].second = EstimatePackageSize(newPackageList[1].first);
                }

                newPackageList.RemoveAt(0);
            }
        }

        private void SortPackageListBySize(List<Pair<int, long>> packageList)
        {
            packageList.Sort((p1, p2) =>
            {
                return p1.second.CompareTo(p2.second);
            });
        }

        private long EstimatePackageSize(int packageId)
        {
            long size = 0;

            if (packageIdToPackageSize_.TryGetValue(packageId, out size))
            {
                return size;
            }

            HashSet<string> bundleSet;

            if (packageIdToBundleSet_.TryGetValue(packageId, out bundleSet))
            {
                foreach (string bundleName in bundleSet)
                {
                    size += IOUtility.GetFileSize(Path.Combine(GetVersionPath(), bundleName));
                }
            }

            packageIdToPackageSize_.Add(packageId, size);

            return size;
        }

        private void MergePackage(int sourcePackageId, int targetPackageId)
        {
            Debug.Assert(sourcePackageId != targetPackageId);
            Debug.Assert(sourcePackageId >= bd_.currentPackageId);

            HashSet<string> bundleSet;

            if (packageIdToBundleSet_.TryGetValue(sourcePackageId, out bundleSet))
            {
                foreach (string bundleName in bundleSet.ToList())
                {
                    MoveBundleToPackage(bundleName, targetPackageId);
                }
            }

            DebugOutput("MergePackage " + sourcePackageId + " -> " + targetPackageId);
        }

        private void MoveBundleToPackage(string bundleName, int packageId)
        {
            int oldPackageId = GetPackageFromBundleName(bundleName);

            Debug.Assert(oldPackageId != packageId);

            if (oldPackageId > 0)
            {
                packageIdToBundleSet_[oldPackageId].Remove(bundleName);

                if (packageIdToBundleSet_[oldPackageId].Count == 0)
                {
                    packageIdToBundleSet_.Remove(oldPackageId);
                }

                packageIdToPackageSize_.Remove(oldPackageId);
            }

            HashSet<string> bundleSet;

            if (!packageIdToBundleSet_.TryGetValue(packageId, out bundleSet))
            {
                bundleSet = new HashSet<string>();
                packageIdToBundleSet_.Add(packageId, bundleSet);
            }

            bundleSet.Add(bundleName);

            bundleNameToPackageId_[bundleName] = packageId;

            packageIdToPackageSize_.Remove(packageId);
        }

        private int GetPackageFromBundleName(string bundleName)
        {
            return DataUtil.GetValueOrDefault(bundleNameToPackageId_, bundleName, 0);
        }

        private int NewPackageId()
        {
            return currentPackageId_++;
        }

        private string GetVersionPath()
        {
            return Path.Combine(Path.Combine(args_.location,
                GetTargetDirectoryName(args_.buildTarget)), args_.version);
        }

        private string GetParentVersionPath()
        {
            return Path.Combine(Path.Combine(args_.location,
                    GetTargetDirectoryName(args_.buildTarget)), args_.parentVersion);
        }

        private void DebugOutput(string line)
        {
            if (dbgOut_ != null)
            {
                dbgOut_.WriteLine(line);
            }
        }

        private static int CompareTag(string a, string b)
        {
            if (a == null && b != null)
            {
                return -1;
            }
            else if (a != null && b == null)
            {
                return 1;
            }
            else if (a == null && b == null)
            {
                return 0;
            }
            else
            {
                return a.CompareTo(b);
            }
        }

        private static string[] GetDependencies(string path, bool recursive)
        {
            if (recursive)
            {
                List<string> list = new List<string>();
                HashSet<string> set = new HashSet<string>();

                list.Add(path);
                set.Add(AssetDatabase.AssetPathToGUID(path));

                for (int i = 0; i < list.Count; ++i)
                {
                    foreach (string p in GetDependencies(list[i]))
                    {
                        if (set.Add(AssetDatabase.AssetPathToGUID(p)))
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
                return GetDependencies(path);
            }
        }

        private static string[] GetDependencies(string path)
        {
            List<string> list = new List<string>();
            HashSet<string> set = new HashSet<string>();

            set.Add(AssetDatabase.AssetPathToGUID(path));

            foreach (string p in AssetDatabase.GetDependencies(path, false))
            {
                if (IsSceneAsset(p))
                {
                    continue;
                }

                if (set.Add(AssetDatabase.AssetPathToGUID(p)))
                {
                    list.Add(p);
                }
            }

            return list.ToArray();
        }

        private static bool IsSceneAsset(string path)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return (type == typeof(SceneAsset));
        }

        private static bool IsLightingDataAsset(string path)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return (type == typeof(LightingDataAsset));
        }

        private static bool IsEditorOnlyAsset(string path)
        {
            if (string.Compare(Path.GetFileName(path), "LightingData.asset", true) == 0)
            {
                return true;
            }

            return false;
        }

        private static bool IsExcludedAsset(string path)
        {
            if (Path.GetExtension(path).Equals(".DLL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);

            if (type == typeof(MonoScript))
            {
                return true;
            }

            return false;
        }

        private static bool AssetExists(string path)
        {
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        private static string NormalizeAssetPath(string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        private static long CalculateTextureSize(int width, int height, bool mipmapEnabled, TextureImporterFormat format)
        {
            float size = width * height * 4 * (1.0f / 4.0f);

            if (mipmapEnabled)
            {
                size = size * 1.33f;
            }

            return (long)size;
        }


        private static AssetBundleBuild[] MakeBuildingMap(Dictionary<string, string> assetPathToBundleName, List<Pair<string, List<string>>> oldBuild = null)
        {
            //bundlename 对应的 资源列表
            List<Pair<string, List<string>>> list = new List<Pair<string, List<string>>>();
            //bundlename 对应的 list中的index
            Dictionary<string, int> bundleNameToIndex = new Dictionary<string, int>();

            foreach (KeyValuePair<string, string> p in assetPathToBundleName)
            {
                Debug.Assert(!IsExcludedAsset(p.Key));
                Debug.Assert(!IsEditorOnlyAsset(p.Key));

                int index;
                //循环 路径对bundlename的列表 根据bundlename把资源路径添加到list  bundleNameToIndex保存bundlename到list的索引
                if (bundleNameToIndex.TryGetValue(p.Value, out index))
                {
                    list[index].second.Add(p.Key);
                }
                else
                {
                    list.Add(DataUtil.MakePair(p.Value, new List<string>() { p.Key }));

                    bundleNameToIndex.Add(p.Value, list.Count - 1);
                }
            }

            if (oldBuild != null)
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    int n = oldBuild.FindIndex(x => x.first == list[i].first);

                    if (n >= 0)
                    {
                        list[i].second = list[i].second.OrderBy(name =>
                        {
                            string guid = AssetDatabase.AssetPathToGUID(name);

                            int m = oldBuild[n].second.FindIndex(x => x == guid);

                            if (m >= 0)
                            {
                                return m;
                            }
                            else
                            {
                                return oldBuild[n].second.Count + list[i].second.IndexOf(name);
                            }
                        }).ToList();
                    }
                }

                list = list.OrderBy(p =>
                {
                    int n = oldBuild.FindIndex(x => x.first == p.first);

                    if (n >= 0)
                    {
                        return n;
                    }
                    else
                    {
                        return oldBuild.Count + list.FindIndex(y => y.first == p.first);
                    }
                }).ToList();
            }

            //转换为unity支持的bundle格式
            AssetBundleBuild[] builds = new AssetBundleBuild[list.Count];

            for (int i = 0; i < list.Count; ++i)
            {
                builds[i].assetBundleName = list[i].first;
                builds[i].assetNames = list[i].second.ToArray();
            }

            return builds;
        }

        private static string GetAssetSizeHintDirectory(BuildTarget buildTarget)
        {
            return Path.Combine(Path.Combine("LycheeBundleBuilder", "AssetSizeHint"),
                GetTargetDirectoryName(buildTarget));
        }

        private static string GetDebugOutputDirectory(BuildTarget buildTarget)
        {
            return Path.Combine(Path.Combine("LycheeBundleBuilder", "DebugOutput"),
                GetTargetDirectoryName(buildTarget));
        }

        private static string GetTargetDirectoryName(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                case BuildTarget.StandaloneOSX:
                    return "MacOS";
                case BuildTarget.StandaloneLinux:
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneLinuxUniversal:
                    return "Linux";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.WSAPlayer:
                    return "WindowsStore";
                case BuildTarget.Tizen:
                    return "Tizen";
                case BuildTarget.PSP2:
                    return "PSP2";
                case BuildTarget.PS4:
                    return "PS4";
                case BuildTarget.XboxOne:
                    return "XboxOne";
                case BuildTarget.SamsungTV:
                    return "SamsungTV";
                case BuildTarget.N3DS:
                    return "Nintendo3DS";
                case BuildTarget.WiiU:
                    return "WiiU";
                case BuildTarget.tvOS:
                    return "tvOS";
                case BuildTarget.Switch:
                    return "Switch";
            }

            throw new Exception("Unknown build target");
        }

        private static string GetTexPlatformString(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux:
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneLinuxUniversal:
                    return "Standalone";
                case BuildTarget.iOS:
                    return "iPhone";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.WSAPlayer:
                    return "Windows Store Apps";
                case BuildTarget.Tizen:
                    return "Tizen";
                case BuildTarget.PSP2:
                    return "PSP2";
                case BuildTarget.PS4:
                    return "PS4";
                case BuildTarget.XboxOne:
                    return "XboxOne";
                case BuildTarget.SamsungTV:
                    return "Samsung TV";
                case BuildTarget.N3DS:
                    return "Nintendo 3DS";
                case BuildTarget.WiiU:
                    return "WiiU";
                case BuildTarget.tvOS:
                    return "tvOS";
                case BuildTarget.Switch:
                    return "Switch";
            }

            return "";
        }

        private static void ClearDirectory(string path)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(path);

            foreach (FileSystemInfo info in dirInfo.GetFileSystemInfos())
            {
                if (info is FileInfo)
                {
                    FileInfo fi = info as FileInfo;
                    fi.Delete();
                }

                if (info is DirectoryInfo)
                {
                    DirectoryInfo di = info as DirectoryInfo;
                    di.Delete(true);
                }
            }
        }

    }

}