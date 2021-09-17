# Logging examples
This page contains an example of how to use the Logging package in the context of a system.

You can configure Loggers (there could be several) like this:
```c#
        Log.Logger = new LoggerConfig()
                     .MinimumLevel.Debug()
                     .OutputTemplate("{Timestamp} - {Level} - {Message}")
                     .WriteTo.File("LogName.log", minLevel: LogLevel.Verbose)
                     .WriteTo.UnityDebugLog(outputTemplate: "{Message}")
                     .WriteTo.Console(outputTemplate: "{Level} || {Timestamp} || {Message}").CreateLogger();

        var logFileOnly = new LoggerConfig()
                                     .OutputTemplate("{Timestamp} | {Level} | {Message}")
                                     .WriteTo.File("Logs/AllLogs.log", minLevel: LogLevel.Verbose).CreateLogger();

        Log.Info("This will go to the default logger and file-only one");
        Log.To(logFileOnly).Info("But this one will go to Logs/AllLogs.log");
```

1. `ConsoleSinkSystem` - Writes to the system console (stdout). With DOTS Runtime Web builds, this goes to `js_console`
2. `FileSinkSystem` - Writes to a file for Hybrid or DOTS Runtime projects

> [!NOTE]
> `ConsoleSinkSystem` doesn't automatically redirect stdout to the Editor console. Use `UnityDebugLog` to achieve that.

```c#
using System.Collections;
using System.Collections.Generic;
using Unity.Logging;
using Unity.Logging.Legacy;
using Unity.Entities;

public class SampleLoggingScript : SystemBase
{
    protected override void OnCreate()
    {
        Log.Logger = new Logger(new LoggerConfig()
            .MinimumLevel.Debug()
            .OutputTemplate("{Timestamp} - {Level} - {Message}")
            .WriteTo.File("LogName.log", restrictedToMinimumLevel: LogLevel.Verbose)
            .WriteTo.Console(outputTemplate: "{Level} || {Timestamp} || {Message}"));

        SelfLog.SetMode(SelfLog.Mode.EnabledInUnityEngineDebugLogError);

        Log.Verbose("Hello Verbose {0}", 42);                                                                      // file only
        Log.Debug("Hello Debug");                                                                                                          // console & file
        Log.Info("Hello Info");                                                                                    // console & file
        Log.Warning("Hello Warning");                                                                                                      // console & file
        Log.Error("Hello Error");                                                                                  // console & file
        Log.Fatal("Hello Fatal. That was {Level}");

        base.OnCreate();
    }

    protected override void OnUpdate()
    {
        Unity.Logging.Internal.LoggerManager.ScheduleUpdateLoggers(); // make sure to call this once per frame. can be in any place
    }
}
```

## Custom Sink

If you want to implement your own Sinks, see the Sinks located at `Runtime/Sinks/` directory of the package for examples.

## Burst function pointer

If you need to call code that is managed from a Bursted context, see the file located at `Runtime/ManagedOperations.cs` for an example.