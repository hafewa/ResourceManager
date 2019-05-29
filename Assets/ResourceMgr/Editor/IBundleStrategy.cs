using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ResourceMoudle
{
    public enum GroupMode
    {
        Standalone,
        Exclusive,
    }
    public interface IBundleStrategy
    {
        void Awake();

        string Name();

        bool HasUI();
        void DrawUI();

        bool PreBuildEvent(BuildArguments args);
        void PostBuildEvent(IBuildResult result);

        bool AddGroup(out string groupId, out string[] assets, out GroupMode mode);

        bool AddPackage(out string[] groupIds);

        bool AddCustomFiles(out string[] files);
    }
}