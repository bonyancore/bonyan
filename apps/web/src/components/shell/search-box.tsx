"use client";

import { Search } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useRef } from "react";
import { cn } from "@/lib/utils";

export function SearchBox({ className }: { className?: string }) {
  const t = useTranslations("topbar");
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const focusOnShortcut = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        input.current?.focus();
      }
    };
    window.addEventListener("keydown", focusOnShortcut);
    return () => window.removeEventListener("keydown", focusOnShortcut);
  }, []);

  return (
    <label
      className={cn(
        className,
        "max-w-120 flex-1 flex-row items-center gap-2.5 text-ink-600 focus-within:shadow-[inset_0_-2px_0_var(--color-primary)]",
      )}
    >
      <Search size={16} aria-hidden />
      <input
        ref={input}
        type="search"
        aria-label={t("searchLabel")}
        placeholder={t("searchPlaceholder")}
        className="w-full bg-transparent text-sm text-foreground outline-none placeholder:text-ink-600"
      />
      <kbd className="num text-xs">Ctrl K</kbd>
    </label>
  );
}
