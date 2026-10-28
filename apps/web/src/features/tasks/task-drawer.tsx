"use client";

import { AnimatePresence } from "motion/react";
import * as m from "motion/react-m";
import * as Dialog from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Progress } from "@/components/ui/kanban-card";
import { getDirection, type Locale } from "@/i18n/locales";
import { cn } from "@/lib/utils";
import { columnIds, type CardData, type ColumnId } from "./types";

type TaskDrawerProps = {
  card: CardData | undefined;
  column: ColumnId | undefined;
  onClose: () => void;
  onMove: (id: string, to: ColumnId) => void;
};

const spring = { type: "spring", stiffness: 320, damping: 34 } as const;

export function TaskDrawer({ card, column, onClose, onMove }: TaskDrawerProps) {
  const t = useTranslations("tasks");
  const hidden = getDirection(useLocale() as Locale) === "rtl" ? "-100%" : "100%";

  return (
    <Dialog.Root open={!!card} onOpenChange={(open) => !open && onClose()}>
      <AnimatePresence>
        {card && column && (
          <Dialog.Portal forceMount>
            <Dialog.Overlay asChild>
              <m.div
                className="fixed inset-0 z-40 bg-ink-950/30"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
              />
            </Dialog.Overlay>
            <Dialog.Content
              asChild
              aria-describedby={undefined}
              onCloseAutoFocus={(event) => {
                event.preventDefault();
                document.querySelector<HTMLElement>(`[data-card="${card.id}"]`)?.focus();
              }}
            >
              <m.aside
                className="fixed inset-y-0 end-0 z-50 flex w-md max-w-full flex-col gap-6 overflow-y-auto bg-card p-6 shadow-hover outline-none"
                initial={{ x: hidden }}
                animate={{ x: 0 }}
                exit={{ x: hidden }}
                transition={spring}
              >
                <div className="flex items-center gap-2">
                  <span className="num text-xs font-semibold tracking-[0.2px] text-ink-700">{card.code}</span>
                  <Chip tone={card.tag.tone}>{card.tag.label}</Chip>
                  <Dialog.Close asChild>
                    <Button variant="ghost" size="icon" className="ms-auto -me-2" aria-label={t("drawer.close")}>
                      <X aria-hidden className="size-5" />
                    </Button>
                  </Dialog.Close>
                </div>
                <div className="flex flex-col gap-2">
                  <Dialog.Title className="text-h2 font-semibold">{card.title}</Dialog.Title>
                  {card.meta && <p className="text-ui text-ink-700">{card.meta}</p>}
                </div>
                <section className="flex flex-col gap-2">
                  <h3 className="text-overline font-medium text-ink-600">{t("drawer.status")}</h3>
                  <div className="grid grid-cols-2 gap-2">
                    {columnIds.map((id) => (
                      <button
                        key={id}
                        type="button"
                        aria-pressed={id === column}
                        onClick={() => onMove(card.id, id)}
                        className={cn(
                          "h-9 rounded-md px-3 text-ui font-semibold transition-colors",
                          id === column ? "bg-primary text-primary-foreground" : "bg-muted hover:bg-hover",
                        )}
                      >
                        {t(`columns.${id}`)}
                      </button>
                    ))}
                  </div>
                </section>
                <dl className="flex flex-col gap-4 text-ui">
                  <div className="flex items-center justify-between gap-4">
                    <dt className="text-ink-700">{t("drawer.assignee")}</dt>
                    <dd className="flex items-center gap-2 font-semibold">
                      <span className="inline-flex size-7 items-center justify-center rounded-md bg-accent text-xs font-bold text-accent-foreground">
                        {card.assignee.initial}
                      </span>
                      {card.assignee.name}
                    </dd>
                  </div>
                  <div className="flex items-center justify-between gap-4">
                    <dt className="text-ink-700">{t("drawer.due")}</dt>
                    <dd>
                      <Chip tone={card.due.tone} marker={false}>{card.due.label}</Chip>
                    </dd>
                  </div>
                  {card.progress !== undefined && (
                    <div className="flex items-center justify-between gap-4">
                      <dt className="text-ink-700">{t("drawer.progress")}</dt>
                      <dd className="flex w-48 items-center gap-3">
                        <Progress value={card.progress} className="flex-1" />
                        <span className="num font-semibold">{card.progress}%</span>
                      </dd>
                    </div>
                  )}
                </dl>
              </m.aside>
            </Dialog.Content>
          </Dialog.Portal>
        )}
      </AnimatePresence>
    </Dialog.Root>
  );
}
