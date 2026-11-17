import localFont from "next/font/local";

/**
 * Arabic glyphs come from Plex Sans Arabic, Latin glyphs fall through to Plex Sans
 * (the Arabic subset has no Latin letters), so mixed Arabic/English UI stays in one
 * family. Mono is for numbers and codes. The CSS variables are consumed in
 * src/app/globals.css (@theme inline).
 *
 * next/font/local options must be written as literals (no helpers), so the
 * weight lists below are intentionally explicit.
 */
export const plexArabic = localFont({
  src: [
    { path: "../fonts/plex-arabic-400.woff2", weight: "400", style: "normal" },
    { path: "../fonts/plex-arabic-500.woff2", weight: "500", style: "normal" },
    { path: "../fonts/plex-arabic-600.woff2", weight: "600", style: "normal" },
    { path: "../fonts/plex-arabic-700.woff2", weight: "700", style: "normal" },
  ],
  variable: "--font-plex-arabic",
  display: "swap",
});

export const plexSans = localFont({
  src: [
    { path: "../fonts/plex-sans-400.woff2", weight: "400", style: "normal" },
    { path: "../fonts/plex-sans-500.woff2", weight: "500", style: "normal" },
    { path: "../fonts/plex-sans-600.woff2", weight: "600", style: "normal" },
    { path: "../fonts/plex-sans-700.woff2", weight: "700", style: "normal" },
  ],
  variable: "--font-plex-sans",
  display: "swap",
});

export const plexMono = localFont({
  src: [
    { path: "../fonts/plex-mono-400.woff2", weight: "400", style: "normal" },
    { path: "../fonts/plex-mono-500.woff2", weight: "500", style: "normal" },
    { path: "../fonts/plex-mono-600.woff2", weight: "600", style: "normal" },
  ],
  variable: "--font-plex-mono",
  display: "swap",
});

/** Put on <html>: className={fontVariables} */
export const fontVariables = [plexArabic.variable, plexSans.variable, plexMono.variable].join(" ");
