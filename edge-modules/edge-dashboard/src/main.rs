// Copyright (c) Microsoft. All rights reserved.

use edge_dashboard::{Context, Error, Main};

fn main() -> Result<(), Error> {
    let context = Context::new();
    let app = Main::new(context);
    app.run()
}
