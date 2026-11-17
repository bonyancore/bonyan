"use client";

import { isActive } from "@/config/navigation";
import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@/lib/utils";

type ModuleTabsProps = {
  label: string;
  items: { href: string; label: string }[];
};

export function ModuleTabs({ label, items }: ModuleTabsProps) {
  const pathname = usePathname();

  return (
    <nav aria-label={label} className="flex flex-wrap border-b">
      {items.map((item) => {
        const active = isActive(pathname, item.href);

        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "-mb-px inline-flex h-10 items-center border-b-2 border-transparent px-4.5 text-sm font-medium text-ink-700 transition-colors hover:bg-black/5 hover:text-foreground",
              active && "border-primary font-bold text-primary",
            )}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
