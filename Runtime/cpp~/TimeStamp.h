#pragma once

#include <Unity/Runtime.h>

namespace Unity
{
namespace Logging
{
    DOTS_EXPORT(int64_t) GetTimeStamp();
    DOTS_EXPORT(int) GetFormattedTimeStampString(int64_t ts, char* buffer, int bufferSize);
    DOTS_EXPORT(int) GetFormattedTimeStampStringForFileName(int64_t ts, char* buffer, int bufferSize);
}
} // namespace Unity::Logging
