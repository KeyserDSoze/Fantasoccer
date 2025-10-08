// Hook to handle post-login redirect
// Checks for saved return URL after authentication and navigates accordingly
import { useEffect } from 'react'
import { useNavigation } from '@react-navigation/native'
import { useAuth } from '../providers/AuthProvider'
import { returnUrlService } from '../services/returnUrlService'

/**
 * Hook to handle automatic redirect after login
 * Place this in your main app component or navigation container
 * 
 * Example usage:
 * ```tsx
 * function App() {
 *   usePostLoginRedirect()
 *   return <NavigationContainer>...</NavigationContainer>
 * }
 * ```
 */
export const usePostLoginRedirect = () => {
  const { isAuthenticated, isLoading } = useAuth()
  const navigation = useNavigation()

  useEffect(() => {
    // Only check for redirect after authentication is complete
    if (!isAuthenticated || isLoading) return

    const checkAndRedirect = async () => {
      try {
        // Check if there's a saved return URL
        const returnUrl = await returnUrlService.getReturnUrl()
        
        if (!returnUrl) return

        console.log('🔍 Found saved return URL:', returnUrl)

        // Parse the URL to get route info
        const routeInfo = returnUrlService.parseReturnUrl(returnUrl)
        
        if (!routeInfo) {
          console.warn('⚠️ Unable to parse return URL:', returnUrl)
          await returnUrlService.clearReturnUrl()
          return
        }

        console.log('🎯 Navigating to:', routeInfo.routeName, 'with params:', routeInfo.params)

        // Clear the return URL before navigating
        await returnUrlService.clearReturnUrl()

        // Navigate to the saved route
        // Small delay to ensure auth state is fully updated
        setTimeout(() => {
          // Type assertion needed because routeName comes from string parsing
          // but navigation.navigate expects specific route names from RootStackParamList
          navigation.navigate(routeInfo.routeName as any, routeInfo.params)
        }, 300)

      } catch (error) {
        console.error('❌ Post-login redirect failed:', error)
        // Clear the return URL on error to prevent loops
        await returnUrlService.clearReturnUrl()
      }
    }

    checkAndRedirect()
  }, [isAuthenticated, isLoading, navigation])
}
