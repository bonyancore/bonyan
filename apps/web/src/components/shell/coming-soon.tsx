import { useTranslations } from "next-intl";
import { Card } from "@/components/ui/card";

export function ComingSoon() {
  const t = useTranslations("common");

  return (
    <Card className="grid min-h-60 place-items-center p-10 text-ink-600">{t("comingSoon")}</Card>
  );
}
