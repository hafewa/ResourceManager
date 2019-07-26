using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ResourceMoudle
{
    public class VerifyUtility
    {
        public static Pair<T1, T2> MakePair<T1, T2>(T1 a, T2 b)
        {
            return new Pair<T1, T2>(a, b);
        }

        public static Triple<T1, T2, T3> MakeTriple<T1, T2, T3>(T1 a, T2 b, T3 c)
        {
            return new Triple<T1, T2, T3>(a, b, c);
        }

        public static T[] SubArray<T>(T[] array, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(array, index, result, 0, length);
            return result;
        }

        static public void SaveObjectToFile(IFileSystem fs, FileLocator fl, object obj)
        {
            using (Stream stream = IOUtility.Create(fs, fl))
            {
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(stream, obj);
            }
            SignFile(fs, fl, new FileLocator(fl.location, fl.filename + ".sign"));
        }

        static public object LoadObjectFromFile(IFileSystem fs, FileLocator fl)
        {
            object obj = null;

            if (VerifyFile(fs, fl, new FileLocator(fl.location, fl.filename + ".sign")))
            {
                using (Stream stream = IOUtility.OpenRead(fs, fl))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    obj = formatter.Deserialize(stream);
                }
            }

            return obj;
        }

        static public void DeleteObject(IFileSystem fs, FileLocator fl)
        {
            fs.Delete(fl.location, fl.filename);
            fs.Delete(fl.location, fl.filename + ".sign");
            
        }

        public static void SignFile(IFileSystem fs, FileLocator fl, FileLocator fl2)
        {
            using (Stream stream = IOUtility.Create(fs, fl2))
            {
                byte[] hash = SignFile(fs, fl);

                IOUtility.WriteFile(stream, hash);
            }
        }

        public static byte[] SignFile(IFileSystem fs, FileLocator fl)
        {
            using (Stream stream = IOUtility.OpenRead(fs, fl))
            {
                return MD5.Create().ComputeHash(stream);
            }
        }

        public static bool VerifyFile(IFileSystem fs, FileLocator fl, FileLocator fl2)
        {
            try
            {
                using (Stream stream = IOUtility.OpenRead(fs, fl2))
                {
                    byte[] hash = new byte[16];

                    if (IOUtility.ReadFile(stream, hash, 16) != 16)
                    {
                        return false;
                    }

                    return VerifyFile(fs, fl, hash);
                }
            }
            catch (System.Exception)
            {
            }

            return false;
        }

        public static bool VerifyFile(IFileSystem fs, FileLocator fl, byte[] hash)
        {
            try
            {
                using (Stream stream = IOUtility.OpenRead(fs, fl))
                {
                    return MD5.Create().ComputeHash(stream).SequenceEqual(hash);
                }
            }
            catch (System.Exception)
            {
            }

            return false;
        }

        public static string HashToString(byte[] hash)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hash.Length; ++i)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }

        public static uint CalculateHash(byte[] buf, int len = -1, uint seed = 0)
        {
            uint hash = XXHash32.CalculateHash(buf, len, seed);
            return hash;
        }

    }
}