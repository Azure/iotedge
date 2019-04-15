use std::io::Write;

use termcolor::WriteColor;

pub(super) enum Stdout {
    ColoredText {
        stdout: termcolor::StandardStream,
        success_color_spec: termcolor::ColorSpec,
        warning_color_spec: termcolor::ColorSpec,
        error_color_spec: termcolor::ColorSpec,
    },

    DefaultJson,

    DefaultText,
}

impl Stdout {
    pub(super) fn new(output_format: super::OutputFormat) -> Self {
        if output_format == super::OutputFormat::Text && atty::is(atty::Stream::Stdout) {
            let stdout = termcolor::StandardStream::stdout(termcolor::ColorChoice::Auto);

            let mut success_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Green` maps to `FG_GREEN` which is too hard to read on the default blue-background profile that PS uses.
                // PS uses `FG_GREEN | FG_INTENSITY` == 8 == `[ConsoleColor]::Green` as the foreground color for its error text,
                // so mimic that.
                success_color_spec.set_fg(Some(termcolor::Color::Rgb(0, 255, 0)));
            } else {
                success_color_spec.set_fg(Some(termcolor::Color::Green));
            }

            let mut warning_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Yellow` maps to `FOREGROUND_GREEN | FOREGROUND_RED` == 6 == `ConsoleColor::DarkYellow`.
                // In its default blue-background profile, PS uses `ConsoleColor::DarkYellow` as its default foreground text color
                // and maps it to a dark gray.
                //
                // So use explicit RGB to define yellow for Windows. Also use a black background to mimic PS warnings.
                //
                // Ref:
                // - https://docs.rs/termcolor/0.3.6/src/termcolor/lib.rs.html#1380 defines `termcolor::Color::Yellow` as `wincolor::Color::Yellow`
                // - https://docs.rs/wincolor/0.1.6/x86_64-pc-windows-msvc/src/wincolor/win.rs.html#18
                //   defines `wincolor::Color::Yellow` as `FG_YELLOW`, which in turn is `FOREGROUND_GREEN | FOREGROUND_RED`
                // - https://docs.microsoft.com/en-us/windows/console/char-info-str defines `FOREGROUND_GREEN | FOREGROUND_RED` as `2 | 4 == 6`
                // - https://docs.microsoft.com/en-us/dotnet/api/system.consolecolor#fields defines `6` as `[ConsoleColor]::DarkYellow`
                // - `$Host.UI.RawUI.ForegroundColor` in the default PS profile is `DarkYellow`, and writing in it prints dark gray text.
                warning_color_spec.set_fg(Some(termcolor::Color::Rgb(255, 255, 0)));
                warning_color_spec.set_bg(Some(termcolor::Color::Black));
            } else {
                warning_color_spec.set_fg(Some(termcolor::Color::Yellow));
            }

            let mut error_color_spec = termcolor::ColorSpec::new();
            if cfg!(windows) {
                // `Color::Red` maps to `FG_RED` which is too hard to read on the default blue-background profile that PS uses.
                // PS uses `FG_RED | FG_INTENSITY` == 12 == `[ConsoleColor]::Red` as the foreground color for its error text,
                // with black background, so mimic that.
                error_color_spec.set_fg(Some(termcolor::Color::Rgb(255, 0, 0)));
                error_color_spec.set_bg(Some(termcolor::Color::Black));
            } else {
                error_color_spec.set_fg(Some(termcolor::Color::Red));
            }

            Stdout::ColoredText {
                stdout,
                success_color_spec,
                warning_color_spec,
                error_color_spec,
            }
        } else {
            match output_format {
                super::OutputFormat::Json => Stdout::DefaultJson,
                super::OutputFormat::Text => Stdout::DefaultText,
            }
        }
    }

    pub(super) fn write_success<F>(&mut self, f: F)
    where
        F: FnOnce(&mut dyn Write) -> std::io::Result<()>,
    {
        let result = match self {
            Stdout::ColoredText {
                stdout,
                success_color_spec,
                ..
            } => write_colored(stdout, success_color_spec, f),
            Stdout::DefaultJson => Ok(()),
            Stdout::DefaultText => f(&mut std::io::stdout()),
        };
        result.expect("could not write to stdout");
    }

    pub(super) fn write_warning<F>(&mut self, f: F)
    where
        F: FnOnce(&mut dyn Write) -> std::io::Result<()>,
    {
        let result = match self {
            Stdout::ColoredText {
                stdout,
                warning_color_spec,
                ..
            } => write_colored(stdout, warning_color_spec, f),
            Stdout::DefaultJson => Ok(()),
            Stdout::DefaultText => f(&mut std::io::stdout()),
        };
        result.expect("could not write to stdout");
    }

    pub(super) fn write_error<F>(&mut self, f: F)
    where
        F: FnOnce(&mut dyn Write) -> std::io::Result<()>,
    {
        let result = match self {
            Stdout::ColoredText {
                stdout,
                error_color_spec,
                ..
            } => write_colored(stdout, error_color_spec, f),
            Stdout::DefaultJson => Ok(()),
            Stdout::DefaultText => f(&mut std::io::stdout()),
        };
        result.expect("could not write to stdout");
    }
}

fn write_colored<F>(
    stdout: &mut termcolor::StandardStream,
    spec: &termcolor::ColorSpec,
    f: F,
) -> std::io::Result<()>
where
    F: FnOnce(&mut dyn Write) -> std::io::Result<()>,
{
    let _ = stdout.set_color(spec);
    let result = f(stdout);
    let _ = stdout.reset();
    result
}
