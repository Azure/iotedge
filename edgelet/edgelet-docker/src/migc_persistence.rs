use std::io::Write;
use std::sync::{Arc, Mutex};
use std::time::UNIX_EPOCH;
use std::{collections::HashMap, fs, time::Duration};

use edgelet_core::Module;

use crate::{DockerModule, Error};

// TODO: Determine if we need to read from homedir to make it configurable
// const FILE_NAME: &str = "dummy path fill in later";

#[derive(Debug, Clone)]
struct MIGCPersistenceInner {
    filename: String,
}

#[derive(Debug, Clone)]
pub struct MIGCPersistence {
    inner: Arc<Mutex<MIGCPersistenceInner>>,
}

impl MIGCPersistence {
    pub fn new(filename: String) -> Self {
        Self {
            inner: Arc::new(Mutex::new(MIGCPersistenceInner { filename })),
        }
    }

    pub fn write_image_to_file(&self, _id: &str) {
        // from ID, derive image hash (might entail calling ModuleRuntime::list_with_details()) if hash is not readily available

        // get lock
        // read file contents into memory
        // find the image hash (as key) and update the timestamp (as value)
        // write it to the file
        // release lock
    }

    pub async fn prune_images_from_file(
        &self,
        running_modules: std::vec::Vec<(
            DockerModule<http_common::Connector>,
            edgelet_core::ModuleRuntimeState,
        )>,
    ) -> HashMap<String, Duration> {
        let guard = self.inner.lock().unwrap();

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)
        let image_map = get_images_with_timestamp(guard.filename.clone())
            .map_err(|e| e)
            .unwrap();

        /* ============================== */

        // process maps
        let (images_to_delete, carry_over) = process_state(image_map, running_modules);

        /* ============================== */

        // write previously removed entries back to file
        write_images_with_timestamp(&carry_over, guard.filename.clone())
            .map_err(|e| e)
            .unwrap();

        /* ============================== */

        drop(guard);

        // these are the images we need to prune; MIGC file has already been updated
        images_to_delete
    }
}

fn get_images_with_timestamp(filename: String) -> Result<HashMap<String, Duration>, Error> {
    let res = fs::read_to_string(filename);
    if res.is_err() {
        log::error!("Could not read MIGC store");
        return Err(Error::Dummy());
    }

    let contents = res.unwrap();

    let mut image_map: HashMap<String, Duration> = HashMap::new(); // all images in MIGC store

    // TL;DR: this dumps MIGC store contents into the image_map, where
    // Key: Image hash, Value: Timestamp when image was last used (in epoch)
    contents
        .lines()
        .map(|line| line.split(' ').collect::<Vec<&str>>())
        .map(|vec| (vec[0].to_string(), vec[1]))
        .fold((), |_, (k, v)| {
            image_map.insert(k, Duration::from_secs(v.parse::<u64>().unwrap()));
        });

    Ok(image_map)
}

fn write_images_with_timestamp(
    state_to_persist: &HashMap<String, Duration>,
    filename: String,
) -> Result<(), Error> {
    // instead of deleting existing entries from file, we just recreate it
    // TODO: handle synchronization
    let mut file = std::fs::File::create(filename).unwrap();

    for (key, value) in state_to_persist {
        let image_details = format!("{} {:?}\n", key, value);
        let res = write!(file, "{}", image_details);
        if res.is_err() {
            let msg = format!(
                "Could not write image:{} with timestamp:{} to MIGC store",
                key,
                value.as_secs()
            );
            log::error!("{}", msg);
            return Err(Error::Dummy());
        }
    }

    Ok(())
}

fn process_state(
    mut image_map: HashMap<String, Duration>,
    running_modules: Vec<(
        DockerModule<http_common::Connector>,
        edgelet_core::ModuleRuntimeState,
    )>,
) -> (HashMap<String, Duration>, HashMap<String, Duration>) {
    let current_time = std::time::SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap();

    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by MIGC in this run

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for module in running_modules {
        let key = module.0.config().image_hash();

        // Since the images are currently being used, we update the timestamp to the current time
        // This avoids the case where a container crash just as MIGC is kicking off removes a needed image
        carry_over.insert(key.unwrap().to_string(), current_time);
    }

    // track entries younger than min age
    // TODO: read min_age from settings, let's assume min_age as 1 day for now
    for (key, value) in &image_map {
        if current_time.as_secs() - value.as_secs() < 86400 {
            carry_over.insert(key.to_string(), *value);
        }
    }

    // clean up image map to make sure entries that need to be preserved are not removed
    for key in carry_over.keys() {
        image_map.remove(key);
    }

    (image_map, carry_over)
}
