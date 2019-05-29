using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
namespace ResourceMoudle
{
    public enum EventId
    {
        DownloadCheck,
        DownloadUptodate,
        DownloadReady,
        DownloadFinished,

        ApplyCheck,
        ApplyReady,
        ApplyFinished,
        ApplyFailed,

        Quit,

        TransferBegin,
        TransferEnd,

        PatchBegin,
        PatchEnd,

        GenerateListsBegin,
        GenerateListsEnd,

        RequestSignaturesBegin,
        RequestSignaturesEnd,

        VerifyDownloadedDataBegin,
        VerifyDownloadedDataEnd,

        VerifyLocalFilesBegin,
        VerifyLocalFilesEnd,
    }

    public enum ControlFlag
    {
        Continue,
        Wait,
        Quit
    }

    public enum VerifyMode
    {
        Relaxed,
        Strict,
    }

    public interface EventListener
    {
        void OnEvent(EventId id);
    }

    class OutPara<T>
    {
        public T para;
    }


    public class UpdateManager : MonoBehaviour
    {
        //文件系统
        IFileSystem _fileSystem;
        //网络对象
        IHttp _http;
        //事件监听
        EventListener _eventListener;

        //存放更新信息的文件路径
        FileLocator _infoFileLocator = new FileLocator(Location.Update, "info");

        //存放更新数据的文件路径
        FileLocator _dataFileLocator = new FileLocator(Location.Update, "data");

        //应用数据路径
        FileLocator _applyFileLocator = new FileLocator(Location.Download, ".apply");

        //更新错误信息
        public string ErrorMessage { get; private set; }
        //当前状态进度
        public float Progress { get; private set; }
        //版本更新总下载量
        public long DownloadSize { get; private set; }
        //已经下载的大小 版本更新的本地缓存量
        public long BytesDownloaded { get; private set; }
        //本次更新已经传输的数据量
        public long BytesTransferred { get; private set; }

        public int NumberOfSuccessfulRequests { get; private set; }
        public int NumberOfFailedRequests { get; private set; }

        //状态控制参数
        public ControlFlag ControlFlag { get; set; }

        //更新的校验模式
        public VerifyMode VerifyMode { get; set; }
        //下载协程数
        public int Concurrent { get; set; }

        public static UpdateManager Create(GameObject where, IFileSystem fileSystem, IHttp http, EventListener eventListener)
        {
            UpdateManager manager = where.AddComponent<UpdateManager>();

            manager._fileSystem = fileSystem;
            manager._http = http;
            manager._eventListener = eventListener;

            manager.Progress = 0.0f;

            manager.DownloadSize = 0;
            manager.BytesDownloaded = 0;

            manager.BytesTransferred = 0;

            manager.NumberOfSuccessfulRequests = 0;
            manager.NumberOfFailedRequests = 0;

            manager.ControlFlag = ControlFlag.Continue;

            manager.VerifyMode = VerifyMode.Relaxed;

            manager.Concurrent = 2;

            return manager;
        }

        public bool HasApplyFlag()
        {
            return FileExists(_applyFileLocator);
        }

        public void ClearApplyFlag()
        {
            if (FileExists(_applyFileLocator))
            {
                FileDelete(_applyFileLocator);
            }
        }

        public void StartDownloadUpdate(string url)
        {
            ErrorMessage = "";
            Progress = 0.0f;
            DownloadSize = 0;
            BytesDownloaded = 0;
            BytesTransferred = 0;
            NumberOfSuccessfulRequests = 0;
            NumberOfFailedRequests = 0;
            ControlFlag = ControlFlag.Continue;

            StartCoroutine(DownloadUpdate(url));
        }

        public void StartApplyUpdate()
        {
            ErrorMessage = "";
            Progress = 0.0f;
            ControlFlag = ControlFlag.Continue;

            StartCoroutine(ApplyUpdate());
        }

        IEnumerator ApplyUpdate()
        {
            _eventListener.OnEvent(EventId.ApplyCheck);

            UpdateInfo info = VerifyUtility.LoadObjectFromFile(_fileSystem, _infoFileLocator) as UpdateInfo;

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            if (info == null)
            {
                ErrorMessage = "bad update info";

                _eventListener.OnEvent(EventId.ApplyFailed);
                yield break;
            }

            List<FileLocator> retainedList = info.GetRetainedList();

            if (VerifyMode == VerifyMode.Strict)
            {
                OutPara<bool> result = new OutPara<bool>();

                yield return VerifyDownloadedDataFile(info, _dataFileLocator, result);

                while (ControlFlag == ControlFlag.Wait)
                {
                    yield return null;
                }

                if (ControlFlag == ControlFlag.Quit)
                {
                    _eventListener.OnEvent(EventId.Quit);
                    yield break;
                }

                if (!result.para)
                {
                    ErrorMessage = "bad update data";

                    _eventListener.OnEvent(EventId.ApplyFailed);
                    yield break;
                }

                if (retainedList.Count > 0)
                {
                    Progress = 0.0f;
                    _eventListener.OnEvent(EventId.VerifyLocalFilesBegin);

                    for (int i = 0; i < retainedList.Count; ++i)
                    {
                        if (!FileExists(retainedList[i]))
                        {
                            ErrorMessage = "file missing";

                            _eventListener.OnEvent(EventId.ApplyFailed);
                            yield break;
                        }

                        string remoteSign = info.FindInRemoteList(retainedList[i].filename).hash;

                        OutPara<string> fileHash = new OutPara<string>();

                        yield return GetFileHashString(retainedList[i], fileHash);

                        while (ControlFlag == ControlFlag.Wait)
                        {
                            yield return null;
                        }

                        if (ControlFlag == ControlFlag.Quit)
                        {
                            _eventListener.OnEvent(EventId.Quit);
                            yield break;
                        }

                        if (fileHash.para != remoteSign)
                        {
                            ErrorMessage = "file corrupted";

                            _eventListener.OnEvent(EventId.ApplyFailed);
                            yield break;
                        }

                        Progress = MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, retainedList.Count);
                        yield return null;
                    }

                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        _eventListener.OnEvent(EventId.Quit);
                        yield break;
                    }

                    Progress = 1.0f;
                    _eventListener.OnEvent(EventId.VerifyLocalFilesEnd);
                }
            }

            _eventListener.OnEvent(EventId.ApplyReady);

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            FileCreate(_applyFileLocator).Close();

            Progress = 0.0f;
            _eventListener.OnEvent(EventId.PatchBegin);

            using (Stream dstream = FileOpenRead(_dataFileLocator))
            {
                byte[] buffer = new byte[Setting.chunkSize];

                int[] fileIdList = info.GetFileIds();

                for (int i = 0; i < fileIdList.Length; ++i)
                {
                    int fileId = fileIdList[i];

                    Signature sign = info.GetSignature(fileId);

                    if (sign == null)
                    {
                        continue;
                    }

                    string name = info.GetFileName(fileId);

                    using (Stream stream = FileCreate(Location.Download, name))
                    {
                        int j = 0;

                        for (int chunk = 0; chunk < sign.hashes.Length; ++chunk)
                        {
                            while (ControlFlag == ControlFlag.Wait)
                            {
                                yield return null;
                            }

                            if (ControlFlag == ControlFlag.Quit)
                            {
                                _eventListener.OnEvent(EventId.Quit);
                                yield break;
                            }

                            dstream.Read(buffer, 0, Setting.chunkSize);

                            if (chunk == sign.hashes.Length - 1)
                            {
                                stream.Write(buffer, 0, sign.length % Setting.chunkSize);
                            }
                            else
                            {
                                stream.Write(buffer, 0, Setting.chunkSize);
                            }

                            if (++j % 10 == 0)
                            {
                                yield return null;
                            }
                        }

                        if (VerifyMode == VerifyMode.Strict)
                        {
                            stream.Close();

                            OutPara<string> fileHash = new OutPara<string>();

                            yield return GetFileHashString(Location.Download, name, fileHash);

                            while (ControlFlag == ControlFlag.Wait)
                            {
                                yield return null;
                            }

                            if (ControlFlag == ControlFlag.Quit)
                            {
                                _eventListener.OnEvent(EventId.Quit);
                                yield break;
                            }

                            if (fileHash.para != info.FindInRemoteList(name).hash)
                            {
                                ErrorMessage = "write error";

                                _eventListener.OnEvent(EventId.ApplyFailed);
                                yield break;
                            }
                        }
                    }

                    Progress = MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, fileIdList.Length);
                    yield return null;
                }
            }

            foreach (var f in info.GetRemovedList())
            {
                if (FileExists(Location.Download, f))
                {
                    FileDelete(Location.Download, f);
                }
            }

            List<FileItem> list = new List<FileItem>();

            foreach (var fi in info.GetRemoteList())
            {
                if (!retainedList.Any(x => x.filename.Equals(fi.name) && x.location.Equals(Location.Initial)))
                {
                    list.Add(fi);
                }
            }

            WriteList(FileCreate(Location.Download, "_list"), list);

            FileDelete(_applyFileLocator);

            VerifyUtility.DeleteObject(_fileSystem, _infoFileLocator);
            FileDelete(_dataFileLocator);

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            Progress = 1.0f;
            _eventListener.OnEvent(EventId.PatchEnd);

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            _eventListener.OnEvent(EventId.ApplyFinished);
        }

        IEnumerator DownloadUpdate(string url)
        {
            _eventListener.OnEvent(EventId.DownloadCheck);

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            if (url.EndsWith("/"))
            {
                url = url.Remove(url.Length - 1);
            }

            OutPara<IHttpRequest> req = new OutPara<IHttpRequest>();

            yield return HttpGet(_http, url + "/_list", req);

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            List<FileItem> remoteList = ParseList(req.para.Text());

            List<FileItem> localListInitial = new List<FileItem>();
            List<FileItem> localListDownload = new List<FileItem>();

            yield return GenerateLists(localListInitial, localListDownload);

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            List<FileLocator> retainedList = new List<FileLocator>();
            List<string> downloadList = new List<string>();
            List<string> removedList = new List<string>();

            foreach (var fi in remoteList)
            {
                if (localListInitial.Contains(fi))
                {
                    retainedList.Add(new FileLocator(Location.Initial, fi.name));
                }
                else if (localListDownload.Contains(fi))
                {
                    retainedList.Add(new FileLocator(Location.Download, fi.name));
                }
                else
                {
                    downloadList.Add(fi.name);
                }
            }

            foreach (var fi in remoteList)
            {
                if (retainedList.Any(x => x.filename.Equals(fi.name) && x.location.Equals(Location.Initial)))
                {
                    if (FileExists(Location.Download, fi.name))
                    {
                        removedList.Add(fi.name);
                    }
                }
            }

            foreach (string f in GetFiles(Location.Download))
            {
                if (f == "_list")
                {
                    continue;
                }

                if (!remoteList.Any(x => x.name.Equals(f)))
                {
                    removedList.Add(f);
                }
            }

            if (downloadList.Count == 0 && removedList.Count == 0)
            {
                _eventListener.OnEvent(EventId.DownloadUptodate);
                yield break;
            }

            bool resume = false;

            UpdateInfo info = VerifyUtility.LoadObjectFromFile(_fileSystem, _infoFileLocator) as UpdateInfo;

            if (info != null)
            {
                if (!info.VerifyRemoteList(remoteList) || !info.VerifyRetainedList(retainedList))
                {
                    info = null;
                }
                else
                {
                    resume = true;
                }
            }

            if (info == null)
            {
                info = new UpdateInfo();

                info.SetRemoteList(remoteList);
                info.SetRetainedList(retainedList);
                info.SetRemovedList(removedList);

                for (int i = 0; i < remoteList.Count; ++i)
                {
                    info.RegisterFile(remoteList[i].name, i);
                }

                if (downloadList.Count > 0)
                {
                    Progress = 0.0f;
                    _eventListener.OnEvent(EventId.RequestSignaturesBegin);

                    for (int i = 0; i < downloadList.Count; ++i)
                    {
                        yield return HttpGet(_http, url + "/" + downloadList[i] + ".signature", req);

                        while (ControlFlag == ControlFlag.Wait)
                        {
                            yield return null;
                        }

                        if (ControlFlag == ControlFlag.Quit)
                        {
                            _eventListener.OnEvent(EventId.Quit);
                            yield break;
                        }

                        int fileId = info.GetFileId(downloadList[i]);

                        Signature sign = ReadSignature(req.para.Data());

                        info.RegisterSignature(fileId, sign);

                        Progress = MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, downloadList.Count);
                        yield return null;
                    }

                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        _eventListener.OnEvent(EventId.Quit);
                        yield break;
                    }

                    Progress = 1.0f;
                    _eventListener.OnEvent(EventId.RequestSignaturesEnd);
                }

                VerifyUtility.SaveObjectToFile(_fileSystem, _infoFileLocator, info);
            }

            foreach (int fileId in info.GetFileIds())
            {
                Signature sign = info.GetSignature(fileId);

                if (sign != null)
                {
                    DownloadSize += sign.length;
                }
            }

            List<Pair<int, int>> chunkList = CreateDownloadChunkList(info);

            int imax = chunkList.Count;
            int finished = 0;

            Stream stream = null;
            try
            {
                if (resume && FileExists(_dataFileLocator))
                {
                    stream = FileOpenReadWrite(_dataFileLocator.location, _dataFileLocator.filename);

                    OutPara<int> count = new OutPara<int>();
                    OutPara<long> length = new OutPara<long>();

                    yield return VerifyDownloadedData(info, stream, chunkList, count, length);

                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        _eventListener.OnEvent(EventId.Quit);
                        yield break;
                    }

                    chunkList.RemoveRange(0, count.para);
                    finished += count.para;

                    BytesDownloaded = length.para;

                    Progress = MathUtility.CalculateProgress(0.0f, 1.0f, imax - chunkList.Count, imax);
                }
                else
                {
                    stream = FileCreate(_dataFileLocator.location, _dataFileLocator.filename);

                    Progress = 0.0f;
                }

                _eventListener.OnEvent(EventId.DownloadReady);

                while (ControlFlag == ControlFlag.Wait)
                {
                    yield return null;
                }

                if (ControlFlag == ControlFlag.Quit)
                {
                    _eventListener.OnEvent(EventId.Quit);
                    yield break;
                }

                if (BytesDownloaded < DownloadSize)
                {
                    _eventListener.OnEvent(EventId.TransferBegin);

                    TaskManager taskManager = new TaskManager(this, Concurrent);

                    int lastFileId = -1;
                    string lastFileName = null;
                    string lastUrl = null;
                    Signature lastSign = null;

                    long position = finished * Setting.chunkSize;

                    byte[] padding = new byte[Setting.chunkSize];

                    while (finished < imax)
                    {
                        while (ControlFlag == ControlFlag.Wait)
                        {
                            yield return null;
                        }

                        if (ControlFlag == ControlFlag.Quit)
                        {
                            taskManager.Cancel = true;

                            while (taskManager.Count > 0)
                            {
                                taskManager.Update();
                                yield return null;
                            }

                            _eventListener.OnEvent(EventId.Quit);
                            yield break;
                        }

                        if (taskManager.PendingCount < Concurrent && chunkList.Count > 0)
                        {
                            Pair<int, int> p = chunkList.First();

                            int fileId = p.first;
                            int chunk = p.second;

                            if (fileId != lastFileId)
                            {
                                lastFileName = info.GetFileName(p.first);
                                lastUrl = url + "/" + lastFileName;
                                lastSign = info.GetSignature(fileId);
                                lastFileId = fileId;
                            }

                            int offset = Setting.chunkSize * chunk;
                            int length = Setting.chunkSize;

                            if (chunk == lastSign.hashes.Length - 1)
                            {
                                length = lastSign.length % Setting.chunkSize;
                            }

                            Task task = new HttpRequestTask(_http, lastUrl, offset, length);

                            task.userData = position;

                            taskManager.Add(task);

                            position += Setting.chunkSize;

                            chunkList.RemoveAt(0);

                            continue;
                        }

                        foreach (Task task in taskManager.FetchFinishedTasks())
                        {
                            stream.Seek((long)task.userData, SeekOrigin.Begin);

                            byte[] data = ((IHttpRequest)task.result).Data();

                            stream.Write(data, 0, data.Length);

                            if (data.Length < Setting.chunkSize)
                            {
                                stream.Write(padding, 0, Setting.chunkSize - data.Length);
                            }

                            BytesDownloaded += data.Length;
                            BytesTransferred += data.Length;

                            ++NumberOfSuccessfulRequests;
                            ++finished;
                        }

                        foreach (Task task in taskManager.FetchFailedTasks())
                        {
                            Debug.LogWarning(task.errorMessage);

                            taskManager.Add(task);

                            ++NumberOfFailedRequests;
                        }

                        taskManager.Update();

                        Progress = MathUtility.CalculateProgress(0.0f, 1.0f, finished, imax);
                        yield return null;
                    }

                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        _eventListener.OnEvent(EventId.Quit);
                        yield break;
                    }

                    Progress = 1.0f;
                    _eventListener.OnEvent(EventId.TransferEnd);
                }
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            while (ControlFlag == ControlFlag.Wait)
            {
                yield return null;
            }

            if (ControlFlag == ControlFlag.Quit)
            {
                _eventListener.OnEvent(EventId.Quit);
                yield break;
            }

            _eventListener.OnEvent(EventId.DownloadFinished);
        }

        IEnumerator HttpGet(IHttp http, string url, OutPara<IHttpRequest> req)
        {
            for (; ; )
            {
                while (ControlFlag == ControlFlag.Wait)
                {
                    yield return null;
                }

                if (ControlFlag == ControlFlag.Quit)
                {
                    yield break;
                }

                req.para = http.Get(url);

                yield return req.para.Send();

                if (req.para.IsError())
                {
                    ++NumberOfFailedRequests;
                    Debug.LogWarning(req.para.ErrorMessage());
                    continue;
                }

                ++NumberOfSuccessfulRequests;
                yield break;
            }
        }

        IEnumerator GetFileHashString(Location location, string name, OutPara<string> hash)
        {
            return GetFileHashString(new FileLocator(location, name), hash);
        }

        IEnumerator GetFileHashString(FileLocator fl, OutPara<string> hash)
        {
            using (Stream stream = IOUtility.OpenRead(_fileSystem, fl))
            {
                MD5 md5 = MD5.Create();

                byte[] buffer = new byte[65536];
                int n;
                int i = 0;

                while ((n = IOUtility.ReadFile(stream, buffer, buffer.Length)) > 0)
                {
                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        yield break;
                    }

                    md5.TransformBlock(buffer, 0, n, null, 0);

                    if (++i % 10 == 0)
                    {
                        yield return null;
                    }
                }

                md5.TransformFinalBlock(buffer, 0, 0);

                hash.para = VerifyUtility.HashToString(md5.Hash);
            }
        }

        IEnumerator VerifyDownloadedDataFile(UpdateInfo info, FileLocator dataFileLocator, OutPara<bool> result)
        {
            if (FileExists(dataFileLocator))
            {
                using (Stream stream = FileOpenRead(dataFileLocator))
                {
                    List<Pair<int, int>> chunkList = CreateDownloadChunkList(info);

                    OutPara<int> count = new OutPara<int>();
                    OutPara<long> length = new OutPara<long>();

                    yield return VerifyDownloadedData(info, stream, chunkList, count, length);

                    result.para = (count.para == chunkList.Count);
                }
            }
            else
            {
                result.para = false;
            }
        }

        IEnumerator VerifyDownloadedData(UpdateInfo info, Stream stream, List<Pair<int, int>> downloadChunkList, OutPara<int> count, OutPara<long> length)
        {
            Progress = 0.0f;
            _eventListener.OnEvent(EventId.VerifyDownloadedDataBegin);

            List<Pair<int, uint>> hashList = CreateDownloadChunkHashList(info, downloadChunkList);

            byte[] buffer = new byte[Setting.chunkSize];

            count.para = 0;

            try
            {
                for (int i = 0; i < downloadChunkList.Count; ++i)
                {
                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        yield break;
                    }

                    if (IOUtility.ReadFile(stream, buffer, Setting.chunkSize) != Setting.chunkSize)
                    {
                        stream.Seek(count.para * Setting.chunkSize, SeekOrigin.Begin);
                        yield break;
                    }

                    uint hash = VerifyUtility.CalculateHash(buffer, hashList[i].first);

                    if (hash != hashList[i].second)
                    {
                        stream.Seek(count.para * Setting.chunkSize, SeekOrigin.Begin);
                        yield break;
                    }

                    count.para++;
                    length.para += hashList[i].first;

                    if (i % 10 == 0)
                    {
                        Progress = MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, downloadChunkList.Count);
                        yield return null;
                    }
                }
            }
            finally
            {
                Progress = 1.0f;
                _eventListener.OnEvent(EventId.VerifyDownloadedDataEnd);
            }
        }

        List<Pair<int, int>> CreateDownloadChunkList(UpdateInfo info)
        {
            List<Pair<int, int>> result = new List<Pair<int, int>>();

            foreach (int fileId in info.GetFileIds())
            {
                Signature sign = info.GetSignature(fileId);

                if (sign == null)
                {
                    continue;
                }

                for (int i = 0; i < sign.hashes.Length; ++i)
                {
                    result.Add(VerifyUtility.MakePair(fileId, i));
                }
            }

            return result;
        }

        List<Pair<int, uint>> CreateDownloadChunkHashList(UpdateInfo info, List<Pair<int, int>> downloadChunkList)
        {
            List<Pair<int, uint>> result = new List<Pair<int, uint>>();

            int lastFileId = -1;
            Signature lastSign = null;

            foreach (Pair<int, int> p in downloadChunkList)
            {
                int fileId = p.first;
                int chunk = p.second;

                if (fileId != lastFileId)
                {
                    lastSign = info.GetSignature(fileId);
                    lastFileId = fileId;
                }

                int length = Setting.chunkSize;

                if (chunk == lastSign.hashes.Length - 1)
                {
                    length = lastSign.length % Setting.chunkSize;
                }

                result.Add(VerifyUtility.MakePair(length, lastSign.hashes[chunk]));
            }

            return result;
        }

        Signature ReadSignature(byte[] buffer)
        {
            Signature sign = new Signature();

            int index = 0;

            sign.length = IOUtility.ReadInt(buffer, index);
            index += sizeof(int);

            int count = sign.length / Setting.chunkSize;

            if (sign.length % Setting.chunkSize != 0)
            {
                count += 1;
            }

            sign.hashes = new uint[count];

            for (int i = 0; i < count; ++i)
            {
                sign.hashes[i] = IOUtility.ReadUInt(buffer, index);
                index += sizeof(uint);
            }

            return sign;
        }

        IEnumerator GenerateLists(List<FileItem> localListInitial, List<FileItem> localListDownload)
        {
            Progress = 0.0f;
            _eventListener.OnEvent(EventId.GenerateListsBegin);

            if (FileExists(Location.Initial, "_list"))
            {
                localListInitial.AddRange(ParseList(FileOpenRead(Location.Initial, "_list")));
            }

            if (VerifyMode == VerifyMode.Strict)
            {
                List<string> files = GetFiles(Location.Download);

                for (int i = 0; i < files.Count; ++i)
                {
                    if (files[i] == "_list")
                    {
                        continue;
                    }

                    OutPara<string> fileHash = new OutPara<string>();

                    yield return GetFileHashString(Location.Download, files[i], fileHash);

                    while (ControlFlag == ControlFlag.Wait)
                    {
                        yield return null;
                    }

                    if (ControlFlag == ControlFlag.Quit)
                    {
                        yield break;
                    }

                    localListDownload.Add(new FileItem(files[i], fileHash.para));

                    Progress = MathUtility.CalculateProgress(0.0f, 1.0f, i + 1, files.Count);
                    yield return null;
                }
            }
            else
            {
                if (FileExists(Location.Download, "_list"))
                {
                    localListDownload.AddRange(ParseList(FileOpenRead(Location.Download, "_list")));
                }
            }

            Progress = 1.0f;
            _eventListener.OnEvent(EventId.GenerateListsEnd);
        }

        List<string> GetFiles(Location location)
        {
            return _fileSystem.GetFiles(location).ToList();
        }

        void FileDelete(Location location, string name)
        {
            _fileSystem.Delete(location, name);
        }

        void FileDelete(FileLocator fl)
        {
            _fileSystem.Delete(fl.location, fl.filename);
        }

        bool FileExists(Location location, string filename)
        {
            return _fileSystem.Exists(location, filename);
        }

        bool FileExists(FileLocator fl)
        {
            return _fileSystem.Exists(fl.location, fl.filename);
        }

        Stream FileOpenRead(Location location, string filename)
        {
            return _fileSystem.Open(location, filename, FileMode.Open, FileAccess.Read);
        }

        Stream FileOpenRead(FileLocator fl)
        {
            return FileOpenRead(fl.location, fl.filename);
        }

        Stream FileOpenReadWrite(Location location, string filename)
        {
            return _fileSystem.Open(location, filename, FileMode.Open, FileAccess.ReadWrite);
        }

        Stream FileCreate(Location location, string filename)
        {
            return _fileSystem.Open(location, filename, FileMode.Create, FileAccess.Write);
        }

        Stream FileCreate(FileLocator fl)
        {
            return FileCreate(fl.location, fl.filename);
        }

        List<FileItem> ParseList(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                return ParseList(reader);
            }
        }

        List<FileItem> ParseList(string text)
        {
            using (StringReader reader = new StringReader(text))
            {
                return ParseList(reader);
            }
        }

        List<FileItem> ParseList(TextReader reader)
        {
            List<FileItem> result = new List<FileItem>();

            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] v = line.Split('/');
                result.Add(new FileItem(v[0], v[1]));
            }

            return result;
        }

        void WriteList(Stream stream, List<FileItem> list)
        {
            using (TextWriter writer = new StreamWriter(stream))
            {
                foreach (var fi in list)
                {
                    writer.Write(fi.name + "/" + fi.hash + "\r\n");
                }
            }
        }

    }
}