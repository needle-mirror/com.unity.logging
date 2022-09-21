# Architecture

When you're calling `Log.Verbose`, `Log.Debug`, `Log.Info`, `Log.Warning`, `Log.Error` or `Log.Fatal` with some arguments - they're stored for future processing in double-buffered storage.

Then when `ScheduleUpdateLoggers` is called and it is actually executed - all logs are sorted and then passed to the corresponding sinks in parallel.

So it is important to understand that the call of Log and processing the log have some distance in time. So when you're logging something - it doesn't immediately appear in sinks. That’s why the new logging solution is asynchronous. The benefit of this is that logging is significantly faster.

Note: Timestamps and stacktraces will match the moment the logging are called.

Note: `Log.Fatal` is planned to become synchronous, to avoid losing information on crash.

Log messages can be sent to different `Loggers` that are stored in `LoggerManager` by calling `Log.To(loggerHandle).`

Every `Logger` has:
- unique handle (see `LoggerHandle` type).
- list of `Sinks` configured for this particular Logger.
- underlying `LogController`

`LogController` is an internal part of `Logger` that shares the same unique handle (`LoggerHandle`) and also has `MemoryManager` and `DispatchQueue` inside.
This part is Burst-friendly and thread-safe.

## Update mechanism

```c#
JobHandle Unity.Logging.Internal.LoggerManager.ScheduleUpdateLoggers(JobHandle dependency = default)
```

This method creates jobs that are:
   - Update all Loggers in parallel:
      - Sort received messages (they can arrive in non-deterministic order)
      - Run all sinks in parallel
      - After all sinks are completed - dispatch queue's read part is cleared and read/write parts flipped (double-buffering)
      - Logger's MemoryManager.Update is called
   - We're making sure that all console output is flushed


## Design Details

The core logging data is simply a byte array holding the actual message data, called the Payload, which is accompanied by a few other essential fields. Together they form the LogMessage component.

Each log “sink” is a job that processes all LogMessage's and outputs the message contained in the Payload. Multiple sinks can query for the same LogMessage and output it to separate locations, e.g. a sink writes text messages to a file while another writes the same message to stdout.
