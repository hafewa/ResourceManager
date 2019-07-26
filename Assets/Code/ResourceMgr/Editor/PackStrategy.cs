using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PackStrategy : ScriptableObject
{
    public string strategyName;
    //不打bundle 直接放进包里的文件
    public List<string[]> unBundleFiles;

    public List<string[]> packages;

    public List<string> groups;
}
