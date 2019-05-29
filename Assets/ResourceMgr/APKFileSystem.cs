using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ResourceMoudle.APKStruct;
using System.IO;
using System.Linq;

namespace ResourceMoudle
{
    public class APKFileSystem
    {
        public APKFileSystem(string basePath = "")
        {
            basePath = basePath.Replace('\\', '/');

            if (basePath.StartsWith("/"))
            {
                basePath.Remove(0, 1);
            }

            if (basePath.Length > 0 && !basePath.EndsWith("/"))
            {
                basePath += "/";
            }

            fileInfos_ = new Dictionary<string, APKFileInfo>(StringComparer.OrdinalIgnoreCase);

            Stream stream = OpenAPK();

            AndroidIOUtility.GetFileInfosFromAPK(stream, basePath, fileInfos_);

            stream.Close();
        }

        public bool Exists(string filename)
        {
            return fileInfos_.ContainsKey(filename);
        }

        public string[] GetFiles()
        {
            return fileInfos_.Keys.ToArray();
        }

        public long GetLength(string filename)
        {
            APKFileInfo fi;

            if (!fileInfos_.TryGetValue(filename, out fi))
            {
                throw new FileNotFoundException(filename);
            }

            return fi.size;
        }

        public Stream OpenRead(string filename)
        {
            APKFileInfo fi;

            if (!fileInfos_.TryGetValue(filename, out fi))
            {
                throw new FileNotFoundException(filename);
            }

            return new SubReadStream(OpenAPK(), fi.start, fi.size);
        }

        private FileStream OpenAPK()
        {
            return File.Open(Application.dataPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        Dictionary<string, APKFileInfo> fileInfos_;
    }
}