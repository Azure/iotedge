use std::marker::PhantomData;

use tracing::{Event, Level, Subscriber};
use tracing_log::NormalizeEvent;
use tracing_subscriber::fmt::{
    time::ChronoLocal, time::FormatTime, FmtContext, FormatEvent, FormatFields,
};
use tracing_subscriber::registry::LookupSpan;

/// Marker for `Format` that indicates that the syslog format should be used.
pub(crate) struct EdgeHub;

/// Custom event formatter.
pub(crate) struct Format<F = EdgeHub, T = ChronoLocal> {
    format: PhantomData<F>,
    timer: T,
}

impl Default for Format<EdgeHub, ChronoLocal> {
    fn default() -> Self {
        Format {
            format: PhantomData,
            timer: ChronoLocal::with_format("%F %T.%3f %:z".into()),
        }
    }
}

impl<S, N, T> FormatEvent<S, N> for Format<EdgeHub, T>
where
    S: Subscriber + for<'a> LookupSpan<'a>,
    N: for<'a> FormatFields<'a> + 'static,
    T: FormatTime,
{
    fn format_event(
        &self,
        ctx: &FmtContext<'_, S, N>,
        writer: &mut dyn std::fmt::Write,
        event: &Event<'_>,
    ) -> std::fmt::Result {
        let normalized_meta = event.normalized_metadata();
        let meta = normalized_meta.as_ref().unwrap_or_else(|| event.metadata());

        let (fmt_level, fmt_ctx) = (FmtLevel::new(meta.level()), FullCtx::new(&ctx));

        write!(writer, "<{}> ", fmt_level.syslog_level())?;
        self.timer.format_time(writer)?;
        write!(writer, "[{}] [{}{}] - ", fmt_level, fmt_ctx, meta.target(),)?;
        ctx.format_fields(writer, event)?;
        writeln!(writer)
    }
}

/// Wrapper around `Level` to format it accordingly to `syslog` rules.
struct FmtLevel<'a> {
    level: &'a Level,
}

impl<'a> FmtLevel<'a> {
    fn new(level: &'a Level) -> Self {
        Self { level }
    }

    fn syslog_level(&self) -> i8 {
        match *self.level {
            Level::ERROR => 3,
            Level::WARN => 4,
            Level::INFO => 6,
            Level::DEBUG | Level::TRACE => 7,
        }
    }
}

impl<'a> std::fmt::Display for FmtLevel<'a> {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match *self.level {
            Level::TRACE => f.pad("TRC"),
            Level::DEBUG => f.pad("DBG"),
            Level::INFO => f.pad("INF"),
            Level::WARN => f.pad("WRN"),
            Level::ERROR => f.pad("ERR"),
        }
    }
}

/// Wrapper around log entry context to format entry.
struct FullCtx<'a, S, N> {
    ctx: &'a FmtContext<'a, S, N>,
}

impl<'a, S, N: 'a> FullCtx<'a, S, N> {
    fn new(ctx: &'a FmtContext<'a, S, N>) -> Self {
        Self { ctx }
    }
}

impl<'a, S, N> std::fmt::Display for FullCtx<'a, S, N>
where
    S: Subscriber + for<'b> LookupSpan<'b>,
    N: for<'b> FormatFields<'b> + 'static,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let mut seen = false;
        self.ctx.visit_spans(|span| {
            write!(f, "{}", span.name())?;
            seen = true;

            let fields = span.fields();
            if !fields.is_empty() {
                write!(f, "{{{}}}", fields)?;
            }
            ":".fmt(f)
        })?;
        if seen {
            f.pad(" ")?;
        }
        Ok(())
    }
}
