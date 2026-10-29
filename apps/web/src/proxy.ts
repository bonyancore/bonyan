import createMiddleware from "next-intl/middleware";
import { routing } from "./i18n/routing";

/**
 * Next.js 16 renamed middleware.ts to proxy.ts.
 * Detects the locale (URL, then NEXT_LOCALE cookie, then Accept-Language, then
 * default), redirects unprefixed paths to /<locale>/..., and stores the choice.
 *
 * When auth arrives, wrap this: run the session check, then call intlProxy(request).
 */
export default createMiddleware(routing);

export const config = {
  // Skip API routes, Next internals and anything with a file extension.
  matcher: "/((?!api|_next|_vercel|.*\\..*).*)",
};
