import { useEffect } from 'react'
import { Platform } from 'react-native'
import { useAuth } from '../providers/AuthProvider'
import { reconcilePushDevice } from '../services/pushDeviceService'
import { subscribeToPushSubscriptionChanges } from '../services/webPushClient'

export function PushDeviceSync() {
  const { user, isAuthenticated } = useAuth()
  const email = isAuthenticated ? user?.email : undefined
  useEffect(() => {
    if (!email || Platform.OS !== 'web') return
    let disposed = false
    let running = false
    const sync = async () => {
      if (disposed || running) return
      running = true
      try { await reconcilePushDevice(email, () => !disposed) }
      catch (error) { if (!disposed) console.warn('Sincronizzazione push rimandata:', error) }
      finally { running = false }
    }
    const onVisible = () => { if (document.visibilityState === 'visible') void sync() }
    const unsubscribe = subscribeToPushSubscriptionChanges(() => { void sync() })
    const interval = setInterval(() => { void sync() }, 60000)
    window.addEventListener('online', sync)
    document.addEventListener('visibilitychange', onVisible)
    void sync()
    return () => {
      disposed = true
      clearInterval(interval)
      unsubscribe()
      window.removeEventListener('online', sync)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [email])
  return null
}
