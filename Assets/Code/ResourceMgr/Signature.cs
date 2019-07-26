using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ResourceMoudle
{
    //文件签名
    [Serializable]
    class Signature
    {
        public int length;
        public uint[] hashes;
    }
}