using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SharpRiff
{
    /// <summary>
    /// Represents a riff LIST chunk.
    /// The riff LIST chunks contain sub-chunks.
    /// </summary>
    public class RiffList : IDisposable
    {
        internal const long HeaderLength = 12; // 3 * 4

        private readonly RiffChunk _baseChunk;
        private readonly IEnumerable<RiffChunk> _chunks;
        private readonly string _listId;

        // busy when there's a chunk active and open
        private int _busyLevel;
        
        /// <summary>
        /// Gets the stream from which the data is read.
        /// </summary>
        public virtual Stream BaseStream
        {
            get { return _baseChunk.BaseStream; }
        }

        /// <summary>
        /// Gets the chunk object that represents this list.
        /// </summary>
        public RiffChunk BaseChunk
        {
            get { return _baseChunk; }
        }

        /// <summary>
        /// Gets an enumerable collection representing the chunks contained in this list.
        /// </summary>
        public IEnumerable<RiffChunk> Chunks
        {
            get {
                if (IsBusy)
                    throw new InvalidOperationException("The " + _listId + " is Busy.");

                if (!_baseChunk.IsOpen)
                    throw new InvalidOperationException("The " + _listId + " is not Open.");

                if (!_baseChunk.IsWriting)
                    throw new InvalidOperationException("The " + _listId + " is in Reading mode and cannot be modified.");

                return _chunks; 
            }
        }

        /// <summary>
        /// Gets the FourCC identifier for this list's chunk.
        /// Usually "LIST" or "RIFF".
        /// </summary>
        public virtual string ChunkId
        {
            get { return _baseChunk.ChunkId; }
        }

        /// <summary>
        /// Gets the FourCC for the list.
        /// </summary>
        public virtual string ListId
        {
            get { return _listId; }
        }

        /// <summary>
        /// Gets the offset at which the list contents start within the base stream.
        /// </summary>
        public virtual long Offset
        {
            get { return _baseChunk.Offset; }
        }

        /// <summary>
        /// Gets the length, in bytes, of the list's contents.
        /// </summary>
        public virtual long Length
        {
            get { return _baseChunk.Length; }
        }

        /// <summary>
        /// Gets a boolean specifying if this list is still open.
        /// </summary>
        public virtual bool IsOpen
        {
            get { return _baseChunk.IsOpen; }
        }

        /// <summary>
        /// Gets the access mode this list was created for.
        /// </summary>
        public virtual bool IsWriting
        {
            get { return _baseChunk.IsWriting; }
        }

        /// <summary>
        /// Gets a boolean specifying if the list has active enumerators (read mode)
        ///  or a child chunk is still open.
        /// </summary>
        public virtual bool IsBusy
        {
            get { return _busyLevel > 0; }
        }

        internal RiffList(RiffChunk chunk, string expectedChunkId) // read mode
        {
            if (chunk == null)
                throw new ArgumentNullException("chunk");

            if (_baseChunk.ChunkId != expectedChunkId)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, 
                    "chunk must be a {0} chunk", expectedChunkId));

            _baseChunk = chunk;

            byte[] b = _baseChunk.ReadBytes(4);

            _listId = new string(new[] {(char) b[0], (char) b[1], (char) b[2], (char) b[3]});

            _chunks = Descendants();
        }

        internal RiffList(Stream target, String listId, String chunkId) // write mode
        {
            _baseChunk = new RiffChunk(null, target, chunkId);

            if (listId.Length != 4)
                throw new ArgumentException("listID must be 4 characters in length");

            _listId = listId;

            _baseChunk.WriteByte((byte)_listId[0]);
            _baseChunk.WriteByte((byte)_listId[1]);
            _baseChunk.WriteByte((byte)_listId[2]);
            _baseChunk.WriteByte((byte)_listId[3]);
        }

        /// <summary>
        /// Closes the list, making it invalid to access its contents or modify them.
        /// </summary>
        public virtual void Close()
        {
            _baseChunk.Close();
        }

        internal virtual void Capture()
        {
            _busyLevel++;
        }

        internal virtual void Release()
        {
            _busyLevel--;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RiffChunk> Descendants()
        {
            Capture();

            try
            {
                var startOffset = BaseChunk.Offset + HeaderLength;
                var endOffset = startOffset + BaseChunk.Length;

                var newOffset = startOffset;
                while ((newOffset + RiffChunk.HeaderLength) < endOffset)
                {
                    var current = new RiffChunk(this, BaseChunk.BaseStream, newOffset);

                    yield return current;

                    newOffset = current.Offset + RiffChunk.HeaderLength + current.Length;

                    if ((newOffset & 1) == 1)
                        newOffset++;
                }
            }
            finally
            {
                Release();
            }
        }


        /// <summary>
        /// Creates a new chunk object with the specified FourCC.
        /// This chunk is created in write mode.
        /// </summary>
        /// <param name="chunkId">The FourCC code for the new chunk.</param>
        /// <returns></returns>
        public virtual RiffChunk CreateChunk(String chunkId)
        {
            if (IsBusy)
                throw new InvalidOperationException("The " + _listId + " is Busy.");

            if (!_baseChunk.IsOpen)
                throw new InvalidOperationException("The " + _listId + " is not Open.");

            if (!_baseChunk.IsWriting)
                throw new InvalidOperationException("The " + _listId + " is in Reading mode and cannot be modified.");

            return new RiffChunk(this, BaseStream, chunkId);
        }

        /// <summary>
        /// Creates a new list object with "LIST" chunk id and the specified FourCC as list id.
        /// </summary>
        /// <param name="id">The FourCC code for the list.</param>
        /// <returns></returns>
        public virtual RiffList CreateList(String id)
        {
            if (IsBusy)
                throw new InvalidOperationException("The " + _listId + " is Busy.");

            if (!_baseChunk.IsOpen)
                throw new InvalidOperationException("The " + _listId + " is not Open.");

            if (!_baseChunk.IsWriting)
                throw new InvalidOperationException("The " + _listId + " is in Reading mode and cannot be modified.");

            return new RiffList(BaseStream, id, "LIST");
        }

        /// <summary>
        /// Releases the resources used by the list, and closes it.
        /// </summary>
        /// <param name="disposing">True if the function is been called by user code, false if it is called by a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            Close();
        }

        /// <summary>
        /// Releases the resources used by the list, and closes it.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}