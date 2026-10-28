import { useFormatter, useTranslations } from "next-intl";
import type { Currency } from "@/i18n/formats";

export function Money({ value, currency }: { value: number; currency: Currency }) {
  const t = useTranslations("common");
  const tc = useTranslations("currency");
  const format = useFormatter();

  return (
    <span className="inline-flex items-baseline gap-[0.3em]">
      {t.rich("money", {
        amount: format.number(value, "integer"),
        currency: tc(currency),
        num: (chunks) => <span className="num">{chunks}</span>,
        cur: (chunks) => <span>{chunks}</span>,
      })}
    </span>
  );
}
