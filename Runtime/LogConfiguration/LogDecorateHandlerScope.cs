using System;
using Unity.Burst;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Disposable struct that adds decorate handler <see cref="LoggerManager.OutputWriterDecorateHandler"/>> f to the scope
    /// </summary>
    /// <remarks>
    /// Examples:
    /// <code>
    ///   using LogDecorateHandlerScope a = Log.Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, true);
    /// </code>
    /// or
    /// <code>
    ///   using LogDecorateHandlerScope threadIdDecor = Log.To(log2).Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, false);
    /// </code>
    /// </remarks>
    [BurstCompile]
    public readonly struct LogDecorateHandlerScope : IDisposable
    {
        private readonly LoggerHandle m_Handle;
        private readonly FunctionPointer<LoggerManager.OutputWriterDecorateHandler> m_Func;

        /// <summary>
        /// Adds a global (for all loggers / logs calls) decorator handler.
        /// <para />
        /// Constructor for this type of call:
        /// <code>
        /// using LogDecorateHandlerScope a = Log.Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, true);
        /// </code>
        /// </summary>
        /// <param name="f">decorate handler <see cref="LoggerManager.OutputWriterDecorateHandler"/></param>
        public LogDecorateHandlerScope(FunctionPointer<LoggerManager.OutputWriterDecorateHandler> f)
        {
            m_Func = f;
            LoggerManager.AddDecorateHandler(m_Func);
            m_Handle = default;
        }

        /// <summary>
        /// Adds a decorator handler for a particular logger
        /// <para />
        /// Constructor for this type of call:
        /// <code>
        ///   using LogDecorateHandlerScope a = Log.To(log2).Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, true);
        /// </code>
        /// <seealso cref="LogControllerScopedLock"/>
        /// </summary>
        /// <remarks>Warning: the lock is disposed because it can't be held for the whole scope.</remarks>
        /// <param name="f">decorate handler <see cref="LoggerManager.OutputWriterDecorateHandler"/></param>
        /// <param name="lock">The lock for <see cref="LogController"/></param>
        public LogDecorateHandlerScope(FunctionPointer<LoggerManager.OutputWriterDecorateHandler> f, LogControllerScopedLock @lock)
        {
            @lock.MustBeValid();

            m_Handle = @lock.Handle;
            m_Func = f;

            ref var controller = ref @lock.GetLogController();
            controller.AddDecorateHandler(m_Func);
            @lock.Dispose(); // cannot hold the lock, need to memorize handle and create another lock on Dispose of this struct
        }

        /// <summary>
        /// Removes decorator handler added in the constructor.
        /// </summary>
        public void Dispose()
        {
            if (m_Handle.IsValid)
            {
                using var scopedLock = LogControllerScopedLock.Create(m_Handle);

                ref var controller = ref scopedLock.GetLogController();
                controller.RemoveDecorateHandler(m_Func);
            }
            else
            {
                LoggerManager.RemoveDecorateHandler(m_Func);
            }
        }
    }
}
