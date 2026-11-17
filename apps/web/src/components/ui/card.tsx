import * as React from "react";
import { cn } from "@/lib/utils";

/** White surface, 8px radius, 1px hairline (shadow-hair) — no blur shadows. */
export function Card({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("rounded-lg bg-card text-card-foreground shadow-hair", className)} {...props} />;
}

/** Small uppercase-style label used above values and in table heads. */
export function Overline({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn("mb-1.5 flex items-center gap-2 text-overline font-medium text-ink-600", className)}
      {...props}
    />
  );
}
