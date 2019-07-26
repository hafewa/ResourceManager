using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System;

namespace GameCore
{
    public class ExportIphone
    {
        public static void OnPreProcessBuild(out List<string> scenes,out string project,out BuildOptions options)
        {
            //控制台参数
            List<string> commandArgs = new List<string>(Environment.GetCommandLineArgs());
            //默认随包场景处理
            scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                scene.enabled = false;
                switch(Path.GetFileNameWithoutExtension(scene.path))
                {
                    case "Scene_Start":
                    case "Scene_Switch":
                        scenes.Add(scene.path);
                        break;
                }
            }
            AssetDatabase.SaveAssets();
            //导出工程名称设置
            project = commandArgs.Find((arg) => { return arg.StartsWith("project-"); });
            project = string.IsNullOrEmpty(project) ? "Build/xcode_test" : "Build/xcode_" + project.Split('-')[1];
            //打包选项处理
            options = commandArgs.Exists((arg) => { return arg == "DEBUG"; }) ? BuildOptions.Development : BuildOptions.None;
            options |= commandArgs.Exists((arg) => { return arg == "PROFILER"; }) ? BuildOptions.ConnectWithProfiler : BuildOptions.None;
            //TODO: 不同渠道打包处理
        }

        [PostProcessBuild()]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget != BuildTarget.iOS) return;
#if UNITY_IOS
            var proj = new UnityEditor.iOS.Xcode.PBXProject();
            string projPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(buildPath);
            proj.ReadFromFile(projPath);
            string target = proj.TargetGuidByName("Unity-iPhone");
            string plistPath = buildPath + "/Info.plist";
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);

            //获取所有的配置文件
            string CONFIG_PATH = Application.dataPath + "/../SDK/iOS/";
            List<Unity_Xcode_Json> jsons = new List<Unity_Xcode_Json>();
            string[] files = Directory.GetFiles(CONFIG_PATH, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                jsons.Add(JsonUtility.FromJson<Unity_Xcode_Json>(File.ReadAllText(files[i])));
            }

            //创建资源目录
            string frameworkPath = buildPath + "/Frameworks/Plugins/iOS/";
            if (!Directory.Exists(frameworkPath))
                Directory.CreateDirectory(frameworkPath);
            string aPath = buildPath + "/Libraries/Plugins/iOS/";
            if (!Directory.Exists(aPath))
                Directory.CreateDirectory(aPath);

            proj.SetBuildProperty(target, "FRAMEWORK_SEARCH_PATHS", "$(SRCROOT)/Frameworks/Plugins/iOS");
            proj.AddBuildProperty(target, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");

            proj.SetBuildProperty(target, "LIBRARY_SEARCH_PATHS", "$(SRCROOT)/Libraries/Plugins/iOS");
            proj.AddBuildProperty(target, "LIBRARY_SEARCH_PATHS", "$(SRCROOT)/Libraries");
            proj.AddBuildProperty(target, "LIBRARY_SEARCH_PATHS", "$(SRCROOT)");
            proj.AddBuildProperty(target, "LIBRARY_SEARCH_PATHS", "$(inherited)");

            for (int i = 0; i < jsons.Count; i++)
            {
                Unity_Xcode_Json json = jsons[i];
                //系统静态库引用
                if (json.internal_frameworks != null)
                {
                    for (int j = 0; j < json.internal_frameworks.Length; j++)
                    {
                        proj.AddFrameworkToProject(target, json.internal_frameworks[j], false);
                    }
                }

                //系统动态库引用
                if (json.internal_dynamiclibs != null)
                {
                    for (int j = 0; j < json.internal_dynamiclibs.Length; j++)
                    {
                        proj.AddFileToBuild(target, proj.AddFile("usr/lib/" + json.internal_dynamiclibs[j], "Frameworks/" + json.internal_dynamiclibs[j], UnityEditor.iOS.Xcode.PBXSourceTree.Sdk));
                    }
                }

                //外部静态库引用
                if (json.external_frameworks != null)
                {
                    for (int j = 0; j < json.external_frameworks.Length; j++)
                    {
                        EditorUtils_Common.copy_files(CONFIG_PATH + json.external_frameworks[j], frameworkPath);
                        string fileName = Path.GetFileName(json.external_frameworks[j]);
                        proj.AddFileToBuild(target, proj.AddFile("Frameworks/Plugins/iOS/" + fileName, "Frameworks/" + fileName, UnityEditor.iOS.Xcode.PBXSourceTree.Source));
                    }
                }

                //外部静态库引用
                if (json.external_staticlibs != null)
                {
                    for (int j = 0; j < json.external_staticlibs.Length; j++)
                    {
                        EditorUtils_Common.copy_files(CONFIG_PATH + json.external_staticlibs[j], aPath);
                        string fileName = Path.GetFileName(json.external_staticlibs[j]);
                        proj.AddFileToBuild(target, proj.AddFile("Libraries/Plugins/iOS/" + fileName, "Libraries/" + fileName, UnityEditor.iOS.Xcode.PBXSourceTree.Source));
                    }
                }

                //外部文件引用
                if (json.external_files != null)
                {
                    for (int j = 0; j < json.external_files.Length; j++)
                    {
                        EditorUtils_Common.copy_files(CONFIG_PATH + json.external_files[j], aPath);
                        string fileName = Path.GetFileName(json.external_files[j]);
                        proj.AddFileToBuild(target, proj.AddFile("Libraries/Plugins/iOS/" + fileName, "Libraries/" + fileName, UnityEditor.iOS.Xcode.PBXSourceTree.Source));
                    }
                }

                if(json.lib_searchpath != null)
                {
                    for(int j = 0;j < json.lib_searchpath.Length;j++)
                    {
                        proj.AddBuildProperty(target, "LIBRARY_SEARCH_PATHS", "$(SRCROOT)/Libraries/Plugins/iOS/" + json.lib_searchpath[j]);
                    }
                }

                if (json.framework_searchpath != null)
                {
                    for (int j = 0; j < json.framework_searchpath.Length; j++)
                    {
                        proj.AddBuildProperty(target, "FRAMEWORK_SEARCH_PATHS", "$(SRCROOT)/Frameworks/Plugins/iOS/" + json.framework_searchpath[j]);
                    }
                }

                //BuildSetting
                if (json.buildset_set != null)
                {
                    for (int j = 0; j < json.buildset_set.Length; j++)
                    {
                        proj.SetBuildProperty(target, json.buildset_set[j].key, json.buildset_set[j].value);
                    }
                }
                if (json.buildset_add != null)
                {
                    for (int j = 0; j < json.buildset_add.Length; j++)
                    {
                        proj.UpdateBuildProperty(target, json.buildset_add[j].key, new List<string>() { json.buildset_add[j].value },new List<string>());
                    }
                }

                //Info.plist
                if (json.plistset != null)
                {
                    var dict = plist.root.AsDict();
                    for (int j = 0; j < json.plistset.Length; j++)
                    {
                        dict.SetString(json.plistset[j].key, json.plistset[j].value);
                    }
                }
                if (json.plistarray != null)
                {
                    var dict = plist.root.AsDict();
                    for (int j = 0; j < json.plistarray.Length; j++)
                    {
                        var array = dict.CreateArray(json.plistarray[j].key);
                        for (int k = 0; k < json.plistarray[j].value.Length; k++)
                        {
                            array.AddString(json.plistarray[j].value[k]);
                        }
                    }
                }
                if (json.plistarraydict != null)
                {
                    var dict = plist.root.AsDict();
                    for (int j = 0; j < json.plistarraydict.Length; j++)
                    {
                        var arrayDict = dict.CreateDict(json.plistarraydict[j].key);
                        var array = arrayDict.CreateArray(json.plistarraydict[j].value.key);
                        for (int k = 0; k < json.plistarraydict[j].value.value.Length; k++)
                        {
                            array.AddString(json.plistarraydict[j].value.value[k]);
                        }
                    }
                }
            }

            proj.WriteToFile(projPath);
            plist.WriteToFile(plistPath);
#endif
        }

        [System.Serializable]
        public class Unity_Xcode_Json_KV
        {
            public string key;
            public string value;
        }

        [System.Serializable]
        public class Unity_Xcode_Json_KA
        {
            public string key;
            public string[] value;
        }

        [System.Serializable]
        public class Unity_Xcode_Json_KAD
        {
            public string key;
            public Unity_Xcode_Json_KA value;
        }

        [System.Serializable]
        public class Unity_Xcode_Json
        {
            public string[] internal_frameworks;
            public string[] internal_dynamiclibs;
            public string[] external_frameworks;
            public string[] external_staticlibs;
            public string[] external_files;
            public string[] lib_searchpath;
            public string[] framework_searchpath;
            public Unity_Xcode_Json_KV[] buildset_set;
            public Unity_Xcode_Json_KV[] buildset_add;
            public Unity_Xcode_Json_KV[] plistset;
            public Unity_Xcode_Json_KA[] plistarray;
            public Unity_Xcode_Json_KAD[] plistarraydict;
        }
    }

}
