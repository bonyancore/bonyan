import { getLocale, getTranslations } from "next-intl/server";
import type { Locale } from "@/i18n/locales";
import { mockBoard, mockTasks, type Due } from "@/mocks/tasks";
import { user } from "@/mocks/session";
import type { BoardState, CardData, TaskDetail, Tone } from "./types";

const dueTones = {
  today: "danger",
  tomorrow: "neutral",
  overdue: "danger",
} as const;

const isTime = (due: Due): due is `${number}:${number}` => due.includes(":");

export async function getBoard() {
  const locale = (await getLocale()) as Locale;
  const [t, common] = await Promise.all([
    getTranslations("tasks"),
    getTranslations("common"),
  ]);
  const name = user.name[locale];

  const doneIds = new Set(mockBoard.done);

  const cards = Object.fromEntries(
    mockTasks.map((task): [string, CardData] => {
      const dueLabel = isTime(task.due) ? task.due : common(task.due);
      const dueTone: Tone = doneIds.has(task.id)
        ? "ok"
        : isTime(task.due)
          ? "warn"
          : dueTones[task.due];
      return [
        task.id,
        {
          id: task.id,
          code: task.code,
          module: task.module,
          title: task.title[locale],
          meta: task.meta?.[locale],
          tag: { label: t(`tags.${task.tag.key}`), tone: task.tag.tone },
          progress: task.progress,
          assignee: { initial: name.charAt(0), name },
          due: { label: dueLabel, tone: dueTone },
        },
      ];
    }),
  );

  const details = Object.fromEntries(
    mockTasks.map((task): [string, TaskDetail] => [
      task.id,
      {
        description: task.description[locale],
        attachments: task.attachments.map((file, index) => ({
          ...file,
          id: `${task.id}-file-${index}`,
        })),
        comments: task.comments.map((comment, index) => ({
          id: `${task.id}-comment-${index}`,
          author: comment.author[locale],
          text: comment.text[locale],
          when: comment.when[locale],
        })),
      },
    ]),
  );

  const board: BoardState = mockBoard;
  const dueToday = mockTasks.filter(
    (task) => task.due === "today" && !doneIds.has(task.id),
  ).length;

  return {
    cards,
    details,
    board,
    dueToday,
    me: { name, initial: name.charAt(0) },
  };
}
