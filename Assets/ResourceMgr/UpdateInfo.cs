using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ResourceMoudle
{
    //资源更细信息
    [Serializable]
    class UpdateInfo
    {
        //远端文件列表
        public void SetRemoteList(List<FileItem> remoteList)
        {
            remoteList_ = remoteList;
        }

        public List<FileItem> GetRemoteList()
        {
            return remoteList_;
        }

        //校验远端文件签名
        public bool VerifyRemoteList(List<FileItem> remoteList)
        {
            return remoteList_.SequenceEqual(remoteList);
        }

        public void SetRetainedList(List<FileLocator> retainedList)
        {
            retainedList_ = retainedList;
        }

        public List<FileLocator> GetRetainedList()
        {
            return retainedList_;
        }

        public bool VerifyRetainedList(List<FileLocator> retainedList)
        {
            return retainedList_.SequenceEqual(retainedList);
        }

        public void SetRemovedList(List<string> removedList)
        {
            removedList_ = removedList;
        }

        public List<string> GetRemovedList()
        {
            return removedList_;
        }

        public FileItem FindInRemoteList(string name)
        {
            if (remoteListCache_ == null)
            {
                remoteListCache_ = new Dictionary<string, FileItem>();

                for (int i = 0; i < remoteList_.Count; ++i)
                {
                    remoteListCache_.Add(remoteList_[i].name, remoteList_[i]);
                }
            }

            FileItem fi;
            remoteListCache_.TryGetValue(name, out fi);

            return fi;
        }

        public int[] GetFileIds()
        {
            return fileIdToName_.Keys.OrderBy(p => p).ToArray();
        }

        public void RegisterFile(string name, int fileId)
        {
            fileNameToId_.Add(name, fileId);
            fileIdToName_.Add(fileId, name);
        }

        public int GetFileId(string name)
        {
            int fileId;
            fileNameToId_.TryGetValue(name, out fileId);

            return fileId;
        }

        public string GetFileName(int fileId)
        {
            string name;
            fileIdToName_.TryGetValue(fileId, out name);

            return name;
        }

        public Signature GetSignature(int fileId)
        {
            Signature result;
            fileIdToSign_.TryGetValue(fileId, out result);

            return result;
        }

        public void RegisterSignature(int fileId, Signature sign)
        {
            fileIdToSign_.Add(fileId, sign);
        }

        List<FileItem> remoteList_ = new List<FileItem>();
        List<FileLocator> retainedList_ = new List<FileLocator>();
        List<string> removedList_ = new List<string>();

        Dictionary<string, int> fileNameToId_ = new Dictionary<string, int>();
        Dictionary<int, string> fileIdToName_ = new Dictionary<int, string>();

        Dictionary<int, Signature> fileIdToSign_ = new Dictionary<int, Signature>();

        [NonSerialized]
        Dictionary<string, FileItem> remoteListCache_;
    }
}