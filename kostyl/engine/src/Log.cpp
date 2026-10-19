#include "kostyl/Log.h"

#include <chrono>
#include <cstdarg>
#include <cstdio>

#ifdef _WIN32
#  ifndef WIN32_LEAN_AND_MEAN
#    define WIN32_LEAN_AND_MEAN
#  endif
#  ifndef NOMINMAX
#    define NOMINMAX
#  endif
#  include <windows.h>
#endif

namespace kostyl::log {

namespace {
const auto g_start = std::chrono::steady_clock::now();

#ifdef _WIN32
// Чтобы кириллица в логах не превращалась в кракозябры в cmd/PowerShell.
[[maybe_unused]] const bool g_utf8Console = [] {
    SetConsoleOutputCP(CP_UTF8);
    return true;
}();
#endif
}

void write(Level level, const char* fmt, ...)
{
    const char* tag = "info";
    switch (level) {
    case Level::Info:  tag = "info "; break;
    case Level::Warn:  tag = "warn "; break;
    case Level::Error: tag = "ERROR"; break;
    }

    const double t = std::chrono::duration<double>(std::chrono::steady_clock::now() - g_start).count();
    std::fprintf(stderr, "[%8.3f] [%s] ", t, tag);

    va_list args;
    va_start(args, fmt);
    std::vfprintf(stderr, fmt, args);
    va_end(args);

    std::fputc('\n', stderr);
    std::fflush(stderr);
}

} // namespace kostyl::log
