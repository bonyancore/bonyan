"use client";

import { useId } from "react";
import * as m from "motion/react-m";
import { curveMonotoneX } from "@visx/curve";
import { LinearGradient } from "@visx/gradient";
import { ParentSize } from "@visx/responsive";
import { scaleLinear } from "@visx/scale";
import { AreaClosed, LinePath } from "@visx/shape";
import { useDirection } from "@/hooks/use-direction";

type Size = { width: number; height: number };

function Spark({ values, width, height }: Size & { values: number[] }) {
  const id = useId();
  const rtl = useDirection() === "rtl";
  const pad = 4;
  const x = scaleLinear({
    domain: [0, values.length - 1],
    range: rtl ? [width - pad, pad] : [pad, width - pad],
  });
  const y = scaleLinear({
    domain: [Math.min(...values), Math.max(...values)],
    range: [height - pad, pad],
  });
  const data = values.map((value, index) => ({ x: x(index), y: y(value) }));
  const last = data[data.length - 1];

  return (
    <svg width={width} height={height} aria-hidden>
      <LinearGradient
        id={id}
        from="var(--primary)"
        to="var(--primary)"
        fromOpacity={0.22}
        toOpacity={0}
      />
      <AreaClosed
        data={data}
        x={(d) => d.x}
        y={(d) => d.y}
        yScale={y}
        curve={curveMonotoneX}
        fill={`url(#${id})`}
      />
      <LinePath
        data={data}
        x={(d) => d.x}
        y={(d) => d.y}
        curve={curveMonotoneX}
        className="stroke-primary"
        strokeWidth={2}
        strokeLinecap="round"
        fill="none"
      />
      <circle
        cx={last.x}
        cy={last.y}
        r={3.5}
        className="fill-primary stroke-card"
        strokeWidth={2}
      />
    </svg>
  );
}

export function Sparkline({ values }: { values: number[] }) {
  const rtl = useDirection() === "rtl";

  return (
    <m.div
      className="h-12 w-full"
      initial={{ clipPath: rtl ? "inset(0 0 0 100%)" : "inset(0 100% 0 0)" }}
      animate={{ clipPath: "inset(0 0% 0 0%)" }}
      transition={{ duration: 0.9, ease: "easeOut" }}
    >
      <ParentSize>
        {({ width, height }) =>
          width > 0 && <Spark values={values} width={width} height={height} />
        }
      </ParentSize>
    </m.div>
  );
}
