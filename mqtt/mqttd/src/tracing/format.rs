//! Code in this module is greatly inspired by `tracing-subscriber` internals.
#![allow(clippy::unused_self)]
use std::{
    fmt::{self, Write},
    marker::PhantomData,
};

use tracing_core::{span, Event, Level, Subscriber};
use tracing_subscriber::{
    fmt::{
        time::{ChronoLocal, FormatTime},
        FmtContext, FormatEvent, FormatFields, FormattedFields,
    },
    registry::LookupSpan,
};

#[cfg(feature = "tracing-log")]
use tracing_log::NormalizeEvent;

#[cfg(feature = "ansi")]
use ansi_term::{Colour, Style};

/// Marker for `Format` that indicates that the syslog format should be used.
pub(crate) struct EdgeHub;

pub(crate) struct Format<F = EdgeHub, T = ChronoLocal> {
    format: PhantomData<F>,
    timer: T,

    #[cfg(feature = "ansi")]
    ansi: bool,
}

impl Format<EdgeHub, ChronoLocal> {
    pub fn edgehub() -> Self {
        Format {
            format: PhantomData,
            timer: ChronoLocal::with_format("%F %T.%3f %:z".into()),

            #[cfg(feature = "ansi")]
            ansi: true,
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
        writer: &mut dyn fmt::Write,
        event: &Event<'_>,
    ) -> fmt::Result {
        #[cfg(feature = "tracing-log")]
        let normalized_meta = event.normalized_metadata();

        #[cfg(feature = "tracing-log")]
        let meta = normalized_meta.as_ref().unwrap_or_else(|| event.metadata());

        #[cfg(not(feature = "tracing-log"))]
        let meta = event.metadata();

        let fmt_level = {
            #[cfg(feature = "ansi")]
            {
                FmtLevel::new(meta.level(), self.ansi)
            }
            #[cfg(not(feature = "ansi"))]
            {
                FmtLevel::new(meta.level())
            }
        };

        write!(writer, "<{}> ", fmt_level.syslog_level())?;

        #[cfg(feature = "ansi")]
        time::write(&self.timer, writer, self.ansi)?;

        #[cfg(not(feature = "ansi"))]
        time::write(&self.timer, writer)?;

        let full_ctx = {
            #[cfg(feature = "ansi")]
            {
                FullCtx::new(ctx, event.parent(), self.ansi)
            }
            #[cfg(not(feature = "ansi"))]
            {
                FullCtx::new(ctx, event.parent())
            }
        };

        write!(writer, "[{}] [{}{}] - ", fmt_level, full_ctx, meta.target(),)?;
        ctx.format_fields(writer, event)?;
        writeln!(writer)
    }
}

struct FmtLevel<'a> {
    level: &'a Level,
    #[cfg(feature = "ansi")]
    ansi: bool,
}

impl<'a> FmtLevel<'a> {
    #[cfg(feature = "ansi")]
    pub(crate) fn new(level: &'a Level, ansi: bool) -> Self {
        Self { level, ansi }
    }

    #[cfg(not(feature = "ansi"))]
    pub(crate) fn new(level: &'a Level) -> Self {
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

const TRACE_STR: &str = "TRC";
const DEBUG_STR: &str = "DBG";
const INFO_STR: &str = "INF";
const WARN_STR: &str = "WRN";
const ERROR_STR: &str = "ERR";

#[cfg(not(feature = "ansi"))]
impl<'a> fmt::Display for FmtLevel<'a> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match *self.level {
            Level::TRACE => f.pad(TRACE_STR),
            Level::DEBUG => f.pad(DEBUG_STR),
            Level::INFO => f.pad(INFO_STR),
            Level::WARN => f.pad(WARN_STR),
            Level::ERROR => f.pad(ERROR_STR),
        }
    }
}

#[cfg(feature = "ansi")]
impl<'a> fmt::Display for FmtLevel<'a> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if self.ansi {
            match *self.level {
                Level::TRACE => write!(f, "{}", Colour::Purple.paint(TRACE_STR)),
                Level::DEBUG => write!(f, "{}", Colour::Blue.paint(DEBUG_STR)),
                Level::INFO => write!(f, "{}", Colour::Green.paint(INFO_STR)),
                Level::WARN => write!(f, "{}", Colour::Yellow.paint(WARN_STR)),
                Level::ERROR => write!(f, "{}", Colour::Red.paint(ERROR_STR)),
            }
        } else {
            match *self.level {
                Level::TRACE => f.pad(TRACE_STR),
                Level::DEBUG => f.pad(DEBUG_STR),
                Level::INFO => f.pad(INFO_STR),
                Level::WARN => f.pad(WARN_STR),
                Level::ERROR => f.pad(ERROR_STR),
            }
        }
    }
}

struct FullCtx<'a, S, N>
where
    S: Subscriber + for<'lookup> LookupSpan<'lookup>,
    N: for<'writer> FormatFields<'writer> + 'static,
{
    ctx: &'a FmtContext<'a, S, N>,
    span: Option<&'a span::Id>,
    #[cfg(feature = "ansi")]
    ansi: bool,
}

impl<'a, S, N: 'a> FullCtx<'a, S, N>
where
    S: Subscriber + for<'lookup> LookupSpan<'lookup>,
    N: for<'writer> FormatFields<'writer> + 'static,
{
    #[cfg(feature = "ansi")]
    pub(crate) fn new(
        ctx: &'a FmtContext<'a, S, N>,
        span: Option<&'a span::Id>,
        ansi: bool,
    ) -> Self {
        Self { ctx, span, ansi }
    }

    #[cfg(not(feature = "ansi"))]
    pub(crate) fn new(ctx: &'a FmtContext<'a, S, N>, span: Option<&'a span::Id>) -> Self {
        Self { ctx, span }
    }

    fn bold(&self) -> Style {
        #[cfg(feature = "ansi")]
        {
            if self.ansi {
                return Style::new().bold();
            }
        }

        Style::new()
    }
}

impl<'a, S, N> fmt::Display for FullCtx<'a, S, N>
where
    S: Subscriber + for<'lookup> LookupSpan<'lookup>,
    N: for<'writer> FormatFields<'writer> + 'static,
{
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let bold = self.bold();
        let mut seen = false;

        let span = self
            .span
            .and_then(|id| self.ctx.span(&id))
            .or_else(|| self.ctx.lookup_current());

        let scope = span.into_iter().flat_map(|span| span.scope());

        for span in scope {
            write!(f, "{}", bold.paint(span.metadata().name()))?;
            seen = true;

            let ext = span.extensions();
            let fields = &ext
                .get::<FormattedFields<N>>()
                .expect("Unable to find FormattedFields in extensions; this is a bug");
            if !fields.is_empty() {
                write!(f, "{}{}{}", bold.paint("{"), fields, bold.paint("}"))?;
            }
            f.write_char(':')?;
        }

        if seen {
            f.write_char(' ')?;
        }
        Ok(())
    }
}

#[cfg(not(feature = "ansi"))]
struct Style;

#[cfg(not(feature = "ansi"))]
impl Style {
    fn new() -> Self {
        Style
    }
    fn paint(&self, d: impl fmt::Display) -> impl fmt::Display {
        d
    }
}

mod time {
    #![allow(clippy::inline_always)]
    use std::fmt::{Result, Write};

    #[cfg(feature = "ansi")]
    use ansi_term::Style;
    use tracing_subscriber::fmt::time::FormatTime;

    #[inline(always)]
    #[cfg(feature = "ansi")]
    pub(crate) fn write<T>(timer: T, writer: &mut dyn Write, with_ansi: bool) -> Result
    where
        T: FormatTime,
    {
        if with_ansi {
            let style = Style::new().dimmed();
            write!(writer, "{}", style.prefix())?;
            timer.format_time(writer)?;
            write!(writer, "{}", style.suffix())?;
        } else {
            timer.format_time(writer)?;
        }
        writer.write_char(' ')?;
        Ok(())
    }

    #[inline(always)]
    #[cfg(not(feature = "ansi"))]
    pub(crate) fn write<T>(timer: T, writer: &mut dyn Write) -> Result
    where
        T: FormatTime,
    {
        timer.format_time(writer)?;
        write!(writer, " ")
    }
}
