using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ResourceMoudle
{
    //文件定位器 保存文件位置和名称
    [Serializable]
    public class FileLocator
    {
        public FileLocator(Location location, string filename)
        {
            this.location = location;
            this.filename = filename;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int a = location.GetHashCode();
                int b = filename.GetHashCode();
                return (a + b) * (a + b + 1) / 2 + b;
            }
        }

        public override bool Equals(object other)
        {
            var o = other as FileLocator;

            if (o == null)
            {
                return false;
            }

            return location.Equals(o.location) && filename.Equals(o.filename);
        }

        public Location location;
        public string filename;
    }

    //文件位置 三个位置 初始化一般为StreamingAssets Download为持久数据位置PersisentDataPath Update位于持久数据位置的缓冲文件夹
    public enum Location
    {
        Initial = 1,
        Download = 2,
        Update = 3,
    }

    //文件系统接口
    public interface IFileSystem
    {
        bool Exists(Location location, string filename);

        void Delete(Location location, string filename);
        long GetLength(Location location, string filename);

        void Copy(Location location, string filename, Location location2, string filename2);

        string[] GetFiles(Location location);

        Stream Open(Location location, string filename, FileMode mode, FileAccess access);
    }
}