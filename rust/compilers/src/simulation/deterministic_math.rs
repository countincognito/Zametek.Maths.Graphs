//! The natural logarithm and the exponential, for the simulation, written so
//! that the same arguments give the same bits in every language and on every
//! platform. Port of `DeterministicMath.cs`.
//!
//! The functions of the standard library call the maths library of the
//! platform, and the platforms do not agree in the last bit. These use only
//! addition, subtraction, multiplication, division and the bit patterns of a
//! `f64`. IEEE 754 fixes the result of each of those to the bit, and Rust does
//! not fuse a multiplication with an addition. The order of the operations is
//! part of the contract: it is the order of the C# code, and the shared
//! baseline file checks both.

// ln 2 in two parts. The digits are those of the C# source, so that the two
// can be compared by eye. The high part has 21 trailing zero bits in its
// significand, so that the product of it and any integer of 21 bits or fewer is
// exact.
#[allow(clippy::excessive_precision)]
const LN2_HIGH: f64 = 6.93147180369123816490e-01;
#[allow(clippy::excessive_precision)]
const LN2_LOW: f64 = 1.90821492927058770002e-10;

const INVERSE_LN2: f64 = std::f64::consts::LOG2_E;
const SQUARE_ROOT_OF_TWO: f64 = std::f64::consts::SQRT_2;

// exp(x) is larger than the largest double above the first value, and smaller
// than the smallest subnormal double below the other.
#[allow(clippy::excessive_precision)]
const EXP_OVERFLOW: f64 = 709.78271289338397;
#[allow(clippy::excessive_precision)]
const EXP_UNDERFLOW: f64 = -745.13321910194122;

const EXPONENT_MASK: u64 = 0x7FF0_0000_0000_0000;
const SIGNIFICAND_MASK: u64 = 0x000F_FFFF_FFFF_FFFF;
const EXPONENT_OF_ONE: u64 = 0x3FF0_0000_0000_0000;

/// The natural logarithm of `x`. The result is NaN for a negative `x` or a NaN,
/// negative infinity for zero, and infinity for infinity.
#[allow(clippy::neg_cmp_op_on_partial_ord)]
pub(crate) fn ln(mut x: f64) -> f64 {
    if !(x > 0.0) {
        return if x == 0.0 {
            f64::NEG_INFINITY
        } else {
            f64::NAN
        };
    }
    if x.is_infinite() {
        return x;
    }

    let mut k: i32 = 0;
    let mut bits = x.to_bits();
    if (bits & EXPONENT_MASK) == 0 {
        // A subnormal number: scale it into the normal range first (2 to the
        // power 54).
        x *= 18014398509481984.0;
        bits = x.to_bits();
        k = -54;
    }
    k += ((bits >> 52) & 0x7FF) as i32 - 1023;

    // x is m times 2 to the power k, with m in [1, 2). Move m into
    // [sqrt(2) / 2, sqrt(2)] so that the series below is short.
    let mut m = f64::from_bits((bits & SIGNIFICAND_MASK) | EXPONENT_OF_ONE);
    if m > SQUARE_ROOT_OF_TWO {
        m *= 0.5;
        k += 1;
    }

    // ln(m) = 2 s (1 + z / 3 + z^2 / 5 + ...), with s = f / (2 + f), z = s s and
    // f = m - 1. The subtraction is exact, because m is in [1/2, 2].
    let f = m - 1.0;
    let s = f / (2.0 + f);
    let z = s * s;
    let mut series = 1.0 / 25.0;
    series = (1.0 / 23.0) + (z * series);
    series = (1.0 / 21.0) + (z * series);
    series = (1.0 / 19.0) + (z * series);
    series = (1.0 / 17.0) + (z * series);
    series = (1.0 / 15.0) + (z * series);
    series = (1.0 / 13.0) + (z * series);
    series = (1.0 / 11.0) + (z * series);
    series = (1.0 / 9.0) + (z * series);
    series = (1.0 / 7.0) + (z * series);
    series = (1.0 / 5.0) + (z * series);
    series = (1.0 / 3.0) + (z * series);
    series = 1.0 + (z * series);
    let lnm = 2.0 * s * series;

    let e = f64::from(k);
    (e * LN2_HIGH) + ((e * LN2_LOW) + lnm)
}

/// e to the power `x`. The result is zero below the range of a double, and
/// infinity above it.
pub(crate) fn exp(x: f64) -> f64 {
    if x.is_nan() {
        return x;
    }
    if x > EXP_OVERFLOW {
        return f64::INFINITY;
    }
    if x < EXP_UNDERFLOW {
        return 0.0;
    }

    // x = k ln 2 + r, with |r| at most about 0.35.
    let kd = ((x * INVERSE_LN2) + 0.5).floor();
    let k = kd as i32;
    let r = (x - (kd * LN2_HIGH)) - (kd * LN2_LOW);

    // 1 + r (1 + r / 2 (1 + r / 3 (1 + ...))), to the term r^14 / 14!.
    let mut series = 1.0;
    for n in (1..=14).rev() {
        series = 1.0 + ((r / f64::from(n)) * series);
    }

    // The scaling by 2 to the power k, in two steps when k is outside the range
    // of one power of two.
    let first = k / 2;
    let second = k - first;
    series * power_of_two(first) * power_of_two(second)
}

// 2 to the power n, for n in [-1022, 1023].
fn power_of_two(n: i32) -> f64 {
    f64::from_bits(((n + 1023) as u64) << 52)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn spread(count: usize) -> Vec<f64> {
        (0..count)
            .map(|i| {
                let mantissa = 1.0 + (((i * 7919) % 1000) as f64) / 1000.0;
                let exponent = (((i * 31) % 601) as i32) - 300;
                mantissa * 10f64.powi(exponent)
            })
            .collect()
    }

    #[test]
    fn ln_given_many_positive_numbers_then_is_within_a_few_units_of_the_last_place_of_std() {
        for x in spread(2000) {
            let expected = x.ln();
            let actual = ln(x);
            assert!(
                (actual - expected).abs() <= (expected.abs() * 8e-16).max(1e-300),
                "ln({x:e})"
            );
        }
    }

    #[test]
    fn ln_given_one_or_the_edges_of_the_domain_then_has_the_usual_value() {
        assert_eq!(ln(1.0), 0.0);
        assert_eq!(ln(0.0), f64::NEG_INFINITY);
        assert!(ln(-1.0).is_nan());
        assert!(ln(f64::NAN).is_nan());
        assert_eq!(ln(f64::INFINITY), f64::INFINITY);
    }

    #[test]
    fn ln_given_a_subnormal_number_then_is_the_logarithm() {
        assert!((ln(5e-324) - 5e-324f64.ln()).abs() < 1e-12);
        assert!((ln(5e-322) - 5e-322f64.ln()).abs() < 1e-12);
    }

    #[test]
    fn exp_given_many_numbers_then_is_within_a_few_units_of_the_last_place_of_std() {
        for i in 0..=4000 {
            let x = -700.0 + (f64::from(i) * 0.35);
            let expected = x.exp();
            assert!((exp(x) - expected).abs() <= expected * 8e-16, "exp({x})");
        }
    }

    #[test]
    fn exp_given_the_edges_of_the_range_then_is_zero_or_infinity() {
        assert_eq!(exp(0.0), 1.0);
        assert_eq!(exp(710.0), f64::INFINITY);
        assert_eq!(exp(-746.0), 0.0);
        assert_eq!(exp(f64::NEG_INFINITY), 0.0);
        assert!(exp(f64::NAN).is_nan());
        assert!(exp(-740.0) > 0.0);
    }
}
