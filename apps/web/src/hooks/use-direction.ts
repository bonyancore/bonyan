import { useLocale } from "next-intl";
import { getDirection, type Locale } from "@/i18n/locales";

export const useDirection = () => getDirection(useLocale() as Locale);
