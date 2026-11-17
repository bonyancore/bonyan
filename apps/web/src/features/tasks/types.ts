import type { ChipProps } from "@/components/ui/chip";

export const columnIds = ["todo", "doing", "waiting", "done"] as const;

export type ColumnId = (typeof columnIds)[number];

export const modules = ["procurement", "site", "money", "quality"] as const;

export type Module = (typeof modules)[number];

export type Tone = NonNullable<ChipProps["tone"]>;

export type BoardState = Record<ColumnId, string[]>;

export type CardData = {
  id: string;
  code: string;
  module: Module;
  title: string;
  meta?: string;
  tag: { label: string; tone: Tone };
  progress?: number;
  assignee: { initial: string; name: string };
  due: { label: string; tone: Tone };
};

export const columnTones = {
  todo: "primary",
  doing: "info",
  waiting: "warn",
  done: "ok",
} as const satisfies Record<ColumnId, Tone>;

export type FileKind = "file" | "image" | "sheet";

export type Attachment = {
  id: string;
  name: string;
  sizeKb: number;
  kind: FileKind;
};

export type Comment = {
  id: string;
  author: string;
  text: string;
  when: string;
};

export type TaskDetail = {
  description: string;
  attachments: Attachment[];
  comments: Comment[];
};
