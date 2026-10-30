import * as React from "react";
import { cn } from "@/lib/utils";
import { Chip, type ChipProps } from "./chip";

/** Segmented progress: 8px track with a tick every 5%. Matches .pg in the boards. */
export function Progress({ value, className }: { value: number; className?: string }) {
  const v = Math.max(0, Math.min(100, value));
  return (
    <div
      role="progressbar"
      aria-valuenow={v}
      aria-valuemin={0}
      aria-valuemax={100}
      className={cn("relative h-2 overflow-hidden rounded-[1px] bg-track", className)}
    >
      <i className="block h-full bg-primary" style={{ width: `${v}%` }} />
      <span
        aria-hidden
        className="absolute inset-0 opacity-90"
        style={{ background: "linear-gradient(to left,#fff 1.5px,transparent 1.5px) 0 0/5% 100%" }}
      />
    </div>
  );
}

export interface KanbanCardProps extends React.HTMLAttributes<HTMLDivElement> {
  code: string;
  title: string;
  meta?: string;
  tag?: { label: string; tone: ChipProps["tone"] };
  progress?: number;
  assignee: { initial: string; name: string };
  due: { label: string; tone: ChipProps["tone"] };
  selected?: boolean;
  /** Spread dnd-kit's attributes/listeners/ref here when making it sortable. */
  dragging?: boolean;
}

/**
 * Presentational task card (no drag logic inside). Wrap it with dnd-kit's
 * useSortable in the board, so the card stays reusable in lists and mobile too.
 */
export function KanbanCard({
  code, title, meta, tag, progress, assignee, due, selected, dragging, className, ...props
}: KanbanCardProps) {
  return (
    <div
      className={cn(
        "relative flex flex-col gap-2.5 rounded-md bg-card p-3.5 shadow-hair transition-shadow",
        "hover:shadow-hover",
        selected && "shadow-selected",
        dragging && "rotate-[0.6deg] shadow-selected",
        className,
      )}
      {...props}
    >
      <div className="flex items-center gap-2">
        <span className="num text-xs font-semibold tracking-[0.2px] text-ink-700">{code}</span>
        {tag && <Chip tone={tag.tone} className="ms-auto">{tag.label}</Chip>}
      </div>
      <div className="text-ui font-semibold">{title}</div>
      {meta && <div className="text-caption text-ink-700">{meta}</div>}
      {progress !== undefined && <Progress value={progress} />}
      <div className="flex items-center gap-2">
        <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-md bg-accent text-xs font-bold text-accent-foreground">
          {assignee.initial}
        </span>
        <span className="flex-1 text-caption text-ink-700">{assignee.name}</span>
        <Chip tone={due.tone} marker={false}>{due.label}</Chip>
      </div>
    </div>
  );
}
