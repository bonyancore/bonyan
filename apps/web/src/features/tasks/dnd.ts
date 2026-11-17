import { KeyboardSensor, PointerActivationConstraints, PointerSensor, type Droppable, type Sensors } from "@dnd-kit/dom";
import { isSortable } from "@dnd-kit/react/sortable";
import { columnIds, type ColumnId } from "./types";

export const ITEM_TYPE = "item";
export const COLUMN_TYPE = "column";

export const sensors = (defaults: Sensors) => [
  ...defaults.filter((sensor) => sensor !== PointerSensor && sensor !== KeyboardSensor),
  KeyboardSensor.configure({
    keyboardCodes: {
      start: ["Space"],
      cancel: ["Escape"],
      end: ["Space", "Tab"],
      up: ["ArrowUp"],
      down: ["ArrowDown"],
      left: ["ArrowLeft"],
      right: ["ArrowRight"],
    },
  }),
  PointerSensor.configure({
    activationConstraints: (event) =>
      event.pointerType === "touch"
        ? [new PointerActivationConstraints.Delay({ value: 200, tolerance: { x: 8, y: 8 } })]
        : [new PointerActivationConstraints.Distance({ value: 6 })],
  }),
];

export function columnOf(target: Droppable | null | undefined): ColumnId | undefined {
  const id = target && isSortable(target) ? target.group : target?.id;
  return columnIds.find((column) => column === id);
}
