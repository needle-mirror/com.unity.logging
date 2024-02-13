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

## Sinks writing in the console

Writing in the console provides an immediate error visualization, which is the best way to monitor what's going on while debugging. You can use the [Editor sink](xref:Unity.Logging.Sinks.UnityEditorConsoleSink) and the [standard output sink](xref:Unity.Logging.Sinks.StdOutSinkSystem) for this.

**Note**: On standalone desktop platforms (Windows, Mac, and Linux), Unity redirects standard output to the `Player.log` file. Thus on these platforms, using `WriteTo.StdOut()` will cause the logs to be written to that file instead. This is _not_ the case when running headless however. Running a server build, or running a build with the `-batchmode` command line argument will cause logs to be written to the normal standard output stream. Alternatively, it is also possible to disable the file redirection by passing the `-logfile` file command line argument without specifying a file name.

Note that the editor console sink will write its own stack trace and timestamp directly, so there is no need to include the "{Stacktrace}" or "{Timestamp}" part in your output template. Doing so will result in the stack trace and/or timestamp being printed twice in the console output. Here's an example configuration that avoids the duplication:

```csharp
new LoggerConfig()
    .CaptureStacktrace()
    .OutputTemplate("[{Timestamp}] {Message}{NewLine}{Stacktrace}")
    .WriteTo.UnityEditorConsole(outputTemplate: "{Message}")
```

## Common sink attributes

Several attributes are common to all the sinks. These are:

* minLevel
* outputTemplate
* LogLevel
* captureStacktrace

The working of each attribute is similar to their equivalent methods (`OutputTemplate()`,`MinimumLevel.Level`,`captureStacktrace`).

You can use the attributes of the sink itself to explicitly define the value of these attributes. To implicitly define their values, omit the sink attribute and define the equivalent pipeline method. If neither is defined, the value is the default one. 
