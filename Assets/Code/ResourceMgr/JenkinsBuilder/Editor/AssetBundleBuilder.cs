using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;


public class AssetBundleBuilder
{
    
    public static bool BuildAssetBundles(string outputPath, AssetBundleBuild[] builds, bool forceRebuild, bool useChunkBasedCompression, BuildTarget buildTarget)
    {
        var options = BuildAssetBundleOptions.None;
        if (useChunkBasedCompression)
            options |= BuildAssetBundleOptions.ChunkBasedCompression;

        if (forceRebuild)
            options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

        Directory.CreateDirectory(outputPath);
        var manifest = CompatibilityBuildPipeline.BuildAssetBundles(outputPath, builds, options, buildTarget);
        return manifest != null;
    }

    public static bool BuildAssetBundles(string outputPath, bool forceRebuild, bool useChunkBasedCompression, BuildTarget buildTarget)
    {
        var options = BuildAssetBundleOptions.None;
        if (useChunkBasedCompression)
            options |= BuildAssetBundleOptions.ChunkBasedCompression;

        if (forceRebuild)
            options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

        Directory.CreateDirectory(outputPath);
        var manifest = CompatibilityBuildPipeline.BuildAssetBundles(outputPath, options, buildTarget);
        return manifest != null;
    }
    class CustomBuildParameters : BundleBuildParameters
    {
        public Dictionary<string, UnityEngine.BuildCompression> PerBundleCompression { get; set; }

        public CustomBuildParameters(BuildTarget target, BuildTargetGroup group, string outputFolder) : base(target, group, outputFolder)
        {
            PerBundleCompression = new Dictionary<string, UnityEngine.BuildCompression>();
        }

        public override UnityEngine.BuildCompression GetCompressionForIdentifier(string identifier)
        {
            UnityEngine.BuildCompression value;
            if (PerBundleCompression.TryGetValue(identifier, out value))
                return value;
            return BundleCompression;
        }
    }

    public static bool BuildAssetBundles(string outputPath, bool useChunkBasedCompression, BuildTarget buildTarget, BuildTargetGroup buildGroup)
    {
        var buildContent = new BundleBuildContent(ContentBuildInterface.GenerateAssetBundleBuilds());
        var buildParams = new CustomBuildParameters(buildTarget, buildGroup, outputPath);
        buildParams.PerBundleCompression.Add("Bundle1", UnityEngine.BuildCompression.Uncompressed);
        buildParams.PerBundleCompression.Add("Bundle2", UnityEngine.BuildCompression.LZ4);

        if (m_Settings.compressionType == UnityEngine.CompressionType.None)
            buildParams.BundleCompression = UnityEngine.BuildCompression.Uncompressed;
        else if (m_Settings.compressionType == UnityEngine.CompressionType.Lzma)
            buildParams.BundleCompression = UnityEngine.BuildCompression.LZMA;
        else if (m_Settings.compressionType == UnityEngine.CompressionType.Lz4 || m_Settings.compressionType == UnityEngine.CompressionType.Lz4HC)
            buildParams.BundleCompression = UnityEngine.BuildCompression.LZ4;

        IBundleBuildResults results;
        ReturnCode exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results);
        return exitCode == ReturnCode.Success;
    }

}
