using System.Collections;
using System.Collections.Generic;

using Unity.Logging;
using Unity.Logging.Internal.Debug;
using Unity.Logging.Sinks;
using UnityEngine;
using Logger = Unity.Logging.Logger;

namespace Samples
{
    /// <summary>
    /// Example - how to use Unity.Logging
    /// </summary>
    public class SampleLoggingScript : MonoBehaviour
    {
        void Awake()
        {
            Log.Logger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .WriteTo.File("LogName.log", minLevel: LogLevel.Verbose, formatter: LogFormatterJson.Formatter)
                .WriteTo.StdOut(outputTemplate: "{Level} || {Timestamp} || {Message}"));


            SelfLog.SetMode(SelfLog.Mode.EnabledInUnityEngineDebugLogError);

            using (var scope = Log.Decorate("Source", "Awake"))
            {
                Log.Verbose("Hello Verbose {0}", 42); // file only
                Log.Debug("Hello Debug");             // console & file
                Log.Info("Hello Info");               // console & file
                Log.Warning("Hello Warning");         // console & file
                Log.Error("Hello Error");             // console & file
                Log.Fatal("Hello Fatal. That was {Level}");
            }
        }

        void Update()
        {
            // This will log every frame.
            Log.Info("Hello World!");
        }
    }
}
