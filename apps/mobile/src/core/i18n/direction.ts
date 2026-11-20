import { reloadAppAsync } from 'expo';
import { I18nManager, Platform } from 'react-native';

import { languages, type Language } from './languages';

export const isRTL = () => {
  if (Platform.OS === 'web' && typeof document !== 'undefined') {
    return document.documentElement.dir === 'rtl' || I18nManager.isRTL;
  }
  return I18nManager.isRTL;
};

export const applyDirection = (language: Language) => {
  const rtl = languages[language].direction === 'rtl';

  if (Platform.OS === 'web') {
    if (typeof document !== 'undefined') {
      document.documentElement.dir = rtl ? 'rtl' : 'ltr';
    }
    I18nManager.allowRTL(rtl);
    I18nManager.forceRTL(rtl);
    return false;
  }

  if (I18nManager.isRTL === rtl) return false;
  I18nManager.allowRTL(rtl);
  I18nManager.forceRTL(rtl);
  return true;
};

export const ensureDirection = async (language: Language) => {
  if (Platform.OS === 'web') {
    applyDirection(language);
    return;
  }
  if (applyDirection(language)) {
    await reloadAppAsync();
  }
};
