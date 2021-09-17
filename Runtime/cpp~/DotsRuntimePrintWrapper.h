#pragma once

#include <Unity/Runtime.h>

namespace Unity
{
namespace Logging
{
    // In the context of Tiny Web players this will log out to console
    DOTS_EXPORT(void) ConsoleWrite(const unsigned char* byteBuffer, int numBytes, int8_t newLine);
    DOTS_EXPORT(void) BeginBatchConsoleWrite();
    DOTS_EXPORT(void) Flush();
    DOTS_EXPORT(void) EndBatchConsoleWrite();
}
} // namespace Unity::Logging
