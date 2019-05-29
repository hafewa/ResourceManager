using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ResourceMoudle
{
    public class CompareResult
    {
        public long sourceVersionCapacity;
        public long destVersionCapacity;
        public long downloadSize;

        public string[] unchangedFiles;
        public string[] newFiles;
        public string[] changedFiles;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Source version: ");
            sb.Append((sourceVersionCapacity / 1024.0 / 1024.0).ToString("F2"));
            sb.Append("MBytes");
            sb.AppendLine();

            sb.Append("Destination version: ");
            sb.Append((destVersionCapacity / 1024.0 / 1024.0).ToString("F2"));
            sb.Append("MBytes");
            sb.AppendLine();
            sb.AppendLine();

            sb.Append("Download: ");
            sb.Append((downloadSize / 1024.0 / 1024.0).ToString("F2"));
            sb.Append("MBytes (");
            sb.Append(((double)downloadSize / destVersionCapacity * 100.0).ToString("F2"));
            sb.Append("% of dest)");
            sb.AppendLine();
            sb.AppendLine();

            sb.Append("Reusable: ");
            sb.Append(((destVersionCapacity - downloadSize) / 1024.0 / 1024.0).ToString("F2"));
            sb.Append("MBytes (");
            sb.Append(((double)(destVersionCapacity - downloadSize) / sourceVersionCapacity * 100.0).ToString("F2"));
            sb.Append("% of source)");
            sb.AppendLine();
            sb.AppendLine();

            sb.Append("Unchanged files (");
            sb.Append(unchangedFiles.Length);
            sb.Append("): ");
            for (int i = 0; i < unchangedFiles.Length; ++i)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(unchangedFiles[i]);
            }
            sb.AppendLine();
            sb.AppendLine();

            sb.Append("New files (");
            sb.Append(newFiles.Length);
            sb.Append("): ");
            for (int i = 0; i < newFiles.Length; ++i)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(newFiles[i]);
            }
            sb.AppendLine();
            sb.AppendLine();

            sb.Append("Changed files (");
            sb.Append(changedFiles.Length);
            sb.Append("): ");
            for (int i = 0; i < changedFiles.Length; ++i)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(changedFiles[i]);
            }
            sb.AppendLine();
            sb.AppendLine();

            return sb.ToString();
        }
    }

    public class Comparer
    {
        public Comparer(string sourceVersion, string destVersion)
        {
            sourceVersion_ = sourceVersion;
            destVersion_ = destVersion;
        }

        public CompareResult Compare()
        {
            CompareResult result = new CompareResult();

            List<FileItem> srcList = LoadList(Path.Combine(sourceVersion_, Setting.listFileName));
            List<FileItem> destList = LoadList(Path.Combine(destVersion_, Setting.listFileName));

            List<FileItem> unchangedFileList = new List<FileItem>(
                destList.Where(f1 => srcList.Any(f2 => f2.name == f1.name && f2.hash == f1.hash)));

            List<FileItem> newFileList = new List<FileItem>(
                destList.Where(f1 => srcList.All(f2 => f2.name != f1.name)));

            List<FileItem> changedFileList = new List<FileItem>(
                destList.Where(f1 => srcList.Any(f2 => f2.name == f1.name && f2.hash != f1.hash)));

            foreach (var fi in srcList)
            {
                FileInfo info = new FileInfo(Path.Combine(sourceVersion_, fi.name));

                result.sourceVersionCapacity += info.Length;
            }

            foreach (var fi in destList)
            {
                FileInfo info = new FileInfo(Path.Combine(destVersion_, fi.name));

                result.destVersionCapacity += info.Length;
            }

            List<string> list = new List<string>();

            foreach (var fi in unchangedFileList)
            {
                list.Add(fi.name);
            }

            result.unchangedFiles = list.ToArray();
            list.Clear();

            foreach (var fi in newFileList)
            {
                list.Add(fi.name);

                FileInfo info = new FileInfo(Path.Combine(destVersion_, fi.name));

                result.downloadSize += info.Length;
            }

            result.newFiles = list.ToArray();
            list.Clear();

            foreach (var fi in changedFileList)
            {
                list.Add(fi.name);

                FileInfo info = new FileInfo(Path.Combine(destVersion_, fi.name));

                result.downloadSize += info.Length;
            }

            result.changedFiles = list.ToArray();

            return result;
        }

        private List<FileItem> LoadList(string path)
        {
            List<FileItem> result = new List<FileItem>();

            StreamReader reader = new StreamReader(path);

            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] v = line.Split('/');
                result.Add(new FileItem(v[0], v[1]));
            }

            reader.Close();

            return result;
        }

        private string sourceVersion_;
        private string destVersion_;
    }
}