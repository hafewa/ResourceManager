using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMemoryHosting<T>
{
    void InitMemory();
    void ClearMemory();
    void CopyMemory(ref T value);
}
