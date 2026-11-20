import { createInstance } from 'i18next';
import { initReactI18next } from 'react-i18next';

import { usePreferences } from '@/core/preferences/store';

import { ensureDirection } from './direction';
import type { Language } from './languages';
import { ar } from './locales/ar';
import { en, type Resources } from './locales/en';

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation';
    resources: { translation: Resources };
  }
}

const i18n = createInstance();

i18n.use(initReactI18next).init({
  lng: usePreferences.getState().language,
  fallbackLng: 'en',
  resources: {
    en: { translation: en },
    ar: { translation: ar },
  },
  interpolation: { escapeValue: false },
});

export const changeLanguage = async (language: Language) => {
  usePreferences.getState().setLanguage(language);
  await i18n.changeLanguage(language);
  await ensureDirection(language);
};

export default i18n;
