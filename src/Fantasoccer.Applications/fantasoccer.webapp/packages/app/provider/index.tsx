import { useColorScheme } from 'react-native'
import {
  CustomToast,
  TamaguiProvider,
  type TamaguiProviderProps,
  ToastProvider,
  config,
  isWeb,
} from '@my/ui'
import { ToastViewport } from './ToastViewport'
import { AuthProvider } from '../auth'
import { LoadingProvider } from './LoadingProvider'
import { AppProvider } from './AppProvider'
import { AppInitializer } from '../components/AppInitializer'
import { MobileEnhancements } from '../components/MobileEnhancements'
import { setupRepositoryServices } from '../bootstrap/repositoryConfig'

// Initialize repository services
setupRepositoryServices()

export function Provider({
  children,
  defaultTheme = 'dark',
  ...rest
}: Omit<TamaguiProviderProps, 'config'> & { defaultTheme?: string }) {
  const colorScheme = useColorScheme()
  const theme = defaultTheme || (colorScheme === 'dark' ? 'dark' : 'light')

  return (
    <TamaguiProvider config={config} defaultTheme={theme} {...rest}>
      <ToastProvider swipeDirection="horizontal" duration={6000} native={isWeb ? [] : ['mobile']}>
        <LoadingProvider>
          <AuthProvider>
            <AppProvider>
              <AppInitializer>
                {children}
              </AppInitializer>
              <CustomToast />
              <ToastViewport />
              <MobileEnhancements />
            </AppProvider>
          </AuthProvider>
        </LoadingProvider>
      </ToastProvider>
    </TamaguiProvider>
  )
}
