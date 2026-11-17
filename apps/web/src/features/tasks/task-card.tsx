"use client";

import { memo } from "react";
import { useSortable } from "@dnd-kit/react/sortable";
import { KanbanCard } from "@/components/ui/kanban-card";
import { ITEM_TYPE } from "./dnd";
import type { CardData, ColumnId } from "./types";

type TaskCardProps = {
  card: CardData;
  column: ColumnId;
  index: number;
  disabled: boolean;
  onOpen: (id: string) => void;
};

export const TaskCard = memo(function TaskCard({ card, column, index, disabled, onOpen }: TaskCardProps) {
  const { ref, isDragging } = useSortable({
    id: card.id,
    index,
    group: column,
    type: ITEM_TYPE,
    accept: ITEM_TYPE,
    disabled,
    data: { title: card.title },
  });

  return (
    <div
      ref={ref}
      data-card={card.id}
      className="cursor-pointer rounded-md outline-offset-2"
      onClick={() => onOpen(card.id)}
      onKeyDown={(event) => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        onOpen(card.id);
      }}
    >
      <KanbanCard
        code={card.code}
        title={card.title}
        meta={card.meta}
        tag={card.tag}
        progress={card.progress}
        assignee={card.assignee}
        due={card.due}
        dragging={isDragging}
      />
    </div>
  );
});
