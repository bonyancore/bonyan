import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";


export const buttonVariants = cva(
  "inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-md font-semibold tracking-[0.1px] transition-colors disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        primary: "bg-primary text-primary-foreground hover:bg-steel-900",
        outline:
          "bg-transparent text-primary shadow-[inset_0_0_0_1.5px_var(--color-steel-700)] hover:bg-accent",
        ghost: "bg-transparent text-muted-foreground hover:bg-hover",
        danger: "bg-destructive text-white hover:bg-danger-900",
      },
      size: {
        default: "h-10 px-[18px] text-ui",
        sm: "h-8 rounded-[5px] px-3 text-caption",
        icon: "size-10",
      },
    },
    defaultVariants: { variant: "primary", size: "default" },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {}

export function Button({ className, variant, size, type = "button", ...props }: ButtonProps) {
  return <button type={type} className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}
