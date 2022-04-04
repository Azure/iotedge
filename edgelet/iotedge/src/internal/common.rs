use anyhow::anyhow;
use anyhow::Context;
use nix::unistd::{Uid, User};

// In production, running as root is the easiest way to guarantee the tool has write access to every service's config file.
// But it's convenient to not do this for the sake of development because the the development machine doesn't necessarily
// have the package installed and the users created, and it's easier to have the config files owned by the current user anyway.
//
// So when running as root, get the four users appropriately.
// Otherwise, if this is a debug build, fall back to using the current user.
// Otherwise, tell the user to re-run as root.

pub(crate) fn get_system_user(name: &str) -> anyhow::Result<User> {
    if Uid::current().is_root() {
        Ok(User::from_name(name)
            .with_context(|| format!("could not query {} user information", name))?
            .ok_or_else(|| anyhow!("could not query {} user information", name))?)
    } else if cfg!(debug_assertions) {
        Ok(User::from_uid(Uid::current())
            .context("could not query current user information")?
            .ok_or_else(|| anyhow!("could not query current user information"))?)
    } else {
        Err(anyhow!("this command must be run as root"))
    }
}
