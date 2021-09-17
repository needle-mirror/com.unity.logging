using Unity.Logging;
using Unity.Logging.Sinks;

public class SimpleWriteCallTest
{
    void Init()
    {
        Log.Info("Hello world");
    }

    public static void SomeFunction()
    {
    }
}
