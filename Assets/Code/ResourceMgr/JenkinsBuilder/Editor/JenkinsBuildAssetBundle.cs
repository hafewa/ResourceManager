using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using ResourceMoudle;

public class JenkinsBuildAssetBundle
{
    private static string m_AbPath = Application.dataPath + "/../../TmpAssetBundle";
    private static string m_AbVerPath = m_AbPath + "/version.txt";
    private static string m_AbComparePath = m_AbPath + "/CompareResult";
    private static string m_CodelistPath = m_AbPath + "/_codelist.txt";
    private static string m_AbStreamAssetPath = Application.dataPath + "/StreamingAssets/bundles";
    private static string m_CurrAbVersion = "";
    private static string m_LastAbVersion = "";
    private static string m_AppVersion = "";
    private static string m_AppURL = "http://10.12.20.117/package";
    private static string m_ResURL = "http://10.12.20.117/res";
    private static VersionInfo m_VersionInfo;
    private static string m_SvnVersion = "";
    //是否进行大版本更新
    private static bool m_AppUpdate = true;

    [MenuItem("Jenkins/BuildAsset/BuildBytesPackage")]
    public static void BuildBytesPackage()
    {
        Debug.Log("start build data package to streamasset");
      //  PackageManager.CreatePackage("Assets/Res/Data", "Assets/Lua", "Assets/StreamingAssets/bundles");
        Debug.Log("finish build data package to streamasset ok");
    }
    [MenuItem("Jenkins/BuildAsset/BuildAssetBundlesIOS")]
    public static bool BuildAssetBundleToStreamAssets_IOS()
    {
        Debug.Log("start build ios assetbundles to streamasset");
        bool result = true;
        ReadSvnVersionInfo(BuildTarget.iOS);
        InitAssetBundleVersion(BuildTarget.iOS);
        ClearAssetBundles();
        result = BuildAssetBundle(BuildTarget.iOS, m_AbPath);
        if (!result) { return result; }
        //c#代码版本号生成
        BuildCodeSign(BuildTarget.iOS);
        //更新版本号文件
        UpdateAssetBundleVersion(BuildTarget.iOS);
        CopyAssetBundles(BuildTarget.iOS);
        Debug.Log("finish build ios assetbundles to streamasset ok");
        return result;
    }
    [MenuItem("Jenkins/BuildAsset/BuildAssetBundlesAndroird")]
    public static bool BuildAssetBundleToStreamAssets_Android()
    {
        Debug.Log("start build android assetbundles to streamasset");
        bool result = true;
        ReadSvnVersionInfo(BuildTarget.Android);
        InitAssetBundleVersion(BuildTarget.Android);
        ClearAssetBundles();
        result=BuildAssetBundle(BuildTarget.Android, m_AbPath);
        if (!result) { return result; }
        //c#代码版本号生成
        BuildCodeSign(BuildTarget.Android);
        //更新版本号文件
        UpdateAssetBundleVersion(BuildTarget.Android);
        CopyAssetBundles(BuildTarget.Android);
        Debug.Log("finish build android assetbundles to streamasset ok");
        return result;
    }
    [MenuItem("Jenkins/BuildAsset/BuildAssetBundlesWindows")]
    public static bool BuildAssetBundleToStreamAssets_Window()
    {
        bool result = true;
        Debug.Log("start build windows assetbundles to streamasset");
        ReadSvnVersionInfo(BuildTarget.StandaloneWindows64);
        InitAssetBundleVersion(BuildTarget.StandaloneWindows64);
        ClearAssetBundles();
        result = BuildAssetBundle(BuildTarget.StandaloneWindows64, m_AbPath);
        if (!result) { return result; }
        //c#代码版本号生成
        BuildCodeSign(BuildTarget.StandaloneWindows64);
        //更新版本号文件
        UpdateAssetBundleVersion(BuildTarget.StandaloneWindows64);
        CopyAssetBundles(BuildTarget.StandaloneWindows64);
        Debug.Log("finish build windows assetbundles to streamasset ok");
        return result;
    }
    #region 使用ConfigureBuild
    public static bool BuildAssetBundle()
    {
        Debug.Log("start build android assetbundles to streamasset");
        JenkinBuilderConfigure config = JenkinBuilderConfigure.Configure;
        if (config!=null)
        {
            BuildTarget target = config.buildTarget;
            bool result = true;
            m_SvnVersion = config.svnVersion.ToString();
            int nVer = config.resVersion;
            if (nVer == -1)
            {
                m_LastAbVersion = "-1";
                m_CurrAbVersion = "100";
            }
            else
            {
                m_LastAbVersion = nVer.ToString();
                m_CurrAbVersion = (nVer + 1).ToString();
            }
            m_AppURL = config.appURL;
            m_ResURL = config.resURL;
            m_AppUpdate = config.appUpdate;
            bool isSame = CompareCodeSign(target);
            if (config.codeVersion == -1)
            {
                m_AppVersion = "100";
            }
            else
            {
                m_AppVersion = config.codeVersion.ToString();
                if (!isSame && m_AppUpdate)
                {
                    m_AppVersion = (config.codeVersion + 1).ToString();
                }
            }
           
            ClearAssetBundles();
            result = BuildAssetBundle(target, config.bundlePath);
            if (!result) { return result; }
            UpdateAssetBundleVersion(target);
            CopyAssetBundles(target);
            Debug.Log("finish build android assetbundles to streamasset ok");
        }
        else
        {
            return false;
        }
        return true;
    }
    public static void SaveConfigure()
    {
        JenkinBuilderConfigure config = JenkinBuilderConfigure.Configure;
        config.parentResVersion = int.Parse(m_LastAbVersion);
        config.resVersion = int.Parse(m_CurrAbVersion);
        config.codeVersion = int.Parse(m_AppVersion);
        JenkinBuilderConfigure.SaveBuildConfigure();
    }
    #endregion
    public static string[] GetPlatformCodeList(BuildTarget buildTarget)
    {
        List<string> list = new List<string>();
        string[] pattern = new string[] { "*.cs", "*.dll" };
        string[] allCs = SearchFilesFilter("Assets/Code", pattern);
       // string[] allCs1 = SearchFilesFilter("Assets/CYOU", pattern);
        string[] allCs2 = SearchFilesFilter("Assets/Res", pattern);
        list.AddRange(allCs);
       // list.AddRange(allCs1);
        list.AddRange(allCs2);
        switch (buildTarget)
        {
            case BuildTarget.Android:
                string[] allpluginandroid = SearchFilesFilter("Assets/Plugins/Android", new string[]{ "*"});
                list.AddRange(allpluginandroid);
                break;
            case BuildTarget.iOS:
                string[] allpluginios = SearchFilesFilter("Assets/Plugins/iOS", new string[] { "*" });
                list.AddRange(allpluginios);
                break;
            case BuildTarget.StandaloneWindows64:
                string[] allpluginpc = SearchFilesFilter("Assets/Plugins/x86_64", new string[] { "*" });
                list.AddRange(allpluginpc);
                break;
            default:

                break;
        }
        return list.ToArray();
    }
    public static bool CompareCodeSign(BuildTarget buildTarget)
    {
        Debug.Log("start BuildCodeSign");
        string[] allfiles = GetPlatformCodeList(buildTarget);

        Dictionary<string, string> oldcodelist = new Dictionary<string, string>();
        Dictionary<string, string> newcodelist = new Dictionary<string, string>();

        string m_CodelistPath = GetCodeListPath(buildTarget);
        if (File.Exists(m_CodelistPath))
        {
            StreamReader reader = new StreamReader(m_CodelistPath, false);
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                string[] sp = line.Split(new string[] { "===" }, StringSplitOptions.None);
                string path = sp[0];
                string hash = sp[1];
                path = path.Replace("\\", "/");
                oldcodelist.Add(path, hash);
            }
            reader.Close();
            reader.Dispose();
        }

        foreach (string destFileFullPath in allfiles)
        {
            byte[] buffer = File.ReadAllBytes(destFileFullPath);
            byte[] md5hash = MD5.Create().ComputeHash(buffer);
            string hash = VerifyUtility.HashToString(md5hash);
            //uint hash = Lychee.XXHash32.CalculateHash(buffer, buffer.Length);
            string path = destFileFullPath.Replace("\\", "/");
            newcodelist.Add(path, hash);
        }
        //版本是否一致
        bool isSame = true;
        //dsadkdgsdgdsg

        if (oldcodelist.Count != newcodelist.Count)
        {
            isSame = false;
        }
        else
        {
            foreach (var pair in oldcodelist)
            {
                if (newcodelist.ContainsKey(pair.Key) && newcodelist[pair.Key] == pair.Value)
                {
                    continue;
                }
                else
                {
                    isSame = false;
                    break;
                }
            }
        }

        if (!isSame)
        {
            StreamWriter writer = new StreamWriter(m_CodelistPath, false);
            writer.Flush();
            foreach (var pair in newcodelist)
            {
                writer.Write(pair.Key + "===" + pair.Value + "\r\n", false);
            }
            writer.Close();
            writer.Dispose();
        }
        return isSame;
    }
    public static void BuildCodeSign(BuildTarget buildTarget)
    {
        bool isSame = CompareCodeSign(buildTarget);
        if (ReadVersionInfo(buildTarget))
        {
            int nVer = 0;
            if (int.TryParse(m_VersionInfo.appVersion, out nVer))
            {
                m_AppVersion = m_VersionInfo.appVersion;
            }
            else
            {
                m_AppVersion = "100";
            }
            if (m_AppVersion == "")
            {
                m_AppVersion = "100";
            }
        }
        else
        {
            m_AppVersion = "100";
        }
        if (!isSame && m_AppUpdate)
        {
            //更新app版本号
            int nVer = 0;
            if (int.TryParse(m_VersionInfo.appVersion, out nVer))
            {
                m_AppVersion = (nVer + 1).ToString();
            }
            else
            {
                m_AppVersion = "100";
            }
            Debug.Log("更新app版本号");

            SaveVersionInfo(buildTarget);
        }
        Debug.Log("BuildCodeSignok");
    }
    private static void InitAssetBundleVersion(BuildTarget buildTarget)
    {
        string verPath = GetVersionPath(buildTarget);

        //版本文件不存在
        if (!File.Exists(verPath))
        {
            m_CurrAbVersion = "100";
            m_LastAbVersion = "-1";
            m_AppVersion = "100";
            return;
        }
        
        string sVer = "";
        if (ReadVersionInfo(buildTarget))
        {
            sVer = m_VersionInfo.resVersion;
        }

        //版本号为空
        if (sVer == null || sVer.Equals(""))
        {
            m_CurrAbVersion = "100";
            m_LastAbVersion = "-1";
            m_AppVersion = "100";
            return;
        }

        //版本号无法识别
        int nVer;
        if (!int.TryParse(sVer, out nVer))
        {
            m_CurrAbVersion = "100";
            m_LastAbVersion = "-1";
            m_AppVersion = "100";
            return;
        }

        //版本资源不存在
        if (!Directory.Exists(GetBundlePath(buildTarget, nVer.ToString())))
        {
            m_CurrAbVersion = "100";
            m_LastAbVersion = "-1";
            m_AppVersion = "100";
            return;
        }

        //递增版本号
        m_LastAbVersion = nVer.ToString(); ;
        m_CurrAbVersion = (nVer + 1).ToString();
    }
    private static bool BuildAssetBundle(BuildTarget buildTarget, string location)
    {
        if (!Directory.Exists(location)) Directory.CreateDirectory(location);
        BuildArguments buildArgs = new BuildArguments();
        if (m_LastAbVersion.Equals("-1"))
        {
            buildArgs.mode = Mode.FullBuild;
        }
        else
        {
            buildArgs.mode = Mode.IncrementalBuild;
        }

        buildArgs.location = location + "/";
        buildArgs.version = m_CurrAbVersion;
        buildArgs.prefix = "ldj";
        buildArgs.parentVersion = m_LastAbVersion;
        buildArgs.buildTarget = buildTarget;
        //资源包大小上限
        buildArgs.bundleSizeHint = 8.0f;
        buildArgs.packageSizeHint = 8.0f;
        //是否压缩资源包
        buildArgs.compressAssetBundles = true;
        buildArgs.reportVersionComparision = true;

        buildArgs.customScripts = new CustomBundleStrategye[] { new CustomBundleStrategye() };
        buildArgs.scenesInBuild = new string[0];

        BundleBuilder builder = new BundleBuilder(buildArgs);
        bool success =  builder.Build(true);
       
        return success;
    }
    private static bool ReadSvnVersionInfo(BuildTarget buildTarget)
    {
        string[] args = Environment.GetCommandLineArgs();
        string path = GetSvnVersionPath(buildTarget);
        if (File.Exists(path))
        {
            string alltext = File.ReadAllText(path);
            int verNum = 0;
            if (int.TryParse(alltext, out verNum))
            {
                m_SvnVersion = alltext;
                return true;
            }
            else
            {
                Debug.LogError("svnversion 错误");
                m_SvnVersion = "";
                return true;
            }
        }
        else
        {
            File.WriteAllText(path, "");
            Debug.LogError("svnversion 错误");
            m_SvnVersion = "";
            return true;
        }
    }
    private static bool ReadVersionInfo(BuildTarget buildTarget)
    {
        string path = GetVersionPath(buildTarget);
        if (File.Exists(path))
        {
            string alltext = File.ReadAllText(path);
            try
            {
                m_VersionInfo = JsonUtility.FromJson<VersionInfo>(alltext);
                return true;
            }
            catch
            {
                if (m_VersionInfo == null)
                    m_VersionInfo = new VersionInfo();
                Debug.LogError("version 文件json解析失败");
                int verNum = 0;
                if (int.TryParse(alltext, out verNum))
                {
                    m_CurrAbVersion = alltext;
                    m_VersionInfo.resVersion = m_CurrAbVersion;
                    return true;
                }
                else
                {
                    Debug.LogError("version 文件数字解析失败");
                    return false;
                }
            }
        }
        else
        {
            if (m_VersionInfo == null)
                m_VersionInfo = new VersionInfo();
            return false;
        }
    }
    private static void SaveVersionInfo(BuildTarget buildTarget)
    {
        string path = GetVersionPath(buildTarget);
        
        if (m_VersionInfo == null)
            m_VersionInfo = new VersionInfo();
        
        m_VersionInfo.resVersion = m_CurrAbVersion;
        m_VersionInfo.appVersion = m_AppVersion;
        m_VersionInfo.resUrl = m_ResURL;
        m_VersionInfo.version = string.Format("{0}.{1}.{2}",1,m_AppVersion, m_CurrAbVersion);
        string name = GetAppName(buildTarget,m_AppVersion,m_CurrAbVersion);
        m_VersionInfo.appName = name;
        m_VersionInfo.appUrl = GetAppUpdateUrl(m_AppURL, buildTarget,name);
        string resultJson = JsonUtility.ToJson(m_VersionInfo);
        File.WriteAllText(path, resultJson);
    }
    private static void UpdateAssetBundleVersion(BuildTarget buildTarget)
    {
        //两个版本资源变化比对
        if (!m_LastAbVersion.Equals("-1"))
        {
            string parentVerPath = GetBundlePath(buildTarget, m_LastAbVersion);
            string curVerPath = GetBundlePath(buildTarget, m_CurrAbVersion);
            Comparer c = new Comparer(parentVerPath, curVerPath);
            CompareResult result = c.Compare();
            VersionCompare versionCompare = new VersionCompare
            {
                unchangedFiles = result.unchangedFiles.Length,
                newFiles = result.newFiles.Length,
                changedFiles = result.changedFiles.Length,
                sourceVersionCapacity = result.sourceVersionCapacity,
                destVersionCapacity = result.destVersionCapacity,
                downloadSize = result.downloadSize,
            };
            string resultJson = JsonUtility.ToJson(versionCompare);
           // string sResultJsonPath = GetComparePath(buildTarget) + "/compare_" + m_LastAbVersion  + "_to_" + m_CurrAbVersion + ".json";
            string sResultPath = GetComparePath(buildTarget) + "/compare_" + m_LastAbVersion + "_to_" + m_CurrAbVersion + ".text";

            if (!Directory.Exists(GetComparePath(buildTarget))) Directory.CreateDirectory(GetComparePath(buildTarget));
           // File.WriteAllText(sResultJsonPath, resultJson);
            File.WriteAllText(sResultPath, result.ToString());
        }

        //将当前版本写入文件中，用于下次版本比较
        //File.WriteAllText(GetVersionPath(buildTarget), m_CurrAbVersion.ToString());
        SaveVersionInfo(buildTarget);
        //将当前版本version.txt 写入bundle文件夹
        //File.WriteAllText(GetBundlePath(buildTarget,m_CurrAbVersion), m_CurrAbVersion.ToString());
        File.Copy(GetVersionPath(buildTarget), GetBundleVersionPath(buildTarget, m_CurrAbVersion), true);

        //删除太久的资源
        if (!m_CurrAbVersion.Equals("100"))
        {
            int nVer = int.Parse(m_CurrAbVersion);
            int nStartDelVer = nVer - 5;
            for (int i = 0; i <= 10; i++)
            {
                string path = GetBundlePath(buildTarget, nStartDelVer.ToString());
                if (Directory.Exists(path)) Directory.Delete(path, true);
                nStartDelVer--;
            }
        }
    }
    private static string GetAppName(BuildTarget buildTarget, string appVerion,string resVerion)
    {
        string sdate = DateTime.Now.ToString("yyyy_MM_dd_HH_mm");
        string app = ".apk";
        if (buildTarget == BuildTarget.iOS)
        {
            app = ".ipa";
        }
        else if (buildTarget == BuildTarget.Android)
        {
            app = ".apk";
        }
        else
        {
            app = ".exe";
        }
        return string.Format("ldj_{0}_{1}_{2}_{3}{4}", sdate,m_SvnVersion, appVerion, resVerion, app);
    }
    private static string GetAppUpdateUrl(string serverRoot,BuildTarget buildTarget,string name)
    {
        string targetDir = string.Empty;
        if (buildTarget == BuildTarget.iOS)
        {
            targetDir = "ios";
        }
        else if (buildTarget == BuildTarget.Android)
        {
            targetDir = "android";
        }
        else
        {
            targetDir = "windows";
        }
        return string.Format("{0}/{1}/{2}", serverRoot, targetDir,name);
    }
    private static string PlatformPath(BuildTarget buildTarget)
    {
        string targetDir = string.Empty;
        if (buildTarget == BuildTarget.iOS)
        {
            targetDir = "iOS";
        }
        else if (buildTarget == BuildTarget.Android)
        {
            targetDir = "Android";
        }
        else
        {
            targetDir = "Windows";
        }
        string path = string.Format("{0}/{1}", m_AbPath, targetDir);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
    private static string GetConfigurePath()
    {
        return string.Format("{0}/{1}", m_AbPath, "buildconfigure.json");
    }
    private static string GetVersionPath(BuildTarget buildTarget)
    {
        return string.Format("{0}/{1}", PlatformPath(buildTarget), "version.txt");
    }
    private static string GetSvnVersionPath(BuildTarget buildTarget)
    {
        return string.Format("{0}/{1}", PlatformPath(buildTarget), "svnversion.txt");
    }
    private static string GetCodeListPath(BuildTarget buildTarget)
    {
        return string.Format("{0}/{1}", PlatformPath(buildTarget), "_codelist.txt");
    }
    private static string GetBundlePath(BuildTarget buildTarget, string verion)
    {
        return string.Format("{0}/{1}", PlatformPath(buildTarget), verion);
    }
    private static string GetBundleVersionPath(BuildTarget buildTarget, string verion)
    {
        return string.Format("{0}/{1}/version.txt", PlatformPath(buildTarget), verion);
    }
    private static string GetComparePath(BuildTarget buildTarget)
    {
        return string.Format("{0}/{1}", PlatformPath(buildTarget), "CompareResult");
    }
    private static void ClearAssetBundles()
    {
        if (Directory.Exists(m_AbStreamAssetPath)) Directory.Delete(m_AbStreamAssetPath, true);
            Directory.CreateDirectory(m_AbStreamAssetPath);
    }
    public static void CopyResAppToSharePath()
    {
        string targetDir = string.Empty;
        BuildTarget buildTarget = JenkinBuilderConfigure.Configure.buildTarget;
        if (buildTarget == BuildTarget.iOS)
        {
            targetDir = "iOS";
        }
        else if (buildTarget == BuildTarget.Android)
        {
            targetDir = "Android";
        }
        else
        {
            targetDir = "Windows";
        }
        string version = JenkinBuilderConfigure.Configure.resVersion.ToString();
        string packageName = GetAppName(JenkinBuilderConfigure.Configure.buildTarget, JenkinBuilderConfigure.Configure.codeVersion.ToString(), JenkinBuilderConfigure.Configure.resVersion.ToString());
        string resSrc = string.Format("{0}/{1}/{2}", JenkinBuilderConfigure.Configure.bundlePath, targetDir, version);
        string resDst = string.Format("{0}/{1}/{2}", JenkinBuilderConfigure.Configure.resExportPath, targetDir, version);
        string packageSrc = string.Format("{0}/Build/{1}", JenkinBuilderConfigure.Configure.projectPath, packageName);
        string packageDst = string.Format("{0}/{1}/{2}", JenkinBuilderConfigure.Configure.packageExportPath, targetDir, packageName);
        
        CopyFiles(resSrc, resDst, null);
      
        if (File.Exists(packageSrc))
        {
            File.Copy(packageSrc, packageDst,true);
        }
    }
    private static void CopyAssetBundles(BuildTarget buildTarget)
    {
        DeleteMainfest(buildTarget);
        string srcBundlePath = GetBundlePath(buildTarget, m_CurrAbVersion);
        string dstBundlePath = m_AbStreamAssetPath;
        CopyFiles(srcBundlePath, dstBundlePath, ".signature");
    }
    private static void DeleteMainfest(BuildTarget buildTarget)
    {
        string srcBundlePath = GetBundlePath(buildTarget, m_CurrAbVersion);
        string[] files = Directory.GetFiles(srcBundlePath, "*.manifest", SearchOption.AllDirectories);
        //当前目录
        foreach (var file in files)
        {
            string path = file.Replace("\\", "/");
            File.Delete(path);
        }
    }
    private static void CopyFiles(string src, string dst,string filter)
    {
        try
        {
            if (Directory.Exists(src))
            {
                if (!Directory.Exists(dst)) Directory.CreateDirectory(dst);
                List<string> files = new List<string>();
                if (filter != null)
                {
                     files = Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly).Where(s => !s.EndsWith(filter)).ToList();
                }
                else
                {
                     files = Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly).ToList();
                }
                //当前目录
                foreach (var file in files)
                {
                    File.Copy(file, dst + "/" + Path.GetFileName(file), true);
                }
                //子目录
                foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.TopDirectoryOnly))
                {
                    CopyFiles(dir, dst + "/" + Path.GetFileName(dir), filter);
                }
            }
            else
            {
                //拷贝文件
                File.Copy(src, dst + "/" + Path.GetFileName(src), true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("copy res error : " + e.Message);
        }
    }
    private static string[] SearchFilesFilter(string src, string[] pattern)
    {
        List<string> list = new List<string>();
        try
        {
            if (Directory.Exists(src))
            {
                foreach (var pa in pattern)
                {
                    //当前目录
                    foreach (var path in Directory.GetFiles(src, pa, SearchOption.TopDirectoryOnly).Where(q => !q.EndsWith(".meta")))
                    {
                        list.Add(path);
                    }
                }
               
                //子目录
                foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.TopDirectoryOnly).Where(q => !q.EndsWith("Editor") && !q.Contains("\\..")))
                {
                    string[] childlist = SearchFilesFilter(dir, pattern);
                    list.AddRange(childlist);
                }
            }
            else
            {
                return null;
            }
            return list.ToArray();
        }
        catch (Exception e)
        {

            Debug.LogError("SearchFilesFilter error : " + e.Message);
            return null;
        }
    }
}