using Unity.Logging;
using Unity.Logging.Sinks;

public class ComplexWriteCallTest
{
    void Deinit()
    {
        Log.Info("1");
    }

    public struct ContextTestStruct1
    {
        public int Field1;

        public override string ToString()
        {
            return $"[{Field1}]";
        }
    }

    public struct ContextTestStruct2
    {
        public float Field1;
        public int Field2;

        public override string ToString()
        {
            return $"[{Field1}, {Field2}]";
        }
    }

    public struct ContextTestStruct3
    {
        public float Field1;
        public int Field2;
        public bool Field3;

        public override string ToString()
        {
            return $"[{Field1}, {Field2}, {Field3}]";
        }
    }

    public static void SomeFunction()
    {
        var C1 = new[]
        {
            new ContextTestStruct1
            {
                Field1 = 101,
            },
            new ContextTestStruct1
            {
                Field1 = 2,
            },
            new ContextTestStruct1
            {
                Field1 = 3,
            },
            new ContextTestStruct1
            {
                Field1 = -1,
            },
            new ContextTestStruct1
            {
                Field1 = 999,
            },
        };

        var C2 = new[]
        {
            new ContextTestStruct2
            {
                Field1 = 0.001f,
                Field2 = 42,
            },
            new ContextTestStruct2
            {
                Field1 = 1000.1f,
                Field2 = 999,
            },
            new ContextTestStruct2
            {
                Field1 = -2.0f,
                Field2 = 12345,
            },
        };

        var C3 = new[]
        {
            new ContextTestStruct3
            {
                Field1 = 3.14f,
                Field2 = 1001,
                Field3 = true,
            },
            new ContextTestStruct3
            {
                Field1 = 1.00001f,
                Field2 = 1234,
                Field3 = false,
            },
            new ContextTestStruct3
            {
                Field1 = 123.456f,
                Field2 = 128,
                Field3 = true,
            },
        };
        Log.Info("This message has {0} contexts - {2}{1}", C1[2], C2[1], C3[1]);
    }
}
