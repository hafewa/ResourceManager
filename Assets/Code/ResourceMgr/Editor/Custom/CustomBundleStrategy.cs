using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ResourceMoudle;
using UnityEditor;
using System.IO;
using System.Threading;
using System.Text;

public class CustomBundleStrategye : IBundleStrategy
{
    //资源组
    private List<Triple<string, string[], GroupMode>> groups_;
    private List<string[]> customFiles_;
    private int index_;
    private List<string[]> packages_;
    /// <summary>
    /// 自定义类型
    /// </summary>
    /// //根据依赖关系划分的一组资源
    private class AssetUnit
    {
        public List<string> mAssets;
        public string mGroupId;
        public GroupMode mGroupMode;

        public AssetUnit(List<string> assets, string groupId, GroupMode groupMode)
        {
            mAssets = assets;
            mGroupId = groupId;
            mGroupMode = groupMode;
        }
    }

    //所有将要打包的资源组集合
    private List<AssetUnit> mAllAssets = new List<AssetUnit>();

    //这个地方公共部分groupid不能变。否则会导致打包错误。目前公共包占用Groupid 0  ~1000
    private const int CUSTOM_SHADER_GROUPID = 0;                //COMMON SHADERS
    private const int CUSTOM_UPDATE_GROUPID = 1;                //UPDATE_UI && UI_SHADERS && UI_FONTS
    private const int CUSTOM_DEBUG_GROUPID = 900;               //DEBUG_UI

    //资源组动态ID,每个AssetUnit分配一个ID,不重复
    private int mGroupID = 1001;

    //打包时控制获取资源组的下标
    private int mAssetIndex = 0;
    //打包时资源包导出目录
    private string mAssetOutLocation;
    //打包时控制自定义文件是否添加结束
    private bool mFileBuildFinish = false;

    public void Awake()
    {
    }

    public string Name()
    {
        return "LycheeCustomScript_LDJ";
    }

    public bool HasUI()
    {
        return true;
    }

    public void DrawUI()
    {
        EditorGUILayout.LabelField("Yes you can add custom UI here !");
    }

    //找出所有文件夹全目录下的资源路径  AssetDatabase.FindAssets
    private string[] FindAssets(string filter, string[] searchInFolders)
    {
        foreach (var p in searchInFolders)
        {
            if (System.IO.Directory.Exists(p) == false)
            {
                System.IO.Directory.CreateDirectory(p);
            }
        }

        string[] guids = AssetDatabase.FindAssets(filter, searchInFolders);

        string[] paths = new string[guids.Length];
        List<string> outPaths = new List<string>();
        for (int i = 0; i < guids.Length; ++i)
        {
            string temppath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (File.Exists(temppath) && !outPaths.Contains(temppath))
            {
                outPaths.Add(temppath);
            }
        }
        return outPaths.ToArray();
    }

    //找出所有文件夹全目录下的资源路径 Directory.GetFiles
    private string[] GetFiles(string path, System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
    {
        List<string> dirs = new List<string>();
        string[] files = System.IO.Directory.GetFiles(path, "*", searchOption);

        foreach (string file in files)
        {
            if (file.EndsWith(".meta"))
                continue;
            if (file.Contains(".svn"))
                continue;
            if (file.Contains("/Editor/"))
                continue;
            if (file.Contains("/Editor"))
                continue;
            dirs.Add(file.Replace("\\", "/"));
        }
        return dirs.ToArray();
    }

    //找出目录下的子目录
    string[] GetDirectorysIn(string rootPath, System.IO.SearchOption sp = System.IO.SearchOption.TopDirectoryOnly)
    {
        List<string> outdirs = new List<string>();
        string[] dirs = System.IO.Directory.GetDirectories(rootPath, "*", sp);
        if (dirs.Length > 0)
        {
            for (int i = 0; i < dirs.Length; i++)
            {
                string childdir = dirs[i];
                childdir = childdir.Replace("\\", "/");
                if (childdir.EndsWith(".meta"))
                    continue;
                if (childdir.Contains(".svn"))
                    continue;
                if (childdir.Contains("/Editor/"))
                    continue;
                if (childdir.EndsWith("/Editor"))
                    continue;
                outdirs.Add(childdir);
            }
        }
        return outdirs.ToArray();

    }

    private string GetFileName(string path)
    {
        int index = path.LastIndexOf("/");
        return path.Substring(index + 1, path.Length - index - 1);
    }

    private string SystemPathToAssetPath(string systemPath)
    {
        return "Assets" + systemPath.Substring(Application.dataPath.Length, systemPath.Length - Application.dataPath.Length);

    }

    private void OpenDirectory(string path)
    {
#if UNITY_EDITOR_WIN
        // 新开线程防止锁死
        Thread newThread = new Thread(new ParameterizedThreadStart(CmdOpenDirectory));
        newThread.Start(path);
#endif
    }

    private void CmdOpenDirectory(object obj)
    {
        System.Diagnostics.Process p = new System.Diagnostics.Process();
        p.StartInfo.FileName = "cmd.exe";
        p.StartInfo.Arguments = "/c start " + obj.ToString();
        //UnityEngine.Debug.Log(p.StartInfo.Arguments);
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        p.Start();

        p.WaitForExit();
        p.Close();
    }

    delegate void IterateAction(string[] fileExtlist, string path, GroupMode mode = GroupMode.Exclusive);

    //遍历各级目录 每次在子目录下执行action
    private void DirectorIterationDo(string[] fileExtlist, string path, GroupMode mode, IterateAction action)
    {
        string[] dirs = GetDirectorysIn(path, System.IO.SearchOption.TopDirectoryOnly);
        if (dirs.Length > 0)
        {
            for (int i = 0; i < dirs.Length; i++)
            {
                string childdir = dirs[i];
                DirectorIterationDo(fileExtlist, childdir, mode, action);
            }
        }
        action(fileExtlist, path, mode);
    }
    //添加一个目录下的所有typelist的资源到一个bundle guid是rootpath typelist 是findasset的类型 "t:texture"
    private void AddAllTypesInRootPath(string[] typelist, string rootpath, GroupMode mode = GroupMode.Exclusive)
    {
        List<string> assets = new List<string>();
        for (int i = 0; i < typelist.Length; i++)
        {
            var paths = FindAssets(typelist[i], new string[] { rootpath });
            assets.AddRange(paths);
        }
        string guid = MakeGroupId(rootpath);
        mAllAssets.Add(new AssetUnit(assets, guid, mode));
    }

    //添加一个目录下的所有fileExtlist类型的资源到一个bundle rootpath是文件guid  fileExtlist 扩展名
    private void AddAllFileExtInRootPath(string[] fileExtlist, string rootpath, GroupMode mode = GroupMode.Exclusive)
    {
        var files = GetFiles(rootpath, SearchOption.AllDirectories);

        List<string> assets = new List<string>();

        foreach (var file in files)
        {
            foreach (var ext in fileExtlist)
            {
                if (file.EndsWith(ext))
                {
                    assets.Add(file);
                }
            }
        }
        string guid = MakeGroupId(rootpath);
        mAllAssets.Add(new AssetUnit(assets, guid, mode));
    }


    //添加一个目录下的所有filter类型的资源 有几个就是几个bundle guid是文件guid filter 是findasset的类型 "t:texture"
    private void AddOneKindInRootPathToBundles(string fileExtlist, string rootpath, GroupMode mode = GroupMode.Exclusive)
    {
        var paths = FindAssets(fileExtlist, new string[] { rootpath });
        foreach (var path in paths)
        {
            List<string> assets = new List<string>();
            assets.Add(path);
            string guid = MakeGroupId(path);
            mAllAssets.Add(new AssetUnit(assets, guid, mode));
        }
    }
    //添加一个目录下的所有fileExtlist结尾类型的资源 有几个就是几个bundle
    private void AddOneExtInRootPathToBundles(string fileExt, string rootpath, GroupMode mode = GroupMode.Exclusive)
    {
        var files = GetFiles(rootpath, SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            List<string> assets = new List<string>();
            if (file.EndsWith(fileExt))
            {
                assets.Add(file);
            }
            string guid = MakeGroupId(file);
            mAllAssets.Add(new AssetUnit(assets, guid, mode));
        }
    }
    //添加一个目录一级目录下的所有扩展名的资源岛一个package
    private void AddOneExtInPathToPackage(string fileExt, string rootpath)
    {
        var files = GetFiles(rootpath, SearchOption.TopDirectoryOnly);

        List<string> assets = new List<string>();

        foreach (var file in files)
        {
            if (file.EndsWith(fileExt))
            {
                string guid = MakeGroupId(file);
                assets.Add(guid);
            }
        }
        packages_.Add(assets.ToArray());
    }

    //添加每一个子目录中的所有fileExtlist的资源 文件扩展名
    private void AddBranchAssets(string[] fileExtlist, string path, GroupMode mode = GroupMode.Exclusive)
    {
        var files = GetFiles(path, SearchOption.TopDirectoryOnly);

        List<string> assets = new List<string>();

        foreach (var file in files)
        {
            foreach (var ext in fileExtlist)
            {
                if (file.EndsWith(ext))
                {
                    assets.Add(file);
                }
            }
        }
        string guid = MakeGroupId(path);
        mAllAssets.Add(new AssetUnit(assets, guid, mode));
    }

    //添加一个目录下每一个子目录中的所有typelist的资源到一个bundle guid是每一集目录
    private void AddAllTypesInBranchsUnderRootPath(string[] fileExtlist, string rootpath, GroupMode mode = GroupMode.Exclusive)
    {
        DirectorIterationDo(fileExtlist, rootpath, mode, AddBranchAssets);
    }


    private void InitFiles(BuildArguments args)
    {
        EditorUtility.DisplayProgressBar("打包", "开始收集资源", 0);

        //添加固定分组
        //AddStaticGroups();

        //添加场景
        AddScenes(args);
        AddAllTypesInBranchsUnderRootPath(new string[] { ".prefab", ".Prefab" }, "Assets/Res/unity-chan!/Unity-chan! Model/Prefabs", GroupMode.Exclusive);
        //添加角色
        // AddModeWithAnim();

        //添加捏脸资源
        //  AddAvatarRes();

        //添加特效
        //  AddEffect();

        //添加UI资源
        // AddUI();

        //添加fmod bank
        // AddFmod();

        //添加视频
        // AddVideo();

        //添加剧情
        // AddSequence();

        //添加调试UI
        // AddPerformanceUI_Per();

        //添加其他资源
        // AddMisc();

        //添加Spine动画资源
        // AddSpine();

        //添加屏幕安全区配置
        // AddSafeAreaConfig();

        //3转2
        //  AddPicture2D();

        //摄像机动画
        //  AddCamera();

        EditorUtility.ClearProgressBar();

        mGroupID = 0;
    }

    public bool PreBuildEvent(BuildArguments args)
    {
        groups_ = new List<Triple<string, string[], GroupMode>>();
        customFiles_ = new List<string[]>();
        packages_ = new List<string[]>();
        Debug.Log("PreBuildEvent start");
        mAssetOutLocation = args.location;
        mAssetIndex = 0;
        mAllAssets.Clear();
        InitFiles(args);

        for (int i = 0; i < mAllAssets.Count; i++)
        {
            AssetUnit unit = mAllAssets[i];
            groups_.Add(DataUtil.MakeTriple(
            unit.mGroupId.ToString(),
            unit.mAssets.ToArray(),
            unit.mGroupMode
         ));
        }

        //添加package
        //packages_.Add(new string[] { });
       // AddOneExtInPathToPackage(".prefab", "Assets/Res/Sequence");
        //AddOneExtInPathToPackage(".bytes", "Assets/Res/Fmod");

        if (!mFileBuildFinish)
        {
            mFileBuildFinish = true;
           // string packagePath = PackageManager.CreatePackage("Assets/Res/Data", "Assets/Lua", Path.GetFullPath("Assets/../Temp"));
           // customFiles_.Add(new string[] { packagePath });
        }
        else
        {
            customFiles_ = null;
        }

        index_ = 0;

        Debug.Log("PreBuildEvent end");
        return true;
    }

    public void PostBuildEvent(IBuildResult result)
    {
        foreach (string p in result.GetSceneBundles())
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(result.GetAssetsInBundle(p)[0]);
            sb.Append(": ");

            string[] dependencies = result.GetBundleDependencies(p, true);

            sb.Append("(");
            sb.Append(dependencies.Length);
            sb.Append(") ");

            for (int i = 0; i < dependencies.Length; ++i)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(dependencies[i]);
            }

            Debug.Log(sb.ToString());
        }
        Debug.Log("build asset ok");
        mAssetIndex = 0;
        mAllAssets.Clear();
        //if (EditorUtility.DisplayDialog("打包", "打包完成", "确定"))
        // {
        Debug.Log("打包完成");
        OpenDirectory(mAssetOutLocation);
        // }
    }

    private string MakeGroupId(string path)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        Debug.Log("##############   assetpath: " + path + "  guid  " + guid);
        return guid;
    }

    /// <summary>
    /// 添加固定分组的资源包
    /// </summary>
    private void AddStaticGroups()
    {
        {
            //添加shader和shader的变体文件。
            //临时先添加一个自动收集的变体，shader整理需要需要重新弄下。
            List<string> assets = new List<string>();
            var guids = FindAssets("t:shader", new string[] { "Assets/Shader" });
            assets.AddRange(guids);
            string shaderVariantsPath = "Assets/Shader/ShaderCollection/Shaders.shadervariants";
            assets.Add(shaderVariantsPath);
            var shaders = FindAssets("t:shader", new string[] { "Assets/Code/External/NGUI/Resources/Shaders" });
            assets.AddRange(shaders);
            string guid = MakeGroupId("Assets/Shader");
            mAllAssets.Add(new AssetUnit(assets, guid, GroupMode.Exclusive));
        }

        {
            //把更新界面打到一个包内
            List<string> assets = new List<string>();

            var fonts = FindAssets("t:font", new string[] { "Assets/Res/UI/Fonts" });
            var guids = FindAssets("t:shader", new string[] { "Assets/Res/UI/Fonts" });
            assets.AddRange(guids);
            assets.AddRange(fonts);
            string guid = MakeGroupId("Assets/Res/UI/Fonts");
            mAllAssets.Add(new AssetUnit(assets, guid, GroupMode.Standalone));
        }
    }

    /// <summary>
    /// 添加Res目录下的Scene_开头的场景
    /// </summary>
    /// <param name="args"></param>
    private void AddScenes(BuildArguments args)
    {
        List<string> scenes = new List<string>();
        string[] paths = FindAssets("t:scene", new string[] { "Assets/Res" });

        foreach (var path in paths)
        {
            if (path.Contains("SampleScene"))
                scenes.Add(path);
        }
        args.scenesInBuild = scenes.ToArray();
    }

    /// <summary>
    /// 添加Res/Role目录下的所有有效角色
    /// </summary>
    private void AddModeWithAnim()
    {
        string rootDir = "Assets/Res/Role";
        string[] includeDirs = new string[] { "Player", "SelectRole", "NPC", "Other", "Zuoqi", "Animal" };

        if (!Directory.Exists(rootDir))
        {
            Debug.LogError("no path " + rootDir);
            return;
        }
        string[] fileExtlist = new string[] { ".fbx", ".FBX", ".prefab", ".controller" };
        //遍历每个类型的角色目录
        foreach (var parentDir in includeDirs)
        {
            string roleTypePath = rootDir + "/" + parentDir;
            string[] modelDirs = GetDirectorysIn(roleTypePath, System.IO.SearchOption.TopDirectoryOnly);
            foreach (var modelpath in modelDirs)
            {
                var files = GetFiles(modelpath, SearchOption.AllDirectories);

                List<string> assets = new List<string>();

                foreach (var file in files)
                {
                    foreach (var ext in fileExtlist)
                    {
                        if (file.EndsWith(ext))
                        {
                            assets.Add(file);
                        }
                    }
                }
                if (parentDir == "SelectRole")
                {
                    string makeUpPath = modelpath + "/MakeUp";
                    string[] texs = FindAssets("t:texture", new string[] { makeUpPath.Replace("\\", "/") });
                    foreach (var t in texs)
                    {
                        assets.Add(t);
                    }
                }

                string guid = MakeGroupId(modelpath.Replace("\\", "/"));
                mAllAssets.Add(new AssetUnit(assets, guid, GroupMode.Exclusive));

                //  AddAllFileExtInRootPath(new string[] { ".fbx", ".FBX", ".prefab", ".controller" }, modelpath, GroupMode.Exclusive);
            }
        }
    }


    /// <summary>
    /// 添加Res/Effects/Prefabs目录下的所有特效资源
    ///     目前所有特效在一个包内,后面需要按照功能划分目录 TODO
    /// </summary>
    private void AddEffect()
    {
        AddAllTypesInBranchsUnderRootPath(new string[] { ".prefab", ".Prefab" }, "Assets/Res/Effects/Prefabs", GroupMode.Exclusive);
    }

    /// <summary>
    /// 添加Res/UI目录下的所有UI资源
    /// </summary>
    private void AddUI()
    {
        //所有图片的资源
        {
            AddAllTypesInRootPath(new string[] { "t:texture" }, "Assets/Res/UI/Icon", GroupMode.Exclusive);
        }
        //所有图片的资源
        {
            AddAllTypesInRootPath(new string[] { "t:texture" }, "Assets/Res/UI/UIBg", GroupMode.Exclusive);
        }
        //所有预设资源
        {
            AddAllTypesInRootPath(new string[] { "t:prefab" }, "Assets/Res/UI/Prefab", GroupMode.Exclusive);
        }
    }

    /// <summary>
    /// 调试UI界面
    /// </summary>
    void AddPerformanceUI_Per()
    {
        AddAllTypesInRootPath(new string[] { "t:prefab" }, "Assets/Res/UI/PerformanceUI/Prefab", GroupMode.Standalone);
    }

    /// <summary>
    /// 添加Res/Sequence目录下的剧情资源
    ///     目前每个剧情是一个单独的组,后期根据资源大小和个数优化 TODO
    /// </summary>
    private void AddSequence()
    {
        AddOneKindInRootPathToBundles("t:prefab", "Assets/Res/Sequence", GroupMode.Exclusive);
    }

    private void AddPicture2D()
    {
        AddAllTypesInRootPath(new string[] { "t:texture", "t:Material", "t:prefab" }, "Assets/Res/Picture2D", GroupMode.Standalone);
    }

    /// <summary>
    /// 添加Fmod Bank
    /// </summary>
    private void AddFmod()
    {
        AddOneExtInRootPathToBundles(".bytes", "Assets/Res/Fmod", GroupMode.Exclusive);
    }

    /// <summary>
    /// 添加视频
    /// </summary>
    private void AddVideo()
    {
        var paths = FindAssets("", new string[] { "Assets/Res/Video" });
        customFiles_.Add(paths);
    }

    /// <summary>
    /// 安全屏区域配置文件
    /// </summary>
    private void AddSafeAreaConfig()
    {
        var paths = FindAssets("", new string[] { "Assets/Res/SafeArea" });
        customFiles_.Add(paths);
    }

    /// <summary>
    ///Avatar配置
    /// </summary>
    public void AddAvatarRes()
    {
        AddAllTypesInBranchsUnderRootPath(new string[] { ".asset" }, "Assets/Res/Role/Avatar", GroupMode.Standalone);
    }


    /// <summary>
    /// 添加一些特殊的没有引用的资源
    /// </summary>
    private void AddMisc()
    {
        AddAllTypesInRootPath(new string[] { "t:prefab", "t:texture", "t:material" }, "Assets/Res/Misc", GroupMode.Exclusive);
    }

    /// <summary>
    /// Spine动画,只添加预制体
    /// </summary>
    private void AddSpine()
    {
        AddAllTypesInRootPath(new string[] { "t:prefab" }, "Assets/Res/UI/SpineAni", GroupMode.Exclusive);
    }

    /// <summary>
    /// 镜头动画
    /// </summary>
    private void AddCamera()
    {
        AddAllTypesInRootPath(new string[] { "t:prefab", "t:AnimationClip" }, "Assets/Res/Camera", GroupMode.Exclusive);
    }

    public bool AddCustomFiles(out string[] files)
    {
        if (index_ < customFiles_.Count)
        {
            files = customFiles_[index_];

            ++index_;

            return true;
        }
        else
        {
            files = null;
            index_ = 0;

            return false;
        }
    }

    public bool AddGroup(out string groupId, out string[] assets)
    {
        groupId = null;
        assets = null;
        return false;
    }

    public bool AddGroup(out string groupId, out string[] assets, out GroupMode groupMode)
    {
        if (index_ < groups_.Count)
        {
            groupId = groups_[index_].first;
            assets = groups_[index_].second;
            groupMode = groups_[index_].third;

            ++index_;

            return true;
        }
        else
        {
            groupId = null;
            assets = null;
            groupMode = GroupMode.Exclusive;

            index_ = 0;

            return false;
        }
    }

    public bool AddPackage(out string[] groupIds)
    {
        if (index_ < packages_.Count)
        {
            groupIds = packages_[index_];

            ++index_;

            return true;
        }
        else
        {
            groupIds = null;

            index_ = 0;

            return false;
        }
    }
}
