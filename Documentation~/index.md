# Logging package overview

The Unity Logging package is a structured asynchronous logging system that's Burst, and jobs compatible. At this time it isn't intended as a replacement for Debug.Log in a Classic Unity project.

## Dependencies

This library has the following dependencies:

* Jobs package
* Burst package
* Collections package
* Roslyn package for Unity Editors where it is not included into the Editor itself

## Limitations

The Logging package has the following limitations:

- You must adjust the package's asmdef file like so:
  - Enable 'unsafe' code
  - Add 'com.unity.collections'
  - Add 'com.unity.burst'
- Because the logging is asynchronous, you can't rely on it to flush the messages before a crash. This will be addressed in a future release.
- Reference types support is limited, but more reference types will be added in a future release.

## DOTS Runtime Project specifics

To use this library within a DOTS Runtime project, you must use DOTS Runtime version 0.26.
