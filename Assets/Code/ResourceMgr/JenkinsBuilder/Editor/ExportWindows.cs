using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System;

namespace GameCore
{
    public class ExportWindows
    {
        public static void OnPreProcessBuild(out List<string> scenes, out string project, out BuildOptions options)
        {
            //控制台参数
            List<string> commandArgs = new List<string>(Environment.GetCommandLineArgs());
            //默认随包场景处理
            scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                scene.enabled = false;
                switch (Path.GetFileNameWithoutExtension(scene.path))
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
            project = string.IsNullOrEmpty(project) ? "Build/ldj" : "Build/" + project.Split('-')[1];
            project += ".exe";
            //打包选项处理
            options = commandArgs.Exists((arg) => { return arg == "DEBUG"; }) ? BuildOptions.Development : BuildOptions.None;
            options |= commandArgs.Exists((arg) => { return arg == "PROFILER"; }) ? BuildOptions.ConnectWithProfiler : BuildOptions.None;
            //TODO: 不同渠道打包处理
        }

        [PostProcessBuild()]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget != BuildTarget.StandaloneWindows64) return;
        }
    }

}
