import { DefaultTheme, DarkTheme } from '@react-navigation/native';
import React, { useMemo } from 'react';

import 'react-native-gesture-handler';

import * as SplashScreen from 'expo-splash-screen';
import { useFonts } from 'expo-font';

import Navigation from './navigation';
import { LoadingProvider } from 'providers/LoadingProvider';
import { AuthProvider } from 'providers/AuthProvider';
import { AppProvider } from 'providers/AppProvider';
import { ThemeProvider, useAppTheme } from 'providers/ThemeProvider';
import { setupRepositoryServices } from 'configurations/repositoryConfig';

SplashScreen.preventAutoHideAsync();

setupRepositoryServices();

export default function App() {
  const [loaded] = useFonts({
    Inter: require('@tamagui/font-inter/otf/Inter-Medium.otf'),
    InterBold: require('@tamagui/font-inter/otf/Inter-Bold.otf'),
  });

  if (!loaded) {
    return null;
  }

  return (
    <ThemeProvider>
      <AppContent />
    </ThemeProvider>
  );
}

const AppContent = () => {
  const { theme } = useAppTheme();

  const navigationTheme = useMemo(
    () => (theme === 'dark' ? DarkTheme : DefaultTheme),
    [theme]
  );

  return (
    <LoadingProvider>
      <AuthProvider>
        <AppProvider>
          <Navigation
            theme={navigationTheme}
            onReady={() => SplashScreen.hideAsync()}
          />
        </AppProvider>
      </AuthProvider>
    </LoadingProvider>
  );
};
