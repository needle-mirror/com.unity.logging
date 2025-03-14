# Changelog

## [1.4.0-exp.2] - 2025-03-07

### Changed

* Updated the `com.unity.burst` dependency to version `1.8.19`


## [1.3.6] - 2025-02-17

### Fixed

* Fixed special characters (like newlines) not being escaped properly in JSON properties.


## [1.3.5] - 2025-01-16

### Fixed

* Addressed an issue on Unity 6 and iOS where having the package installed would cause an exception at startup on player builds. An effect of this fix is that log files are no longer produced by default on iOS. This can be overridden through a custom logger configuration (see `DefaultSettings.CreateDefaultLogger`).


## [1.3.4] - 2024-10-04

### Changed
* Updated entities packages dependencies
* Updated Burst dependency to version 1.8.18


## [1.3.2] - 2024-09-06

### Changed
* Updated entities packages dependencies


### Changed
* Updated Burst dependency to version 1.8.17


## [1.3.0-pre.4] - 2024-07-17

### Changed
* Updated Burst dependency to version 1.8.16
* Updated entities packages dependencies


## [1.3.0-exp.1] - 2024-06-11

### Changed
* Updated entities packages dependencies


## [1.2.4] - 2024-08-14

### Changed
* Updated entities packages dependencies


## [1.2.3] - 2024-05-30

### Changed
* Updated entities packages dependencies


## [1.2.1] - 2024-04-26

### Changed
* Updated entities packages dependencies


## [1.2.0] - 2024-03-22

### Changed
*Release Preparation


## [1.2.0-pre.12] - 2024-02-13

### Changed

* Updated Burst dependency to version 1.8.12

### Fixed

* Fixed an issue where `WriteTo.UnityEditorConsole` would fail to preserve the location of the log line. For example when double-clicking the log entry in the editor console, it would not take you to the appropriate line in your IDE.
* Fixed a memory leak in the JSON sink.


## [1.2.0-pre.6] - 2023-12-13

### Changed

* Promotion preparation


## [1.2.0-pre.4] - 2023-11-28

### Changed

* Updated Burst dependency to 1.8.10


## [1.2.0-exp.3] - 2023-11-09

### Changed

* The minimum supported editor version is now 2022.3.11f1


### Fixed

* Fixed an issue where using format specifiers would lead to invalid JSON (e.g. using a format specifier that adds leading zeros to a number). Properties are simply not formatted anymore in the JSON objects.
* Fixed an issue where logging a string that contains braces to the Unity console would result in FormatException being thrown.
* You can now call Dispose on a deleted Logger object without it throwing an exception.


## [1.1.0-pre.3] - 2023-10-17

### Changed

* Updated version for release preparation


## [1.1.0-exp.1] - 2023-09-18

### Added

* When using `RedirectUnityLogs`, native logs (logs emitted from C++ code in Unity) will now also be redirected. Note that this is only available in player builds as a current limitation prevents this redirection from working in the editor. Redirection of logs emitted by C# code remains supported both in player builds and in the editor, however.

### Fixed

* Under stress, it was possible to drop a log message.
* Allow empty strings in logging messages
* Performance issue with `LogControllerWrapper.GetLogControllerIndexUnderLockNoThrow` when creating many worlds.
* Log sinks forwarded to UnityDebugLogSink will now no longer extract erroneous stacktraces from UnityDebugLogSink.cs. While it does not correctly forward stacktraces (if enabled), it at least doesn't report incorrect ones.
* Fixed an issue where OutputTemplate is ignored when logging into UnityEditorConsole.


## [1.0.16] - 2023-09-11


### Changed

* Updated Burst dependency to version 1.8.8


### Fixed

* Fixed an issue where using a string that requires more than 2 bytes per character when encoded in UTF-8 in a formatted log would result in either an exception being thrown or the editor hanging, depending on the sync mode.



## [1.0.14] - 2023-07-27

### Changed

* Updated Burst dependency to version 1.8.7


## [1.0.11] - 2023-05-30

### Fixed

* Fixed compilation errors with 2022.3.


## [1.0.10] - 2023-05-23

### Fixed

* Fixed memory leaks on domain reloads.



## [1.0.8] - 2023-04-17

### Added

* Support for redirecting Unity Debug logs into logging package

### Changed

* Updated Burst version to 1.8.4

### Fixed

* Generated names are now deterministic
* Update XML documenation.
* When targeting the IL2CPP backend with Burst enabled and Managed Code Stripping set to values higher than 'Minimal', logging messages should no longer cause a compilation failure. Some code paths internal to the package and necessary for Burst compatibility were erroneously being stripped with Managed Code Stripping enabled.



## [1.0.0-pre.37] - 2023-03-21

### Changed

* Updated Burst version in use to 1.8.3


## [1.0.0-pre.21] - 2023-02-13

### Changed
* Console sink reports timestamp in the local timezone, instead of UTC.
* Logger settings set 'CaptureStacktrace' to true by default.

### Fixed

* Improve compilation when using UNITY_DOTS_DEBUG

### Security

## [1.0.0-pre.11] - 2022-11-19

* Release preparations, no functional changes.


## [1.0.0-exp.7] - 2022-10-19

### Added

* Ability to provide custom ToString-like formatting
* Missing documentation
* Structures with unsafe pointers inside can be logged.
* Pointers can be logged

### Changed

* Incremental sourcegen implemented

### Removed

* Jobs dependency removed
* USE_BASELIB define removed - it is always set

### Fixed

* Unity 2023 compatibility
* Initialization order because of cctor is not called in some Burst cases

## [1.0.0] - 2022-09-21

### Added

* Stacktrace support. .net core and ns20 have much faster stack traces.
* AggressiveInlining attributes
* Il2CppSetOption attributes with disabled runtime checks for perf critical code
* Public way to create a LoggerHandle in case it cannot be used directly
* Support of UNITY_DOTS_DEBUG  define
* Sync logging
* Support for strings in logging calls
* SourceGenerator's speed by detecting compilation abort better
* Ability to log properties
* LogWithName attribute added
* NotLogged / NonSerialized attribute added
* Static analyzer for Log. messages that validates the message and arguments.
* Default Logger is created if a user didn't do that
* Logging system will call update automatically, using PlayerLoop
* Logging will flush and cleanup on application quit
* SelfLog error on empty template for text logging

### Changed

* USE_BASELIB define removed to work with all Unity Editors
* minor `in` -> `ref` replace for the messages in the queue
* in -> ref in few places
* Burst 1.6.4 used
* T constraint on `Builder.BuildContext` from `struct` to `unmanaged`.
* Logging's source generators are not emitting warnings on not burst compatible calls anymore
* Default and max buffer capacity reduced
* Log writing to files now has log rolling enabled by default, with a maximum log file size of 5MB and 15 maximum log files that can exist before older log files are deleted as the logger rolls to a new one.
* Added composite formatting and a subset of format specifiers to logging statements for integer types. Standard numeric format specifiers included are `DdXx`, custom numeric format specifiers are `0#,.\` and string literals enclosed by `''` or `""`.
* SourceGenerator's code refactoring.
* Log.* calls are generated even if no usage is detected to improve auto-completion
* Mirror structs are not visible to users anymore
* Formatter's architecture that gives the ability to provide custom formatters for user types
* FlushAll is called on Logger's Dispose

### Deprecated


### Removed

* Unity.Mathematics dependency from Logging logic removed
* MTT-1916 reverted (Align mem allocations to 8 bytes)
* Dead code and asmrefs removed
* No need to enable 'unsafe' code to use the package.
* SourceGenerator's code to access file system removed as not used.
* Dead code removed

### Fixed

* Arm64 crash fix
* DisableDirectCall Burst issue that was fixed in 1.6
* Dots Runtime compilation fixes
* Rare race condition fixed
* SetMinimalLogLevelAcrossAllSinks not updating m_MinimalLogLevelAcrossAllSinks fixed
* Lock when trying to log using invalid logging handle.
* Version define in asmdef's changed to comply with Rider IDE requirements
* Timestamp stability improved
* Bug with package detection code, now we generate Log.* code always
* SharedStatic alignment added to fix the crash on ARM
* Omitted message near non-omitted one cause wrong codegen
* Reserved argument names parsed incorrectly
* Crash in some cases when a user calls Log. from non-main thread first (so cctor is called on non-main thread)
* HideInStacktrace now works for burst's directcalls

### Security




## [0.4.0] - 2021-09-17

### Added

* Logger API to be able to Add Sinks after creation
* OnNewLoggerCreated and CallForEveryLogger for LoggerManager
* Ability to change min logging level in runtime
* Test StringSink and UnityDebugLogSink added
* Tests for burst compatibility added

### Changed

* Better error reporting from source generators
* Initialize call for Sinks doesn't require LoggerConfig
* Better Log call detection in the source generators
* il2cpp issue about missing MonoPInvokeCallback is fixed.

### Fixed

* Workaround for Burst bug: https://jira.unity3d.com/browse/DST-548
* Tests issues with il2cpp



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
