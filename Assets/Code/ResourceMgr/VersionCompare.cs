using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class VersionCompare
{
    public int unchangedFiles;
    public int newFiles;
    public int changedFiles;
    public long sourceVersionCapacity;
    public long destVersionCapacity;
    public long downloadSize;
}
