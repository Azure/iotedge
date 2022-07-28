use std::io::Write;
use std::sync::{Arc, Mutex};
use std::time::UNIX_EPOCH;
use std::{collections::HashMap, fs, time::Duration};

use edgelet_core::Module;
use edgelet_settings::base::image::MIGCSettings;

use crate::{DockerModule, Error};

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

    pub async fn record_image_use_timestamp(
        &self,
        name_or_id: &str,
        is_image_id: bool,
        image_name_to_id: HashMap<String, String>, // HashMap<image_name, image_id>
    ) {
        if is_image_id {
            self.write_image_use_to_file(name_or_id).await;
        } else {
            let _ = match image_name_to_id.get(name_or_id) {
                Some(id) => {
                    return self.write_image_use_to_file(id).await;
                }
                None => {
                    log::error!("Could not find image with id: {}", name_or_id);
                    return;
                }
            };
        }
    }

    pub async fn prune_images_from_file(
        &self,
        running_modules: std::vec::Vec<(
            DockerModule<http_common::Connector>,
            edgelet_core::ModuleRuntimeState,
        )>,
    ) -> HashMap<String, Duration> {
        let guard = self
            .inner
            .lock()
            .expect("Could not lock images file for image garbage collection");

        let settings = guard.settings.clone().unwrap();

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)
        let image_map = match get_images_with_timestamp(guard.filename.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::error!("Could not read auto-prune data; image garbage collection did not prune any images: {}", e);
                return HashMap::new();
            }
        };

        /* ============================== */

        // process maps
        let (images_to_delete, carry_over) =
            process_state(image_map, running_modules, settings.min_age());

        /* ============================== */

        // write previously removed entries back to file
        _ = match write_images_with_timestamp(&carry_over, guard.filename.clone()).map_err(|e| e) {
            Ok(_) => {}
            Err(_) => {
                // nothing to do: images will still be deleted, but file that tracks LRU images was not updated
                // next run will try to delete images that have already been deleted
            }
        };

        /* ============================== */

        drop(guard);

        // these are the images we need to prune; MIGC file has already been updated
        images_to_delete
    }

    /* ===================================== HELPER METHODS ==================================== */

    async fn write_image_use_to_file(&self, name_or_id: &str) {
        let guard = self
            .inner
            .lock()
            .expect("Could not lock images file for image garbage collection");

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)

        let mut image_map = match get_images_with_timestamp(guard.filename.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::error!("Could not read auto-prune data; image garbage collection did not prune any images: {}", e);
                return;
            }
        };

        let current_time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        image_map.insert(name_or_id.to_string(), current_time);

        // write entries back to file
        _ = write_images_with_timestamp(&image_map, guard.filename.clone());

        drop(guard);
    }

    /*pub async fn record_image_use_timestamp(&self, name_or_id: &str, is_image_id: bool) {
        let guard = self.inner.lock().expect("Could not lock images file for image garbage collection")

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)
        let mut image_map = get_images_with_timestamp(guard.filename.clone())
            .map_err(|e| e)
            .unwrap();

        let current_time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        // We don't know if what has been passed in is the image name or image id
        // Since there's no easy way to know, we read the MIGC file and see if the
        // name_or_id is present in it. If so, we know it's an image hash.
        // If not, it's the image name, and we now need to determine the corresponding
        // hash by looking it up by a call to the Docker Engine API.

        if is_image_id || image_map.contains_key(name_or_id) {
            image_map.insert(name_or_id.to_string(), current_time);

            // write entries back to file
            write_images_with_timestamp(&image_map, guard.filename.clone())
                .map_err(|e| e)
                .unwrap();

            drop(guard);
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

            // HashMap<image_name, image_id>
            let result = ModuleRuntime::list_images(&self.runtime).await.unwrap();

            let _ = match result.get(name_or_id) {
                Some(id) => {
                    // we have found the image id, but a recursive call will be an infinite loop
                    // without the is_image_id flag set to true
                    return self.record_image_use_timestamp(id, true).await;
                }
                None => {
                    log::error!("Could not find image with id: {}", name_or_id);
                    // bubble error up?
                    return;
                }
            };
        }
    }*/
}

fn get_images_with_timestamp(filename: String) -> Result<HashMap<String, Duration>, Error> {
    let res = fs::read_to_string(filename);
    if res.is_err() {
        let msg = "Could not read image persistence data";
        log::error!("{}", msg);
        return Err(Error::FileOperation(msg.to_string()));
    }

    let contents = res.expect("Reading image persistence data failed");

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
    // write to a temp file and then rename/overwrite to image persistence file
    let temp_file = "/tmp/images.txt";
    let mut file =
        std::fs::File::create(temp_file).expect("Could not create images.txt under /tmp");

    for (key, value) in state_to_persist {
        let image_details = format!("{} {:?}\n", key, value);
        let res = write!(file, "{}", image_details);
        if res.is_err() {
            let msg = format!(
                "Could not write image:{} with timestamp:{} to store",
                key,
                value.as_secs()
            );
            log::error!("{}", msg);
            return Err(Error::FileOperation(msg.to_string()));
        }
    }

    // add retries?
    _ = match fs::rename(temp_file, filename) {
        Ok(_) => Ok(()),
        Err(_) => Err(Error::FileOperation(
            "Could not update auto-prune data; next run may try to delete images that are no longer present on device".to_string(),
        )),
    };

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
        .expect("Could not get EPOCH time");

    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by MIGC in this run

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for module in running_modules {
        let key = module
            .0
            .config()
            .image_hash()
            .expect("Could not get image id from module");

        // Since the images are currently being used, we update the timestamp to the current time
        // This avoids the case where a container crash just as MIGC is kicking off removes a needed image
        carry_over.insert(key.to_string(), current_time);
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
