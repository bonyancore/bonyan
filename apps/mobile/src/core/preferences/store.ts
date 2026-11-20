import { getLocales } from 'expo-localization';
import Storage from 'expo-sqlite/kv-store';
import { Platform } from 'react-native';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import { defaultLanguage, isLanguage, type Language } from '@/core/i18n/languages';

type PreferencesState = {
  language: Language;
  setLanguage: (language: Language) => void;
};

const detectLanguage = (): Language => {
  try {
    const code = getLocales()[0]?.languageCode;
    return isLanguage(code) ? code : defaultLanguage;
  } catch {
    return defaultLanguage;
  }
};

const storage = createJSONStorage(() => ({
  getItem: (key) => {
    if (Platform.OS === 'web') {
      try {
        return typeof localStorage !== 'undefined' ? localStorage.getItem(key) : null;
      } catch {
        return null;
      }
    }
    return Storage.getItemSync(key);
  },
  setItem: (key, value) => {
    if (Platform.OS === 'web') {
      try {
        if (typeof localStorage !== 'undefined') localStorage.setItem(key, value);
      } catch {}
      return;
    }
    Storage.setItemSync(key, value);
  },
  removeItem: (key) => {
    if (Platform.OS === 'web') {
      try {
        if (typeof localStorage !== 'undefined') localStorage.removeItem(key);
      } catch {}
      return;
    }
    Storage.removeItemSync(key);
  },
}));

export const usePreferences = create<PreferencesState>()(
  persist(
    (set) => ({
      language: detectLanguage(),
      setLanguage: (language) => set({ language }),
    }),
    {
      name: 'preferences',
      storage,
      partialize: ({ language }) => ({ language }),
    },
  ),
);
