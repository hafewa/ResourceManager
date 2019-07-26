using System;

namespace ResourceMoudle
{
    [Serializable]
    public class FileItem
    {
        public FileItem(string name, string hash)
        {
            this.name = name;
            this.hash = hash;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int a = name.GetHashCode();
                int b = hash.GetHashCode();
                return (a + b) * (a + b + 1) / 2 + b;
            }
        }

        public override bool Equals(object other)
        {
            var o = other as FileItem;

            if (o == null)
            {
                return false;
            }

            return name.Equals(o.name) && hash.Equals(o.hash);
        }

        public string name;
        public string hash;
    }
}

