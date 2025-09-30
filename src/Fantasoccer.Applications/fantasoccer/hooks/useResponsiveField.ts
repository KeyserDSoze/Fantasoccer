import { useState, useEffect } from 'react'
import { Dimensions } from 'react-native'

interface FieldDimensions {
  width: number
  height: number
  isMobile: boolean
  isTablet: boolean
  isDesktop: boolean
  orientation: 'horizontal' | 'vertical'
  fieldWidthPercent: number
  cardScale: number // Nuovo: fattore di scala per le card
}

// Breakpoints
const MOBILE_BREAKPOINT = 768
const TABLET_BREAKPOINT = 1024

export const useResponsiveField = (): FieldDimensions => {
  const [dimensions, setDimensions] = useState(() => {
    const screen = Dimensions.get('window')
    return calculateFieldDimensions(screen.width, screen.height)
  })

  useEffect(() => {
    const subscription = Dimensions.addEventListener('change', ({ window }) => {
      setDimensions(calculateFieldDimensions(window.width, window.height))
    })

    return () => subscription?.remove()
  }, [])

  return dimensions
}

function calculateFieldDimensions(screenWidth: number, screenHeight: number): FieldDimensions {
  const isMobile = screenWidth < MOBILE_BREAKPOINT
  const isTablet = screenWidth >= MOBILE_BREAKPOINT && screenWidth < TABLET_BREAKPOINT
  const isDesktop = screenWidth >= TABLET_BREAKPOINT

  // Campo sempre al 100% della larghezza
  const fieldWidthPercent = 100
  const fieldWidth = screenWidth
  
  // Aspect ratio per campo da calcio: circa 1.55:1 (larghezza:altezza)
  // Per mobile verticale, invertiamo il rapporto
  const aspectRatio = isMobile ? 0.65 : 1.55 // mobile: più alto che largo
  const fieldHeight = fieldWidth / aspectRatio

  // Calcola fattore di scala per le card basato sulla larghezza del campo
  // Riferimento: 1400px = scala 1.0
  const baseWidth = 1400
  const cardScale = Math.min(fieldWidth / baseWidth, 1.2) // max 1.2x, min proporzionale

  // Determina orientamento
  const orientation = isMobile ? 'vertical' : 'horizontal'

  return {
    width: fieldWidth,
    height: fieldHeight,
    isMobile,
    isTablet,
    isDesktop,
    orientation,
    fieldWidthPercent,
    cardScale
  }
}

// Hook aggiuntivo per ottenere breakpoints CSS-like
export const useBreakpoints = () => {
  const { isMobile, isTablet, isDesktop } = useResponsiveField()
  
  return {
    isMobile,
    isTablet,
    isDesktop,
    isSmallScreen: isMobile,
    isMediumScreen: isTablet,
    isLargeScreen: isDesktop
  }
}