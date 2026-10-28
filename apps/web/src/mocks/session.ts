import type { Locale } from "@/i18n/locales";

export const project = {
  code: "NKH-B3",
  name: {
    ar: "مجمع النخيل · المبنى 3",
    en: "Al Nakheel Complex · Building 3",
  },
} satisfies { code: string; name: Record<Locale, string> };

export const user = {
  name: {
    ar: "كريم",
    en: "Karim",
  },
} satisfies { name: Record<Locale, string> };
