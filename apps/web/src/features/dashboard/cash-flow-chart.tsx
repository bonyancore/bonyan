"use client";

import { useId, useState, type PointerEvent } from "react";
import * as m from "motion/react-m";
import { curveMonotoneX } from "@visx/curve";
import { LinearGradient } from "@visx/gradient";
import { Group } from "@visx/group";
import { ParentSize } from "@visx/responsive";
import { scaleLinear, scalePoint } from "@visx/scale";
import { AreaClosed, LinePath } from "@visx/shape";
import { useFormatter, useTranslations } from "next-intl";
import { useDirection } from "@/hooks/use-direction";

type Point = {
  label: string;
  planned: number;
  actual: number | null;
  plannedText: string;
  actualText: string | null;
  description: string;
};

type Size = { width: number; height: number };

function Chart({ points, width, height }: Size & { points: Point[] }) {
  const id = useId();
  const t = useTranslations("dashboard.cashFlow");
  const format = useFormatter();
  const rtl = useDirection() === "rtl";
  const [active, setActive] = useState<number>();

  const margin = { top: 12, bottom: 30, start: 44, end: 12 };
  const left = rtl ? margin.end : margin.start;
  const right = rtl ? margin.start : margin.end;
  const w = width - left - right;
  const h = height - margin.top - margin.bottom;

  const x = scalePoint({
    domain: points.map((p) => p.label),
    range: rtl ? [w, 0] : [0, w],
    padding: 0.15,
  });
  const y = scaleLinear({
    domain: [
      0,
      Math.max(...points.map((p) => Math.max(p.planned, p.actual ?? 0))) * 1.08,
    ],
    range: [h, 0],
    nice: true,
  });
  const px = (p: Point) => x(p.label) ?? 0;

  const done = points.filter(
    (p): p is Point & { actual: number } => p.actual !== null,
  );
  const last = done[done.length - 1];
  const hovered = active === undefined ? undefined : points[active];

  const onMove = (event: PointerEvent<SVGRectElement>) => {
    const offset =
      event.clientX - event.currentTarget.getBoundingClientRect().left;
    let nearest = 0;
    points.forEach((p, i) => {
      if (Math.abs(px(p) - offset) < Math.abs(px(points[nearest]) - offset))
        nearest = i;
    });
    setActive(nearest);
  };

  return (
    <div className="relative" style={{ width, height }}>
      <svg width={width} height={height} role="img" aria-label={t("unit")}>
        <LinearGradient
          id={id}
          from="var(--primary)"
          to="var(--primary)"
          fromOpacity={0.2}
          toOpacity={0}
        />
        <Group left={left} top={margin.top}>
          {y.ticks(4).map((tick) => (
            <g key={tick} transform={`translate(0,${y(tick)})`}>
              <line
                x2={w}
                className={tick === 0 ? "stroke-sand-400" : "stroke-border"}
                strokeDasharray={tick === 0 ? undefined : "3 4"}
              />
              <text
                x={rtl ? w + 10 : -10}
                dy="0.32em"
                textAnchor={rtl ? "start" : "end"}
                className="fill-ink-600 text-caption"
              >
                {format.number(tick, "integer")}
              </text>
            </g>
          ))}
          {points.map((p) => (
            <text
              key={p.label}
              x={px(p)}
              y={h + 20}
              textAnchor="middle"
              className="fill-ink-700 text-caption"
            >
              {p.label}
            </text>
          ))}

          <m.g
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.5 }}
          >
            <LinePath
              data={points}
              x={px}
              y={(p) => y(p.planned)}
              curve={curveMonotoneX}
              className="stroke-ink-300"
              strokeWidth={2}
              strokeDasharray="5 5"
              fill="none"
            />
            <AreaClosed
              data={done}
              x={px}
              y={(p) => y(p.actual)}
              yScale={y}
              curve={curveMonotoneX}
              fill={`url(#${id})`}
            />
            <LinePath
              data={done}
              x={px}
              y={(p) => y(p.actual)}
              curve={curveMonotoneX}
              className="stroke-primary"
              strokeWidth={3}
              strokeLinecap="round"
              fill="none"
            />
          </m.g>

          {last && (
            <g transform={`translate(${px(last)},${y(last.actual)})`}>
              <circle
                r={9}
                className="origin-center animate-ping fill-primary/30 transform-fill"
              />
              <circle
                r={5}
                className="fill-primary stroke-card"
                strokeWidth={2.5}
              />
            </g>
          )}

          {hovered && (
            <g pointerEvents="none">
              <line
                x1={px(hovered)}
                x2={px(hovered)}
                y2={h}
                className="stroke-ink-300"
                strokeDasharray="3 3"
              />
              <circle
                cx={px(hovered)}
                cy={y(hovered.planned)}
                r={4}
                className="fill-card stroke-ink-300"
                strokeWidth={2}
              />
              {hovered.actual !== null && (
                <circle
                  cx={px(hovered)}
                  cy={y(hovered.actual)}
                  r={5}
                  className="fill-primary stroke-card"
                  strokeWidth={2.5}
                />
              )}
            </g>
          )}
          <rect
            width={w}
            height={h}
            fill="transparent"
            onPointerMove={onMove}
            onPointerLeave={() => setActive(undefined)}
          />
        </Group>
      </svg>

      {hovered && (
        <div
          className="pointer-events-none absolute z-10 min-w-40 -translate-x-1/2 rounded-md bg-ink-950 px-3 py-2.5 text-caption text-white shadow-hover"
          style={{ left: left + px(hovered), top: margin.top }}
        >
          <div className="mb-1.5 font-semibold">{hovered.label}</div>
          <dl className="flex flex-col gap-1">
            <div className="flex items-center justify-between gap-4">
              <dt className="flex items-center gap-1.5 text-ink-300">
                <i aria-hidden className="size-2 rounded-[1px] bg-ink-300" />
                {t("planned")}
              </dt>
              <dd className="font-semibold">{hovered.plannedText}</dd>
            </div>
            {hovered.actualText && (
              <div className="flex items-center justify-between gap-4">
                <dt className="flex items-center gap-1.5 text-ink-300">
                  <i
                    aria-hidden
                    className="size-2 rounded-[1px] bg-amber-500"
                  />
                  {t("actual")}
                </dt>
                <dd className="font-semibold">{hovered.actualText}</dd>
              </div>
            )}
          </dl>
        </div>
      )}
    </div>
  );
}

export function CashFlowChart({ points }: { points: Point[] }) {
  return (
    <>
      <div className="min-h-64 flex-1">
        <ParentSize>
          {({ width, height }) =>
            width > 0 && (
              <Chart
                points={points}
                width={width}
                height={Math.max(height, 256)}
              />
            )
          }
        </ParentSize>
      </div>
      <ul className="sr-only">
        {points.map((p) => (
          <li key={p.label}>{p.description}</li>
        ))}
      </ul>
    </>
  );
}
