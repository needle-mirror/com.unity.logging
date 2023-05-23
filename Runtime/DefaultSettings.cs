using System;
using System.IO;
using Unity.Jobs;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using UnityEngine;

namespace Unity.Logging
{
    /// <summary>
    /// Default integration of the logging package in the Unity player loop.
    /// </summary>
    public static class DefaultSettings
    {
        private static JobHandle s_UpdateHandle;

#if UNITY_DOTSRUNTIME
        public static void Initialize()
        {
            RunOnStart();
            CreateDefaultLogger();
        }
#endif

        /// <summary>
        /// Creates a default logger and sets it as the current one, if the current logger is null.
        /// Automatically called with <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>
        /// priority. Thus to override the default logger, once can be created in
        /// <see cref="RuntimeInitializeLoadType.BeforeSplashScreen"/> instead.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateDefaultLogger()
        {
            if (LoggerManager.Logger == null)
            {
                LoggerManager.Logger = new LoggerConfig()
                    .SyncMode.FatalIsSync()
                    .MinimumLevel.Debug()
                    .CaptureStacktrace()
                    .OutputTemplate("[{Timestamp}] {Level} | {Message}{NewLine}{Stacktrace}")
// Switch file system is not writable from the Unity Runtime
#if !PLATFORM_SWITCH
                    .WriteTo.File(GetLogFilePath())
                    .WriteTo.JsonFile(GetJsonLogFilePath())
#endif
#if UNITY_CONSOLE_API
                    .WriteTo.StdOut()
                    .WriteTo.UnityEditorConsole()
#else
                    .WriteTo.UnityDebugLog()
#endif
                    .CreateLogger(LogMemoryManagerParameters.Default);

                if (Debug.isDebugBuild)
                {
                    Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInUnityEngineDebugLogError);
                }
            }
        }

        private static string GetLogFilePath()
        {
#if UNITY_DOTSRUNTIME
            // If a log file was passed on the command line, use that.
            var args = Environment.GetCommandLineArgs();
            var optIndex = System.Array.IndexOf(args, "-logFile");
            if (optIndex >=0 && ++optIndex < (args.Length - 1) && !args[optIndex].StartsWith("-"))
                return args[optIndex];
#endif
            return Path.Combine(GetLogDirectory(), "Output.log");
        }

        private static string GetJsonLogFilePath()
        {
            return GetLogFilePath() + ".json";
        }

        private static string GetLogDirectory()
        {
#if UNITY_DOTSRUNTIME
            var assemblyDir = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            var firstArg = Environment.GetCommandLineArgs()[0];
            var currentDir = Directory.GetCurrentDirectory();

            var dir = "";
            if (string.IsNullOrEmpty(assemblyDir))
                dir = assemblyDir;
            else if (string.IsNullOrEmpty(firstArg))
                dir = firstArg;
            else if (string.IsNullOrEmpty(currentDir))
                dir = currentDir;

            var logDir = Path.Combine(dir, "Logs");
            Directory.CreateDirectory(logDir);
            return logDir;

#elif UNITY_EDITOR
            var dataDir = Path.GetDirectoryName(Application.dataPath);
            var logDir = Path.Combine(dataDir, "Logs");
            Directory.CreateDirectory(logDir);
            return logDir;

#else
            var logPath = string.IsNullOrEmpty(Application.consoleLogPath)
                ? Application.persistentDataPath
                : Application.consoleLogPath;

            var logDir = Path.Combine(Path.GetDirectoryName(logPath), "Logs");
            Directory.CreateDirectory(logDir);
            return logDir;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void RunOnStart()
        {
            // Need to reset the job handle in case we're using fast domain reload, since otherwise
            // we might attempt to complete a job scheduled in a previous run, which won't work.
            s_UpdateHandle = default;

            LoggerManager.Initialize(); // TODO Is this needed?

#if !UNITY_DOTSRUNTIME
            // Make sure we don't subscribe many times.
            Application.quitting -= CleanupFunction;
            Application.quitting += CleanupFunction;

            IntegrateIntoPlayerLoop();
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void RunOnStartEditor()
        {
            // After each assembly reload, re-initialize the logging systems since they would have
            // been cleaned up by the before assembly reload callback. This also ensures that
            // initialization happens in a context where Burst is guaranteed to be ready.
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                RunOnStart();

                // Give a user one frame to setup their own default logger.
                UnityEditor.EditorApplication.delayCall += CreateDefaultLogger;
            };

            // Clean up before reloading assemblies. Need to do this to avoid memory leaks.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupFunction;

            // The above doesn't cover the editor quitting...
            UnityEditor.EditorApplication.quitting += CleanupFunction;
        }
#endif

#if !UNITY_DOTSRUNTIME
        private static void IntegrateIntoPlayerLoop()
        {
            var loggingManagerType = typeof(LoggerManager);

            var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var oldListLength = playerLoop.subSystemList?.Length ?? 0;
            var newSubsystemList = new UnityEngine.LowLevel.PlayerLoopSystem[oldListLength + 1];
            for (var i = 0; i < oldListLength; ++i)
            {
                if (playerLoop.subSystemList![i].type == loggingManagerType)
                    return; // Already added to the player loop.
                newSubsystemList[i] = playerLoop.subSystemList[i];
            }

            newSubsystemList[oldListLength] = new UnityEngine.LowLevel.PlayerLoopSystem
            {
                type = loggingManagerType,
                updateDelegate = UpdateFunction
            };
            playerLoop.subSystemList = newSubsystemList;
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoop);
        }
#endif

        // Don't make this method private! DOTS Runtime needs to be able to call it from outside.
        /// <summary>
        /// Runs the internal update of the logging package. No need to call this manually; it gets
        /// called manually. That it is exposed in the public API is only an implementation detail.
        /// </summary>
        public static void UpdateFunction()
        {
            s_UpdateHandle.Complete();
            s_UpdateHandle = LoggerManager.ScheduleUpdateLoggers();
        }

        private static void CleanupFunction()
        {
            LoggerManager.DeleteAllLoggers();
        }
    }
}
