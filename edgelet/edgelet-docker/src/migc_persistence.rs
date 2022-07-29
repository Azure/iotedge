use std::io::Write;
use std::sync::{Arc, Mutex};
use std::time::UNIX_EPOCH;
use std::{collections::HashMap, fs, time::Duration};

use edgelet_core::Module;
use edgelet_settings::base::image::MIGCSettings;

use crate::{DockerModule, Error};

const TEMP_FILE: &str = "/tmp/images";

#[derive(Debug, Clone)]
struct MIGCPersistenceInner {
    filename: String,
    settings: MIGCSettings,
}

#[derive(Debug, Clone)]
pub struct MIGCPersistence {
    inner: Arc<Mutex<MIGCPersistenceInner>>,
}

impl MIGCPersistence {
    pub fn new(filename: String, settings: Option<MIGCSettings>) -> Self {
        // TODO: if no migc settings are generated, it means MIGC should be disabled.
        // For now I am unwrapping settings, but when we add enabled / disabled flag,
        // we need to create a MIGCSettings instance that is disabled.
        Self {
            inner: Arc::new(Mutex::new(MIGCPersistenceInner {
                filename,
                settings: settings.unwrap(),
            })),
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
            match image_name_to_id.get(name_or_id) {
                Some(id) => {
                    return self.write_image_use_to_file(id).await;
                }
                None => {
                    log::error!("Could not find image with id: {}", name_or_id);
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
    ) -> Result<HashMap<String, Duration>, Error> {
        let guard = self.inner.lock().unwrap();

        // TODO: If MIGC is disabled we need to not execute. Add check for whether MIGC is disabled and skip logic if so.
        let settings = guard.settings.clone();

        // Read MIGC persistence file into in-mem map. This map now contains
        // all images deployed to the device (through an IoT Edge deployment).
        // If MIGC persistence file cannot be read we will return a new map so
        // new MIGC persistence file will be created.
        let image_map = match get_images_with_timestamp(guard.filename.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::error!("Could not read image auto-prune data. Image garbage collection did not prune any images. {}", e);
                return Ok(HashMap::new());
            }
        };

        /* ============================== */

        // process maps
        let (images_to_delete, carry_over) =
            process_state(image_map, running_modules, settings.min_age())?;

        /* ============================== */

        // write previously removed entries back to file
        if let Err(e) =
            write_images_with_timestamp(&carry_over, TEMP_FILE.to_string(), guard.filename.clone())
        {
            log::error!("Failed to update image auto pruning persistence file. File will be updated on next scheduled run. {}", e)
        };

        /* ============================== */

        drop(guard);

        // these are the images we need to prune; MIGC file has already been updated
        Ok(images_to_delete)
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
        let _res =
            write_images_with_timestamp(&image_map, TEMP_FILE.to_string(), guard.filename.clone());

        drop(guard);
    }
}

fn get_images_with_timestamp(filename: String) -> Result<HashMap<String, Duration>, Error> {
    let res = fs::read_to_string(filename);
    if let Err(e) = res {
        let msg = format!("Could not read image persistence data: {}", e);
        log::error!("{msg}");
        return Err(Error::FileOperation(msg));
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
    temp_file: String,
    filename: String,
) -> Result<(), Error> {
    // write to a temp file and then rename/overwrite to image persistence file
    let mut file =
        std::fs::File::create(temp_file.clone()).expect("Could not create images under /tmp");

    for (key, value) in state_to_persist {
        let image_details = format!("{} {}\n", key, value.as_secs());
        let res = write!(file, "{}", image_details);
        if res.is_err() {
            let msg = format!(
                "Could not write image:{} with timestamp:{} to store",
                key,
                value.as_secs()
            );
            return Err(Error::FileOperation(msg));
        }
    }

    // add retries?
    match fs::rename(temp_file, filename) {
        Ok(_) => {},
        Err(_) => return Err(Error::FileOperation(
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
) -> Result<(HashMap<String, Duration>, HashMap<String, Duration>), Error> {
    let current_time = std::time::SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|e| Error::GetCurrentTimeEpoch(e))?;

    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by MIGC in this run

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for module in running_modules {
        let key = module
            .0
            .config()
            .image_hash()
            .ok_or(Error::GetImageHash())?;

        // Since the images are currently being used, we update the timestamp to the current time
        // This avoids the case where a container crash just as MIGC is kicking off removes a needed image

        // TODO: Do we need to trim the key here?
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

    Ok((image_map, carry_over))
}

#[cfg(test)]
mod tests {
    use std::{collections::HashMap, time::Duration};

    use crate::migc_persistence::get_images_with_timestamp;

    use super::write_images_with_timestamp;

    const TEMP_FILE: &str = "/tmp/images";

    #[test]
    #[should_panic]
    fn test_panic_write_images_with_timestamp() {
        let mut result = write_images_with_timestamp(
            &HashMap::new(),
            "/etc/other_file".to_string(),
            TEMP_FILE.to_string(),
        );
        assert!(result.is_err());

        result = write_images_with_timestamp(
            &HashMap::new(),
            TEMP_FILE.to_string(),
            "/etc/other_file".to_string(),
        );
        assert!(result.is_err());
    }

    #[test]
    fn test_file_rename_write_images_with_timestamp() {
        let result = write_images_with_timestamp(
            &HashMap::new(),
            TEMP_FILE.to_string(),
            "/tmp/images2".to_string(),
        );
        assert!(result.is_ok());
        assert!(std::path::Path::new("/tmp/images2").exists());

        // cleanup
        let _res = std::fs::remove_file("/tmp/images2");
    }

    #[test]
    // tests both get_images_with_timestamp() and write_images_with_timestamp()
    fn test_get_write_images_with_timestamp() {
        let current_time = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        let mut hash_map: HashMap<String, Duration> = HashMap::new();
        hash_map.insert("test1".to_string(), current_time);
        hash_map.insert("test2".to_string(), current_time);
        hash_map.insert("test3".to_string(), current_time);

        let result = write_images_with_timestamp(
            &hash_map,
            TEMP_FILE.to_string(),
            "/tmp/images2".to_string(),
        );
        assert!(result.is_ok());

        // assert file not empty, verify file write
        let result_map: HashMap<String, Duration> =
            get_images_with_timestamp("/tmp/images2".to_string()).unwrap();
        assert!(result_map.len() == 3);
        assert!(result_map.contains_key(&"test1".to_string()));
        assert!(result_map.contains_key(&"test2".to_string()));
        assert!(result_map.contains_key(&"test3".to_string()));

        // cleanup
        let _res = std::fs::remove_file("/tmp/images2");
    }
}
