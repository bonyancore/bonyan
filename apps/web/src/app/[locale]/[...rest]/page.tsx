import NotFound from "../not-found";

/**
 * Catch-all so unknown URLs under a locale render the localized not-found.tsx
 * (inside the layout, with the right lang/dir) instead of the framework default.
 */
export default function CatchAll() {
  return NotFound();
}
