using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Text;

/// <summary>
/// Jenkin Builder unity相关的函数调用。供给给外部使用
/// </summary>
public class JenkinBuilder
{
    private static BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
    [MenuItem("Jenkins/Build/ByConfigure")]
    public static void BuilderByJenkinBuilderConfigure()
    {
        if (!EditorUtility.DisplayDialog("Build", "Are you sure to build by configure?", "Yes", "No"))
        {
            return;
        }
        if (JenkinBuilderConfigure.Configure!=null)
        {
            if (JenkinsBuildAssetBundle.BuildAssetBundle())
            {
                BuildAppByConfigure();
            }
        }
    }


    public static void BuildAppByConfigure()
    {
        DateTime begin = DateTime.Now;
        if (JenkinBuilderConfigure.Configure == null)
        {
            EditorUtility.DisplayDialog("JenkinBuilderConfigure Empty", "Please make JenkinBuilderConfigure Ready", "Yes");
            return;
        }
        try
            {
           
                if (JenkinBuilderConfigure.Configure.buildTarget == BuildTarget.Android || JenkinBuilderConfigure.Configure.buildTarget == BuildTarget.iOS || JenkinBuilderConfigure.Configure.buildTarget == BuildTarget.StandaloneWindows64)
                {
                    string oldvesion = PlayerSettings.bundleVersion;
                    List<string> scenes; string project; BuildOptions options;
                    switch (JenkinBuilderConfigure.Configure.buildTarget)
                    {
                        case BuildTarget.Android:
                            {
                                GameCore.ExportAndroid.OnPreProcessBuild(out scenes, out project, out options);
                                break;
                            }
                        case BuildTarget.iOS:
                            {
                                GameCore.ExportIphone.OnPreProcessBuild(out scenes, out project, out options);
                                break;
                            }
                        case BuildTarget.StandaloneWindows64:
                            {
                                GameCore.ExportWindows.OnPreProcessBuild(out scenes, out project, out options);
                                break;
                            }
                        default:
                            {
                                GameCore.ExportAndroid.OnPreProcessBuild(out scenes, out project, out options);
                                break;
                            }
                    }
                    JenkinBuilderConfigure.Configure.GenPublishVersion();
                    PlayerSettings.bundleVersion = JenkinBuilderConfigure.Configure.publishVersion;
                    buildPlayerOptions.scenes = scenes.ToArray();
                    buildPlayerOptions.locationPathName = project;
                    buildPlayerOptions.target = JenkinBuilderConfigure.Configure.buildTarget;
                    buildPlayerOptions.options = JenkinBuilderConfigure.Configure.options;

                    BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                    BuildResult ret = report.summary.result;
                    LogBuild(report);
                    if (ret == BuildResult.Succeeded)
                    {
                        JenkinsBuildAssetBundle.SaveConfigure();
                        if (JenkinBuilderConfigure.Configure.publish)
                            JenkinsBuildAssetBundle.CopyResAppToSharePath();
                    }
                    else
                    {
                        PlayerSettings.bundleVersion = oldvesion;
                    }
                }
                else
                {
                    Debug.LogError("Not Support BuildTarget");
                }
            }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n" + e.StackTrace);
        }
        DateTime end = DateTime.Now;
        Debug.Log((end.Ticks - begin.Ticks) / 1000f + "s");
    }

    /// <summary>
    /// unity build android apk脚本函数，供给给外部sh脚本调用。也可作为Unity版本打包使用
    /// </summary>    
	[MenuItem("Jenkins/Build/Android")]
	public static void BuilderAndroid()
    {
        DateTime begin = DateTime.Now;
        try
        {
            List<string> scenes; string project; BuildOptions options;
            GameCore.ExportAndroid.OnPreProcessBuild(out scenes, out project, out options);
           // JenkinBuilderConfigure.Configure.GenPublishVersion();
          //  PlayerSettings.bundleVersion = JenkinBuilderConfigure.Configure.publishVersion;
            buildPlayerOptions.scenes = scenes.ToArray();
            buildPlayerOptions.locationPathName = project;
            buildPlayerOptions.target = BuildTarget.Android;
            buildPlayerOptions.options = options;
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildResult ret = report.summary.result;
            LogBuild(report);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n" + e.StackTrace);
        }
        DateTime end = DateTime.Now;
        Debug.Log((end.Ticks - begin.Ticks) / 1000f + "s");
    }

    /// <summary>
    /// unity build ios xcode 脚本函数，供给给外部sh脚本调用。也可作为Unity版本打包使用
    /// </summary>  
    [MenuItem("Jenkins/Build/iOS")]
	public static void BuildIOS()
    {
        DateTime begin = DateTime.Now;
        try
        {
            List<string> scenes; string project; BuildOptions options;
            GameCore.ExportIphone.OnPreProcessBuild(out scenes, out project, out options);
            LogBuild(BuildPipeline.BuildPlayer(scenes.ToArray(), project, BuildTarget.iOS, options));
        }
        catch(Exception e)
        {
            Debug.LogError(e.Message + "\n" + e.StackTrace);
        }
        DateTime end = DateTime.Now;
        Debug.Log((end.Ticks - begin.Ticks)/1000f +"s");
    }

    /// <summary>
    /// unity build BuildPC 脚本函数，供给给外部sh脚本调用。也可作为Unity版本打包使用
    /// </summary>  
    [MenuItem("Jenkins/Build/PC")]
    public static void BuildPC()
    {
        DateTime begin = DateTime.Now;
        try
        {
            List<string> scenes; string project; BuildOptions options;
            GameCore.ExportWindows.OnPreProcessBuild(out scenes, out project, out options);
            LogBuild(BuildPipeline.BuildPlayer(scenes.ToArray(), project, BuildTarget.StandaloneWindows64, options));
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n" + e.StackTrace);
        }
        DateTime end = DateTime.Now;
        Debug.Log((end.Ticks - begin.Ticks) / 1000f + "s");
    }

    private static void LogBuild(BuildReport report)
    {
        string filePath = Path.GetFullPath(Application.dataPath + "/../Build/BuildDetailLog.txt");
        StreamWriter streamWriter = new StreamWriter(filePath, false, Encoding.UTF8);

        //输出文件列表
        streamWriter.WriteLine("Files");
        for (int i = 0; i < report.files.Length; i++)
        {
            var file = report.files[i];
            streamWriter.WriteLine("\t{0}",file.ToString());
        }
        //阶段信息列表
        streamWriter.WriteLine("Steps");
        for (int i = 0; i < report.steps.Length; i++)
        {
            var step = report.steps[i];
            streamWriter.WriteLine("\t{0}", step.ToString());
        }
        //剥离信息列表
        if(report.strippingInfo != null)
        {
            streamWriter.WriteLine("Strips");
            streamWriter.WriteLine("\t{0}", report.strippingInfo.ToString());
        }

        //总览信息
        streamWriter.WriteLine("Summary");
        streamWriter.WriteLine("\tbuildResult:{0} TotalSize:{1}MB TotalTime:{2} TotalErrors:{3} TotalWarnings:{4}", 
                                report.summary.result.ToString(),
                                (report.summary.totalSize / 1024.0f / 1024.0f).ToString(),
                                report.summary.totalTime.ToString(),
                                report.summary.totalErrors.ToString(),
                                report.summary.totalWarnings.ToString());

        streamWriter.Flush(); streamWriter.Close();
    }
}
