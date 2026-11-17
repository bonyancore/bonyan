import type { ReactNode } from "react";
import { MotionProvider } from "@/components/motion/provider";
import { Rail } from "./rail";
import { TopBar } from "./top-bar";

export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="bg-blueprint flex min-h-dvh">
      <Rail />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar />
        <main className="flex flex-col gap-6 px-8 py-6">
          <MotionProvider>{children}</MotionProvider>
        </main>
      </div>
    </div>
  );
}
