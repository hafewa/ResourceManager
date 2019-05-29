using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//队列池 对象生产接口 位于逻辑层和资源系统层之间
public class QueuePool<T>  where T : IMemoryHosting<T> ,new() {

    private Queue<T> m_Queue = new Queue<T>();
    private HashSet<T>  m_HashSet =  new HashSet<T>();

    public T New()
    {
        T obj;
        if (m_Queue.Count == 0)
        {
            obj = new T();
        }
        else
        {
            obj = m_Queue.Dequeue();
            m_HashSet.Remove(obj);
        }
        obj.InitMemory();
        return obj;
    }

    public void Detele(ref T obj)
    {
        if(m_HashSet.Contains(obj)==false)
        {
            m_Queue.Enqueue(obj);
            m_HashSet.Add(obj);
            obj.ClearMemory();
        }
    }

    public void Clear()
    {
        m_Queue.Clear();
        m_HashSet.Clear();
    }

}
