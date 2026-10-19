#pragma once

// Простейший логгер: printf-формат, вывод в stderr с меткой времени.
//   KLOG_INFO("Окно %dx%d", w, h);

namespace kostyl::log {

enum class Level { Info, Warn, Error };

#if defined(__GNUC__) || defined(__clang__)
__attribute__((format(printf, 2, 3)))
#endif
void write(Level level, const char* fmt, ...);

} // namespace kostyl::log

#define KLOG_INFO(...)  ::kostyl::log::write(::kostyl::log::Level::Info,  __VA_ARGS__)
#define KLOG_WARN(...)  ::kostyl::log::write(::kostyl::log::Level::Warn,  __VA_ARGS__)
#define KLOG_ERROR(...) ::kostyl::log::write(::kostyl::log::Level::Error, __VA_ARGS__)
