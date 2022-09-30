// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::UNIX_EPOCH;
use std::{collections::HashMap, collections::HashSet, fs, time::Duration};

use edgelet_settings::base::image::ImagePruneSettings;

use crate::Error;

const IMAGE_USE_FILENAME: &str = "image_use";
const TMP_FILENAME: &str = "image_use_tmp";

#[derive(Debug, Clone)]
struct ImagePruneInner {
    image_use_filepath: String,
    tmp_filepath: String,
    settings: ImagePruneSettings,
}

/// <summary>
/// The methods associated with this struct are at the heart of the image garbage collection
/// feature. As such, this struct does not hold any user data, but simply holds information
/// needed to collect/process state that (eventually) enables unused image garbage collection.
#[derive(Debug, Clone)]
pub struct ImagePruneData {
    inner: Arc<Mutex<ImagePruneInner>>,
}

impl ImagePruneData {
    pub fn new(homedir: &Path, settings: ImagePruneSettings) -> Result<Self, Error> {
        let fp: PathBuf = homedir.join(IMAGE_USE_FILENAME);
        let tmp_fp: PathBuf = homedir.join(TMP_FILENAME);

        let image_use_filepath = fp
            .to_str()
            .ok_or_else(|| Error::FilepathCreationError(IMAGE_USE_FILENAME.into()))?;
        let tmp_filepath = tmp_fp
            .to_str()
            .ok_or_else(|| Error::FilepathCreationError(TMP_FILENAME.into()))?;

        Ok(Self {
            inner: Arc::new(Mutex::new(ImagePruneInner {
                image_use_filepath: image_use_filepath.to_string(),
                tmp_filepath: tmp_filepath.to_string(),
                settings,
            })),
        })
    }

    /// <summary>
    /// This method takes the `image_id` and adds (if the image is new) OR updates the last-used timestamp associated
    /// with this `image_id`. This state is maintained for use during image garbage collection.
    /// This method is (currently) called whenever a new image is pulled, a container is created, or when a container is removed.
    pub fn record_image_use_timestamp(&self, image_id: &str) -> Result<(), Error> {
        let guard = self
            .inner
            .lock()
            .expect("Image garbage collection file operation failed");

        // read persistence file into in-mem map
        // this map now contains all images deployed to the device (through an IoT Edge deployment)

        let mut image_map = match get_images_with_timestamp(guard.image_use_filepath.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::warn!("Could not read image garbage collection data. Latest time of use will not be updated for image: {}. Error: {}", image_id, e);
                return Err(e);
            }
        };

        let current_time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        image_map.insert(image_id.to_string(), current_time);

        // write entries back to file
        let res = write_images_with_timestamp(
            &image_map,
            guard.tmp_filepath.clone(),
            guard.image_use_filepath.clone(),
        );

        if res.is_ok() {
            log::debug!(
                "Image with ID {} tracked in image garbage collection state.",
                image_id
            );
        }

        drop(guard);

        Ok(())
    }

    /// <summary>
    /// This method is called during image garbage collection. It returns a map of images that
    /// will be deleted by the image garbage collector.
    /// The `in_use_image_ids` is a set of image IDs currently being used on the device [and
    /// contains image_ids that may or may not have been deployed by IoTEdge].
    pub fn prune_images_from_file(
        &self,
        in_use_image_ids: HashSet<String>,
    ) -> Result<HashMap<String, Duration>, Error> {
        let guard = self
            .inner
            .lock()
            .map_err(|e| Error::LockError(e.to_string()))?;

        let settings = guard.settings.clone();

        // Read persistence file into in-mem map. This map now contains
        // all images deployed to the device (through an IoT Edge deployment).
        // If persistence file cannot be read we will return a new map so
        // new persistence file will be created.
        let iotedge_images_map = match get_images_with_timestamp(guard.image_use_filepath.clone()) {
            Ok(map) => map,
            Err(e) => {
                drop(guard);
                log::warn!("Could not read image garbage collection data. Image garbage collection will not prune any images. {}", e);
                return Ok(HashMap::new());
            }
        };

        /* ============================== */

        // process maps
        let (images_to_delete, carry_over) = process_state(
            iotedge_images_map,
            in_use_image_ids,
            settings.image_age_cleanup_threshold(),
        )?;

        /* ============================== */

        // write previously removed entries back to file
        if let Err(e) = write_images_with_timestamp(
            &carry_over,
            guard.tmp_filepath.clone(),
            guard.image_use_filepath.clone(),
        ) {
            log::warn!("Failed to update image auto pruning persistence file. File will be updated on next scheduled run. {}", e);
        };

        /* ============================== */

        drop(guard);

        // these are the images we need to prune; file has already been updated
        Ok(images_to_delete)
    }
}

/* ===================================== HELPER METHODS ==================================== */

fn get_images_with_timestamp(
    image_use_filepath: String,
) -> Result<HashMap<String, Duration>, Error> {
    if !std::path::Path::new(&image_use_filepath).exists() {
        log::info!(
            "Image garbage collection data file not found; creating file at: {}",
            image_use_filepath.as_str()
        );
        let _file = fs::File::create(image_use_filepath.clone()).map_err(Error::CreateFile)?;
    }

    let contents = match fs::read_to_string(image_use_filepath) {
        Ok(ct) => ct,
        Err(e) => {
            let msg = format!("Could not read image persistence data: {}", e);
            log::error!("{msg}");
            return Err(Error::FileOperation(msg));
        }
    };

    let mut image_map: HashMap<String, Duration> = HashMap::new(); // all image pruning data

    // TL;DR: this dumps pruning data into the image_map, where
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
    image_use_filepath: String,
) -> Result<(), Error> {
    // write to a temp file and then rename/overwrite to image persistence file (to prevent file write failures or corruption)
    let mut file = std::fs::File::create(temp_file.clone()).map_err(Error::CreateFile)?;

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

    match fs::rename(temp_file, image_use_filepath) {
        Ok(_) => {}
        Err(err) => {
            return Err(Error::FileOperation(format!(
                "Could not update garbage collection data {}",
                err
            )))
        }
    };

    Ok(())
}

// This method separates out the images to be deleted from the images not to be deleted,
// and returns those as a tuple: (images to be deleted, images to be written back to file)
// It takes as input all the images present on the device (that we know about through an
// iotedge deployment) and the images currently in-use (which may or may not have been
// deployed using iotedge), along with the minimum "age" for which the images can stay
// unused. Any (unused) images (except the bootstrap edge agent image) older than this
// minimum age are marked for deletion.
#[allow(clippy::type_complexity)]
fn process_state(
    mut iotedge_images_map: HashMap<String, Duration>,
    in_use_image_ids: HashSet<String>,
    image_age_cleanup_threshold: Duration,
) -> Result<(HashMap<String, Duration>, HashMap<String, Duration>), Error> {
    let current_time = std::time::SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(Error::GetCurrentTimeEpoch)?;

    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by pruning in this run

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for image_id in in_use_image_ids {
        // Since in_use_image_ids contains *all* the images currently being used, we need to filter on whether said image
        // was deployed/managed by iotedge or no.
        if iotedge_images_map.contains_key(&image_id) {
            // Since the images are currently being used, we update the timestamp to the current time
            // This avoids the case where a container crash just as pruning is kicking off removes a needed image
            carry_over.insert(image_id, current_time);
        }
    }

    // track entries younger than min age
    for (key, value) in &iotedge_images_map {
        if current_time.as_secs() - value.as_secs() < image_age_cleanup_threshold.as_secs() {
            carry_over.insert(key.to_string(), *value);
        }
    }

    // clean up image map to make sure entries that need to be preserved are not removed
    for key in carry_over.keys() {
        iotedge_images_map.remove(key);
    }

    Ok((iotedge_images_map, carry_over))
}

#[cfg(test)]
mod tests {
    use std::{
        collections::{HashMap, HashSet},
        path::Path,
        time::{Duration, UNIX_EPOCH},
    };

    use chrono::{Timelike, Utc};
    use edgelet_settings::base::image::ImagePruneSettings;
    use nix::libc::sleep;
    use serial_test::serial;

    use crate::{
        image_prune_data::{
            get_images_with_timestamp, process_state, IMAGE_USE_FILENAME, TMP_FILENAME,
        },
        ImagePruneData,
    };

    use super::write_images_with_timestamp;

    const TEST_FILE_DIR: &str = "test-data";

    /* =============================================================== PUBLIC API TESTS ============================================================ */

    #[tokio::test]
    #[serial]
    async fn test_record_image_use_timestamp() {
        let curr_time = (Utc::now().hour() * 60 + Utc::now().minute()).into();

        let test_file_dir = std::env::current_dir().unwrap().join(TEST_FILE_DIR);
        if test_file_dir.is_dir() {
            std::fs::remove_dir_all(test_file_dir.clone()).unwrap();
        }
        std::fs::create_dir(Path::new(&test_file_dir)).unwrap();

        let settings = ImagePruneSettings::new(
            Duration::from_secs(30),
            Duration::from_secs(10),
            curr_time,
            false,
        );
        let image_use_data = ImagePruneData::new(&test_file_dir, settings).unwrap();

        // write new image
        image_use_data
            .record_image_use_timestamp(
                "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7",
            )
            .unwrap();
        let images = get_images_with_timestamp(
            test_file_dir
                .join(IMAGE_USE_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
        )
        .unwrap();

        assert!(images.contains_key(
            "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7"
        ));
        assert!(images.len() == 1);
        let old_time =
            images.get("sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7");

        unsafe {
            sleep(1);
        }

        // update existing image
        image_use_data
            .record_image_use_timestamp(
                "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7",
            )
            .unwrap();
        let new_images = get_images_with_timestamp(
            test_file_dir
                .join(IMAGE_USE_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
        )
        .unwrap();
        assert!(new_images.contains_key(
            "sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7"
        ));
        assert!(new_images.len() == 1);
        let new_time = new_images
            .get("sha256:a4d112e0884bd2ba078ab8222e099bc989cc65cd433dfbb74d6de7cee188g4g7");

        assert!(old_time < new_time);

        // cleanup
        std::fs::remove_dir_all(test_file_dir).unwrap();
    }

    #[tokio::test]
    #[serial]
    async fn test_prune_images_from_file() {
        // setup
        let time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        let test_file_dir = std::env::current_dir().unwrap().join(TEST_FILE_DIR);
        if test_file_dir.is_dir() {
            std::fs::remove_dir_all(test_file_dir.clone()).unwrap();
        }
        std::fs::create_dir(Path::new(&test_file_dir)).unwrap();

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

        let _write = write_images_with_timestamp(
            &image_map,
            test_file_dir
                .join(TMP_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
            test_file_dir
                .join(IMAGE_USE_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
        );

        let curr_time = (Utc::now().hour() * 60 + Utc::now().minute()).into();
        let settings = ImagePruneSettings::new(
            Duration::from_secs(30),
            Duration::from_secs(5),
            curr_time,
            true,
        );
        let image_use_data = ImagePruneData::new(&test_file_dir, settings).unwrap();

        let mut in_use_image_ids: HashSet<String> = HashSet::new();
        in_use_image_ids.insert(
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
        );
        in_use_image_ids.insert(
            "sha256:7a45202c8491b92b7e7a9a0cbf887079fcdb86ea3cb0b7a4cb8f5491281e985d".to_string(),
        );
        in_use_image_ids.insert(
            "sha256:a40d3130a63918663f6e412178d2e83010994bb5a6bdb9ba314ca43013c05331".to_string(),
        );
        in_use_image_ids.insert(
            "sha256:85fdb1e9675c837c18b75f103be6f156587d1058eced1fc508cdb84a722e4f82".to_string(),
        );

        unsafe { sleep(6) };

        // image prune enabled, remove stuff
        let images_to_delete = image_use_data
            .prune_images_from_file(in_use_image_ids)
            .unwrap();
        assert!(images_to_delete.len() == 2);

        // cleanup
        std::fs::remove_dir_all(test_file_dir).unwrap();
    }

    /* =============================================================== MORE TESTS ============================================================ */

    #[test]
    #[serial]
    fn test_file_rename_succeeds() {
        //setup
        let test_file_dir = std::env::current_dir().unwrap().join(TEST_FILE_DIR);
        if test_file_dir.is_dir() {
            std::fs::remove_dir_all(test_file_dir.clone()).unwrap();
        }
        std::fs::create_dir(Path::new(&test_file_dir)).unwrap();

        let result = write_images_with_timestamp(
            &HashMap::new(),
            test_file_dir
                .join(TMP_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
            test_file_dir
                .join(IMAGE_USE_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
        );
        assert!(result.is_ok());
        assert!(std::path::Path::new(&test_file_dir.join(IMAGE_USE_FILENAME)).exists());

        // cleanup
        std::fs::remove_dir_all(test_file_dir).unwrap();
    }

    #[test]
    #[serial]
    fn test_get_write_images_with_timestamp() {
        // setup
        let test_file_dir = std::env::current_dir().unwrap().join(TEST_FILE_DIR);
        if test_file_dir.is_dir() {
            std::fs::remove_dir_all(test_file_dir.clone()).unwrap();
        }
        std::fs::create_dir(Path::new(&test_file_dir)).unwrap();

        let current_time = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        let mut hash_map: HashMap<String, Duration> = HashMap::new();
        hash_map.insert("test1".to_string(), current_time);
        hash_map.insert("test2".to_string(), current_time);
        hash_map.insert("test3".to_string(), current_time);

        let result = write_images_with_timestamp(
            &hash_map,
            test_file_dir
                .join(TMP_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
            test_file_dir
                .join(IMAGE_USE_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
        );
        assert!(result.is_ok());
        // assert file not empty, verify file write
        let result_map: HashMap<String, Duration> = get_images_with_timestamp(
            test_file_dir
                .join(IMAGE_USE_FILENAME)
                .into_os_string()
                .into_string()
                .unwrap(),
        )
        .unwrap();
        assert!(result_map.len() == 3);
        assert!(result_map.contains_key(&"test1".to_string()));
        assert!(result_map.contains_key(&"test2".to_string()));
        assert!(result_map.contains_key(&"test3".to_string()));

        // cleanup
        std::fs::remove_dir_all(test_file_dir).unwrap();
    }

    #[test]
    #[serial]
    fn test_process_state() {
        let (map1, map2) = process_state(
            HashMap::new(),
            HashSet::new(),
            Duration::from_secs(60 * 60 * 24),
        )
        .unwrap();
        assert!(map1.is_empty());
        assert!(map2.is_empty());

        let mut images_being_used: HashSet<String> = HashSet::new();
        images_being_used.insert(
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
        );
        images_being_used.insert(
            "sha256:62aedd01bd8520c43d06b09f7a0f67ba9720bdc04631a8242c65ea995f3ecac8".to_string(),
        );
        images_being_used.insert(
            "sha256:a4d112e0884bd2ba078ab8222e075bc656cc65cd433dfbb74d6de7cee188f2f2".to_string(),
        );
        images_being_used.insert(
            "sha256:0884bd2ba078ab8222e075bc656cc65cd433dfbb74d6de7cee188f2f2a4d112e".to_string(),
        );
        images_being_used.insert(
            "sha256:8222e075bc656cc65cd433dfbb74d6de7cee188f2f2a4d112e0884bd2ba078ab".to_string(),
        );

        let time = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("Could not get EPOCH time");

        let mut all_iotedge_images: HashMap<String, Duration> = HashMap::new();

        // currently used
        all_iotedge_images.insert(
            "sha256:670dcc86b69df89a9d5a9e1a7ae5b8f67619c1c74e19de8a35f57d6c06505fd4".to_string(),
            time - Duration::from_secs(60 * 60 * 24),
        );
        all_iotedge_images.insert(
            "sha256:62aedd01bd8520c43d06b09f7a0f67ba9720bdc04631a8242c65ea995f3ecac8".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 5),
        );
        all_iotedge_images.insert(
            "sha256:a4d112e0884bd2ba078ab8222e075bc656cc65cd433dfbb74d6de7cee188f2f2".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 9),
        );

        // others
        all_iotedge_images.insert(
            "sha256:a40d3130a63918663f6e412178d2e83010994bb5a6bdb9ba314ca43013c05331".to_string(),
            time - Duration::from_secs(60 * 60 * 12),
        );
        all_iotedge_images.insert(
            "sha256:269d9943b0d310e1ab49a55e14752596567a74daa37270c6217abfc33f48f7f5".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 12),
        );
        all_iotedge_images.insert(
            "sha256:a1e6072c125f6102f410418ca0647841376982b460ab570916b01f264daf89af".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 13),
        );
        all_iotedge_images.insert(
            "sha256:a4d112e0884bd2ba078ab8222e075bc989cc65cd433dfbb74d6de7cee188g4g7".to_string(),
            time - Duration::from_secs(60 * 60 * 24 * 8),
        );

        let (to_delete, carry_over) = process_state(
            all_iotedge_images,
            images_being_used,
            Duration::from_secs(60 * 60 * 24),
        )
        .unwrap();
        assert!(to_delete.len() == 3);
        assert!(carry_over.len() == 4);
    }
}
