using System;
using Unity.Collections;

namespace Unity.Logging.Sinks
{
    public interface IFileOperationsImplementation
    {
        IntPtr OpenFile(ref FixedString4096Bytes absFilePath);
        unsafe bool Write(IntPtr fileHandle, byte* data, ulong length, ulong* offsetPtr, ref FixedString4096Bytes absFilePath);
        void FlushFile(IntPtr fileHandle);
        void CloseFile(IntPtr fileHandle, ref FixedString4096Bytes absFilePath);
    }
}
