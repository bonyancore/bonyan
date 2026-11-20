export const languages = {
  en: { label: 'English', direction: 'ltr' },
  ar: { label: 'العربية', direction: 'rtl' },
} as const;

export type Language = keyof typeof languages;

export const languageCodes = Object.keys(languages) as Language[];

export const defaultLanguage: Language = 'en';

export const isLanguage = (value: string | null | undefined): value is Language =>
  value !== null && value !== undefined && value in languages;
