# Changelog

## [0.51.0] - 2022-05-04

### Changed

* Dependencies
    * Burst 1.6.4
    * Jobs 0.51.0

## [0.50.0] - 2021-09-17

### Added

* Customizable timestamp support.
* Stacktrace support. .net core and ns20 have much faster stack traces.
* Logger API to be able to Add Sinks after creation
* OnNewLoggerCreated and CallForEveryLogger for LoggerManager
* Ability to change min logging level in runtime
* Test StringSink and UnityDebugLogSink added
* Tests for burst compatibility added
* USE_BASELIB define is in use for Unity 2020.3.26+ and 2021.2.8+
* USE_BASELIB_FILEIO define is in use for Unity 2021 where it was added to the baselib
* SelfLog FailedToAllocatePayloadBecauseOfItsSize message
* Il2CppSetOption checks disabled on perf critical code
* AggressiveInlining on perf critical code

### Changed

* Better error reporting from source generators
* Initialize call for Sinks doesn't require LoggerConfig
* Better Log call detection in the source generators
* il2cpp issue about missing MonoPInvokeCallback is fixed.
* Documentation and samples updated
* MaximumPayloadSize is increased to 32768
* All memory allocations are aligned to 8 bytes
* MaximumPayloadSize is increased to 1024 * 32
* Default and max buffer capacity reduced

### Deprecated


### Removed


### Fixed

* Workaround for Burst bug: DST-548
* Baselib File sink fixed
* Tests issues with il2cpp
* Arm64 crash fix
* Race condition fixed, new locking mechanism implemented
* Timestamps precision improved significantly, so messages are sorted correctly
* Timestamp stability improved
* Compatibility with Unity 2021 improved
* Incorrect timestamp fixed
* Bug with stacktrace for the text loggers

### Security




## [0.4.0-preview.1] - 2021-05-05

### Added

* Several `Logger` can exist in parallel, having its own memory manager and dispatch queue. `LoggerManager` added to control them.
* `Log.To(log-handle).Level-Name()` API added.
* Few multi-threaded integration stress tests added.
* JsonFileSink added, including tests.
* Log.Decorate added
* Log.Decorate to add custom fields into log messages
* Logger API to be able to Add Sinks after creation
* OnNewLoggerCreated and CallForEveryLogger for LoggerManager
* Ability to change min logging level in runtime
* Test StringSink and UnityDebugLogSink added

### Changed

* Logging internals are based on Jobs.
* SourceGenerator logging reworked.
* `LoggerHandler` renamed to `LoggerHandle`
* Better error reporting from source generators
* Initialize call for Sinks doesn't require LoggerConfig
* Better Log call detection in the source generators

### Fixed

* Race conditions fixed in logging internals.
* `BurstSpinLock` is based on `UnsafeList` that makes it much safer - it is impossible to copy it.
* DST-444. BurstDiscard for non-burstable log calls
* DST-446. Escape user literal into multiline comment
* DST-448. Interpolated string without holes is now burstable
* DST-357. Ambiguous call issue fixed
* Race condition in sourcegen fixed

### Removed

* ECS usage removed.



## [0.3.0-preview.1] - 2021-04-09

### Added
- Log.Logger and log configs added.
- Timestamp sorting.
- Basic `https://messagetemplates.org/` parsing support added.
- Console buffering strategy changed that improved performance x14 times.

### Changed
- DST-382: Multiworld setup - each logger is a separate world. Current 'logger' (Log.Logger) will take all the data from queue to its world.
- DST-340: Early out in Log.Info/Warning/Error if there are no sinks
- DST-385: Template support implemented
- Performance tests replaced. Old legacy loggers tests replaced with new Dots Runtime and Hybrid perf stress tests.
- Timestamp precision improved.

### Fixed
- DST-383: ValueTuple compilation error fixed
- DST-384: Managed string as an argument is not converted to special type fixed string fixed
- DST-386: Argument strings can also cause ambiguous errors.
- DST-388: Char support fixed
- DST-389: Bool support fixed
- DST-387: UnsafeText added to fix burst issues.
- DST-364: Unique handler name generator added to fix burst issues.
- Out-of-memory crash in tests fixed for x86.

### Removed
- Legacy loggers removed.


## [0.2.0-preview.1] - 2021-01-11

### Added
- Add implementation for new Structured Text Logger
- Profiler added to the SourceGenerator
- Special type handling for logging added: FixedString, int, uint, short, etc
- Not-blittable type handling support added: char, bool
- SourceGenerator tests added. Integrated into bee build process.
- SelfLog added so Logging system can report internal errors.
- Code formatting added. Check integrated into CI.
- Log levels added.

### Changed
- SourceGenerator refactored.
- No need to call `LogController` initialization manually. Now it has a static constructor.
- No need to call `RegisterTextLoggerParserOutputHandlers`. Now it is called in a static constructor.
- Better error reporting from the SourceGenerator
- Listeners renamed to Sinks. Common base extracted so user can define custom ones.
- `TextLogger.Write` replaced with `Log.Verbose` / `Log.Debug` / `Log.Info` / `Log.Warning` / `Log.Error` / `Log.Fatal`

### Fixed
- Pinned delegates used with `Marshal.GetFunctionPointerForDelegate` preventing them from GC collection
- DOTS Runtime compilation fixes
- Burst usage in the generated code.


## [0.1.0-preview.4] - 2020-08-18
- This is the first release of Unity Logging lib
