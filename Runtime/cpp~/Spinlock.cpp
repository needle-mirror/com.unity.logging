#include "Spinlock.h"

// Web is single threaded and so don't need SpinLock; all APIs are no-ops
#if !defined(__EMSCRIPTEN__)

#include <unordered_map>
#include <stdexcept>
#include <atomic>

static std::unordered_map<int64_t, std::atomic_flag> s_SpinLockMap(3);
static int64_t s_NextKey(1);


int64_t CreateSpinLockImpl();
bool LockImpl(int64_t handle);
bool TryLockImpl(int64_t handle);
void UnlockImpl(int64_t handle);
void DestroySpinLockImpl(int64_t handle);
inline std::atomic_flag& RetrieveLockFromHandle(int64_t handle);

#endif

namespace Unity
{ namespace Logging
{
    DOTS_EXPORT(int64_t) CreateSpinLock()
    {
#if !defined(__EMSCRIPTEN__)
        return CreateSpinLockImpl();
#else
        return 42;
#endif
    }

    DOTS_EXPORT(bool) Lock(int64_t handle)
    {
#if !defined(__EMSCRIPTEN__)
        return LockImpl(handle);
#else
        return true;
#endif
    }

    DOTS_EXPORT(bool) TryLock(int64_t handle)
    {
#if !defined(__EMSCRIPTEN__)
        return TryLockImpl(handle);
#else
        return true;
#endif
    }

    DOTS_EXPORT(void) Unlock(int64_t handle)
    {
#if !defined(__EMSCRIPTEN__)
        UnlockImpl(handle);
#else
        return;
#endif
    }

    DOTS_EXPORT(void) DestroySpinLock(int64_t handle)
    {
#if !defined(__EMSCRIPTEN__)
        DestroySpinLockImpl(handle);
#else
        return;
#endif
    }
}}

#if !defined(__EMSCRIPTEN__)

int64_t CreateSpinLockImpl()
{
    s_NextKey++;
    auto key = s_NextKey;

    s_SpinLockMap.emplace(std::piecewise_construct,
        std::forward_as_tuple(key),
        std::forward_as_tuple());

    return key;
}

bool LockImpl(int64_t handle)
{
    while (RetrieveLockFromHandle(handle).test_and_set(std::memory_order_acquire))  // acquire lock
        ; // spin

    return true;
}

bool TryLockImpl(int64_t handle)
{
    return RetrieveLockFromHandle(handle).test_and_set(std::memory_order_acquire) == false;
}

void UnlockImpl(int64_t handle)
{
    RetrieveLockFromHandle(handle).clear(std::memory_order_release);
}

void DestroySpinLockImpl(int64_t handle)
{
    s_SpinLockMap.erase(handle);
}

inline std::atomic_flag& RetrieveLockFromHandle(int64_t handle)
{
    auto it = s_SpinLockMap.find(handle);
    if (it == s_SpinLockMap.end())
        std::terminate();

    return it->second;
}

#endif
