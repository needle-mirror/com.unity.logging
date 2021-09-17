#include "TimeStamp.h"

#include <time.h>
#include <string>
#include <chrono>

namespace Unity
{
namespace Logging
{
    DOTS_EXPORT(int64_t) GetTimeStamp()
    {
        auto nanosecondsUTC = std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        return nanosecondsUTC;
    }

    DOTS_EXPORT(int) GetFormattedTimeStampString(int64_t tsNano, char* buffer, int bufferSize)
    {
        int64_t ts = tsNano / 1000000000;
        int32_t tsMSec = (int32_t)((tsNano % 1000000000) / 1000000);
        // Not changing to local time, just getting the equivalent tm struct
        const tm *tptr = localtime((const time_t *)&ts);

        // Date format: YYYY-MM-DD HH:MM:SS,mss
        // Month is 0-based and year is based off years since 1900
        int length = snprintf(buffer, bufferSize, "%.4d-%.2d-%.2d %.2d:%.2d:%.2d,%.3d",
            tptr->tm_year + 1900,
            tptr->tm_mon + 1,
            tptr->tm_mday,
            tptr->tm_hour,
            tptr->tm_min,
            tptr->tm_sec,
            tsMSec);

        // If there was an error with date formatting, return a length of 0, meaning no timestamp
        return length < 0 ? 0 : length;
    }

    DOTS_EXPORT(int) GetFormattedTimeStampStringForFileNameNative(int64_t tsNano, char* buffer, int bufferSize)
    {
        int64_t ts = tsNano / 1000000000;
        // Not changing to local time, just getting the equivalent tm struct
        const tm *tptr = localtime((const time_t *)&ts);

        // Date format: YYYYMMDDHHMM
        // Month is 0-based and year is based off years since 1900
        int length = snprintf(buffer, bufferSize, "%.4d%.2d%.2d%.2d%.2d",
            tptr->tm_year + 1900,
            tptr->tm_mon + 1,
            tptr->tm_mday,
            tptr->tm_hour,
            tptr->tm_min);

        // If there was an error with date formatting, return a length of 0, meaning no timestamp
        return length < 0 ? 0 : length;
    }
}
}
