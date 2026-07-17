use std::time::{SystemTime, UNIX_EPOCH};

/// A small xorshift RNG so the shuffle hook needs no external dependency.
/// The shuffle exists only to prove the CPM passes are order-independent
/// (results must be identical shuffled or not), so quality of randomness is
/// not critical.
pub(crate) struct XorShift64 {
    state: u64,
}

impl XorShift64 {
    pub(crate) fn from_entropy() -> Self {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_nanos() as u64)
            .unwrap_or(0x9E37_79B9_7F4A_7C15);
        Self { state: nanos | 1 }
    }

    fn next_u64(&mut self) -> u64 {
        let mut x = self.state;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        self.state = x;
        x
    }

    fn next_below(&mut self, bound: usize) -> usize {
        (self.next_u64() % bound as u64) as usize
    }
}

/// Fisher-Yates shuffle — the counterpart of the C# `Zametek.Utility.Shuffle`.
pub(crate) fn shuffle<T>(items: &mut [T]) {
    let mut rng = XorShift64::from_entropy();
    for i in (1..items.len()).rev() {
        let j = rng.next_below(i + 1);
        items.swap(i, j);
    }
}
