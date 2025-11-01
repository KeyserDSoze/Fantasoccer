import { useState, useEffect } from 'react'
import { Platform } from 'react-native'

const ZOOM_STORAGE_KEY = 'field-zoom-level'
const MIN_ZOOM = 0.1 // 10%
const MAX_ZOOM = 1.0 // 100%
const DEFAULT_MOBILE_ZOOM = 0.55 // 55%
const DEFAULT_DESKTOP_ZOOM = 1.0 // 100%
const ZOOM_STEP = 0.05 // 5% per click

interface UseFieldZoomProps {
  isMobile: boolean
}

interface UseFieldZoomReturn {
  zoomLevel: number
  increaseZoom: () => void
  decreaseZoom: () => void
  resetZoom: () => void
  minZoom: number
  maxZoom: number
}

export const useFieldZoom = ({ isMobile }: UseFieldZoomProps): UseFieldZoomReturn => {
  const defaultZoom = isMobile ? DEFAULT_MOBILE_ZOOM : DEFAULT_DESKTOP_ZOOM

  const [zoomLevel, setZoomLevel] = useState<number>(() => {
    // Carica zoom da localStorage se disponibile
    if (Platform.OS === 'web') {
      try {
        const saved = localStorage.getItem(ZOOM_STORAGE_KEY)
        if (saved) {
          const parsed = parseFloat(saved)
          if (!isNaN(parsed) && parsed >= MIN_ZOOM && parsed <= MAX_ZOOM) {
            return parsed
          }
        }
      } catch (error) {
        console.warn('Failed to load zoom level from localStorage:', error)
      }
    }
    return defaultZoom
  })

  // Salva zoom in localStorage quando cambia
  useEffect(() => {
    if (Platform.OS === 'web') {
      try {
        localStorage.setItem(ZOOM_STORAGE_KEY, zoomLevel.toString())
      } catch (error) {
        console.warn('Failed to save zoom level to localStorage:', error)
      }
    }
  }, [zoomLevel])

  const increaseZoom = () => {
    setZoomLevel(prev => Math.min(prev + ZOOM_STEP, MAX_ZOOM))
  }

  const decreaseZoom = () => {
    setZoomLevel(prev => Math.max(prev - ZOOM_STEP, MIN_ZOOM))
  }

  const resetZoom = () => {
    setZoomLevel(defaultZoom)
  }

  return {
    zoomLevel,
    increaseZoom,
    decreaseZoom,
    resetZoom,
    minZoom: MIN_ZOOM,
    maxZoom: MAX_ZOOM
  }
}
