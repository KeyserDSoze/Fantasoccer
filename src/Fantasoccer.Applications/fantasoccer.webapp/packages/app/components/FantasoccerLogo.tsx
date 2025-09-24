import React from 'react'

interface FantasoccerLogoProps {
  width?: number | string
  height?: number | string
  color?: string
  className?: string
}

export function FantasoccerLogo({ 
  width = 220, 
  height = 220, 
  color = 'currentColor',
  className 
}: FantasoccerLogoProps) {
  return (
    <svg 
      xmlns="http://www.w3.org/2000/svg" 
      width={width} 
      height={height} 
      viewBox="0 0 200 200" 
      role="img" 
      aria-labelledby="tA dA" 
      style={{ color }}
      className={className}
    >
      <title id="tA">Logo Fantacalcio - Scudetto minimal</title>
      <desc id="dA">Scudetto stilizzato con stella e palla pentagonale minimal</desc>
      <g fill="none" stroke="currentColor" strokeWidth="8" strokeLinecap="round" strokeLinejoin="round">
        {/* stella */}
        <path d="M100 22 l7.7 15.6 17.2 2.5 -12.5 12.2 3 17 -15.4 -8.1 -15.4 8.1 3 -17 -12.5 -12.2 17.2 -2.5z"/>
        {/* scudetto */}
        <path d="M100 14 L170 54 V112c0 33-28 60-70 78-42-18-70-45-70-78V54z"/>
        {/* palla (pentagono) */}
        <g transform="translate(100,115)">
          <circle r="34" stroke="currentColor" />
          <path d="M0,-14 13,-4 8,12 -8,12 -13,-4 Z"/>
        </g>
        {/* arco dinamico */}
        <path d="M60 150a70 70 0 0 0 80 0"/>
      </g>
    </svg>
  )
}

// Variante compatta per header
export function FantasoccerLogoCompact({ 
  width = 40, 
  height = 40, 
  color = 'currentColor',
  className 
}: FantasoccerLogoProps) {
  return (
    <FantasoccerLogo 
      width={width} 
      height={height} 
      color={color} 
      className={className}
    />
  )
}

// Brand completo con logo e testo
interface FantasoccerBrandProps extends FantasoccerLogoProps {
  showText?: boolean
  textSize?: 'small' | 'medium' | 'large'
  orientation?: 'horizontal' | 'vertical'
}

export function FantasoccerBrand({ 
  width = 60, 
  height = 60, 
  color = 'currentColor',
  showText = true,
  textSize = 'medium',
  orientation = 'horizontal',
  className 
}: FantasoccerBrandProps) {
  const textSizes = {
    small: '$4',
    medium: '$6',
    large: '$8'
  } as const

  if (!showText) {
    return <FantasoccerLogo width={width} height={height} color={color} className={className} />
  }

  // Questo import deve essere fatto dinamicamente per evitare problemi di circular dependency
  const Stack = orientation === 'horizontal' ? 'div' : 'div'
  
  return (
    <div 
      style={{
        display: 'flex',
        flexDirection: orientation === 'horizontal' ? 'row' : 'column',
        alignItems: 'center',
        gap: '12px'
      }}
      className={className}
    >
      <FantasoccerLogo width={width} height={height} color={color} />
      <div 
        style={{
          fontWeight: '800',
          fontSize: textSize === 'small' ? '18px' : textSize === 'medium' ? '24px' : '32px',
          color: typeof color === 'string' ? color : '#0066cc'
        }}
      >
        Fantasoccer
      </div>
    </div>
  )
}