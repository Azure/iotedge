#ifndef HSM_LOG_H
#define HSM_LOG_H

#define LVL_DEBUG 0
#define LVL_INFO 1
#define LVL_ERROR 2

#define LOG_ERROR(fmt, ...) log_msg(LVL_ERROR, __FILE__, __func__, __LINE__, fmt, ##__VA_ARGS__)
#define LOG_DEBUG(fmt, ...) log_msg(LVL_DEBUG, __FILE__, __func__, __LINE__, fmt, ##__VA_ARGS__)
#define LOG_INFO(fmt, ...)  log_msg(LVL_INFO, __FILE__, __func__, __LINE__, fmt, ##__VA_ARGS__)

extern void log_init(int level);
extern void log_msg(int level, const char* file, const char* function, int line, const char* fmt_str, ...)
#if defined(__GNUC__) || defined(__clang__)
    __attribute__ ((format (printf, 5, 6)));
#endif
;

#endif  //HSM_LOG_H
