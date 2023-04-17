# Custom configuration using LoggerConfig

To customize the logger, use the [`LoggerConfig`](xref:Unity.Logging.LoggerConfig) object. To use it, you must daisy chain parameters and sinks where each element represents a pipeline stage. Each stage takes as input the output of the previous stage. This means that the order in which you define these stages is important.

## LoggerConfig main pipeline methods

You can configure most of the stages in the pipeline with the following methods.

### SyncMode
Use [`SyncMode`](xref:Unity.Logging.LoggerConfig.SyncMode) to define how to capture the log entries. Either synchronous (`FullSync`), asynchronously (`FullAsync`), or using a mix of the two; where only fatal is synchronous while the other levels are asynchronous (`FatalIsSynch`). This third option is the default one as this offers the best balance between speed and reliability as it ensures that the logger captures the most critical messages before the program crashes.

### MinimumLevel
Use [`MinimumLevel`](xref:Unity.Logging.LoggerConfig.MinimumLevel) to configure the minimal level of the logs for the logger to capture. If you select `Debug` then the logger captures most logs. If you select `Fatal`, then the logger captures only fatal level logs.

### OutputTemplate

Use [`OutputTemplate`](xref:Unity.Logging.LoggerConfig.OutputTemplate*) to configure what information type the logger captures and how it's displayed. The accepted keywords are:

* `Timestamp`: Date and time (UTC)
* `Level`: The level of the log entry
* `Message`: The actual information that you want to capture
* `Stacktrace`: The stack trace where the logger captured the message from.
* `NewLine`: Inserts `Environment.NewLine`.
* `Properties`: Reserved keyword.

The configuration template needs to contain a combination of these keywords surrounded by curly brackets. For example: 

```c#
OutputTemplate("{Timestamp} - {Level} - {Message}")
```

### CaptureStacktrace

Use [`CaptureStackTrace`](xref:Unity.Logging.LoggerConfig.CaptureStacktrace(System.Boolean)) to define if the current logger should capture the stack trace. This is available within Burst compiled code. If you use a version of Burst prior to 1.80, there might be some missing stack frames.

### RedirectUnityLogs

Use [`RedirectUnityLogs`](xref:Unity.Logging.LoggerConfig.RedirectUnityLogs) to route Debug logs into the package.  This is intended as an aid for porting code to the logging package.  Debug logs will be sent to all sinks which set this option.  All Debug logs will be redirected into the package, overriding any currently configured filtering or enabling of Debug logs.


### RetrieveStartupLogs

Use [`RetrieveStartupLogs`[(xref:Unity.Logging.LoggerConfig.RetrieveStartupLogs) to retrieve any logs captured before package startup.  Logs are only captured when the Player Setting Capture Startup Logs has been set.

## Further information

* [Sinks](sinks.md)
* [`LoggerConfig` API documentation](xref:Unity.Logging.LoggerConfig)
