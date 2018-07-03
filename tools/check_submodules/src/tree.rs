// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::fmt;
use std::path::Path;

use git2::Repository;
use hex::encode;

use error::Error;

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
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
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
    url.trim_right_matches(".git")
        .replace("www.", "")
        .to_string()
}

impl Git2Tree {
    fn format(&self, level: i32, f: &mut fmt::Formatter) -> fmt::Result {
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
        let repo = Repository::open(path)?;
        let remote = sanitize_url(repo.find_remote("origin")?.url().unwrap().to_string());
        debug!("remote = {:?}", remote);
        let commit = encode(repo.head()?.peel_to_commit()?.id());
        debug!("commit = {:?}", commit);
        let insert = !remotes.contains_key(&remote);
        let flag = if let Some(c) = remotes.get(&remote) {
            debug!("Already found this remote, found commit {:?}", c);
            if &commit != c {
                true
            } else {
                false
            }
        } else {
            false
        };
        if insert {
            debug!("inserting ({:?},{:?})", remote, commit);
            let _ = remotes.insert(remote.clone(), commit.clone());
        }
        let mut children: Vec<Git2Tree> = Vec::new();
        for sm in repo.submodules()? {
            let subpath = path.join(sm.path());
            debug!(
                "stepping into submodule path {:?}, full subpath {:?}",
                sm.path(),
                subpath
            );
            let child = Git2Tree::new_as_subtree(subpath.as_path(), &mut remotes)?;
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
            + self.children
                .iter()
                .fold(0, |acc, ref x| acc + x.count_flagged())
    }
}

impl fmt::Display for Git2Tree {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        self.format(0, f)
    }
}
