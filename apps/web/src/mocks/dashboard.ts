import type { Currency } from "@/i18n/formats";
import type { Locale } from "@/i18n/locales";

type Text = Record<Locale, string>;

export const dashboard = {
  currency: "EGP" as Currency,
  contract: {
    value: 148_500_000,
    variations: 0.024,
    trend: [140, 141, 141, 144, 145, 147, 148, 148.5],
  },
  progress: {
    actual: 0.62,
    planned: 0.58,
    trend: [31, 36, 41, 46, 50, 54, 58, 62],
  },
  cash: {
    value: 6_480_000,
    weeks: 9,
    trend: [9.2, 8.1, 8.8, 7.4, 7.9, 6.9, 7.2, 6.5],
  },
  approvals: { open: 7, overdue: 2, trend: [3, 5, 4, 6, 5, 8, 6, 7] },
  months: [
    { label: { ar: "مايو", en: "May" }, planned: 8, actual: 7 },
    { label: { ar: "يونيو", en: "Jun" }, planned: 12, actual: 11 },
    { label: { ar: "يوليو", en: "Jul" }, planned: 15, actual: 16 },
    { label: { ar: "أغسطس", en: "Aug" }, planned: 14, actual: 12 },
    { label: { ar: "سبتمبر", en: "Sep" }, planned: 16, actual: 15 },
    { label: { ar: "أكتوبر", en: "Oct" }, planned: 18, actual: 10 },
    { label: { ar: "نوفمبر", en: "Nov" }, planned: 20, actual: null },
    { label: { ar: "ديسمبر", en: "Dec" }, planned: 22, actual: null },
  ] satisfies { label: Text; planned: number; actual: number | null }[],
  packages: [
    {
      name: { ar: "الهيكل الإنشائي", en: "Structure" },
      actual: 0.94,
      planned: 0.92,
    },
    {
      name: { ar: "أعمال المباني", en: "Masonry" },
      actual: 0.78,
      planned: 0.8,
    },
    { name: { ar: "المحارة", en: "Plastering" }, actual: 0.72, planned: 0.66 },
    {
      name: { ar: "الأعمال الكهروميكانيكية", en: "MEP" },
      actual: 0.41,
      planned: 0.48,
    },
    { name: { ar: "التشطيبات", en: "Finishes" }, actual: 0.12, planned: 0.1 },
  ] satisfies { name: Text; actual: number; planned: number }[],
  pipeline: { requested: 5, ordered: 8, delivered: 3, invoiced: 2 },
};
