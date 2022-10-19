# Sinks

A sink is an output where the logger writes the log’s stream to. It contains a formatter and an emitter.
The formatter converts the log entry into a defined representation (text by default), and the emitter either records it on the file system (for example as a txt file or JSON file), displays it in the console (for example in the Editor or Bash), or sends it to a remote back end.

## Use a sink to write to the file system

Recording a log file on the file system is the easiest way to debug an application. To prevent the logs from taking an unreasonable amount of disk space, the file sinks are rolling per default, which means that after a condition is met, such as  a certain number of fragments, a size of each fragment, or an elapsed time, the logger overwrites the initial file with new information. 

However, not every platform has a writable file system, so these sinks aren’t supported on the following platforms:

* Switch
* WebGL

The available file sinks are the file sink and the JSON file sink.

* **File sink:** Use `LoggerConfig.WriteTo.File` to use a file sink. This is the easiest way to capture logs. The file contains each log entry on a single line and is readable through any text editor.
* **JSON file sink:** Use `LoggerConfig.WriteTo.JsonFile` to capture the logs into a JSON line file. Every line is a parsable JSON object where the output template defines each key. This is a useful format when the logs are large and if you want to filter the data to find specific information. It's also useful for server and cloud environments where dedicated tools use the log files for filtering and parsing capabilities.

### File sink attributes

Some attributes are specific to the file sinks:

* `absFileName`: Use this attribute to specify that the sinks that capture files on the file system require an absolute file name to ensure an explicit path for every supported platform.
* `maxFileSizeBytes`: The maximum size in bytes of a file fragment before writing to the next one.
* `maxRoll`: The amount of file fragments the logger creates before rolling back on the initial one.
* `timeSpan`: The maximum time duration before writing the next file fragment.

## Sink writing in the console

Writing in the console provides an immediate error visualization, which is the best way to monitor what's going on while debugging. You can use the [Editor sink](xref:Unity.Logging.Sinks.UnityEditorConsoleSink) and the [stdOut sink](xref:Unity.Logging.Sinks.StdOutSinkSystem) for this:

* **The Editor sink**:  Use `WriteTo.UnityEditorConsole` for the Unity Editor Console. This works with version  2022.2.0a15 and higher. It’s similar to `Debug.Log()` and supports GameObject logging, while adding more information.
* **The StdOut sink:** Use `WriteTo.StdOut` to get a similar standard output like Unix and macOS.

## Other sinks
The following are debug sinks that are useful for testing:

* [UnityDebugLog sink](xref:Unity.Logging.Sinks.UnityDebugLogSink): Reroutes messages into `UnityEngine.Debug.Log`. This is a debug sink, and is slower than using `Debug.Log`, but it can be useful for example for tests that use `LogExpect`.
* [StringLogger sink](xref:Unity.Logging.Sinks.StringLoggerSinkExt.StringLogger*): Reroutes messages into in-memory string. Useful for testing expected log messages.

## Common sink attributes

Several attributes are common to all the sinks. These are:

* minLevel
* outputTemplate
* LogLevel
* captureStacktrace

The working of each attribute is similar to their equivalent methods (`OutputTemplate()`,`MinimumLevel.Level`,`captureStacktrace`).

You can use the attributes of the sink itself to explicitly define the value of these attributes. To implicitly define their values, omit the sink attribute and define the equivalent pipeline method. If neither is defined, the value is the default one. 
