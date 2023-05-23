# Changelog

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
