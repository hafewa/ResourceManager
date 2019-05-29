using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ResourceMoudle
{
    namespace APKStruct
    {
        //安卓apk的数据组织结构单位 ENd of central director record EOCD，主要记录了中央目录大小、偏移量和ZIP注释信息等
        public class EOCD
        {
            public uint signature;
            public ushort numberOfThisDisk;
            public ushort numberOfTheDiskWithStartCD;
            public ushort numberOfCentralDirectoryRecords;
            public ushort totalNumberOfCentralDirectoryRecords;
            public uint sizeOfCentralDirectory;
            public uint offsetOfCentralDirectory;
            public byte[] comment;

            public static EOCD Read(BinaryReader reader)
            {
                EOCD result = new EOCD();

                if ((result.signature = reader.ReadUInt32()) != 0x06054b50)
                {
                    return null;
                }

                result.numberOfThisDisk = reader.ReadUInt16();
                result.numberOfTheDiskWithStartCD = reader.ReadUInt16();
                result.numberOfCentralDirectoryRecords = reader.ReadUInt16();
                result.totalNumberOfCentralDirectoryRecords = reader.ReadUInt16();
                result.sizeOfCentralDirectory = reader.ReadUInt32();
                result.offsetOfCentralDirectory = reader.ReadUInt32();

                ushort commentLength = reader.ReadUInt16();
                result.comment = reader.ReadBytes(commentLength);

                return result;
            }
        }

        public class Zip64EOCD
        {
            public uint signature;
            public ulong sizeOfThisRecord;
            public ushort versionMadeBy;
            public ushort versionNeededToExtract;
            public uint numberOfThisDisk;
            public uint numberOfTheDiskWithStartCD;
            public ulong numberOfCentralDirectoryRecords;
            public ulong totalNumberOfCentralDirectoryRecords;
            public ulong sizeOfCentralDirectory;
            public ulong offsetOfCentralDirectory;

            public static Zip64EOCD Read(BinaryReader reader)
            {
                Zip64EOCD result = new Zip64EOCD();

                if ((result.signature = reader.ReadUInt32()) != 0x06064b50)
                {
                    return null;
                }

                result.sizeOfThisRecord = reader.ReadUInt64();
                result.versionMadeBy = reader.ReadUInt16();
                result.versionNeededToExtract = reader.ReadUInt16();
                result.numberOfThisDisk = reader.ReadUInt32();
                result.numberOfTheDiskWithStartCD = reader.ReadUInt32();
                result.numberOfCentralDirectoryRecords = reader.ReadUInt64();
                result.totalNumberOfCentralDirectoryRecords = reader.ReadUInt64();
                result.sizeOfCentralDirectory = reader.ReadUInt64();
                result.offsetOfCentralDirectory = reader.ReadUInt64();

                return result;
            }
        }

        public class Zip64EOCDLocator
        {
            public uint signature;
            public uint numberOfDiskWithZip64EOCD;
            public ulong offsetOfZip64EOCD;
            public uint totalNumberOfDisks;

            public static Zip64EOCDLocator Read(BinaryReader reader)
            {
                Zip64EOCDLocator result = new Zip64EOCDLocator();

                if ((result.signature = reader.ReadUInt32()) != 0x07064b50)
                {
                    return null;
                }

                result.numberOfDiskWithZip64EOCD = reader.ReadUInt32();
                result.offsetOfZip64EOCD = reader.ReadUInt64();
                result.totalNumberOfDisks = reader.ReadUInt32();

                return result;
            }
        }
        //中央目录central directory file header
        public class CentralDirectoryFileHeader
        {
            public uint signature;
            public ushort versionMadeBy;
            public ushort versionNeededToExtract;
            public ushort generalPurposeBitFlag;
            public ushort compressionMethod;
            public uint lastModified;
            public uint crc32;
            public long compressedSize;
            public long uncompressedSize;
            public ushort filenameLength;
            public ushort extraFieldLength;
            public ushort fileCommentLength;
            public int diskNumberStart;
            public ushort internalFileAttributes;
            public uint externalFileAttributes;
            public long relativeOffsetOfLocalHeader;

            public byte[] filename;

            public static CentralDirectoryFileHeader Read(BinaryReader reader)
            {
                CentralDirectoryFileHeader result = new CentralDirectoryFileHeader();

                if ((result.signature = reader.ReadUInt32()) != 0x02014b50)
                {
                    return null;
                }

                result.versionMadeBy = reader.ReadUInt16();
                result.versionNeededToExtract = reader.ReadUInt16();
                result.generalPurposeBitFlag = reader.ReadUInt16();
                result.compressionMethod = reader.ReadUInt16();
                result.lastModified = reader.ReadUInt32();
                result.crc32 = reader.ReadUInt32();
                result.compressedSize = reader.ReadUInt32();
                result.uncompressedSize = reader.ReadUInt32();
                result.filenameLength = reader.ReadUInt16();
                result.extraFieldLength = reader.ReadUInt16();
                result.fileCommentLength = reader.ReadUInt16();
                result.diskNumberStart = reader.ReadUInt16();
                result.internalFileAttributes = reader.ReadUInt16();
                result.externalFileAttributes = reader.ReadUInt32();
                result.relativeOffsetOfLocalHeader = reader.ReadUInt32();
                result.filename = reader.ReadBytes(result.filenameLength);

                long end = reader.BaseStream.Position +
                    result.extraFieldLength + result.fileCommentLength;

                if (result.extraFieldLength > 4)
                {
                    ushort tag = reader.ReadUInt16();
                    ushort size = reader.ReadUInt16();

                    if (tag == 1)
                    {
                        if (size >= 8 && result.uncompressedSize == uint.MaxValue)
                        {
                            result.uncompressedSize = reader.ReadInt64();
                            size -= 8;
                        }

                        if (size >= 8 && result.compressedSize == uint.MaxValue)
                        {
                            result.compressedSize = reader.ReadInt64();
                            size -= 8;
                        }

                        if (size >= 8 && result.relativeOffsetOfLocalHeader == uint.MaxValue)
                        {
                            result.relativeOffsetOfLocalHeader = reader.ReadInt64();
                            size -= 8;
                        }

                        if (size >= 4 && result.diskNumberStart == ushort.MaxValue)
                        {
                            result.diskNumberStart = reader.ReadInt32();
                            size -= 4;
                        }
                    }
                }

                reader.BaseStream.Seek(end, SeekOrigin.Begin);

                return result;
            }
        }
        //压缩文件local file header
        public class LocalFileHeader
        {
            public uint signature;
            public ushort versionNeededToExtract;
            public ushort generalPurposeBitFlag;
            public ushort compressionMethod;
            public uint lastModified;
            public uint crc32;
            public uint compressedSize;
            public uint uncompressedSize;
            public ushort filenameLength;
            public ushort extraFieldLength;

            public static LocalFileHeader Read(BinaryReader reader)
            {
                LocalFileHeader result = new LocalFileHeader();

                if ((result.signature = reader.ReadUInt32()) != 0x04034b50)
                {
                    return null;
                }

                result.versionNeededToExtract = reader.ReadUInt16();
                result.generalPurposeBitFlag = reader.ReadUInt16();
                result.compressionMethod = reader.ReadUInt16();
                result.lastModified = reader.ReadUInt32();
                result.crc32 = reader.ReadUInt32();
                result.compressedSize = reader.ReadUInt32();
                result.uncompressedSize = reader.ReadUInt32();
                result.filenameLength = reader.ReadUInt16();
                result.extraFieldLength = reader.ReadUInt16();

                reader.BaseStream.Seek(result.filenameLength, SeekOrigin.Current);
                reader.BaseStream.Seek(result.extraFieldLength, SeekOrigin.Current);

                return result;
            }
        }

        public class APKFileInfo
        {
            public APKFileInfo(long start, long size)
            {
                this.start = start;
                this.size = size;
            }

            public long start;
            public long size;
        }
    }

    public class AndroidIOUtility
    {
        public static int ReadFileBackward(Stream stream, byte[] buffer, int count)
        {
            if (stream.Position <= 0)
            {
                return 0;
            }

            if (count > stream.Position)
            {
                count = (int)stream.Position;
            }

            stream.Seek(-count, SeekOrigin.Current);

            count = IOUtility.ReadFile(stream, buffer, count);

            stream.Seek(-count, SeekOrigin.Current);

            return count;
        }

        public static bool SeekSignatureBackward(Stream stream, uint signatureToFind)
        {
            uint signature = 0;

            byte[] buffer = new byte[32];

            int count;

            while ((count = ReadFileBackward(stream, buffer, buffer.Length)) > 0)
            {
                while (count > 0)
                {
                    if ((signature = (signature << 8) | buffer[count - 1]) == signatureToFind)
                    {
                        break;
                    }

                    --count;
                }

                if (count > 0)
                {
                    break;
                }
            }

            if (count > 0)
            {
                stream.Seek(count - 1, SeekOrigin.Current);
                return true;
            }

            return false;
        }

        public static void GetFileInfosFromAPK(Stream stream, string basePath, Dictionary<string, APKStruct.APKFileInfo> result)
        {
            BinaryReader reader = new BinaryReader(stream);

            stream.Seek(0, SeekOrigin.End);

            if (!SeekSignatureBackward(stream, 0x06054B50))
            {
                return;
            }

            long OffsetOfEOCD = stream.Position;

            APKStruct.EOCD eocd = APKStruct.EOCD.Read(reader);

            if (eocd.numberOfThisDisk != eocd.numberOfTheDiskWithStartCD)
            {
                return;
            }

            if (eocd.numberOfCentralDirectoryRecords != eocd.totalNumberOfCentralDirectoryRecords)
            {
                return;
            }

            long numberOfRecords = eocd.numberOfCentralDirectoryRecords;
            long centralDirectoryStart = eocd.offsetOfCentralDirectory;

            if (numberOfRecords == ushort.MaxValue || centralDirectoryStart == uint.MaxValue)
            {
                stream.Seek(OffsetOfEOCD, SeekOrigin.Begin);

                if (!SeekSignatureBackward(stream, 0x07064B50))
                {
                    return;
                }

                APKStruct.Zip64EOCDLocator locator = APKStruct.Zip64EOCDLocator.Read(reader);

                if (locator.offsetOfZip64EOCD > long.MaxValue)
                {
                    return;
                }

                stream.Seek((long)locator.offsetOfZip64EOCD, SeekOrigin.Begin);

                APKStruct.Zip64EOCD zip64eocd = APKStruct.Zip64EOCD.Read(reader);

                if (zip64eocd == null)
                {
                    return;
                }

                if (zip64eocd.numberOfCentralDirectoryRecords != zip64eocd.totalNumberOfCentralDirectoryRecords)
                {
                    return;
                }

                if (zip64eocd.numberOfCentralDirectoryRecords > long.MaxValue)
                {
                    return;
                }

                if (zip64eocd.offsetOfCentralDirectory > long.MaxValue)
                {
                    return;
                }

                numberOfRecords = (long)zip64eocd.numberOfCentralDirectoryRecords;
                centralDirectoryStart = (long)zip64eocd.offsetOfCentralDirectory;
            }

            stream.Seek(centralDirectoryStart, SeekOrigin.Begin);

            APKStruct.CentralDirectoryFileHeader cdHeader;

            while ((cdHeader = APKStruct.CentralDirectoryFileHeader.Read(reader)) != null)
            {
                if (cdHeader.compressedSize != cdHeader.uncompressedSize)
                {
                    continue;
                }

                string filename = Encoding.UTF8.GetString(cdHeader.filename).Replace('\\', '/');

                if (filename.EndsWith("/"))
                {
                    continue;
                }

                if (!filename.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                filename = filename.Remove(0, 7);

                if (filename.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (basePath.Length > 0 && !filename.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                filename = filename.Remove(0, basePath.Length);

                long lastPosition = stream.Position;

                stream.Seek(cdHeader.relativeOffsetOfLocalHeader, SeekOrigin.Begin);

                APKStruct.LocalFileHeader lfHeader = APKStruct.LocalFileHeader.Read(reader);

                if (lfHeader != null)
                {
                    result.Add(filename, new APKStruct.APKFileInfo(stream.Position, cdHeader.uncompressedSize));
                }

                stream.Seek(lastPosition, SeekOrigin.Begin);
            }
        }
    }

    public class IOUtility
    {
        static public Stream Create(IFileSystem fs, FileLocator fl)
        {
            return fs.Open(fl.location, fl.filename, FileMode.Create, FileAccess.Write);
        }

        static public Stream OpenWrite(IFileSystem fs, FileLocator fl)
        {
            return fs.Open(fl.location, fl.filename, FileMode.OpenOrCreate, FileAccess.Write);
        }

        static public Stream OpenRead(IFileSystem fs, FileLocator fl)
        {
            return fs.Open(fl.location, fl.filename, FileMode.Open, FileAccess.Read);
        }

        static public TextReader OpenText(IFileSystem fs, FileLocator fl)
        {
            return new StreamReader(OpenRead(fs, fl));
        }

        static public bool Exists(IFileSystem fs, FileLocator fl)
        {
            return fs.Exists(fl.location, fl.filename);
        }

        static public void WriteAllText(IFileSystem fs, FileLocator fl, string text)
        {
            using (Stream stream = Create(fs, fl))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);

                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public static int ReadFile(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            int read;

            while (count > 0 && (read = stream.Read(buffer, offset, count)) != 0)
            {
                offset += read;
                count -= read;
            }

            return offset;
        }

        public static void WriteFile(Stream stream, byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        public static int ReadInt(byte[] buffer, int startIndex)
        {
            return FromBigEndian(BitConverter.ToInt32(buffer, startIndex));
        }

        public static uint ReadUInt(byte[] buffer, int startIndex)
        {
            return FromBigEndian(BitConverter.ToUInt32(buffer, startIndex));
        }

        public static int FromBigEndian(int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = (int)SwapEndianness((uint)value);
            }

            return value;
        }

        public static uint FromBigEndian(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = SwapEndianness(value);
            }

            return value;
        }

        public static uint SwapEndianness(uint x)
        {
            return ((x & 0x000000ff) << 24) | ((x & 0x0000ff00) << 8)
                | ((x & 0x00ff0000) >> 8) | ((x & 0xff000000) >> 24);
        }

        public static byte[] ToBigEndianByteArray<T>(T a)
        {
            byte[] buf = typeof(BitConverter)
                .GetMethod("GetBytes", new Type[] { typeof(T) })
                .Invoke(null, new object[] { a }) as byte[];

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf);
            }

            return buf;
        }
 
        public static long GetFileSize(string path)
        {
            FileInfo info = new FileInfo(path);

            return info.Exists ? info.Length : 0;
        }
    }
}