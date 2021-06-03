// Copyright (c) Microsoft. All rights reserved.

use std::io::{Seek, Write};

// TODO: make tokio
use std::process::Command as ShellCommand;

use failure::Fail;
use zip::write::FileOptions;
use zip::ZipWriter;

use crate::error::{Error, ErrorKind};

pub async fn write_inspect<W>(
    module_name: &str,
    zip_writer: &mut ZipWriter<W>,
    file_options: &FileOptions,
) -> Result<(), Error>
where
    W: Write + Seek,
{
    print_verbose(&format!("Running docker inspect for {}", module_name));

    let mut inspect = ShellCommand::new("docker");
    inspect.arg("inspect").arg(&module_name);
    let inspect = inspect.output();

    let (file_name, output) = if let Ok(result) = inspect {
        if result.status.success() {
            (format!("inspect/{}.json", module_name), result.stdout)
        } else {
            (format!("inspect/{}_err.json", module_name), result.stderr)
        }
    } else {
        let err_message = inspect.err().unwrap().to_string();
        println!(
            "Could not reach docker. Including error in bundle.\nError message: {}",
            err_message
        );
        (
            format!("inspect/{}_err_docker.txt", module_name),
            err_message.as_bytes().to_vec(),
        )
    };

    zip_writer
        .start_file(file_name, *file_options)
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

    zip_writer
        .write_all(&output)
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

    print_verbose(&format!("Got docker inspect for {}", module_name));

    Ok(())
}

fn print_verbose<S>(message: S)
where
    S: std::fmt::Display,
{
    if true {
        println!("{}", message);
    }
}
