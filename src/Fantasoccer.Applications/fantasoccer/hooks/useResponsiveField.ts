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
  // Per mobile verticale, usiamo più altezza per sfruttare lo schermo
  let fieldHeight: number
  
  if (isMobile) {
    // Su mobile, aumentiamo significativamente l'altezza per evitare sovrapposizioni tra le linee
    // Usiamo una percentuale maggiore dell'altezza schermo e rimuoviamo il limite di larghezza
    fieldHeight = Math.min(screenHeight * 0.85, screenWidth * 2.2)
  } else {
    // Su desktop, manteniamo l'aspect ratio tradizionale
    const aspectRatio = 1.55 // desktop: più largo che alto
    fieldHeight = fieldWidth / aspectRatio
  }

  // Calcola fattore di scala per le card basato sulla larghezza del campo
  // Riferimento: 1400px = scala 1.0
  const baseWidth = 1400
  let cardScale: number
  
  if (isMobile) {
    // Su mobile, incrementiamo significativamente il fattore di scala per rendere le card più visibili
    // Usiamo un minimo di 1.0x su mobile + boost aggiuntivo
    const baseScale = Math.max(fieldWidth / baseWidth, 1.0)
    cardScale = Math.min(baseScale * 0.6, 2.0) // boost del 60% per mobile, max 2.0x
  } else {
    // Su desktop/tablet, manteniamo la scala normale
    cardScale = Math.min(fieldWidth / baseWidth, 1.2) // max 1.2x, min proporzionale
  }

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