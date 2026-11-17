import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

/**
 * Custom design tokens that tailwind-merge cannot infer. Without this, cn() treats
 * `text-ui` as a text COLOR and silently deletes the real colour class next to it
 * (e.g. cn("text-primary-foreground", "text-ui") -> "text-ui").
 * Keep in sync with --text-* and --shadow-* in src/app/globals.css.
 * `npm run check:tokens` fails if they drift.
 */
export const CUSTOM_FONT_SIZES = ["micro", "overline", "caption", "ui", "body", "h1", "h2"];
export const CUSTOM_SHADOWS = ["hair", "hover", "selected"];

const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      "font-size": [{ text: CUSTOM_FONT_SIZES }],
      shadow: [{ shadow: CUSTOM_SHADOWS }],
    },
  },
});

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
