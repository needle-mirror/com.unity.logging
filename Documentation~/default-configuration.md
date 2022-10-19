# Default configuration

You can customize and configure the Logging package. For example, you can directly call the logger with something like:

```c#
Log.Info("Hello Info");
```

Under the hood, Unity configures the following things by default:

* It captures the logs asynchronously except for log entries with Fatal level.
* It sets the minimum captured level to Debug. Including debug level log entries, everything above (info, warning, error, fatal) is captured as well.
* The output template is as follows:
    ```shell
    [{Timestamp}] {Level} | {Message}{NewLine}{Stacktrace}
    ```
* It captures the generated log files in the `logs/` folder of the project under the filename `Output.log.json` and `Output.log`
* The logs also appear in the Unity Editor console when available (for versions of Unity after 2022.2.0a15)

To create a more complex configuration, you can create a `LoggerConfig` object and pipeline different options such as :

```c#
Log.Logger = new Logger(new LoggerConfig()
.MinimumLevel.Debug()
.OutputTemplate("{Timestamp} - {Level} - {Message}")
.WriteTo.File("..absolutPath.../LogName.log", minLevel: LogLevel.Verbose)
.WriteTo.StdOut(outputTemplate: "{Level} || {Timestamp} || {Message}"));
```
The `LoggerConfig` options are detailed in the [Custom configuration](custom-configuration.md) documentation.
