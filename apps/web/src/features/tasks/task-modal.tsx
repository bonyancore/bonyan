"use client";

import type { ReactNode } from "react";
import { AnimatePresence } from "motion/react";
import * as m from "motion/react-m";
import * as Dialog from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Progress } from "@/components/ui/kanban-card";
import { cn } from "@/lib/utils";
import { AttachmentsSection } from "./attachments-section";
import { CommentsSection } from "./comments-section";
import { columnIds, type CardData, type ColumnId, type TaskDetail } from "./types";

type TaskModalProps = {
  card: CardData | undefined;
  detail: TaskDetail | undefined;
  column: ColumnId | undefined;
  onClose: () => void;
  onMove: (id: string, to: ColumnId) => void;
  onComment: (id: string, text: string) => void;
  onAttach: (id: string, files: File[]) => void;
};

const spring = { type: "spring", stiffness: 380, damping: 34 } as const;

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <dt className="text-ink-700">{label}</dt>
      <dd className="font-semibold">{children}</dd>
    </div>
  );
}

export function TaskModal({ card, detail, column, onClose, onMove, onComment, onAttach }: TaskModalProps) {
  const t = useTranslations("tasks");
  const nav = useTranslations("nav");

  return (
    <Dialog.Root open={!!card} onOpenChange={(open) => !open && onClose()}>
      <AnimatePresence>
        {card && detail && column && (
          <Dialog.Portal forceMount>
            <Dialog.Overlay asChild>
              <m.div
                className="fixed inset-0 z-40 bg-ink-950/40"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
              />
            </Dialog.Overlay>
            <div className="pointer-events-none fixed inset-0 z-50 grid place-items-center p-4">
              <Dialog.Content
                asChild
                aria-describedby={undefined}
                onCloseAutoFocus={(event) => {
                  event.preventDefault();
                  document.querySelector<HTMLElement>(`[data-card="${card.id}"]`)?.focus();
                }}
              >
                <m.div
                  className="pointer-events-auto flex max-h-full w-full max-w-4xl flex-col overflow-hidden rounded-lg bg-card shadow-hover outline-none"
                  initial={{ opacity: 0, y: 24, scale: 0.97 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  exit={{ opacity: 0, y: 12, scale: 0.98 }}
                  transition={spring}
                >
                  <header className="flex flex-col gap-3 border-b border-border px-6 pb-4 pt-5">
                    <div className="flex items-center gap-2">
                      <span className="num text-xs font-semibold tracking-[0.2px] text-ink-700">{card.code}</span>
                      <Chip tone={card.tag.tone}>{card.tag.label}</Chip>
                      <Dialog.Close asChild>
                        <Button variant="ghost" size="icon" className="-me-2 -mt-1 ms-auto" aria-label={t("drawer.close")}>
                          <X aria-hidden className="size-5" />
                        </Button>
                      </Dialog.Close>
                    </div>
                    <Dialog.Title className="text-h2 font-semibold">{card.title}</Dialog.Title>
                    {card.meta && <p className="text-ui text-ink-700">{card.meta}</p>}
                  </header>

                  <div className="grid min-h-0 flex-1 overflow-y-auto md:grid-cols-[1fr_17rem]">
                    <div className="flex flex-col gap-8 p-6">
                      <section className="flex flex-col gap-2">
                        <h3 className="text-overline font-medium text-ink-600">{t("drawer.description")}</h3>
                        <p className="text-body text-ink-900">{detail.description}</p>
                      </section>
                      <AttachmentsSection items={detail.attachments} onAdd={(files) => onAttach(card.id, files)} />
                      <CommentsSection items={detail.comments} onAdd={(text) => onComment(card.id, text)} />
                    </div>

                    <aside className="flex flex-col gap-6 bg-muted/60 p-6">
                      <section className="flex flex-col gap-2">
                        <h3 className="text-overline font-medium text-ink-600">{t("drawer.status")}</h3>
                        <div className="flex flex-col gap-1.5">
                          {columnIds.map((id) => (
                            <button
                              key={id}
                              type="button"
                              aria-pressed={id === column}
                              onClick={() => onMove(card.id, id)}
                              className={cn(
                                "h-9 rounded-md px-3 text-start text-ui font-semibold transition-colors",
                                id === column ? "bg-primary text-primary-foreground" : "bg-card hover:bg-hover",
                              )}
                            >
                              {t(`columns.${id}`)}
                            </button>
                          ))}
                        </div>
                      </section>
                      <dl className="flex flex-col gap-4 text-ui">
                        <Field label={t("drawer.assignee")}>
                          <span className="flex items-center gap-2">
                            <span className="inline-flex size-7 items-center justify-center rounded-md bg-accent text-xs font-bold text-accent-foreground">
                              {card.assignee.initial}
                            </span>
                            {card.assignee.name}
                          </span>
                        </Field>
                        <Field label={t("drawer.due")}>
                          <Chip tone={card.due.tone} marker={false}>{card.due.label}</Chip>
                        </Field>
                        <Field label={t("drawer.module")}>{nav(card.module)}</Field>
                        {card.progress !== undefined && (
                          <div className="flex flex-col gap-2">
                            <dt className="text-ink-700">{t("drawer.progress")}</dt>
                            <dd className="flex items-center gap-3">
                              <Progress value={card.progress} className="flex-1" />
                              <span className="num font-semibold">{card.progress}%</span>
                            </dd>
                          </div>
                        )}
                      </dl>
                    </aside>
                  </div>
                </m.div>
              </Dialog.Content>
            </div>
          </Dialog.Portal>
        )}
      </AnimatePresence>
    </Dialog.Root>
  );
}
