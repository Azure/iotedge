// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::fmt;
use std::path::Path;

use failure::Fail;
use git2::Repository;
use hex::encode;
use log::debug;

use crate::error::{Error, ErrorKind};

type RemoteUrl = String;
type CommitId = String;
type RemoteMap = HashMap<RemoteUrl, CommitId>;

#[derive(Debug)]
struct GitModule {
    remote: RemoteUrl,
    commit: CommitId,
    flag: bool,
}

impl fmt::Display for GitModule {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{} : {} {}",
            self.remote,
            self.commit,
            if self.flag { "****" } else { "" }
        )?;
        Ok(())
    }
}

impl GitModule {
    pub fn new(remote: String, commit: String, flag: bool) -> Self {
        GitModule {
            remote,
            commit,
            flag,
        }
    }
}

pub struct Git2Tree {
    root: GitModule,
    children: Vec<Git2Tree>,
}

fn sanitize_url(url: String) -> String {
    url.trim_end_matches(".git").replace("www.", "").to_string()
}

impl Git2Tree {
    fn format(&self, level: i32, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if self.root.flag {
            writeln!(
                f,
                "*** FAILURE ***  Line which follows has a mismatched commit"
            )?;
        }
        for _l in 0..level {
            write!(f, "  ")?;
        }
        write!(f, "|- ")?;
        write!(f, "{}\n", self.root)?;
        for child in self.children.iter() {
            child.format(level + 1, f)?;
        }
        Ok(())
    }

    fn new_as_subtree(path: &Path, mut remotes: &mut RemoteMap) -> Result<Self, Error> {
        debug!("repo path {:?}", path);
        let repo =
            Repository::open(path).map_err(|err| Error::from(err.context(ErrorKind::Git)))?;
        let remote = sanitize_url(
            repo.find_remote("origin")
                .map_err(|err| Error::from(err.context(ErrorKind::Git)))?
                .url()
                .unwrap()
                .to_string(),
        );
        debug!("remote = {:?}", remote);
        let commit = encode(
            repo.head()
                .map_err(|err| Error::from(err.context(ErrorKind::Git)))?
                .peel_to_commit()
                .map_err(|err| Error::from(err.context(ErrorKind::Git)))?
                .id(),
        );
        debug!("commit = {:?}", commit);
        let flag = remotes.get(&remote).map_or(false, |c| &commit != c);
        remotes.entry(remote.clone()).or_insert(commit.clone());

        let mut children: Vec<Git2Tree> = Vec::new();
        for sm in repo
            .submodules()
            .map_err(|err| Error::from(err.context(ErrorKind::Git)))?
        {
            let child = Git2Tree::new_as_subtree(path.join(sm.path()).as_path(), &mut remotes)?;
            children.push(child);
        }
        Ok(Git2Tree {
            root: GitModule::new(remote, commit, flag),
            children,
        })
    }

    pub fn new(path: &Path) -> Result<Self, Error> {
        let mut remotes: RemoteMap = HashMap::new();
        Git2Tree::new_as_subtree(path, &mut remotes)
    }

    pub fn count_flagged(&self) -> i64 {
        let count = if self.root.flag { 1 } else { 0 };
        count
            + self
                .children
                .iter()
                .fold(0, |acc, ref x| acc + x.count_flagged())
    }
}

impl fmt::Display for Git2Tree {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.format(0, f)
    }
}
