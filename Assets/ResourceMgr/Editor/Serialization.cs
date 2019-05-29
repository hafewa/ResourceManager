using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ResourceMoudle
{
    public class Serialization
    {
        public static void ReadFromStream(Stream stream, out Pair<string, List<string>> p)
        {
            var p2 = new Pair<string, List<string>>();

            ReadFromStream(stream, out p2.first);
            ReadFromStream(stream, out p2.second);

            p = p2;
        }

        public static void ReadFromStream(Stream stream, out Triple<string, long, long> t)
        {
            var t2 = new Triple<string, long, long>();

            ReadFromStream(stream, out t2.first);
            ReadFromStream(stream, out t2.second);
            ReadFromStream(stream, out t2.third);

            t = t2;
        }

        public static void ReadFromStream<T>(Stream stream, out List<T> l)
        {
            var f = typeof(Serialization).GetMethod("ReadFromStream", new Type[] { typeof(Stream), typeof(T).MakeByRefType() });

            var list = new List<T>();

            int count;

            ReadFromStream(stream, out count);

            for (int i = 0; i < count; ++i)
            {
                var p = new object[] { stream, null };

                f.Invoke(null, p);

                list.Add((T)p[1]);
            }

            l = list;
        }

        public static void ReadFromStream<T>(Stream stream, out HashSet<T> s)
        {
            var f = typeof(Serialization).GetMethod("ReadFromStream", new Type[] { typeof(Stream), typeof(T).MakeByRefType() });

            var set = new HashSet<T>();

            int count;

            ReadFromStream(stream, out count);

            for (int i = 0; i < count; ++i)
            {
                var p = new object[] { stream, null };

                f.Invoke(null, p);

                set.Add((T)p[1]);
            }

            s = set;
        }

        public static void ReadFromStream<K, V>(Stream stream, out Dictionary<K, V> d)
        {
            var f1 = typeof(Serialization).GetMethod("ReadFromStream", new Type[] { typeof(Stream), typeof(K).MakeByRefType() });
            var f2 = typeof(Serialization).GetMethod("ReadFromStream", new Type[] { typeof(Stream), typeof(V).MakeByRefType() });

            var dict = new Dictionary<K, V>();

            int count;

            ReadFromStream(stream, out count);

            for (int i = 0; i < count; ++i)
            {
                var p1 = new object[] { stream, null };
                var p2 = new object[] { stream, null };

                f1.Invoke(null, p1);
                f2.Invoke(null, p2);

                dict.Add((K)p1[1], (V)p2[1]);
            }

            d = dict;
        }

        public static void ReadFromStream(Stream stream, out string s)
        {
            byte[] buffer;

            ReadFromStream(stream, out buffer);

            s = Encoding.UTF8.GetString(buffer);
        }

        public static void ReadFromStream(Stream stream, out byte[] buffer)
        {
            int length;

            ReadFromStream(stream, out length);

            byte[] buf = new byte[length];

            if (IOUtility.ReadFile(stream, buf, length) != length)
            {
                throw new System.IO.IOException("Bad file");
            }

            buffer = buf;
        }

        public static void ReadFromStream(Stream stream, out int value)
        {
            byte[] buffer = new byte[sizeof(int)];

            if (IOUtility.ReadFile(stream, buffer, buffer.Length) != buffer.Length)
            {
                throw new System.IO.IOException("Bad file");
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            value = BitConverter.ToInt32(buffer, 0);
        }

        public static void ReadFromStream(Stream stream, out uint value)
        {
            byte[] buffer = new byte[sizeof(uint)];

            if (IOUtility.ReadFile(stream, buffer, buffer.Length) != buffer.Length)
            {
                throw new System.IO.IOException("Bad file");
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            value = BitConverter.ToUInt32(buffer, 0);
        }

        public static void ReadFromStream(Stream stream, out long value)
        {
            byte[] buffer = new byte[sizeof(long)];

            if (IOUtility.ReadFile(stream, buffer, buffer.Length) != buffer.Length)
            {
                throw new System.IO.IOException("Bad file");
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            value = BitConverter.ToInt64(buffer, 0);
        }

        public static void WriteToStream(Stream stream, Pair<string, List<string>> p)
        {
            WriteToStream(stream, p.first);
            WriteToStream(stream, p.second);
        }

        public static void WriteToStream(Stream stream, Triple<string, long, long> t)
        {
            WriteToStream(stream, t.first);
            WriteToStream(stream, t.second);
            WriteToStream(stream, t.third);
        }

        public static void WriteToStream<T>(Stream stream, List<T> l)
        {
            var f = typeof(Serialization).GetMethod("WriteToStream", new Type[] { typeof(Stream), typeof(T) });

            WriteToStream(stream, l.Count);

            foreach (var i in l)
            {
                Debug.Assert(i.GetType() == typeof(T));

                f.Invoke(null, new object[] { stream, i });
            }
        }

        public static void WriteToStream<T>(Stream stream, HashSet<T> s)
        {
            var f = typeof(Serialization).GetMethod("WriteToStream", new Type[] { typeof(Stream), typeof(T) });

            WriteToStream(stream, s.Count);

            foreach (var i in s)
            {
                Debug.Assert(i.GetType() == typeof(T));

                f.Invoke(null, new object[] { stream, i });
            }
        }

        public static void WriteToStream<K, V>(Stream stream, Dictionary<K, V> d)
        {
            var f1 = typeof(Serialization).GetMethod("WriteToStream", new Type[] { typeof(Stream), typeof(K) });
            var f2 = typeof(Serialization).GetMethod("WriteToStream", new Type[] { typeof(Stream), typeof(V) });

            WriteToStream(stream, d.Count);

            foreach (var p in d)
            {
                Debug.Assert(p.Key.GetType() == typeof(K));
                Debug.Assert(p.Value.GetType() == typeof(V));

                f1.Invoke(null, new object[] { stream, p.Key });
                f2.Invoke(null, new object[] { stream, p.Value });
            }
        }

        public static void WriteToStream(Stream stream, string s)
        {
            WriteToStream(stream, Encoding.UTF8.GetBytes(s));
        }

        public static void WriteToStream(Stream stream, byte[] buffer)
        {
            WriteToStream(stream, buffer.Length);
            IOUtility.WriteFile(stream, buffer);
        }

        public static void WriteToStream(Stream stream, int value)
        {
            IOUtility.WriteFile(stream, IOUtility.ToBigEndianByteArray(value));
        }

        public static void WriteToStream(Stream stream, uint value)
        {
            IOUtility.WriteFile(stream, IOUtility.ToBigEndianByteArray(value));
        }

        public static void WriteToStream(Stream stream, long value)
        {
            IOUtility.WriteFile(stream, IOUtility.ToBigEndianByteArray(value));
        }
    }
}

