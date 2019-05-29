using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CmdHelper
{
    private static string CmdPath = @"C:\Windows\System32\cmd.exe";

    /// <summary>
    /// 执行cmd命令
    /// 多命令请使用批处理命令连接符：
    /// <![CDATA[
    /// &:同时执行两个命令
    /// |:将上一个命令的输出,作为下一个命令的输入
    /// &&：当&&前的命令成功时,才执行&&后的命令
    /// ||：当||前的命令失败时,才执行||后的命令]]>
    /// 其他请百度
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="output"></param>
    public static void RunCmd(string workingDirectory ,string cmd, out string output)
    {
       

       // var info = new ProcessStartInfo("C:/Program Files/TortoiseSVN/bin/TortoiseProc.exe")
        {
      //      Arguments = string.Format("/command:{0} /path:{1} /closeonend:0","update", workingDirectory)
        };
       // Process.Start(info);

       

        // cmd = cmd.Trim().TrimEnd('&') + "&exit";//说明：不管命令是否成功均执行exit命令，否则当调用ReadToEnd()方法时，会处于假死状态
        using (Process p = new Process())
        {
            p.StartInfo.FileName = "C:/Program Files/TortoiseSVN/bin/TortoiseProc.exe";
            p.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
            p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
            p.StartInfo.CreateNoWindow = true;          //不显示程序窗口 
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.Start();//启动程序

            //向cmd窗口写入命令
            p.StartInfo.Arguments = (string.Format("/command:{0} /path:{1} /closeonend:0", cmd, workingDirectory) );
            p.StandardInput.WriteLine("exit");
            p.StandardInput.AutoFlush = true; 
              
            //获取cmd窗口的输出信息
            output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();//等待程序执行完退出进程
            p.Close();
        }
    }
}

public class JenkinsBuildConfigureEditor : EditorWindow
{ 
    public static JenkinsBuildConfigureEditor curwindow;
    [MenuItem("Jenkins/BuildConfigure/Window",false,100)]
    static void ShowEditor()
    {
        curwindow = EditorWindow.GetWindow<JenkinsBuildConfigureEditor>();
        curwindow.titleContent = new GUIContent("打包设置");         // 窗口的标题  
    }
    #region Main Motheds
    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("BaseSetting");
        BuildTarget old = JenkinBuilderConfigure.Configure.buildTarget;
        // 设置目标平台
        JenkinBuilderConfigure.Configure.buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("buildTarget(目标平台):", JenkinBuilderConfigure.Configure.buildTarget);
        if (old != JenkinBuilderConfigure.Configure.buildTarget)
        {
            JenkinBuilderConfigure.PlatformConfigure.buildTarget = JenkinBuilderConfigure.Configure.buildTarget;
            JenkinBuilderConfigure.SavePlatformConfig();
            JenkinBuilderConfigure.ReadPlatformConfig();
            JenkinBuilderConfigure.ReadBuildConfigure();

        }
        EditorGUILayout.Separator();
        // 设置unity目录
        JenkinBuilderConfigure.Configure.unityPath = EditorGUILayout.TextField("unityPath(Uniy路径):", JenkinBuilderConfigure.Configure.unityPath);
        if (GUILayout.Button("choose unityPath"))
        {
            // 导入
            string path = EditorUtility.OpenFilePanel("choose unityPath", Application.dataPath, "*");
            if (path.Length != 0)
            {
                JenkinBuilderConfigure.Configure.unityPath = path;
            }
        }

        // 设置unity目录
        JenkinBuilderConfigure.Configure.unityPath = EditorGUILayout.TextField("xcodePath(Xcode路径):", JenkinBuilderConfigure.Configure.xcodePath);
        if (GUILayout.Button("choose xcodePath"))
        {
            // 导入
            string path = EditorUtility.OpenFilePanel("choose xcodePath", Application.dataPath, "*");
            if (path.Length != 0)
            {
                JenkinBuilderConfigure.Configure.xcodePath = path;
            }
        }

        EditorGUILayout.Separator();
        // projectPath
        JenkinBuilderConfigure.Configure.projectPath = EditorGUILayout.TextField("projectPath(工程目录):", JenkinBuilderConfigure.Configure.projectPath);
        if (GUILayout.Button("choose projectPath"))
        {
            // 导入
            string path = EditorUtility.OpenFolderPanel("choose projectPath", Application.dataPath, "");
            if (path.Length != 0)
            {
                JenkinBuilderConfigure.Configure.projectPath = path;
            }
        }
        EditorGUILayout.Separator();
        // bundlePath
        JenkinBuilderConfigure.Configure.bundlePath = EditorGUILayout.TextField("bundlePath(资源的生成目录):", JenkinBuilderConfigure.Configure.bundlePath);
        if (GUILayout.Button("choose bundlePath"))
        {
            // 导入
            string path = EditorUtility.OpenFolderPanel("choose bundlePath", Application.dataPath, "");
            if (path.Length != 0)
            {
                JenkinBuilderConfigure.Configure.bundlePath = path;
            }
        }
        EditorGUILayout.Separator();
        // packageExportPath
        JenkinBuilderConfigure.Configure.packageExportPath = EditorGUILayout.TextField("packageExportPath(app发布目录):", JenkinBuilderConfigure.Configure.packageExportPath);
        if (GUILayout.Button("choose packageExportPath"))
        {
            // 导入
            string path = EditorUtility.OpenFolderPanel("choose packageExportPath", Application.dataPath, "");
            if (path.Length != 0)
            {
                JenkinBuilderConfigure.Configure.packageExportPath = path;
            }
        }
        EditorGUILayout.Separator();
        // resExportPath
        JenkinBuilderConfigure.Configure.resExportPath = EditorGUILayout.TextField("resExportPath(资源发布目录):", JenkinBuilderConfigure.Configure.resExportPath);
        if (GUILayout.Button("choose resExportPath"))
        {
            // 导入
            string path = EditorUtility.OpenFolderPanel("choose resExportPath", Application.dataPath, "");
            if (path.Length != 0)
            {
                JenkinBuilderConfigure.Configure.resExportPath = path;
            }
        }
        EditorGUILayout.Separator();

        // publishVersion
        JenkinBuilderConfigure.Configure.publishVersion = EditorGUILayout.TextField("publishVersion(发布版本号)", JenkinBuilderConfigure.Configure.publishVersion);
        EditorGUILayout.Separator();
        // versionPrefix
        JenkinBuilderConfigure.Configure.versionPrefix = EditorGUILayout.TextField("versionPrefix(版本号前缀)", JenkinBuilderConfigure.Configure.versionPrefix);

        EditorGUILayout.Separator();
        // resVersion
        JenkinBuilderConfigure.Configure.parentResVersion = EditorGUILayout.IntField("parentResVersion(上次打包资源版本):", JenkinBuilderConfigure.Configure.parentResVersion);
        EditorGUILayout.Separator();
        // resVersion
        JenkinBuilderConfigure.Configure.resVersion = EditorGUILayout.IntField("resVersion(资源版本):", JenkinBuilderConfigure.Configure.resVersion);
        EditorGUILayout.Separator();
        // svnVersion
        JenkinBuilderConfigure.Configure.svnVersion = EditorGUILayout.IntField("svnVersion:", JenkinBuilderConfigure.Configure.svnVersion);
        EditorGUILayout.Separator();
        // codeVersion
        JenkinBuilderConfigure.Configure.codeVersion = EditorGUILayout.IntField("codeVersion(代码版本):", JenkinBuilderConfigure.Configure.codeVersion);
        EditorGUILayout.Separator();
        // appURL
        JenkinBuilderConfigure.Configure.appURL = EditorGUILayout.TextField("appURL(app更新链接):", JenkinBuilderConfigure.Configure.appURL);
        EditorGUILayout.Separator();
        // resURL
        JenkinBuilderConfigure.Configure.resURL = EditorGUILayout.TextField("resURL(资源热更目录):", JenkinBuilderConfigure.Configure.resURL);
        EditorGUILayout.Separator();
        // hotFix
        JenkinBuilderConfigure.Configure.hotFix = EditorGUILayout.ToggleLeft("hotFix(是否热更新)",JenkinBuilderConfigure.Configure.hotFix);
  
        EditorGUILayout.Separator();
        // appUpdate
        JenkinBuilderConfigure.Configure.appUpdate = EditorGUILayout.ToggleLeft("appUpdate(是否更新大版本)",JenkinBuilderConfigure.Configure.appUpdate);
        EditorGUILayout.Separator();
        // publish
        JenkinBuilderConfigure.Configure.publish = EditorGUILayout.ToggleLeft("publish(拷贝资源到发布目录)", JenkinBuilderConfigure.Configure.publish);


        JenkinBuilderConfigure.Configure.options = (BuildOptions)EditorGUILayout.MaskField(new GUIContent("Build Options(打包app选项)"), (int)JenkinBuilderConfigure.Configure.options, Enum.GetNames(typeof(BuildOptions)));
      

        EditorGUILayout.Separator();

        if (GUILayout.Button("Save"))
        {
            JenkinBuilderConfigure.SaveBuildConfigure();
            JenkinBuilderConfigure.SavePlatformConfig();
        }

        if (GUILayout.Button("Build Asset Only"))
        {
            JenkinBuilderConfigure.SaveBuildConfigure();
            JenkinBuilderConfigure.SavePlatformConfig();

            if (!EditorUtility.DisplayDialog("Build Asset Only", "Are you sure to build asset only ?", "Yes", "No"))
            {
                return;
            }
            JenkinsBuildAssetBundle.BuildAssetBundle();
            JenkinBuilderConfigure.SaveBuildConfigure();
            JenkinBuilderConfigure.SavePlatformConfig();
        }

        if (GUILayout.Button("Build App Only"))
        {
            JenkinBuilderConfigure.SaveBuildConfigure();
            JenkinBuilderConfigure.SavePlatformConfig();
            if (!EditorUtility.DisplayDialog("Build App Only", "Are you sure to build App only ?", "Yes", "No"))
            {
                return;
            }
            JenkinBuilder.BuildAppByConfigure();
        }

        if (GUILayout.Button("Build Asset And App"))
        {
            JenkinBuilderConfigure.SaveBuildConfigure();
            JenkinBuilderConfigure.SavePlatformConfig();
            if (!EditorUtility.DisplayDialog("Build  Asset And App", "Are you sure to build asset and app?", "Yes", "No"))
            {
                return;
            }
            JenkinBuilder.BuilderByJenkinBuilderConfigure();
        }

        EditorGUILayout.EndVertical();
    }

    private void OnDestroy()
    {

    }
    #endregion

}
