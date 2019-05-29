using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace ResourceMoudle
{

    public class FileManager : IFileSystem
    {
        private APKFileSystem mAPKFileSystem;
        private string mInitialRoot;
        private string mUpdateRoot;
        private string mDownloadRoot;

        private static FileManager mInstance;
        public static FileManager Instance
        {
            get
            {
                if (mInstance == null) mInstance = new FileManager();
                return mInstance;
            }
        }

        private FileManager()
        {
            mInitialRoot = Path.Combine(Application.streamingAssetsPath, "bundles");
            mUpdateRoot = Path.Combine(Application.persistentDataPath, "update");
            mDownloadRoot = Path.Combine(Application.persistentDataPath, "download");
#if UNITY_ANDROID && !UNITY_EDITOR
            mAPKFileSystem = new APKFileSystem("bundles");
#endif
            if (!Directory.Exists(mDownloadRoot))
            {
                Directory.CreateDirectory(mDownloadRoot);
            }
            if (!Directory.Exists(mUpdateRoot))
            {
                Directory.CreateDirectory(mUpdateRoot);
            }
        }

        public void Copy(Location location, string filename, Location location2, string filename2)
        {
            Stream stream = Open(location, filename, FileMode.Open, FileAccess.Read);
            Stream stream2 = Open(location2, filename2, FileMode.Create, FileAccess.Write);

            byte[] buffer = new byte[Setting.chunkSize];

            int count;

            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                stream2.Write(buffer, 0, count);
            }

            stream.Close();
            stream2.Close();
        }

        public void Delete(Location location, string filename)
        {
            if (location != Location.Initial)
            {
                File.Delete(GetPath(location, filename));
            }
        }
        public long GetLength(Location location, string filename)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
        if (location == Location.Initial)
        {
            return mAPKFileSystem.GetLength(filename);
        }
#endif

            return IOUtility.GetFileSize(GetPath(location, filename));
        }

        public bool Exists(Location location, string filename)
        {
            if (location == Location.Initial && Application.platform == RuntimePlatform.Android)
            {
                return mAPKFileSystem.Exists(filename);
            }
            else
            {
                return File.Exists(GetPath(location, filename));
            }
        }

        public string[] GetFiles(Location location)
        {
            List<string> list = new List<string>();

            if (location == Location.Initial && Application.platform == RuntimePlatform.Android)
            {
                foreach (string path in mAPKFileSystem.GetFiles())
                {
                    if (!path.Contains("/"))
                    {
                        list.Add(path);
                    }
                }

                return list.ToArray();
            }
            else
            {
                foreach (string path in Directory.GetFiles(LocationToPath(location), "*.*", SearchOption.TopDirectoryOnly))
                {
                    list.Add(Path.GetFileName(path));
                }

                return list.ToArray();
            }
        }

        public Stream Open(Location location, string filename, FileMode mode, FileAccess access)
        {
            if (location == Location.Initial && Application.platform == RuntimePlatform.Android)
            {
                return mAPKFileSystem.OpenRead(filename);
            }
            else
            {
                if (access == FileAccess.Read)
                {
                    return File.OpenRead(GetPath(location, filename));
                }

                return File.Open(GetPath(location, filename), mode, access);
            }
        }

        public string GetPath(Location location, string filename)
        {
            return Path.Combine(LocationToPath(location), filename);
        }

        public string LocationToPath(Location location)
        {
            switch (location)
            {
                case Location.Initial:
                    return mInitialRoot;

                case Location.Download:
                    return mDownloadRoot;

                case Location.Update:
                    return mUpdateRoot;
            }

            throw new DirectoryNotFoundException();
        }

        //获取自定义文件的路径
        public string GetCustomFilePath(string filename)
        {
            if (Exists(Location.Download, filename))
            {
                return GetPath(Location.Download, filename);
            }
            else if (Exists(Location.Initial, filename))
            {
                return GetPath(Location.Initial, filename);
            }
            return string.Empty;
        }

        //打开自定义文件流
        public Stream OpenCustomFilePath(string filename, FileMode mode, FileAccess access)
        {
            if (Exists(Location.Download, filename))
            {
                return Open(Location.Download, filename, mode, access);
            }
            else if (Exists(Location.Initial, filename))
            {
                return Open(Location.Initial, filename, mode, access);
            }
            return null;
        }
    }
}