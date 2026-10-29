import type { Formats } from "next-intl";

/**
 * Western digits (0-9) in every locale. The design boards use them for money,
 * codes and dates, and pinning it here keeps server and browser output identical
 * (browsers disagree on the default digit system for "ar", which would cause
 * hydration mismatches).
 */
const latn = { numberingSystem: "latn" } as const;

/**
 * Named formats. In components use format.number(x, "integer"), not ad-hoc Intl
 * options, so numbers look the same on every screen. Numeric dates are not here on
 * purpose: use isoDate / isoStamp from ./dates (see the note there).
 */
export const formats = {
  number: {
    integer: { ...latn, maximumFractionDigits: 0 },
    decimal: { ...latn, minimumFractionDigits: 1, maximumFractionDigits: 1 },
    quantity: { ...latn, maximumFractionDigits: 2 },
    percent: { ...latn, style: "percent", maximumFractionDigits: 0 },
  },
  dateTime: {
    long: { ...latn, year: "numeric", month: "long", day: "numeric" },
    time: { ...latn, hour: "2-digit", minute: "2-digit", hour12: false },
  },
} satisfies Formats;

export type Currency = "EGP" | "SAR" | "USD";
