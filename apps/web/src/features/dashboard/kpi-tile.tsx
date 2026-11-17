import type { ReactNode } from "react";
import { Reveal } from "@/components/motion/reveal";
import { Card, Overline } from "@/components/ui/card";
import { Sparkline } from "./sparkline";

type KpiTileProps = {
  label: string;
  value: ReactNode;
  note: ReactNode;
  trend: number[];
  order: number;
};

export function KpiTile({ label, value, note, trend, order }: KpiTileProps) {
  return (
    <Reveal delay={order * 0.05} className="h-full">
      <Card className="flex h-full flex-col gap-3 p-5">
        <Overline className="mb-0">{label}</Overline>
        <div className="text-h1 font-semibold [&_.num]:font-sans [&_.num]:tabular-nums">
          {value}
        </div>
        <Sparkline values={trend} />
        <div className="mt-auto">{note}</div>
      </Card>
    </Reveal>
  );
}
