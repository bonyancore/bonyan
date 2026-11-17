import { Progress } from "@/components/ui/kanban-card";

type Item = { name: string; actual: number; planned: number };

export function PackageProgress({ items, plannedLabel }: { items: Item[]; plannedLabel: string }) {
  return (
    <ul className="flex flex-col gap-4">
      {items.map((item) => (
        <li key={item.name} className="flex flex-col gap-1.5">
          <div className="flex items-baseline justify-between gap-3 text-ui">
            <span className="font-semibold">{item.name}</span>
            <span className="num font-semibold">{Math.round(item.actual)}%</span>
          </div>
          <div className="relative">
            <Progress value={item.actual} />
            <i
              role="img"
              aria-label={`${plannedLabel} ${Math.round(item.planned)}%`}
              className="absolute -inset-y-1 w-0.5 rounded-full bg-ink-950"
              style={{ insetInlineStart: `${item.planned}%` }}
            />
          </div>
        </li>
      ))}
    </ul>
  );
}
