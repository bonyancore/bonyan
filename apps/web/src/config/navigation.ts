import {
  Banknote,
  HardHat,
  House,
  LayoutGrid,
  ListChecks,
  Settings,
  ShieldCheck,
  ShoppingBag,
} from "lucide-react";

export const sections = ["procurement", "site", "money", "quality", "projects"] as const;

export type Section = (typeof sections)[number];

export const isSection = (value: string): value is Section => sections.includes(value as Section);

export const navItems = [
  { key: "home", href: "/", icon: House },
  { key: "tasks", href: "/tasks", icon: ListChecks },
  { key: "procurement", href: "/procurement", icon: ShoppingBag },
  { key: "site", href: "/site", icon: HardHat },
  { key: "money", href: "/money", icon: Banknote },
  { key: "quality", href: "/quality", icon: ShieldCheck },
  { key: "projects", href: "/projects", icon: LayoutGrid },
  { key: "system", href: "/system", icon: Settings },
] as const;

export const systemTabs = [
  { slug: "roles", key: "roles" },
  { slug: "approvals", key: "approvals" },
  { slug: "automation", key: "automation" },
  { slug: "master-data", key: "masterData" },
  { slug: "notifications", key: "notifications" },
  { slug: "users", key: "users" },
  { slug: "operator", key: "operator" },
] as const;

export const isActive = (pathname: string, href: string) =>
  href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`);
