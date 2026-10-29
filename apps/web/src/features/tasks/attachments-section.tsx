"use client";

import { useRef } from "react";
import { FileSpreadsheet, FileText, ImageIcon, Paperclip } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import type { Attachment, FileKind } from "./types";

const icons = { file: FileText, image: ImageIcon, sheet: FileSpreadsheet } satisfies Record<FileKind, unknown>;

type AttachmentsSectionProps = {
  items: Attachment[];
  onAdd: (files: File[]) => void;
};

export function AttachmentsSection({ items, onAdd }: AttachmentsSectionProps) {
  const t = useTranslations("tasks.drawer");
  const format = useFormatter();
  const input = useRef<HTMLInputElement>(null);

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center gap-2">
        <h3 className="text-overline font-medium text-ink-600">
          {t("attachments")} <span className="num">{items.length}</span>
        </h3>
        <Button variant="ghost" size="sm" className="ms-auto" onClick={() => input.current?.click()}>
          <Paperclip aria-hidden className="size-4" />
          {t("attach")}
        </Button>
        <input
          ref={input}
          type="file"
          multiple
          hidden
          onChange={(event) => {
            onAdd(Array.from(event.target.files ?? []));
            event.target.value = "";
          }}
        />
      </div>
      {items.length === 0 ? (
        <p className="rounded-md bg-muted px-4 py-3 text-caption text-ink-600">{t("attachmentsEmpty")}</p>
      ) : (
        <ul className="grid gap-2 sm:grid-cols-2">
          {items.map(({ id, name, sizeKb, kind }) => {
            const Icon = icons[kind];
            return (
              <li key={id} className="flex items-center gap-3 rounded-md bg-muted px-3 py-2.5">
                <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-md bg-card text-primary">
                  <Icon aria-hidden className="size-4" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="num block truncate text-ui font-semibold">{name}</span>
                  <span className="block text-caption text-ink-600">
                    {t("kb", { value: format.number(sizeKb, "integer") })}
                  </span>
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
