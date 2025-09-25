import { useCallback } from 'react'
import { Platform } from 'react-native'

// Native implementation - integrate with React Navigation when available.
// This file will be picked up by bundlers that prefer .native resolution
// for mobile builds (Metro). For now it provides a minimal API so imports
// don't break in native environments.
export const useAppNavigation = () => {
  const navigate = useCallback((path: string) => {
    // TODO: Replace this with react-navigation integration, e.g.:
    // const navigation = useNavigation(); navigation.navigate(getScreenNameFromPath(path))
    console.log('[native] navigate to', path)
  }, [])

  const canGoBack = useCallback(() => {
    return true
  }, [])

  const goBack = useCallback(() => {
    console.log('[native] go back')
  }, [])

  return { navigate, canGoBack, goBack }
}

export default useAppNavigation
