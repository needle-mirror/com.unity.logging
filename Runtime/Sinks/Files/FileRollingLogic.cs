// in case if you see 'sharing violation' errors
//#define LOGGING_FILE_OPS_DEBUG

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Class that implements rolling logic
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [BurstCompile]
    [GenerateTestsForBurstCompatibility]
    public struct FileRollingLogic<T> : IFileOperations where T : IFileOperationsImplementation
    {
        private T m_Implementation;

        private struct State
        {
            public ulong Offset;
            public IntPtr FileHandle;
            public FixedString4096Bytes FileHandleName;
            public FileSinkSystem.GeneralSinkConfiguration GeneralConfig;
            public FileSinkSystem.CurrentFileConfiguration FileConfig;
            public RollStruct RollState;
            public byte FirstTimeSeparatorByte;

            public bool HasOpenedFile => FileHandle != IntPtr.Zero;
            public bool FirstTimeSeparator => FirstTimeSeparatorByte != 0;
        }

        [NativeDisableUnsafePtrRestriction]
        private unsafe State* m_State;

        /// <summary>
        /// Create from a state pointer
        /// </summary>
        /// <param name="pointer">State pointer</param>
        public unsafe FileRollingLogic(IntPtr pointer)
        {
            m_State = (State*)pointer.ToPointer();
            m_Implementation = default;
        }

        /// <summary>
        /// Returns state pointer
        /// </summary>
        /// <returns>State pointer</returns>
        public IntPtr GetPointer()
        {
            unsafe
            {
                return new IntPtr(m_State);
            }
        }

        /// <summary>
        /// True if state is not null
        /// </summary>
        public bool IsCreated
        {
            get
            {
                unsafe
                {
                    return m_State != null;
                }
            }
        }

        void InitializeIfNeeded()
        {
            if (IsCreated) return;

            unsafe
            {
                var sizeOf = sizeof(State);
                m_State = (State*)UnsafeUtility.Malloc(sizeOf, UnsafeUtility.AlignOf<State>(), Allocator.Persistent);
                UnsafeUtility.MemClear(m_State, sizeOf);

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log(string.Format("Allocated {0}", new IntPtr(m_State).ToInt64()));
#endif
            }
        }

        /// <summary>
        /// Initializes state with file sink configuration
        /// </summary>
        /// <param name="config">Configuration to use</param>
        /// <returns>True if file stream was created successfully</returns>
        public bool OpenFileForLogging(ref FileSinkSystem.Configuration config)
        {
            InitializeIfNeeded();
            unsafe
            {
                m_State->GeneralConfig = config.GeneralConfig;
                m_State->FileConfig = config.CurrentFileConfig;
                m_State->RollState = RollStruct.Create(ref config.RollingFileConfig);

                return CreateFileStream();
            }
        }

        private bool CreateFileStream()
        {
            CheckMustBeInitialized();
            unsafe
            {
                m_State->FirstTimeSeparatorByte = 1;
                if (m_State->HasOpenedFile)
                {
                    CloseFile();
                }

                m_State->FileHandleName = m_State->RollState.RollFileAbsPath(ref m_State->FileConfig);
                m_State->FileHandle = m_Implementation.OpenFile(ref m_State->FileHandleName);

                m_State->Offset = 0;

                if (m_State->FileHandle != IntPtr.Zero)
                {
                    var success = true;
                    if (m_State->GeneralConfig.Prefix.IsEmpty == false)
                    {
                        success = Write(m_State->GeneralConfig.Prefix.GetUnsafePtr(), (ulong)m_State->GeneralConfig.Prefix.Length, &m_State->Offset);
                    }
                    return success;
                }
            }

            return false;
        }

        private void CloseFile()
        {
            CheckCanAccessFile();

            unsafe
            {
                if (m_State->GeneralConfig.Postfix.IsEmpty == false)
                {
                    Write(m_State->GeneralConfig.Postfix.GetUnsafePtr(), (ulong)m_State->GeneralConfig.Postfix.Length, &m_State->Offset);
                }
            }

            CloseFileInternal();
        }

        private void CloseFileInternal()
        {
            CheckCanAccessFile();

            unsafe
            {
                m_Implementation.CloseFile(m_State->FileHandle);

                m_State->FileHandleName = "";
                m_State->FileHandle = default;
            }
        }

        /// <summary>
        /// Flush any ongoing file operations
        /// </summary>
        public void Flush()
        {
            CheckCanAccessFile();

            unsafe
            {
                m_Implementation.FlushFile(m_State->FileHandle);
            }
        }

        /// <summary>
        /// Dispose all resources
        /// </summary>
        public void Dispose()
        {
#if LOGGING_FILE_OPS_DEBUG
            unsafe
            {
                UnityEngine.Debug.Log(string.Format("Disposing {0}...", new IntPtr(m_State).ToInt64()));
            }
#endif

            CheckMustBeInitialized();

            unsafe
            {
                if (m_State->HasOpenedFile)
                {
#if LOGGING_FILE_OPS_DEBUG
                    UnityEngine.Debug.Log(string.Format("Disposing {1} {0}..", m_State->FileHandle.ToInt64(), m_State->FileHandleName));
#endif
                    CloseFile();
                }

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log(string.Format("Disposing {0}", new IntPtr(m_State).ToInt64()));
#endif

                UnsafeUtility.Free(m_State, Allocator.Persistent);
                m_State = null;
            }

            CheckMustBeUninitialized();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool Write(byte* data, ulong length, ulong* offsetPtr)
        {
            CheckCanAccessFile();

            return m_Implementation.Write(m_State->FileHandle, data, length, offsetPtr);
        }

        /// <summary>
        /// Update state on data that is appended and writes data into the file
        /// </summary>
        /// <param name="data">Data to write</param>
        /// <param name="length">Length of data to write</param>
        /// <param name="newLine">If true - newline is going to be added after the data</param>
        public unsafe void Append(byte* data, ulong length, bool newLine)
        {
            CheckCanAccessFile();

            var offsetPtr = &m_State->Offset;
            var shouldRoll = m_State->RollState.ShouldRoll((long)(*offsetPtr));

            if (shouldRoll)
            {
                m_State->RollState.Roll();
#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log(string.Format("Appending.. Rolling now {0} {1}", m_State->FileHandle.ToInt64(), m_State->FileHandleName));
#endif
                CreateFileStream();
            }

            if (m_State->GeneralConfig.Separator.IsEmpty == false)
            {
                if (m_State->FirstTimeSeparator)
                    m_State->FirstTimeSeparatorByte = 0;
                else
                    Write(m_State->GeneralConfig.Separator.GetUnsafePtr(), (ulong)m_State->GeneralConfig.Separator.Length, offsetPtr);
            }

            Write(data, length, offsetPtr);

            if (newLine)
            {
                ref var newLineChar = ref Builder.EnvNewLine.Data;
                Write((byte*)UnsafeUtility.AddressOf(ref newLineChar.ElementAt(0)), (uint)newLineChar.Length, offsetPtr);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckCanAccessFile()
        {
            unsafe
            {
                if (IsCreated == false)
                    throw new Exception("FileOperationsBaselib is not created!");
                if (m_State->HasOpenedFile == false)
                    throw new Exception("FileOperationsBaselib doesn't have file opened");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckMustBeInitialized()
        {
            if (IsCreated == false)
                throw new Exception("FileOperationsBaselib is not created!");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckMustBeUninitialized()
        {
            if (IsCreated)
                throw new Exception("FileOperationsBaselib is Created, but it shouldn't be!");
        }
    }
}
