import React from 'react'
import { SoccerBallSVG } from './SoccerBallSVG'
import { AssistSVG } from './AssistSVG'
import { BehaviourCardSVG } from './BehaviourCardSVG'

export type IconType =
  | 'goal'
  | 'penalty'
  | 'assist'
  | 'stoppedPenalty'
  | 'wrongedPenalty'
  | 'ownGoal'
  | 'sufferedGoal'
  | 'yellowCard'
  | 'redCard'
  | 'injury'
  | 'manOfTheMatch'

export interface IconFactoryProps {
  type: IconType
  size?: number
}

/**
 * IconFactory - Componente centralizzato per gestire tutte le icone del fantacalcio
 * 
 * Uso:
 * ```tsx
 * <IconFactory type="goal" size={16} />
 * <IconFactory type="assist" />
 * ```
 */
export const IconFactory: React.FC<IconFactoryProps> = ({ type, size = 16 }) => {
  switch (type) {
    case 'goal':
      // Gol normale - pallone bianco con dettagli neri
      return <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#000000" size={size} />

    case 'penalty':
      // Rigore segnato - pallone nero con dettagli bianchi
      return <SoccerBallSVG primaryColor="#000000" secondaryColor="#ffffff" size={size} />

    case 'assist':
      // Assist - emoji bersaglio
      return <span style={{ fontSize: size }}>🎯</span>

    case 'stoppedPenalty':
      // Rigore parato - emoji guantone
      return <span style={{ fontSize: size }}>🧤</span>

    case 'wrongedPenalty':
      // Rigore sbagliato - emoji X
      return <span style={{ fontSize: size }}>❌</span>

    case 'ownGoal':
      // Autogol - emoji pallone rosso
      return <span style={{ fontSize: size }}>🔴</span>

    case 'sufferedGoal':
      // Gol subito - pallone rosso con dettagli bianchi
      return <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" size={size} />

    case 'yellowCard':
      // Cartellino giallo - emoji quadrato giallo
      return <span style={{ fontSize: size }}>🟨</span>

    case 'redCard':
      // Cartellino rosso - emoji quadrato rosso
      return <span style={{ fontSize: size }}>🟥</span>

    case 'injury':
      // Infortunio - emoji cerotto
      return <span style={{ fontSize: size }}>🩹</span>

    case 'manOfTheMatch':
      // Migliore in campo - emoji stella
      return <span style={{ fontSize: size }}>⭐</span>

    default:
      return null
  }
}

/**
 * Hook helper per ottenere il label testuale associato a ogni tipo di icona
 */
export const useIconLabel = (type: IconType): string => {
  const labels: Record<IconType, string> = {
    goal: 'Gol',
    penalty: 'Rigore segnato',
    assist: 'Assist',
    stoppedPenalty: 'Rigore parato',
    wrongedPenalty: 'Rigore sbagliato',
    ownGoal: 'Autogol',
    sufferedGoal: 'Gol subito',
    yellowCard: 'Ammonizione',
    redCard: 'Espulsione',
    injury: 'Infortunio',
    manOfTheMatch: 'Migliore in campo',
  }
  return labels[type]
}

/**
 * Componente helper per mostrare icona + label in un layout comune
 */
export interface IconWithLabelProps {
  type: IconType
  size?: number
  customLabel?: string
}

export const IconWithLabel: React.FC<IconWithLabelProps> = ({ type, size = 16, customLabel }) => {
  const defaultLabel = useIconLabel(type)
  const label = customLabel ?? defaultLabel

  return (
    <>
      <IconFactory type={type} size={size} />
      <span>{label}</span>
    </>
  )
}
