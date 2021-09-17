#include <stdio.h>
#include "DotsRuntimePrintWrapper.h"

namespace Unity
{
namespace Logging
{
    DOTS_EXPORT(void) ConsoleWrite(const unsigned char* byteBuffer, int numBytes, int8_t newLine)
    {
        printf("%.*s%s", numBytes, (const char*)byteBuffer, newLine ? "\n" : "");
    }

    DOTS_EXPORT(void) BeginBatchConsoleWrite()
    {
        const size_t n = 8192;
        static char buf[n];
        setvbuf(stdout, buf, _IOFBF, n);
    }

    DOTS_EXPORT(void) Flush()
    {
        fflush(stdout);
    }

    DOTS_EXPORT(void) EndBatchConsoleWrite()
    {
        Flush();
    }
}
}
