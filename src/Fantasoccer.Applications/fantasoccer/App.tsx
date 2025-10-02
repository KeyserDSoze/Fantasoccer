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
import { ToastProvider, ToastViewport, Toast, useToastState } from '@tamagui/toast';
import { YStack, Text } from 'tamagui';

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
    <ToastProvider>
      <LoadingProvider>
        <AuthProvider>
          <AppProvider>
            <Navigation
              theme={navigationTheme}
              onReady={() => SplashScreen.hideAsync()}
            />
            {/* Toast Viewport e Custom Toast */}
            <ToastViewport />
            <CurrentToast />
          </AppProvider>
        </AuthProvider>
      </LoadingProvider>
    </ToastProvider>
  );
};

// Componente Toast personalizzato
const CurrentToast = () => {
  const currentToast = useToastState()

  if (!currentToast || currentToast.isHandledNatively) return null

  return (
    <Toast
      key={currentToast.id}
      duration={currentToast.duration}
      enterStyle={{ opacity: 0, transform: [{ translateY: 100 }] }}
      exitStyle={{ opacity: 0, transform: [{ translateY: 100 }] }}
      transform={[{ translateY: 0 }]}
      opacity={1}
      scale={1}
      viewportName={currentToast.viewportName}
      backgroundColor="rgba(0, 0, 0, 0.9)"
      borderRadius={12}
      padding={16}
      margin={16}
      borderWidth={1}
      borderColor="rgba(255, 255, 255, 0.2)"
      elevation={10}
    >
      <YStack gap={8}>
        <Toast.Title fontSize={16} fontWeight="bold" color="#fff">
          {currentToast.title}
        </Toast.Title>
        {!!currentToast.message && (
          <Toast.Description fontSize={14} color="rgba(255, 255, 255, 0.9)" lineHeight={20}>
            {currentToast.message}
          </Toast.Description>
        )}
      </YStack>
    </Toast>
  )
}
