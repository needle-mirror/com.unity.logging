using System;
using Unity.Collections;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// General approach for file operations. Currently used to implement file rolling options in <see cref="FileRollingLogic{T}"/>
    /// </summary>
    public interface IFileOperations : IDisposable
    {
        /// <summary>
        /// Initializes state with file sink configuration
        /// </summary>
        /// <param name="config">Configuration to use</param>
        /// <returns>True if file stream was created successfully</returns>
        bool OpenFileForLogging(ref FileSinkSystem.Configuration config);

        /// <summary>
        /// Flush any ongoing file operations
        /// </summary>
        void Flush();

        /// <summary>
        /// Update state on data that is appended and writes data into the file
        /// </summary>
        /// <param name="data">Data to write</param>
        /// <param name="length">Length of data to write</param>
        /// <param name="newLine">If true - newline is going to be added after the data</param>
        unsafe void Append(byte* data, ulong length, bool newLine);
    }
}
