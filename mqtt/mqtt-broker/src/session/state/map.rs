use std::{
    borrow::Borrow,
    collections::btree_map::IntoIter,
    collections::{BTreeMap, HashMap},
    hash::Hash,
};

/// `SmallIndexMap` is a `HashMap`-like collection that preserves order
/// in which items were inserted in the collection.
///
/// It is suppose to work good enough on a very small number of items (~100).
/// DO NOT use for large amount of items!
///
/// Internally it maintains a `HashMap` for fast access to the value by a key.
/// The value of the `HashMap` is a pair (order, value). `order` is used when
/// contructing iterator to restore order in which items were added.
/// `order` has a type `u64` which should be enough to handle 8472380 years
/// uptime with a rate of `70_000` messages/sec incoming rate.
#[derive(Debug, Clone)]
pub struct SmallIndexMap<K, V> {
    last_inserted: u64,
    items: HashMap<K, (u64, V)>,
}

impl<K, V> SmallIndexMap<K, V> {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn clear(&mut self) {
        self.last_inserted = 0;
        self.items.clear();
    }

    pub fn iter(&self) -> Iter<'_, K, V> {
        let mut ordered = BTreeMap::new();
        for (key, (order, value)) in &self.items {
            ordered.insert(order, (key, value));
        }

        Iter(ordered.into_iter())
    }

    pub fn into_iter(self) -> IterOwned<K, V> {
        let mut ordered = BTreeMap::new();
        for (key, (order, value)) in self.items {
            ordered.insert(order, (key, value));
        }

        IterOwned(ordered.into_iter())
    }
}

impl<K, V> SmallIndexMap<K, V>
where
    K: Eq + Hash,
{
    pub fn get<Q: ?Sized>(&self, k: &Q) -> Option<&V>
    where
        K: Borrow<Q>,
        Q: Hash + Eq,
    {
        self.items.get(k).map(|(_, v)| v)
    }

    pub fn insert(&mut self, key: K, value: V) -> Option<V> {
        self.last_inserted += 1;
        let value = (self.last_inserted, value);
        self.items.insert(key, value).map(|pair| pair.1)
    }

    pub fn remove<Q: ?Sized>(&mut self, key: &Q) -> Option<V>
    where
        K: Borrow<Q>,
        Q: Hash + Eq,
    {
        self.items.remove(key).map(|pair| pair.1)
    }

    pub fn len(&self) -> usize {
        self.items.len()
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }
}

impl<K, V> PartialEq for SmallIndexMap<K, V>
where
    K: Eq + Hash,
    V: PartialEq,
{
    fn eq(&self, other: &Self) -> bool {
        self.items == other.items
    }
}

impl<K, V> Default for SmallIndexMap<K, V> {
    fn default() -> Self {
        Self {
            last_inserted: 0,
            items: HashMap::default(),
        }
    }
}

impl<'a, K, V> IntoIterator for &'a SmallIndexMap<K, V> {
    type Item = (&'a K, &'a V);
    type IntoIter = Iter<'a, K, V>;

    fn into_iter(self) -> Self::IntoIter {
        self.iter()
    }
}

impl<K, V> IntoIterator for SmallIndexMap<K, V> {
    type Item = (K, V);
    type IntoIter = IterOwned<K, V>;

    fn into_iter(self) -> Self::IntoIter {
        self.into_iter()
    }
}

pub struct Iter<'a, K, V>(IntoIter<&'a u64, (&'a K, &'a V)>);

impl<'a, K, V> Iterator for Iter<'a, K, V> {
    type Item = (&'a K, &'a V);

    fn next(&mut self) -> Option<Self::Item> {
        self.0.next().map(|(_, v)| v)
    }

    fn size_hint(&self) -> (usize, Option<usize>) {
        self.0.size_hint()
    }
}

pub struct IterOwned<K, V>(IntoIter<u64, (K, V)>);

impl<K, V> Iterator for IterOwned<K, V> {
    type Item = (K, V);

    fn next(&mut self) -> Option<Self::Item> {
        self.0.next().map(|(_, v)| v)
    }

    fn size_hint(&self) -> (usize, Option<usize>) {
        self.0.size_hint()
    }
}

#[cfg(test)]
mod tests {
    use super::SmallIndexMap;

    #[test]
    fn it_iterates_in_insertion_order() {
        let mut map = SmallIndexMap::new();
        assert_eq!(map.iter().next(), None);

        assert_eq!(map.insert(1, 1), None);
        assert_eq!(map.iter().next(), Some((&1, &1)));

        assert_eq!(map.insert(1, 2), Some(1));
        assert_eq!(map.iter().next(), Some((&1, &2)));

        assert_eq!(map.insert(0, 1), None);
        assert_eq!(map.insert(2, 3), None);

        assert_eq!(map.insert(3, 3), None);

        let mut iter = map.iter();
        assert_eq!(iter.next(), Some((&1, &2)));
        assert_eq!(iter.next(), Some((&0, &1)));
        assert_eq!(iter.next(), Some((&2, &3)));
        assert_eq!(iter.next(), Some((&3, &3)));
        drop(iter);

        assert_eq!(map.remove(&0), Some(1));
        map.insert(4, 4);
        map.insert(0, 0);

        let mut iter = map.iter();
        assert_eq!(iter.next(), Some((&1, &2)));
        assert_eq!(iter.next(), Some((&2, &3)));
        assert_eq!(iter.next(), Some((&3, &3)));
        assert_eq!(iter.next(), Some((&4, &4)));
        assert_eq!(iter.next(), Some((&0, &0)));
    }
}
