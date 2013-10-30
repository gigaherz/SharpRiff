using System;
using System.IO;

namespace SharpRiff
{
    /// <summary>
    /// Represents a RIFF file, which behaves the same as a riff LIST.
    /// </summary>
    public class RiffFile: RiffList
    {
        #region Static Members

        internal static readonly bool NeedsByteSwap = (BitConverter.GetBytes(1.0f)[0] != 0); //false;

        #endregion // Static Members

        /// <summary>
        /// Constructs an object representing an existing RIFF file.
        /// The objects returned by it will be in read mode.
        /// </summary>
        /// <param name="source">Stream to read the RIFF data from. The stream must be readable.</param>
        public RiffFile(Stream source) // read mode
            : base(new RiffChunk(null, source, 0), "RIFF")
        {

        }

        /// <summary>
        /// Creates a new RIFF file. 
        /// Objects created from this one will all be in write mode.
        /// </summary>
        /// <param name="target">Stream to write the new RIFF data into. The stream must be writable.</param>
        /// <param name="formatId">FourCC the new file will have.</param>
        public RiffFile(Stream target, String formatId) // write mode
            : base(target, formatId, "RIFF")
        {
        }

        /// <summary>
        /// Closes the file object and its source stream.
        /// </summary>
        public override void Close()
        {
            base.Close();
            BaseStream.Flush();
            BaseStream.Close();
        }

        /// <summary>
        /// Releases the resources used by the list, and closes it.
        /// </summary>
        /// <param name="disposing">True if the function is been called by user code, false if it is called by a finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Close();
        }
    }
}
