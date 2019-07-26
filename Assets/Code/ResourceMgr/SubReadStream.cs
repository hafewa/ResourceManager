using System;
using System.IO;

namespace ResourceMoudle
{
    class SubReadStream : Stream
    {
        Stream _baseStream;
        long _start;
        long _size;

        public SubReadStream(Stream baseStream, long start, long size)
        {
            _baseStream = baseStream;
            _start = start;
            _size = size;

            Seek(0, SeekOrigin.Begin);
        }

        public override bool CanRead
        {
            get
            {
                return _baseStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _baseStream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return _size;
            }
        }

        public override long Position
        {
            get
            {
                return _baseStream.Position - _start;
            }

            set
            {
                _baseStream.Position = _start + value;
            }
        }

        public override void Close()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (_baseStream != null)
            {
                _baseStream.Close();
                _baseStream = null;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > _size - Position) {
                count = Math.Max((int)(_size - Position), 0);
            }

            return _baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                _baseStream.Seek(_start + offset, SeekOrigin.Begin);
            }
            else if (origin == SeekOrigin.End)
            {
                _baseStream.Seek(_start + _size + offset, SeekOrigin.Begin);
            }
            else
            {
                _baseStream.Seek(offset, SeekOrigin.Current);
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void WriteByte(byte value)
        {
            throw new NotImplementedException();
        }

     
    }
}

