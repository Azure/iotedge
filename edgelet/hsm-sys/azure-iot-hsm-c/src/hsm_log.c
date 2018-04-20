#include <stdarg.h>
#include <stdio.h>

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
    static char levels[3][4] = {"DBG", "INF", "ERR"};

    if (level >= log_level) {
        char buffer[MAX_LOG_SIZE];
        va_list args;
        va_start (args, fmt_str);
        vsprintf (buffer, fmt_str, args);
        printf("%s::%s:%s:%d %s\r\n", levels[level], file, function, line, buffer);
        va_end (args);
    }
}
