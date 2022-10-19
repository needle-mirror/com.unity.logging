using System;
using Unity.Collections;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Interface for file writer implementations
    /// </summary>
    public interface IFileOperationsImplementation
    {
        /// <summary>
        /// Opens the file for writing
        /// </summary>
        /// <param name="absFilePath">Absolute file path</param>
        /// <returns>File handle</returns>
        IntPtr OpenFile(ref FixedString4096Bytes absFilePath);

        /// <summary>
        /// Writes data into the file
        /// </summary>
        /// <param name="fileHandle">File handle that was returned by <see cref="OpenFile"/></param>
        /// <param name="data">Pointer to the data to write</param>
        /// <param name="length">Length of the data to write</param>
        /// <param name="offsetPtr">Position in file to write to</param>
        /// <returns>True if write was successful</returns>
        unsafe bool Write(IntPtr fileHandle, byte* data, ulong length, ulong* offsetPtr);
        /// <summary>
        /// Flush file
        /// </summary>
        /// <param name="fileHandle">File handle that was returned by <see cref="OpenFile"/></param>
        void FlushFile(IntPtr fileHandle);

        /// <summary>
        /// Close the file
        /// </summary>
        /// <param name="fileHandle">File handle that was returned by <see cref="OpenFile"/></param>
        void CloseFile(IntPtr fileHandle);
    }
}
