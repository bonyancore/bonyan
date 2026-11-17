"use client";

import { Columns3, List } from "lucide-react";
import { useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import { modules, type Module } from "./types";

export type Filter = Module | "all";
export type View = "board" | "list";

const views = [
  { key: "board", icon: Columns3 },
  { key: "list", icon: List },
] as const;

const segment = "inline-flex h-8 items-center gap-2 rounded-md px-3 text-ui font-semibold transition-colors";
const state = (active: boolean) => (active ? "bg-primary text-primary-foreground" : "bg-muted hover:bg-hover");

type ToolbarProps = {
  filter: Filter;
  view: View;
  onFilter: (filter: Filter) => void;
  onView: (view: View) => void;
};

export function Toolbar({ filter, view, onFilter, onView }: ToolbarProps) {
  const t = useTranslations("tasks.toolbar");
  const nav = useTranslations("nav");

  return (
    <div className="flex flex-wrap items-center gap-3">
      <div role="group" aria-label={t("filters")} className="flex flex-wrap gap-2">
        {(["all", ...modules] as const).map((key) => (
          <button
            key={key}
            type="button"
            aria-pressed={key === filter}
            onClick={() => onFilter(key)}
            className={cn(segment, state(key === filter))}
          >
            {key === "all" ? t("all") : nav(key)}
          </button>
        ))}
      </div>
      {filter !== "all" && view === "board" && <p className="text-caption text-ink-600">{t("dragOff")}</p>}
      <div role="group" aria-label={t("views")} className="ms-auto flex gap-1 rounded-md bg-muted p-1">
        {views.map(({ key, icon: Icon }) => (
          <button
            key={key}
            type="button"
            aria-pressed={key === view}
            onClick={() => onView(key)}
            className={cn(segment, "h-7", key === view ? "bg-card shadow-hair" : "hover:bg-hover")}
          >
            <Icon aria-hidden className="size-4" />
            {t(key)}
          </button>
        ))}
      </div>
    </div>
  );
}
