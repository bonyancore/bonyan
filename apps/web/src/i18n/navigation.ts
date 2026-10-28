import { createNavigation } from "next-intl/navigation";
import { routing } from "./routing";

/**
 * ALWAYS import Link / redirect / usePathname / useRouter from here,
 * never from "next/link" or "next/navigation". These keep the locale prefix.
 */
export const { Link, redirect, usePathname, useRouter, getPathname } = createNavigation(routing);
