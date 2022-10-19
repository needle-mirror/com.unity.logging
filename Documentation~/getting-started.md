# Get started

To get started, you can either create your own assembly definition file, or modify the attached samples.

## Create your own assembly definition file

Create an assembly definition file called `LoggingApplicationExample.asmdef` and add the following packages to it:

* Unity.Logging
* Unity.Burst
* Unity.Collections

Then, create a MonoBehaviour script called `LoggingApplicationExample.cs` with the following:

```c#
using Unity.Logging;

public class UserLogger: MonoBehaviour
{
    void Awake()
    {
        Log.Info("Hello, {username}!", "World");
    }
}
```
## Use the included sample

The Logging package contains an example file and the corresponding `asmdef` file. Copy `LoggingSample` files into a new project and modify the sample to get started.
