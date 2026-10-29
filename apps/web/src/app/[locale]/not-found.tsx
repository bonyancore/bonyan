import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";

export default function NotFound() {
  const t = useTranslations("errors");
  return (
    <main className="bg-blueprint grid min-h-dvh place-items-center p-8">
      <div className="flex max-w-md flex-col items-start gap-4">
        <h1 className="page-title">{t("notFoundTitle")}</h1>
        <p className="text-ink-700">{t("notFoundBody")}</p>
        <Link href="/">
          <Button>{t("backHome")}</Button>
        </Link>
      </div>
    </main>
  );
}
