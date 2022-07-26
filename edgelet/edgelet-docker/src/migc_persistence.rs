use std::io::Write;
use std::sync::{Arc, Mutex};
use std::time::UNIX_EPOCH;
use std::{collections::HashMap, fs, time::Duration};

use edgelet_core::Module;
use edgelet_settings::base::image::MIGCSettings;

use crate::{DockerModule, Error};

// TODO: Determine if we need to read from homedir to make it configurable
// const FILE_NAME: &str = "dummy path fill in later";

#[derive(Debug, Clone)]
struct MIGCPersistenceInner {
    filename: String,
    settings: Option<MIGCSettings>,
}

#[derive(Debug, Clone)]
pub struct MIGCPersistence {
    inner: Arc<Mutex<MIGCPersistenceInner>>,
}

impl MIGCPersistence {
    pub fn new(filename: String, settings: Option<MIGCSettings>) -> Self {
        Self {
            inner: Arc::new(Mutex::new(MIGCPersistenceInner { filename, settings })),
        }
    }

    pub fn record_image_use_timestamp(&self, name_or_id: &str, is_image_id: bool) {
        let guard = self.inner.lock().unwrap();

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)
        let mut image_map = get_images_with_timestamp(guard.filename.clone())
            .map_err(|e| e)
            .unwrap();

        let current_time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap();

        // We don't know if what has been passed in is the image name or image id
        // Since there's no easy way to know, we read the MIGC file and see if the
        // name_or_id is present in it. If so, we know it's an image hash.
        // If not, it's the image name, and we now need to determine the corresponding
        // hash by looking it up by a call to the Docker Engine API.

        if is_image_id || image_map.contains_key(name_or_id) {
            image_map.insert(name_or_id.to_string(), current_time);
        } else {
            drop(guard);

            // At this point, one may wonder if it's just easier to always get the
            // list of images at the beginning of the method, before the mutex is
            // acquired.
            // A choice has been made to read the file first and only call the
            // docker api if necessary for two reasons:
            // 1) Intuitively, it feels like a file read might be faster than a call
            // to the docker api (but only benchmarking will truly tell)
            // 2) It's the "cache-miss" path. If the image hash is present, then
            // wny call the docker api?

            // TODO: let result = ModuleRuntime::list_images(&self);
            let result: HashMap<String, String> = HashMap::new();
            let image_id = result.get(name_or_id).unwrap();

            // we have found the image id, but a recursive call will be an infinite loop
            // without the is_image_id flag set to true
            return self.record_image_use_timestamp(image_id, true);
        }

        // write entries back to file
        write_images_with_timestamp(&image_map, guard.filename.clone())
            .map_err(|e| e)
            .unwrap();

        drop(guard);
    }

    pub async fn prune_images_from_file(
        &self,
        running_modules: std::vec::Vec<(
            DockerModule<http_common::Connector>,
            edgelet_core::ModuleRuntimeState,
        )>,
    ) -> HashMap<String, Duration> {
        let guard = self.inner.lock().unwrap();
        let settings = guard.settings.clone().unwrap();

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)
        let image_map = get_images_with_timestamp(guard.filename.clone())
            .map_err(|e| e)
            .unwrap();

        /* ============================== */

        // process maps
        let (images_to_delete, carry_over) =
            process_state(image_map, running_modules, settings.min_age());

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
    min_age: Duration,
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
    for (key, value) in &image_map {
        if current_time.as_secs() - value.as_secs() < min_age.as_secs() {
            carry_over.insert(key.to_string(), *value);
        }
    }

    // clean up image map to make sure entries that need to be preserved are not removed
    for key in carry_over.keys() {
        image_map.remove(key);
    }

    (image_map, carry_over)
}
