#pragma once

#include <Unity/Runtime.h>

namespace Unity
{ namespace Logging
{
    DOTS_EXPORT(int64_t) CreateSpinLock();
    DOTS_EXPORT(bool) Lock(int64_t handle);
    DOTS_EXPORT(bool) TryLock(int64_t handle);
    DOTS_EXPORT(void) Unlock(int64_t handle);
    DOTS_EXPORT(void) DestroySpinLock(int64_t handle);
}} // namespace Unity::Logging
