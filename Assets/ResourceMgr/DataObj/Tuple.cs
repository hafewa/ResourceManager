using System;

namespace ResourceMoudle
{
    //组元 双对
    [Serializable]
    public class Pair<T1, T2>
    {
        public T1 first;
        public T2 second;

        public Pair()
        {
        }

        public Pair(T1 a, T2 b)
        {
            first = a;
            second = b;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int a = first.GetHashCode();
                int b = second.GetHashCode();
                return (a + b) * (a + b + 1) / 2 + b;
            }
        }

        public override bool Equals(object other)
        {
            var o = other as Pair<T1, T2>;

            if (o == null)
            {
                return false;
            }

            return first.Equals(o.first) && second.Equals(o.second);
        }
    }

    //组元 三对
    [Serializable]
    public class Triple<T1, T2, T3>
    {
        public T1 first;
        public T2 second;
        public T3 third;

        public Triple()
        {
        }

        public Triple(T1 a, T2 b, T3 c)
        {
            first = a;
            second = b;
            third = c;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int a = first.GetHashCode();
                int b = second.GetHashCode();
                int c = third.GetHashCode();
                return (a + b + c) * (a + b + c + 1) / 3 + c;
            }
        }

        public override bool Equals(object other)
        {
            var o = other as Triple<T1, T2, T3>;

            if (o == null)
            {
                return false;
            }

            return first.Equals(o.first) && second.Equals(o.second) && third.Equals(o.third);
        }
    }
}

