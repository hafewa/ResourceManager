using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ResourceMoudle
{
    public enum Mode
    {
        FullBuild = 0,
        IncrementalBuild = 1,
    }
    public class BuildArguments
    {
        public string location;
        public Mode mode;
        public string version;
        public string parentVersion;
        public string prefix;
        public BuildTarget buildTarget;
        public float bundleSizeHint;
        public float packageSizeHint;
        public bool compressAssetBundles;
        public string[] scenesInBuild;
        public IBundleStrategy[] customScripts;
        public bool reportVersionComparision;
    }
}