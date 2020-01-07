#include <stdarg.h>
#include <stdbool.h>
#include <stdio.h>
#include <time.h>

#include "hsm_log.h"

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
#include <Windows.h>
#endif

#define MAX_LOG_SIZE 256

static bool g_is_log_initialized = false;

static int log_level = LVL_INFO;

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
static HANDLE event_log_handle = NULL;
#endif

void log_init(int level) {
    if (!g_is_log_initialized) {
        if ((LVL_DEBUG <= level) && (level <= LVL_ERROR)) {
            log_level = level;
        }

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        // Emit logs as events if running as a service, ie not running in console mode
        if (GetEnvironmentVariable("IOTEDGE_RUN_AS_CONSOLE", NULL, 0) == 0) {
            if (GetLastError() == ERROR_ENVVAR_NOT_FOUND) {
                event_log_handle = RegisterEventSourceA(NULL, "iotedged");

                // Ignore errors. It just means event_log_handle will remain NULL, so logs will not be emitted as Windows events.
            }
        }
#endif

        g_is_log_initialized = true;

        LOG_INFO("Initialized logging");
    }
}

void log_msg(int level, const char* file, const char* function, int line, const char* fmt_str, ...)
{
    static char levels[3][5] = {"DBUG", "INFO", "ERR!"};
    static int  syslog_levels[3] = { 7, 6, 3 };

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    static WORD event_log_levels[3] = { EVENTLOG_SUCCESS, EVENTLOG_INFORMATION_TYPE, EVENTLOG_ERROR_TYPE };

    // The values returned here must match the event message IDs specified
    // in the event_messages.mc file.
    static DWORD event_log_ids[3] = { 4, 3, 1 };
#endif

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

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        if (event_log_handle != NULL) {
            size_t log_length = strlen(file) + strlen(function) + strlen(buffer);
            log_length += 50; // Extra bytes for prefix, punctuation and whitespace
            char* event_log_buffer = malloc(log_length);

            if (snprintf(event_log_buffer, log_length, "libiothsm -- (%s:%s:%d) %s", file, function, line, buffer) > 0) {
                char* event_log_strings[] = { NULL };
                event_log_strings[0] = event_log_buffer;
                ReportEventA(
                    event_log_handle,
                    event_log_levels[level],
                    0,
                    event_log_ids[level],
                    NULL,
                    1,
                    0,
                    event_log_strings,
                    NULL
                );
            }

            free(event_log_buffer);
        }
#endif
    }
}
