use std::{
    borrow::Borrow,
    collections::{btree_map::IntoIter, BTreeMap, HashMap},
    hash::Hash,
};

/// `SmallIndexSet` is a `HashSet`-like collection that preserves order
/// in which items were inserted in the collection.
///
/// It is suppose to work good enough on a very small number of items (~100).
/// DO NOT use for large amount of items!
///
/// Internally it maintains a `HashMap` for fast access to the value.
/// The value of the `HashMap` is a ordering number. `order` is used when
/// contructing iterator to restore order in which items were added.
/// `order` has a type `u64` which should be enough to handle 8472380 years
/// uptime with a rate of `70_000` messages/sec incoming rate.
#[derive(Debug, Clone)]
pub struct SmallIndexSet<V> {
    last_inserted: u64,
    items: HashMap<V, u64>,
}

impl<V> SmallIndexSet<V> {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn iter(&self) -> Iter<'_, V> {
        let mut ordered = BTreeMap::new();
        for (value, order) in &self.items {
            ordered.insert(order, value);
        }

        Iter(ordered.into_iter())
    }
}

impl<V> SmallIndexSet<V>
where
    V: Eq + Hash,
{
    pub fn get<Q: ?Sized>(&self, value: &Q) -> Option<&V>
    where
        V: Borrow<Q>,
        Q: Hash + Eq,
    {
        self.items.get_key_value(value).map(|(k, _)| k)
    }

    pub fn insert(&mut self, value: V) -> bool {
        self.last_inserted += 1;
        self.items.insert(value, self.last_inserted).is_some()
    }

    pub fn remove<Q: ?Sized>(&mut self, value: &Q) -> bool
    where
        V: Borrow<Q>,
        Q: Hash + Eq,
    {
        self.items.remove(value).is_some()
    }

    pub fn len(&self) -> usize {
        self.items.len()
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }
}

impl<V> PartialEq for SmallIndexSet<V>
where
    V: Eq + Hash,
{
    fn eq(&self, other: &Self) -> bool {
        self.items == other.items
    }
}

impl<V> Default for SmallIndexSet<V> {
    fn default() -> Self {
        Self {
            last_inserted: 0,
            items: HashMap::default(),
        }
    }
}

impl<'a, V> IntoIterator for &'a SmallIndexSet<V> {
    type Item = &'a V;
    type IntoIter = Iter<'a, V>;

    fn into_iter(self) -> Self::IntoIter {
        self.iter()
    }
}

pub struct Iter<'a, V>(IntoIter<&'a u64, &'a V>);

impl<'a, V> Iterator for Iter<'a, V> {
    type Item = &'a V;

    fn next(&mut self) -> Option<Self::Item> {
        self.0.next().map(|(_, v)| v)
    }
}

#[cfg(test)]
#[allow(clippy::bool_assert_comparison)]
mod tests {
    use super::SmallIndexSet;

    #[test]
    fn it_iterates_in_insertion_order() {
        let mut set = SmallIndexSet::new();
        assert_eq!(set.iter().next(), None);

        assert_eq!(set.insert(1), false);
        assert_eq!(set.iter().next(), Some(&1));

        assert_eq!(set.insert(2), false);
        {
            let mut iter = set.iter();
            assert_eq!(iter.next(), Some(&1));
            assert_eq!(iter.next(), Some(&2));
            assert_eq!(iter.next(), None);
        }

        assert_eq!(set.insert(1), true);
        {
            let mut iter = set.iter();
            assert_eq!(iter.next(), Some(&2));
            assert_eq!(iter.next(), Some(&1));
            assert_eq!(iter.next(), None);
        }

        assert_eq!(set.insert(0), false);
        {
            let mut iter = set.iter();
            assert_eq!(iter.next(), Some(&2));
            assert_eq!(iter.next(), Some(&1));
            assert_eq!(iter.next(), Some(&0));
            assert_eq!(iter.next(), None);
        }

        assert_eq!(set.remove(&1), true);
        {
            let mut iter = set.iter();
            assert_eq!(iter.next(), Some(&2));
            assert_eq!(iter.next(), Some(&0));
            assert_eq!(iter.next(), None);
        }
    }
}
