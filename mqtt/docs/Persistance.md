# Broker Persistance
The broker's persistance is modeled after mosquitto's. It saves the entire state to one compressed file. File saving is triggered by a configurable timer and on shutdown.

## Requirements
* Minimize space used on disk
* If a valid state exists, memory constraints must not prevent it from being loaded
* Write overhead is small

## Stratagy
### Persist
The broker first collects the entire state into a binary stream. This stream is passed to the GZip algorythm to reduce duplicates. The resulting compressed binary is written directly to disk. This file is called a snapshot.

If this is the first time persisting the state, a symlink is creating pointing to the new snapshot. If there is already a snapshot existing, the symlink is moved from the old snapshot's file to the new snapshot's file. This ensures the file the symlink points to is always valid, even if the broker unexpectedly shutsdown while writing to disk.

 Once the symlink is moved, if there are more snapshots than the configured value (*link to config instructions here*) (default 2, min 1) the oldest snapshots will be deleted. 

### Load
On start, the broker checks if a symlink points to a snapshot. If so, it loads and decompresses the binary represenation of the snapshot. 

While loading, it keeps a hashmap of the loaded payloads in memory, and if it finds duplicate payloads it creates a referance to the previously loaded payload rather than loading the new payload.
