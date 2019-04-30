// Copyright (c) Microsoft. All rights reserved.

use crate::app;
use crate::error::Error;

pub fn run() -> Result<(), Error> {
    let settings = app::init()?;
    let main = super::Main::new(settings);

    main.run()?;
    Ok(())
}
