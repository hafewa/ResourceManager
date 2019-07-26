using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ResourceMoudle
{
    public enum TaskStatus
    {
        Start,
        Finished,
        Failed,
        Cancelled,
    }

    abstract public class Task
    {
        public TaskStatus status = TaskStatus.Start;
        public object result = null;
        public string errorMessage = "";
        public object userData;
        public bool cancel = false;

        public abstract IEnumerator Run();
    }

    public class TaskManager
    {
        //运行协程的mono
        MonoBehaviour _parent;
        //协程数量
        int _concurrent;
        //追加的任务列表
        List<Task> _pending = new List<Task>();
        //运行的任务列表
        List<Task> _running = new List<Task>();
        //完成的任务列表
        List<Task> _finished = new List<Task>();
        //失败的任务列表
        List<Task> _failed = new List<Task>();
        //取消的任务列表
        List<Task> _cancelled = new List<Task>();

        public TaskManager(MonoBehaviour parent, int concurrent)
        {
            _parent = parent;
            _concurrent = concurrent;
        }
        //追加的任务数
        public int PendingCount
        {
            get
            {
                return _pending.Count;
            }
        }
        //运行中任务数
        public int RunningCount
        {
            get
            {
                return _running.Count;
            }
        }
        //当前任务总数
        public int Count
        {
            get
            {
                return _pending.Count + _running.Count;
            }
        }
        //当前状态是否是取消执行任务
        public bool Cancel { get; set; }

        public void CancelTasks()
        {
            Cancel = true;
        }

        public void ResumeTasks()
        {
            _pending.AddRange(_cancelled);
            _cancelled.Clear();
            Cancel = false;
        }

        public void Add(Task task)
        {
            _pending.Add(task);
        }

        public void Update()
        {
            int index = 0;

            while (index < _running.Count)
            {
                Task task = _running[index];

                if (Cancel && !task.cancel)
                {
                    task.cancel = true;
                }

                if (task.status == TaskStatus.Finished)
                {
                    _running.RemoveAt(index);
                    _finished.Add(task);
                    continue;
                }
                else if (task.status == TaskStatus.Failed)
                {
                    _running.RemoveAt(index);
                    _failed.Add(task);
                    continue;
                }
                else if (task.status == TaskStatus.Cancelled)
                {
                    _running.RemoveAt(index);
                    _cancelled.Add(task);
                    continue;
                }

                ++index;
            }

            if (Cancel && _pending.Count > 0)
            {
                _cancelled.AddRange(_pending);
                _pending.Clear();
                return;
            }

            while (_running.Count < _concurrent && _pending.Count > 0)
            {
                Task task = _pending.First();

                _parent.StartCoroutine(task.Run());

                _running.Add(task);
                _pending.RemoveAt(0);
            }
        }

        //取出完成的任务列表 同时清空管理器完成任务列表
        public List<Task> FetchFinishedTasks()
        {
            List<Task> finished = _finished.ToList();

            _finished.Clear();

            return finished;
        }
        //取出失败的任务列表 同时清空管理器失败任务列表
        public List<Task> FetchFailedTasks()
        {
            List<Task> failed = _failed.ToList();

            _failed.Clear();

            return failed;
        }
        //取出取消的任务列表 同时清空管理器取消任务列表
        public List<Task> FetchCancelledTasks()
        {
            List<Task> cancelled = _cancelled.ToList();

            _cancelled.Clear();

            return cancelled;
        }
    }
}