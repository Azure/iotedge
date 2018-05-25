#include <stdarg.h>
#include <stdio.h>
#include <time.h>

#include "hsm_log.h"
#define MAX_LOG_SIZE 256

static int log_level = LVL_ERROR;

void set_log_level(int level)
{
    if ((LVL_DEBUG <= level) && (level <= LVL_ERROR)) {
        log_level = level;
    }
}

void log_msg(int level, const char* file, const char* function, int line, const char* fmt_str, ...)
{
    static char levels[3][5] = {"DBUG", "INFO", "ERR!"};
    static int  syslog_levels[3] = { 7, 6, 3 };

    if (level >= log_level) {
        time_t now;
        char buffer[MAX_LOG_SIZE];
        char time_buf[sizeof("2018-05-24T00:00:00Z")];
        time(&now);
        strftime(time_buf, sizeof(time_buf), "%FT%TZ", gmtime(&now));
        va_list args;
        va_start (args, fmt_str);
        vsnprintf(buffer, MAX_LOG_SIZE, fmt_str, args);
        printf("<%d>%s [%s] (%s:%s:%d) %s\r\n", syslog_levels[level], time_buf, levels[level], file, function, line, buffer);
        va_end (args);
    }
}
