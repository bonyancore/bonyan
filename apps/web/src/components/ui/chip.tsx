import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/**
 * Status chip (.chip in the boards). The 6px square marker is the non-colour cue,
 * so status never relies on hue alone. Text/background pairs are pre-checked for 4.5:1.
 */
export const chipVariants = cva(
  "inline-flex h-6 items-center gap-1.5 whitespace-nowrap rounded-sm px-[9px] text-[12.5px] font-semibold",
  {
    variants: {
      tone: {
        neutral: "bg-sand-200 text-ink-700",
        primary: "bg-steel-100 text-steel-900",
        ok: "bg-ok-100 text-ok-800",
        warn: "bg-warn-100 text-warn-900",
        danger: "bg-danger-100 text-danger-900",
        info: "bg-info-100 text-info-900",
        ai: "bg-purple-100 text-purple-800",
      },
    },
    defaultVariants: { tone: "neutral" },
  },
);

export interface ChipProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof chipVariants> {
  marker?: boolean;
}

export function Chip({ className, tone, marker = true, children, ...props }: ChipProps) {
  return (
    <span className={cn(chipVariants({ tone }), className)} {...props}>
      {marker && <i aria-hidden className="inline-block size-1.5 rounded-[1px] bg-current" />}
      {children}
    </span>
  );
}
