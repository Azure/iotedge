// Copyright (c) Microsoft. All rights reserved.

use winapi::shared::ntdef::HANDLE;

#[derive(Debug)]
pub struct Handle(HANDLE);

unsafe impl Send for Handle {}
unsafe impl Sync for Handle {}

impl Handle {
    pub fn new(handle: HANDLE) -> Handle {
        Handle(handle)
    }

    pub fn raw(&self) -> HANDLE {
        self.0
    }
}
