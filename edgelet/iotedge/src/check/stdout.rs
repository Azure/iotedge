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
            success_color_spec.set_fg(Some(termcolor::Color::Green));

            let mut warning_color_spec = termcolor::ColorSpec::new();
            warning_color_spec.set_fg(Some(termcolor::Color::Yellow));

            let mut error_color_spec = termcolor::ColorSpec::new();
            error_color_spec.set_fg(Some(termcolor::Color::Red));

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
    stdout
        .set_color(spec)
        .expect("failed to set terminal color");
    let result = f(stdout);
    stdout.reset().expect("failed to reset terminal color");
    result
}
