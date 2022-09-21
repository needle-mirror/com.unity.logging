using System;
using Unity.Collections;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// General approach for file operations. Currently used to implement file rolling options in <see cref="FileRollingLogic{T}"/>
    /// </summary>
    public interface IFileOperations : IDisposable
    {
        bool OpenFileForLogging(ref FileSinkSystem.Configuration config);
        void Flush();
        unsafe void Append(byte* data, ulong length, bool newLine);
    }
}
