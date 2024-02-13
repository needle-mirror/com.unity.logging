# Logging package

The Logging package is a versatile and highly configurable structured asynchronous logging solution. It is meant to be used in high-performance servers and other niche applications where performant logging is required. It is _not_ a complete replacement for the traditional `Debug.Log` APIs. If your application does not have specialized logging needs, it is still recommended to use the `Debug.Log` approach as it is more mature and supports more common use cases (e.g. in-editor iteration).

In addition to what you would expect from a traditional logging package (level, timestamp, stacktrace), it contains various ways to stream and record the logs  such as standard out, text or JSON files, `Debug.Log`, and your own custom implementation. You can also individually or collectively configure these logs.

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
* Context highlighting (the possibility of clicking on a log line and selecting the game object that emitted it) is not functional. There is no workaround for this limitation as the logging package is not meant to be used for local iteration in the editor.
