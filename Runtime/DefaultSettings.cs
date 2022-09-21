using System;
using System.IO;
using Unity.Jobs;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using UnityEngine;

namespace Unity.Logging
{
    /// <summary>
    /// Integrates logging into Unity Engine player loop
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateDefaultLogger()
        {
            // This call is in 'BeforeSceneLoad' (so after BeforeSplashScreen), so user is able to create their own Logger instead
            if (LoggerManager.Logger == null)
            {
                // if user didn't do that - a default one is created
                var logDir = GetCurrentAbsoluteLogDirectory();

                LoggerManager.Logger = new Logger(new LoggerConfig()
                                                  .SyncMode.FatalIsSync()
                                                  .MinimumLevel.Debug()
                                                  .CaptureStacktrace()
                                                  .OutputTemplate("[{Timestamp}] {Level} | {Message}{NewLine}{Stacktrace}")
                                                  .WriteTo.JsonFile(Path.Combine(logDir, "Output.log.json"))
#if UNITY_CONSOLE_API
                                                  // if UnityEditor.ConsoleWindow.AddMessage API exists - use it
                                                  .WriteTo.File(Path.Combine(logDir, "Output.log"))
                                                                    .WriteTo.StdOut()
                                                                    .WriteTo.UnityEditorConsole()
#else
                                                  // if not - fallback to UnityEngine.Debug.Log sink
                                                  .WriteTo.UnityDebugLog()
#endif
                                                  );

                if (Debug.isDebugBuild)
                {
                    Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInUnityEngineDebugLogError);
                }
            }
        }

        private static string GetCurrentAbsoluteLogDirectory()
        {
#if UNITY_DOTSRUNTIME
            var args = Environment.GetCommandLineArgs();
            var optIndex = System.Array.IndexOf(args, "-logFile");
            if (optIndex >=0 && ++optIndex < (args.Length - 1) && !args[optIndex].StartsWith("-"))
                return args[optIndex];

            var dir = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Environment.GetCommandLineArgs()[0];
            }
            if (string.IsNullOrEmpty(dir))
            {
                dir = Directory.GetCurrentDirectory();
            }
            dir = Path.Combine(Path.GetDirectoryName(dir) ?? "", "Logs");
            Directory.CreateDirectory(dir);
            return dir;
#elif UNITY_EDITOR
            var projectFolder = Path.GetDirectoryName(Application.dataPath);
            var dir = Path.Combine(projectFolder, "Logs");
            Directory.CreateDirectory(dir);
            return dir;
#else
            var logPath = Application.consoleLogPath;
            if (string.IsNullOrEmpty(logPath) == false)
            {
                 return Path.GetDirectoryName(logPath);
            }

            var dir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(dir);
            return dir;
#endif
        }

#if UNITY_EDITOR
        // Initialize on load is called just after domain reload, and burst could be not initialized
        [UnityEditor.InitializeOnLoadMethod]
        static void RunOnStartEditor()
        {
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                // Burst 100% initialized
                RunOnStart();

                // Give a user one frame to setup their default logger
                UnityEditor.EditorApplication.delayCall += CreateDefaultLogger;
            };
        }
#endif


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void RunOnStart()
        {
            // Next two lines are for fast domain reload support: https://docs.unity3d.com/Manual/DomainReloading.html
            s_UpdateHandle = default;

#if !UNITY_DOTSRUNTIME
            Application.quitting -= Quit;
#endif

            LoggerManager.Initialize();

            IntegrateIntoPlayerLoop();
        }

        public static void IntegrateIntoPlayerLoop()
        {
#if !UNITY_DOTSRUNTIME
            var loggingManagerType = typeof(LoggerManager);

            var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var oldListLength = playerLoop.subSystemList?.Length ?? 0;
            var newSubsystemList = new UnityEngine.LowLevel.PlayerLoopSystem[oldListLength + 1];
            for (var i = 0; i < oldListLength; ++i)
            {
                if (playerLoop.subSystemList![i].type == loggingManagerType) return; // already added
                newSubsystemList[i] = playerLoop.subSystemList[i];
            }

            newSubsystemList[oldListLength] = new UnityEngine.LowLevel.PlayerLoopSystem
            {
                type = loggingManagerType,
                updateDelegate = UpdateFunction
            };
            playerLoop.subSystemList = newSubsystemList;
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoop);

            // make sure we subscribe only once
            Application.quitting -= Quit;
            Application.quitting += Quit;
#endif
        }

        public static void UpdateFunction()
        {
            s_UpdateHandle.Complete();
            s_UpdateHandle = LoggerManager.ScheduleUpdateLoggers();
        }

        private static void Quit()
        {
            LoggerManager.DeleteAllLoggers(); // flushes and deletes all loggers
        }
    }
}
