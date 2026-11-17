"use client";

import { useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import type { Comment } from "./types";

type CommentsSectionProps = {
  items: Comment[];
  onAdd: (text: string) => void;
};

const avatar =
  "inline-flex size-8 shrink-0 items-center justify-center rounded-md bg-accent text-xs font-bold text-accent-foreground";

export function CommentsSection({ items, onAdd }: CommentsSectionProps) {
  const t = useTranslations("tasks.drawer");
  const [text, setText] = useState("");
  const empty = text.trim() === "";

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (empty) return;
    onAdd(text.trim());
    setText("");
  };

  return (
    <section className="flex flex-col gap-4">
      <h3 className="text-overline font-medium text-ink-600">
        {t("comments")} <span className="num">{items.length}</span>
      </h3>
      {items.length === 0 ? (
        <p className="rounded-md bg-muted px-4 py-3 text-caption text-ink-600">{t("commentsEmpty")}</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {items.map(({ id, author, text: body, when }) => (
            <li key={id} className="flex gap-3">
              <span className={avatar}>{author.split(" ").at(-1)?.charAt(0)}</span>
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <div className="flex items-baseline gap-2">
                  <span className="text-ui font-semibold">{author}</span>
                  <span className="text-caption text-ink-600">{when}</span>
                </div>
                <p className="text-ui text-ink-900">{body}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
      <form onSubmit={submit} className="flex flex-col gap-2">
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && (event.metaKey || event.ctrlKey)) submit(event);
          }}
          rows={3}
          aria-label={t("comments")}
          placeholder={t("commentPlaceholder")}
          className="resize-none rounded-md bg-muted p-3 text-ui outline-offset-2 placeholder:text-ink-600"
        />
        <Button type="submit" size="sm" disabled={empty} className="self-end">
          {t("send")}
        </Button>
      </form>
    </section>
  );
}
