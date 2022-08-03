use std::io::Write;
use std::sync::{Arc, Mutex};
use std::time::UNIX_EPOCH;
use std::{collections::HashMap, fs, time::Duration};

use edgelet_settings::base::image::MIGCSettings;

use crate::Error;

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
        let settings = match settings {
            Some(settings) => settings,
            None => MIGCSettings::new(Duration::MAX, Duration::MAX, "00:00".to_string(), false),
        };

        Self {
            inner: Arc::new(Mutex::new(MIGCPersistenceInner { filename, settings })),
        }
    }

    pub fn record_image_use_timestamp(&self, iamge_id: &str) -> Result<(), Error> {
        let guard = self
            .inner
            .lock()
            .map_err(|e| Error::LockError(e.to_string()))?;

        let migc_filename = guard.filename.clone();
        if !std::path::Path::new(&migc_filename).exists() {
            log::info!(
                "Auto-pruning data file not found; creating file at: {}",
                migc_filename.as_str()
            );
            let _file = fs::File::create(migc_filename).map_err(Error::CreateFile)?;
        }

        // read MIGC persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)

        let mut image_map = match get_images_with_timestamp(guard.filename.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::warn!("Could not read auto-prune data. Image garbage collection did not prune any images. Error: {}", e);
                return Ok(());
            }
        };

        let current_time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        image_map.insert(iamge_id.to_string(), current_time);

        let temp_file_name = guard.filename.clone().replace("migc", "images");

        // write entries back to file
        let _res = write_images_with_timestamp(&image_map, temp_file_name, guard.filename.clone());

        drop(guard);

        Ok(())
    }

    pub fn prune_images_from_file(
        &self,
        in_use_image_ids: std::vec::Vec<String>,
    ) -> Result<HashMap<String, Duration>, Error> {
        let guard = self
            .inner
            .lock()
            .map_err(|e| Error::LockError(e.to_string()))?;

        let settings = guard.settings.clone();

        // if migc is disabled then shouldn't remove anything
        if !settings.is_enabled() {
            return Ok(HashMap::new());
        }

        // Read MIGC persistence file into in-mem map. This map now contains
        // all images deployed to the device (through an IoT Edge deployment).
        // If MIGC persistence file cannot be read we will return a new map so
        // new MIGC persistence file will be created.
        let image_map = match get_images_with_timestamp(guard.filename.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::warn!("Could not read image auto-prune data. Image garbage collection did not prune any images. {}", e);
                return Ok(HashMap::new());
            }
        };

        /* ============================== */

        // process maps
        let (images_to_delete, carry_over) = process_state(
            image_map,
            in_use_image_ids,
            settings.image_age_cleanup_threshold(),
        )?;

        /* ============================== */

        let temp_file_name = guard.filename.clone().replace("migc", "images");

        // write previously removed entries back to file
        if let Err(e) =
            write_images_with_timestamp(&carry_over, temp_file_name, guard.filename.clone())
        {
            log::warn!("Failed to update image auto pruning persistence file. File will be updated on next scheduled run. {}", e);
        };

        /* ============================== */

        drop(guard);

        // these are the images we need to prune; MIGC file has already been updated
        Ok(images_to_delete)
    }
}

/* ===================================== HELPER METHODS ==================================== */

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
    let contents = contents
        .lines()
        .map(|line| line.split(' ').collect::<Vec<&str>>())
        .map(|vec| (vec[0].to_string(), vec[1]));
    for (k, v) in contents {
        image_map.insert(
            k,
            Duration::from_secs(v.parse::<u64>().map_err(Error::ParseIntError)?),
        );
    }

    Ok(image_map)
}

fn write_images_with_timestamp(
    state_to_persist: &HashMap<String, Duration>,
    temp_file: String,
    filename: String,
) -> Result<(), Error> {
    // write to a temp file and then rename/overwrite to image persistence file (to prevent file write failures or corruption)
    let mut file = std::fs::File::create(temp_file.clone())
        .expect("Could not create temporary persistence file");

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

type HashMapTuple = (HashMap<String, Duration>, HashMap<String, Duration>);
fn process_state(
    mut image_map: HashMap<String, Duration>,
    image_ids: Vec<String>,
    image_age_cleanup_threshold: Duration,
) -> Result<HashMapTuple, Error> {
    let current_time = std::time::SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(Error::GetCurrentTimeEpoch)?;

    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by MIGC in this run

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for module_id in image_ids {
        // Since the images are currently being used, we update the timestamp to the current time
        // This avoids the case where a container crash just as MIGC is kicking off removes a needed image

        carry_over.insert(module_id, current_time);
    }

    // track entries younger than min age
    for (key, value) in &image_map {
        if current_time.as_secs() - value.as_secs() < image_age_cleanup_threshold.as_secs() {
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
    use std::{
        collections::HashMap,
        time::{Duration, UNIX_EPOCH},
    };

    use chrono::{Timelike, Utc};
    use edgelet_settings::base::image::MIGCSettings;
    use nix::libc::sleep;
    use serial_test::serial;

    use crate::{migc_persistence::get_images_with_timestamp, MIGCPersistence};

    use super::{process_state, write_images_with_timestamp};

    const TEMP_FILE: &str = "/tmp/images";

    /* =============================================================== PUBLIC API TESTS ============================================================ */

    #[tokio::test]
    #[serial]
    async fn test_record_image_use_timestamp() {
        let curr_time: String = format!("{}{}", Utc::now().hour(), Utc::now().minute());

        let mut _res = std::fs::remove_file(TEMP_FILE);
        let settings = MIGCSettings::new(
            Duration::from_secs(30),
            Duration::from_secs(10),
            curr_time,
            false,
        );
        let migc_persistence = MIGCPersistence::new(TEMP_FILE.to_string(), Some(settings));

        // write new image
        migc_persistence
            .record_image_use_timestamp(
                "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7",
            )
            .unwrap();
        let result = get_images_with_timestamp(TEMP_FILE.to_string()).unwrap();

        assert!(result.contains_key(
            "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7"
        ));
        assert!(result.len() == 1);
        let old_time =
            result.get("sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7");

        unsafe {
            sleep(1);
        }

        // update existing image
        migc_persistence
            .record_image_use_timestamp(
                "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7",
            )
            .unwrap();
        let new_result = get_images_with_timestamp(TEMP_FILE.to_string()).unwrap();
        assert!(new_result.contains_key(
            "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7"
        ));
        assert!(new_result.len() == 1);
        let new_time = new_result
            .get("sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7");

        assert!(old_time < new_time);

        // cleanup
        _res = std::fs::remove_file("/tmp/images2");
    }

    #[tokio::test]
    #[serial]
    async fn test_prune_images_from_file() {
        // setup
        let time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        let mut _res = std::fs::remove_file(TEMP_FILE);

        let mut image_map: HashMap<String, Duration> = HashMap::new();
        image_map.insert(
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
            time,
        );
        image_map.insert(
            "sha256:7a45202c8491b92b7e7a9a0cbf887079fcdb86ea3cb0b7a4cb8f5491281e985d".to_string(),
            time,
        );
        image_map.insert(
            "sha256:a40d3130a63918663f6e412178d2e83010994bb5a6bdb9ba314ca43013c05331".to_string(),
            time,
        );
        image_map.insert(
            "sha256:85fdb1e9675c837c18b75f103be6f156587d1058eced1fc508cdb84a722e4f82".to_string(),
            time,
        );
        image_map.insert(
            "sha256:553fd62d98efd413c6c97ded7e6c6c46fc38c7b1e30a2bffd7e08c04f0d65863".to_string(),
            time,
        );
        image_map.insert(
            "sha256:79386db4871013d571ced41443e6384ccc82ccd7f4988ecec4d5f91cbd488a99".to_string(),
            time,
        );

        let _write =
            write_images_with_timestamp(&image_map, "/tmp/temp".to_string(), TEMP_FILE.to_string());

        let curr_time: String = format!("{}{}", Utc::now().hour(), Utc::now().minute());
        let mut settings = MIGCSettings::new(
            Duration::from_secs(30),
            Duration::from_secs(5),
            curr_time,
            false,
        );
        let mut migc_persistence = MIGCPersistence::new(TEMP_FILE.to_string(), Some(settings));

        let mut in_use_image_ids: Vec<String> = vec![
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
        ];
        in_use_image_ids.push(
            "sha256:7a45202c8491b92b7e7a9a0cbf887079fcdb86ea3cb0b7a4cb8f5491281e985d".to_string(),
        );
        in_use_image_ids.push(
            "sha256:a40d3130a63918663f6e412178d2e83010994bb5a6bdb9ba314ca43013c05331".to_string(),
        );
        in_use_image_ids.push(
            "sha256:85fdb1e9675c837c18b75f103be6f156587d1058eced1fc508cdb84a722e4f82".to_string(),
        );

        unsafe { sleep(6) };

        // migc enabled, remove stuff
        let mut images_to_delete = migc_persistence
            .prune_images_from_file(in_use_image_ids.clone())
            .unwrap();
        assert!(images_to_delete.is_empty());

        // migc disable... don't remove stuff
        let curr_time: String = format!("{}{}", Utc::now().hour(), Utc::now().minute());
        settings = MIGCSettings::new(
            Duration::from_secs(30),
            Duration::from_secs(5),
            curr_time,
            true,
        );
        migc_persistence = MIGCPersistence::new(TEMP_FILE.to_string(), Some(settings));

        images_to_delete = migc_persistence
            .prune_images_from_file(in_use_image_ids)
            .unwrap();
        assert!(images_to_delete.len() == 2);

        // cleanup
        _res = std::fs::remove_file(TEMP_FILE);
    }

    /* =============================================================== MORE TESTS ============================================================ */

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
    #[serial]
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
        let mut _res = std::fs::remove_file("/tmp/images2");
    }

    #[test]
    fn test_process_state() {
        let (map1, map2) = process_state(
            HashMap::new(),
            Vec::new(),
            Duration::from_secs(60 * 60 * 24),
        )
        .unwrap();
        assert!(map1.is_empty());
        assert!(map2.is_empty());

        let mut images_being_used: Vec<String> = vec![
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
        ];
        images_being_used.push(
            "sha256:62aedd01bd8520c43d06b09f7a0f67ba9720bdc04631a8242c65ea995f3ecac8".to_string(),
        );
        images_being_used.push(
            "sha256:a4d112e0884bd2ba078ab8222e075bc656cc65cd433dfbb74d6de7cee188f2f2".to_string(),
        );

        let time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        let mut all_images_on_disk: HashMap<String, Duration> = HashMap::new();

        // currently used
        all_images_on_disk.insert(
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
            time - Duration::from_secs(60 * 60 * 24),
        );
        all_images_on_disk.insert(
            "sha256:62aedd01bd8520c43d06b09f7a0f67ba9720bdc04631a8242c65ea995f3ecac8".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 5),
        );
        all_images_on_disk.insert(
            "sha256:a4d112e0884bd2ba078ab8222e075bc656cc65cd433dfbb74d6de7cee188f2f2".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 9),
        );

        // others
        all_images_on_disk.insert(
            "sha256:a40d3130a63918663f6e412178d2e83010994bb5a6bdb9ba314ca43013c05331".to_string(),
            time - Duration::from_secs(60 * 60 * 12),
        );
        all_images_on_disk.insert(
            "sha256:269d9943b0d310e1ab49a55e14752596567a74daa37270c6217abfc33f48f7f5".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 12),
        );
        all_images_on_disk.insert(
            "sha256:a1e6072c125f6102f410418ca0647841376982b460ab570916b01f264daf89af".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 13),
        );
        all_images_on_disk.insert(
            "sha256:a4d112e0884bd2ba078ab8222e075bc989cc65cd433dfbb74d6de7cee188g4g7".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 8),
        );

        let (to_delete, carry_over) = process_state(
            all_images_on_disk,
            images_being_used,
            Duration::from_secs(60 * 60 * 24),
        )
        .unwrap();
        assert!(to_delete.len() == 3);
        assert!(carry_over.len() == 4);
    }
}
