import { getFormatter, getLocale, getTranslations } from "next-intl/server";
import type { Locale } from "@/i18n/locales";
import { dashboard } from "@/mocks/dashboard";

export async function getDashboard() {
  const locale = (await getLocale()) as Locale;
  const [t, format] = await Promise.all([getTranslations("dashboard"), getFormatter()]);
  const millions = (value: number) => t("millions", { value: format.number(value, "decimal") });

  let planned = 0;
  let actual = 0;
  const months = dashboard.months.map((month) => {
    planned += month.planned;
    const done = month.actual !== null;
    if (done) actual += month.actual;
    const cumulativeActual = done ? actual : null;
    return {
      label: month.label[locale],
      planned,
      actual: cumulativeActual,
      plannedText: millions(planned),
      actualText: cumulativeActual === null ? null : millions(cumulativeActual),
      description: t("cashFlow.point", {
        month: month.label[locale],
        planned: millions(planned),
        actual: cumulativeActual === null ? "-" : millions(cumulativeActual),
      }),
    };
  });

  const packages = dashboard.packages.map((item) => ({
    name: item.name[locale],
    actual: item.actual * 100,
    planned: item.planned * 100,
  }));

  return { ...dashboard, months, packages };
}
