"use client";

import { useTranslations } from "next-intl";
import { useSyncExternalStore } from "react";
import { Chip } from "@/components/ui/chip";

const subscribe = (notify: () => void) => {
  window.addEventListener("online", notify);
  window.addEventListener("offline", notify);
  return () => {
    window.removeEventListener("online", notify);
    window.removeEventListener("offline", notify);
  };
};

export function OnlineStatus() {
  const t = useTranslations("topbar");
  const online = useSyncExternalStore(
    subscribe,
    () => navigator.onLine,
    () => true,
  );

  return <Chip tone={online ? "ok" : "danger"}>{online ? t("online") : t("offline")}</Chip>;
}
