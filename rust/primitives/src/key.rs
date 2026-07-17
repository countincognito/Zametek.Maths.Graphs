use std::fmt::{Debug, Display};
use std::hash::Hash;

/// The constraint every ID type must satisfy — the counterpart of the C#
/// `where T : struct, IComparable<T>, IEquatable<T>` bound, plus the
/// `Next()`/`Previous()` key-generation operations the compilers rely on
/// (`KeyExtensions` in C#, which supports `int` and `Guid`).
///
/// Implementations are provided for the primitive integer types; any custom
/// copyable, ordered, hashable type can opt in by implementing this trait.
pub trait Key: Copy + Ord + Eq + Hash + Debug + Display + Default {
    /// The next key in sequence (used by ascending ID generators).
    fn next(self) -> Self;
    /// The previous key in sequence (used by descending ID generators).
    fn previous(self) -> Self;
}

macro_rules! impl_key_for_int {
    ($($t:ty),*) => {
        $(
            impl Key for $t {
                fn next(self) -> Self {
                    self.wrapping_add(1)
                }
                fn previous(self) -> Self {
                    self.wrapping_sub(1)
                }
            }
        )*
    };
}

impl_key_for_int!(i8, i16, i32, i64, i128, isize, u8, u16, u32, u64, u128, usize);
