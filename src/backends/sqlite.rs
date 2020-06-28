use crate::store::{Record, StoreBackend};

use std::path::Path;

use rusqlite::{params, Connection, Error as SQLiteError};

const STORE_NAME: &str = "store.sqlite";
pub struct SQLiteBackend;

impl StoreBackend for SQLiteBackend {
    type Error = SQLiteError;

    fn initialize() -> Result<(), Self::Error> {
        let db = Connection::open(Path::new(STORE_NAME))?;
        db.execute(
            "CREATE TABLE IF NOT EXISTS secrets (
                id    TEXT PRIMARY KEY,
                value TEXT,
                iv    TEXT,
                aad   TEXT
            ) WITHOUT ROWID",
            params![]
        )?;
        Ok(())
    }

    fn write_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        let db = Connection::open(Path::new(STORE_NAME))?;
        db.execute(
            "INSERT INTO secrets (id, value, iv, aad) VALUES (?1, ?2, ?3, ?4)",
            params![id, record.ciphertext, record.iv, record.aad]
        )?;
        Ok(())
    }

    fn update_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        let db = Connection::open(Path::new(STORE_NAME))?;
        db.execute(
            "UPDATE secrets SET value = ?2, iv = ?3, aad = ?4 WHERE id = ?1",
            params![id, record.ciphertext, record.iv, record.aad]
        )?;
        Ok(())
    }

    fn read_record(&self, id: &str) -> Result<Record, Self::Error> {
        let db = Connection::open(Path::new(STORE_NAME))?;
        let mut stmt = db.prepare("SELECT (value, iv, aad) WHERE id = ?")?;
        let mut query = stmt.query(params![id])?;

        if let Some(row) = query.next()? {
            Ok(Record {
                ciphertext: row.get(0)?,
                iv: row.get(1)?,
                aad: row.get(2)?
            })
        }
        else {
            Err(Self::Error::QueryReturnedNoRows)
        }
    }

    fn delete_record(&self, id: &str) -> Result<(), Self::Error> {
        let db = Connection::open(Path::new(STORE_NAME))?;
        db.execute(
            "DELETE FROM secrets WHERE id = ?",
            params![id]
        )?;
        Ok(())
    }
}
