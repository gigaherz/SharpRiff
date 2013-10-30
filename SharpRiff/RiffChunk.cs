using System;
using System.Globalization;
using System.IO;

namespace SharpRiff
{
    /// <summary>
    /// Represents a riff chunk.
    /// When this class is in read mode, it behaves like a BinaryReader class.
    /// When this class is in write mode, it behaves like a BinaryWriter class.
    /// In both cases, it can be used as a stream.
    /// </summary>
    public class RiffChunk : Stream
    {
        internal const long HeaderLength = 8; // 2*4, offset is not part of the chunk 

        private readonly long _localOffset;
        private readonly string _chunkId;
        private readonly RiffList _baseList;

        private long _length;

        /// <summary>
        /// 
        /// </summary>
        public virtual Stream BaseStream { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        public virtual string ChunkId
        {
            get { return _chunkId; }
        }


        /// <summary>
        /// 
        /// </summary>
        public virtual long Offset
        {
            get { return _localOffset; }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual long DataOffset
        {
            get { return Offset+HeaderLength; }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual long DataOffsetEnd
        {
            get { return DataOffset + Length; }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual bool IsOpen { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public virtual bool IsWriting { get; private set; }

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        /// <returns>
        /// A long value representing the length of the stream in bytes.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">A class derived from Stream does not support seeking. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>1</filterpriority>
        public override long Length
        {
            get { return _length; }
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <returns>
        /// The current position within the stream.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>1</filterpriority>
        public override long Position
        {
            get { return BaseStream.Position - DataOffset; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns>
        /// true if the stream supports reading; otherwise, false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanRead
        {
            get { return !IsWriting; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <returns>
        /// true if the stream supports writing; otherwise, false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanWrite
        {
            get { return IsWriting; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <returns>
        /// true if the stream supports seeking; otherwise, false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanSeek
        {
            get { return !IsWriting; }
        }

        /// <summary>
        /// Gets a value that determines whether the current stream can time out.
        /// </summary>
        /// <returns>
        /// A value that determines whether the current stream can time out.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override bool CanTimeout
        {
            get { return false; }
        }
        
        private void TestDataRange(long currentOffset, int readLength)
        {
            if (currentOffset < DataOffset)
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "Attempt to read before the start of the data. ({0} < {1})",
                    currentOffset, DataOffset));

            if (currentOffset + readLength > DataOffsetEnd)
                throw new EndOfStreamException(string.Format(CultureInfo.InvariantCulture,
                    "Tried to read after the end of the data. ({0} > {1})",
                    (currentOffset + readLength), DataOffsetEnd));
        }

        internal RiffChunk(RiffList @base, Stream source, long loadOffset)
        {
            IsWriting = false;
            IsOpen = false;
            _baseList = @base;

            BaseStream = source;

            var b = new byte[4];

            _localOffset = loadOffset;

            if ((loadOffset & 1) != 0)
                throw new InvalidDataException("Chunk offset is ODD!");

            source.Seek(loadOffset, SeekOrigin.Begin);

            source.Read(b, 0, 4);
            _chunkId = "" + (char)b[0] + (char)b[1] + (char)b[2] + (char)b[3];

            source.Read(b, 0, 4);
            _length = (b[3] << 24) | (b[2] << 16) | (b[1] << 8) | b[0];

            if (_baseList != null) _baseList.Capture();
            IsOpen = true;
        }

        internal RiffChunk(RiffList @base, Stream target, String id)
        {
            IsOpen = false;
            IsWriting = true;
            _baseList = @base;

            if (_chunkId.Length != 4)
                throw new ArgumentException("chunkId must be 4 characters in length");

            BaseStream = target;
            _localOffset = (uint)target.Position;

            if ((_localOffset & 1) != 0)
                throw new InvalidDataException("Chunk offset is ODD!");

            _chunkId = id;
            
            target.WriteByte((byte)_chunkId[0]);
            target.WriteByte((byte)_chunkId[1]);
            target.WriteByte((byte)_chunkId[2]);
            target.WriteByte((byte)_chunkId[3]);
            target.WriteByte(0);
            target.WriteByte(0);
            target.WriteByte(0);
            target.WriteByte(0);

            if (_baseList != null) _baseList.Capture();
            IsOpen = true;
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <filterpriority>2</filterpriority>
        public override void Flush()
        {
            BaseStream.Flush();
        }

        /// <summary>
        /// Closes the current stream and releases any resources (such as sockets and file handles) associated with the current stream.
        /// </summary>
        /// <filterpriority>1</filterpriority>
        public override void Close()
        {
            if (IsOpen)
            {
                if (IsWriting)
                {
                    _length = (uint)BaseStream.Position - Offset - HeaderLength;
                    BaseStream.Seek(Offset + 4, SeekOrigin.Begin);
                    Write((uint)Length);
                    BaseStream.Seek(Offset + HeaderLength + Length, SeekOrigin.Begin);

                    if ((Length & 1) == 1) // there's one padding byte if the length is odd
                    {
                        Write((byte)0); // padding byte
                    }
                    if (_baseList != null) _baseList.Release();
                }
                IsOpen = false;
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="count"></param>
        /// <returns>An array with the read bytes</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual byte[] ReadBytes(int count)
        {
            if (IsWriting)
                throw new InvalidOperationException("Stream is in write mode");

            TestDataRange(BaseStream.Position, count);

            var b = new byte[count];

            int actualRead = BaseStream.Read(b, 0, count);

            if(actualRead < count)
                Array.Resize(ref b, actualRead);

            return b;
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array
        /// with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1)
        /// replaced by the bytes read from the current source. </param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data
        /// read from the current stream. </param><param name="count">The maximum number of bytes to be read from the current stream. </param>
        /// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length. </exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer"/> is null. </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>1</filterpriority>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (IsWriting)
                throw new InvalidOperationException("Stream is in write mode");

            TestDataRange(BaseStream.Position, count);

            return BaseStream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one byte,
        /// or returns -1 if at the end of the stream.
        /// </summary>
        /// <returns>
        /// The unsigned byte cast to an Int32, or -1 if at the end of the stream.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>2</filterpriority>
        public override int ReadByte()
        {
            if (IsWriting)
                throw new InvalidOperationException("Stream is in write mode");

            if (BaseStream.Position + 1 > DataOffsetEnd)
                return -1;

            TestDataRange(BaseStream.Position, 1);

            return (byte)BaseStream.ReadByte();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [CLSCompliant(false)]
        public virtual sbyte ReadSByte()
        {
            TestDataRange(BaseStream.Position, 1);

            return (sbyte)ReadByte();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual short ReadInt16()
        {
            TestDataRange(BaseStream.Position, 2);

            return (short)(ReadByte() | (ReadByte() << 8));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [CLSCompliant(false)]
        public virtual ushort ReadUInt16()
        {
            TestDataRange(BaseStream.Position, 2);

            return (ushort)(ReadByte() | (ReadByte() << 8));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual int ReadInt32()
        {
            TestDataRange(BaseStream.Position, 4);

            return (ReadUInt16() | (ReadInt16() << 16));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [CLSCompliant(false)]
        public virtual uint ReadUInt32()
        {
            TestDataRange(BaseStream.Position, 4);

            return (ReadUInt16() | ((uint)ReadUInt16() << 16));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual long ReadInt64()
        {
            TestDataRange(BaseStream.Position, 8);

            return (ReadUInt32() | ((long)ReadInt32() << 32));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [CLSCompliant(false)]
        public virtual ulong ReadUInt64()
        {
            TestDataRange(BaseStream.Position, 8);

            return (ReadUInt32() | ((ulong)ReadUInt32() << 32));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual bool ReadBoolean()
        {
            return (ReadByte() != 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual float ReadSingle() 
        {
            byte[] b = ReadBytes(4);
            float f = BitConverter.ToSingle(b, 0);

            return f;
            //byte[] b = BitConverter.GetBytes(1.0f);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual double ReadDouble()
        {
            byte[] b = ReadBytes(8);
            double f = BitConverter.ToDouble(b, 0);

            return f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual decimal ReadDecimal()
        {
            int[] ints =
            {
                ReadInt32(),
                ReadInt32(),
                ReadInt32(),
                ReadInt32()
            };
            return new decimal(ints);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual char ReadChar()
        {
            return (char)ReadInt32();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public virtual int Read(char[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentOutOfRangeException("buffer");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (offset >= buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if (count < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException("count");

            if ((offset + count) > buffer.Length)
                throw new ArgumentException("The array passed in 'buffer' is not big enough to hold the requested number of bytes");
            
            for (int i = 0; i < count; i++)
            {
                buffer[i + offset] = ReadChar();
            }
            return count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual char[] ReadChars(int count)
        {
            var b = new char[count];

            for (int i = 0; i < count; i++)
                b[i] = ReadChar();

            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual string ReadString()
        {
            var nChars = ReadInt32();
            return "" + ReadChars(nChars);
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter. </param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating 
        /// the reference point used to obtain the new position. </param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking,
        /// such as if the stream is constructed from a pipe or console output. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>1</filterpriority>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long newOffset;

            if (IsWriting)
                throw new InvalidOperationException("Cannot Seek while in write mode");

            switch (origin)
            {
                case SeekOrigin.Begin:
                    newOffset = DataOffset + offset;
                    TestDataRange(newOffset, 0);
                    BaseStream.Seek(newOffset, SeekOrigin.Begin);
                    break;
                case SeekOrigin.Current:
                    newOffset = BaseStream.Position + offset;
                    TestDataRange(newOffset, 0);
                    BaseStream.Seek(Offset, SeekOrigin.Current);
                    break;
                case SeekOrigin.End:
                    newOffset = DataOffsetEnd + offset;
                    TestDataRange(newOffset, 0);
                    BaseStream.Seek(newOffset, SeekOrigin.Begin);
                    break;
            }

            return BaseStream.Position - DataOffset;
        }

        /// <summary>
        /// This stream does not support changing the length explicitly.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes. </param>
        /// <exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking,
        /// such as if the stream is constructed from a pipe or console output. </exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream
        /// and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes
        /// from <paramref name="buffer"/> to the current stream. </param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which
        /// to begin copying bytes to the current stream. </param>
        /// <param name="count">The number of bytes to be written to the current stream. </param>
        /// <filterpriority>1</filterpriority>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!IsWriting)
                throw new InvalidOperationException("Stream is in write mode");

            BaseStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public virtual void Write(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentOutOfRangeException("buffer");

            Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual void Write(byte value)
        {
            if(!IsWriting)
                throw new InvalidOperationException("Stream is in write mode");

            BaseStream.WriteByte(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        [CLSCompliant(false)]
        public virtual void Write(sbyte value)
        {
            Write((byte)value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(short value)
        {
            Write((byte)(value));
            Write((byte)(value>>8));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        [CLSCompliant(false)]
        public virtual void Write(ushort value)
        {
            Write((byte)(value));
            Write((byte)(value>>8));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(int value)
        {
            Write((byte)(value));
            Write((byte)(value >> 8));
            Write((byte)(value >> 16));
            Write((byte)(value >> 24));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        [CLSCompliant(false)]
        public virtual void Write(uint value)
        {
            Write((byte)(value));
            Write((byte)(value >> 8));
            Write((byte)(value >> 16));
            Write((byte)(value >> 24));
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(long value)
        {
            Write((uint)(value));
            Write((uint)(value >> 32));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        [CLSCompliant(false)]
        public virtual void Write(ulong value)
        {
            Write((uint)(value));
            Write((uint)(value >> 32));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(float value)
        {
            byte[] b = BitConverter.GetBytes(value);

            if (RiffFile.NeedsByteSwap)
            {
                for (int i = 3; i >= 0; i--)
                    Write(b[i]);
            }
            else
            {
                Write(b);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(double value)
        {
            byte[] b = BitConverter.GetBytes(value);

            if (RiffFile.NeedsByteSwap)
            {
                for(int i=7;i>=0;i--)
                    Write(b[i]);
            }
            else
            {
                Write(b);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(decimal value)
        {
            var t = Decimal.GetBits(value);
            Write(t[0]);
            Write(t[1]);
            Write(t[2]);
            Write(t[3]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void Write(char value)
        {
            Write((int)value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public virtual void Write(char[] buffer)
        {
            if (buffer == null)
                throw new ArgumentOutOfRangeException("buffer");

            Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public virtual void Write(char[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentOutOfRangeException("buffer");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (offset >= buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if (count < 0)
                throw new ArgumentOutOfRangeException("count");

            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException("count");

            if ((offset + count) > buffer.Length)
                throw new ArgumentException("The array passed in 'buffer' is not big enough to hold the requested number of bytes");
            
            for (int i = 0; i < count; i++)
            {
                Write(buffer[i + offset]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public virtual void Write(string value)
        {
            if (value == null)
                throw new ArgumentOutOfRangeException("value");

            Write(value.Length);
            Write(value.ToCharArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public RiffList ToList()
        {
            if (IsWriting)
                throw new InvalidOperationException("Stream is in write mode");

            if (!IsOpen)
                throw new InvalidOperationException("The chunk is not Open.");

            return new RiffList(this, "LIST");
        }
    }
}
