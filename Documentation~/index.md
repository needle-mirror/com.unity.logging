# Logging package

The Logging package is a versatile and highly configurable structured asynchronous logging solution. In addition to what you would expect from a traditional logging package (level, timestamp, stacktrace), it contains various ways to stream and record the logs  such as StdOut, text or JSON files, DebugLog, and your own custom implementation. You can also individually or collectively configure these logs.

It depends only on Collections package and Burst package; can be used without ECS.

The file-based logs are rolling per default, which prevents your application from taking over the memory space in the case of a long running process such as a server application.

You can also customize the package to create your own serializer. What’s more, you can call the logger from Burst-compatible code.

## Install Logging

To install this package you can either:

* Use **Add package from git URL...** under the **+** menu at the top left of the package manager to add packages either by name (such as `com.unity.entities`), or by Git URL (but this option isn't available for DOTS packages). If you want to use a Git URL instead of just a name in the Package Manager, you must have the git command line tools installed.
* Or, directly edit the `Packages\manifest.json` file in the Unity project. You must add both the package name and its version to the file, which you can find by looking at the documentation of each package (such as `"com.unity.entities" : "x.x.x-preview.x"`).

For more information, see the documentation on [Installing hidden packages](https://docs.unity3d.com/Packages/Installation/manual/index.html).

## Current limitations

Logging has the following limitations:
* It’s not natively supported by the Test Framework, a workaround is to use the `WriteTo.UnityDebugLog` sink at the cost of a negative performance impact.
* The sink for the Editor Console is only available in versions of Unity after 2022.2.0a15. For older versions of the editor a workaround is to use the `WriteTo.UnityDebugLog` sink at the cost of a negative performance impact.
* You can’t log into the Editor Console while running a player.
