import { storage } from '../utils/storage'
import { Platform } from 'react-native'

// Device-only capability. Never copy it to settings, URLs, worker messages,
// caches, telemetry or notification payloads. Native storage uses SecureStore.
const KEY = 'fantasoccer_push_device_capability'
export async function readDeviceProof(endpoint: string): Promise<string | undefined> {
  try {
    const value = Platform.OS === 'web' ? await storage.getItem(KEY) : await (await import('expo-secure-store')).getItemAsync(KEY)
    const saved = value ? JSON.parse(value) : null
    return saved?.endpoint === endpoint && typeof saved.proof === 'string' ? saved.proof : undefined
  } catch { return undefined }
}
export async function saveDeviceProof(endpoint: string, proof: string) {
  const value = JSON.stringify({ endpoint, proof })
  if (Platform.OS === 'web') await storage.setItem(KEY, value)
  else await (await import('expo-secure-store')).setItemAsync(KEY, value)
}
