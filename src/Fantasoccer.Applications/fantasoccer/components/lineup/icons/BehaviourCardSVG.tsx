import React from 'react'

interface BehaviourCardSVGProps {
  color: string
  size?: number
  [key: string]: any // per altri props SVG
}

export function BehaviourCardSVG({ color, size = 20, ...props }: BehaviourCardSVGProps) {
  return (
    <svg 
      width={size} 
      height={size * 1.4} 
      viewBox="0 0 20 28" 
      fill="none"
      role="img"
      aria-label="Behaviour card icon"
      {...props}
    >
      <rect x="2" y="2" width="16" height="24" rx="2" fill={color} stroke="rgba(0,0,0,0.2)" strokeWidth="1"/>
      <rect x="4" y="4" width="12" height="2" rx="1" fill="rgba(255,255,255,0.8)"/>
      <rect x="4" y="8" width="8" height="1" rx="0.5" fill="rgba(255,255,255,0.6)"/>
      <rect x="4" y="11" width="10" height="1" rx="0.5" fill="rgba(255,255,255,0.6)"/>
    </svg>
  )
}

export default BehaviourCardSVG