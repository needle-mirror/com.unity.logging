using System.Runtime.InteropServices;
using Unity.Collections;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Struct that is used to wrap FixedList of <see cref="PayloadHandle"/>s and <see cref="LogControllerScopedLock"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="LogControllerScopedLock"/> can be not valid (default) - that means FixedList of <see cref="PayloadHandle"/> is global, not connected to any <see cref="LogController"/>
    /// <seealso cref="LogDecorateScope"/>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LogContextWithDecorator
    {
        /// <summary>
        /// Mode that defines if this is 512 or 4096 elements list
        /// </summary>
        public enum Mode
        {
            /// <summary>
            /// Means that FixedList512Bytes{PayloadHandle} is used
            /// </summary>
            Length512,

            /// <summary>
            /// Means that FixedList4096Bytes{PayloadHandle} is used
            /// </summary>
            Length4096
        }

        /// <summary>
        /// <see cref="Mode"/> of this <see cref="LogContextWithDecorator"/>
        /// </summary>
        public Mode CurrentMode;

        private FixedList512Bytes<PayloadHandle>* List512;
        private FixedList4096Bytes<PayloadHandle>* List4096;

        /// <summary>
        /// Lock that was used for this <see cref="LogController"/>. Can be not valid (default) - that means FixedList of <see cref="PayloadHandle"/> is global, not connected to any <see cref="LogController"/>
        /// </summary>
        public LogControllerScopedLock Lock;

        /// <summary>
        /// Returns current Length of PayloadHandles list
        /// </summary>
        public ushort Length => (ushort)(CurrentMode == Mode.Length512 ? List512->Length : List4096->Length);

        /// <summary>
        /// Returns the element at a given index.
        /// </summary>
        /// <param name="i">An index.</param>
        /// <returns>The list element at the index.</returns>
        public ref PayloadHandle ElementAt(int i)
        {
            if (CurrentMode == Mode.Length512)
                return ref List512->ElementAt(i);

            return ref List4096->ElementAt(i);
        }

        /// <summary>
        /// Creates List with 512 bytes
        /// </summary>
        /// <param name="addressOfFixedList"><see cref="PayloadHandle"/>'s list</param>
        /// <param name="lockController"><see cref="LogController"/> lock or default if global</param>
        /// <returns>LogContextWithDecorator struct</returns>
        public static LogContextWithDecorator From512(FixedList512Bytes<PayloadHandle>* addressOfFixedList, LogControllerScopedLock lockController = default)
        {
            return new LogContextWithDecorator
            {
                CurrentMode = Mode.Length512,
                List512 = addressOfFixedList,
                List4096 = null,
                Lock = lockController
            };
        }

        /// <summary>
        /// Creates List with 4096 bytes
        /// </summary>
        /// <param name="addressOfFixedList"><see cref="PayloadHandle"/>'s list</param>
        /// <param name="lockController"><see cref="LogController"/>'s lock or default(<see cref="LogControllerScopedLock"/>) if global</param>
        /// <returns>LogContextWithDecorator struct</returns>
        public static LogContextWithDecorator From4096(FixedList4096Bytes<PayloadHandle>* addressOfFixedList, LogControllerScopedLock lockController = default)
        {
            return new LogContextWithDecorator
            {
                CurrentMode = Mode.Length4096,
                List512 = null,
                List4096 = addressOfFixedList,
                Lock = lockController
            };
        }

        /// <summary>
        /// Appends an element to the end of this list. Increments the length by 1.
        /// </summary>
        /// <param name="handle">The element to append at the end of the list.</param>
        public void Add(PayloadHandle handle)
        {
            if (CurrentMode == Mode.Length512)
                List512->Add(handle);
            else
                List4096->Add(handle);
        }

        /// <summary>
        /// Returns the <see cref="LogMemoryManager"/> of <see cref="LogContextWithDecorator"/>
        /// </summary>
        /// <param name="dec">Context</param>
        /// <returns>Memory manager</returns>
        public static ref LogMemoryManager GetMemoryManagerNotThreadSafe(ref LogContextWithDecorator dec)
        {
            if (dec.Lock.IsValid)
                return ref dec.Lock.GetLogController().MemoryManager;
            return ref LoggerManager.GetGlobalDecoratorMemoryManager();
        }
    }
}
