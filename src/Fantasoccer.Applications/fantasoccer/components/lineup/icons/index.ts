// Export centralizzato per IconFactory - componente principale
export { IconFactory, IconWithLabel, useIconLabel } from './IconFactory'
export type { IconType, IconFactoryProps, IconWithLabelProps } from './IconFactory'

// Export di tutti i componenti SVG per bonus/malus e comportamenti (legacy/uso diretto)
export { AssistSVG } from './AssistSVG'
export { BehaviourCardSVG } from './BehaviourCardSVG'
export { SoccerBallSVG } from './SoccerBallSVG'

// Export default per comodità
export { default as AssistSVGDefault } from './AssistSVG'
export { default as BehaviourCardSVGDefault } from './BehaviourCardSVG'
export { default as SoccerBallSVGDefault } from './SoccerBallSVG'