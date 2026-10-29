"use client";

import { useCallback, useMemo, useRef, useState } from "react";
import {
  Accessibility,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from "@dnd-kit/dom";
import { DragDropProvider } from "@dnd-kit/react";
import { move } from "@dnd-kit/helpers";
import { useTranslations } from "next-intl";
import { Column } from "./column";
import { columnOf, sensors } from "./dnd";
import { ListView } from "./list-view";
import { TaskModal } from "./task-modal";
import { Toolbar, type Filter, type View } from "./toolbar";
import {
  columnIds,
  type BoardState,
  type CardData,
  type ColumnId,
  type TaskDetail,
} from "./types";
import { kindOf } from "./files";

type BoardProps = {
  initial: BoardState;
  cards: Record<string, CardData>;
  initialDetails: Record<string, TaskDetail>;
  me: { name: string; initial: string };
};

export function Board({ initial, cards, initialDetails, me }: BoardProps) {
  const t = useTranslations("tasks");
  const [board, setBoard] = useState(initial);
  const [details, setDetails] = useState(initialDetails);
  const [filter, setFilter] = useState<Filter>("all");
  const [view, setView] = useState<View>("board");
  const [openId, setOpenId] = useState<string>();
  const snapshot = useRef(initial);
  const open = useCallback((id: string) => setOpenId(id), []);
  const close = useCallback(() => setOpenId(undefined), []);
  const visible = useMemo(
    () =>
      filter === "all"
        ? board
        : (Object.fromEntries(
            columnIds.map((id) => [
              id,
              board[id].filter((item) => cards[item].module === filter),
            ]),
          ) as BoardState),
    [board, cards, filter],
  );
  const openColumn = columnIds.find(
    (id) => openId && board[id].includes(openId),
  );

  const moveCard = (id: string, to: ColumnId) =>
    setBoard((current) => {
      const next = { ...current };
      for (const key of columnIds)
        next[key] = current[key].filter((item) => item !== id);
      next[to] = [...next[to], id];
      return next;
    });

  const updateDetail = (
    id: string,
    patch: (detail: TaskDetail) => Partial<TaskDetail>,
  ) =>
    setDetails((current) => ({
      ...current,
      [id]: { ...current[id], ...patch(current[id]) },
    }));

  const addComment = (id: string, text: string) =>
    updateDetail(id, (detail) => ({
      comments: [
        ...detail.comments,
        {
          id: crypto.randomUUID(),
          author: me.name,
          text,
          when: t("drawer.justNow"),
        },
      ],
    }));

  const addAttachments = (id: string, files: File[]) =>
    updateDetail(id, (detail) => ({
      attachments: [
        ...detail.attachments,
        ...files.map((file) => ({
          id: crypto.randomUUID(),
          name: file.name,
          sizeKb: Math.max(1, Math.round(file.size / 1024)),
          kind: kindOf(file),
        })),
      ],
    }));

  const title = (source: { data: { title?: string } } | null | undefined) =>
    source?.data.title ?? "";
  const column = (target: Parameters<typeof columnOf>[0]) => {
    const id = columnOf(target);
    return id ? t(`columns.${id}`) : "";
  };

  return (
    <>
      <Toolbar
        filter={filter}
        view={view}
        onFilter={setFilter}
        onView={setView}
      />
      {view === "board" ? (
        <DragDropProvider
          sensors={sensors}
          plugins={(defaults) => [
            ...defaults.filter((plugin) => plugin !== Accessibility),
            Accessibility.configure({
              screenReaderInstructions: { draggable: t("a11y.instructions") },
              announcements: {
                dragstart: ({ operation }: DragStartEvent) =>
                  t("a11y.start", { title: title(operation.source) }),
                dragover: ({ operation }: DragOverEvent) =>
                  t("a11y.over", {
                    title: title(operation.source),
                    column: column(operation.target),
                  }),
                dragend: ({ operation, canceled }: DragEndEvent) =>
                  canceled
                    ? t("a11y.cancel", { title: title(operation.source) })
                    : t("a11y.drop", {
                        title: title(operation.source),
                        column: column(operation.target),
                      }),
              },
            }),
          ]}
          onDragStart={() => {
            snapshot.current = board;
          }}
          onDragOver={(event) => setBoard((current) => move(current, event))}
          onDragEnd={(event) => {
            if (event.canceled) setBoard(snapshot.current);
          }}
        >
          <div
            role="group"
            aria-label={t("board.label")}
            className="grid grid-cols-[repeat(4,minmax(17rem,1fr))] items-start gap-4 overflow-x-auto pb-2"
          >
            {columnIds.map((id, order) => (
              <Column
                key={id}
                id={id}
                ids={visible[id]}
                cards={cards}
                order={order}
                locked={filter !== "all"}
                onOpen={open}
              />
            ))}
          </div>
        </DragDropProvider>
      ) : (
        <ListView board={visible} cards={cards} onOpen={open} />
      )}
      <TaskModal
        card={openId ? cards[openId] : undefined}
        detail={openId ? details[openId] : undefined}
        column={openColumn}
        onClose={close}
        onMove={moveCard}
        onComment={addComment}
        onAttach={addAttachments}
      />
    </>
  );
}
