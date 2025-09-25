import { useCallback } from 'react'
import { useRouter } from 'next/navigation'

// Web implementation using Next.js App Router's client `useRouter`.
// This must only be imported in web builds (Next will bundle it).
export const useAppNavigation = () => {
  const router = useRouter()

  const navigate = useCallback((path: string) => {
    // useRouter().push performs client-side navigation without a full reload
    router.push(path)
  }, [router])

  const canGoBack = useCallback(() => {
    if (typeof window === 'undefined') return false
    return window.history.length > 1
  }, [])

  const goBack = useCallback(() => {
    if (typeof window !== 'undefined') {
      window.history.back()
    }
  }, [])

  return { navigate, canGoBack, goBack }
}

export default useAppNavigation
