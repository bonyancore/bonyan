import "@/global.css";
import "@/core/i18n";

import { useFonts } from "expo-font";
import { Stack } from "expo-router";
import * as SplashScreen from "expo-splash-screen";
import { StatusBar } from "expo-status-bar";
import { useEffect } from "react";
import { Platform } from "react-native";

import { ensureDirection } from "@/core/i18n/direction";
import { AppProviders } from "@/core/providers/AppProviders";
import { usePreferences } from "@/core/preferences/store";
import { colors } from "@/design/tokens";
import { fontSources } from "@/design/fonts";

if (Platform.OS !== "web") {
  SplashScreen.preventAutoHideAsync().catch(() => {});
}

export default function RootLayout() {
  const [fontsLoaded, fontError] = useFonts(fontSources);
  const language = usePreferences((state) => state.language);

  useEffect(() => {
    ensureDirection(language);
  }, [language]);

  useEffect(() => {
    if (fontsLoaded || fontError) {
      if (Platform.OS !== "web") {
        SplashScreen.hideAsync().catch(() => {});
      }
    }
  }, [fontsLoaded, fontError]);

  if (!fontsLoaded && !fontError && Platform.OS !== "web") return null;

  return (
    <AppProviders>
      <StatusBar style="light" />
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: { backgroundColor: colors.background },
        }}
      />
    </AppProviders>
  );
}
