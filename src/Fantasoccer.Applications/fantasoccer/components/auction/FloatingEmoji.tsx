import { useEffect, useState } from 'react'
import { YStack, Text } from 'tamagui'

interface FloatingEmojiProps {
  id: string
  emoji: string
  sender: string
  onComplete: () => void
}

export function FloatingEmoji({ id, emoji, sender, onComplete }: FloatingEmojiProps) {
  const [offsetX] = useState(Math.random() * 600 - 300) // Range: -300px a +300px dal centro
  
  useEffect(() => {
    // Complete animation after 3 seconds
    const timer = setTimeout(() => {
      onComplete()
    }, 3000)
    
    return () => clearTimeout(timer)
  }, [onComplete])

  return (
    <YStack
      key={id}
      position="fixed"
      left="50%"
      bottom={0}
      zIndex={10000}
      pointerEvents="none"
      className="emoji-float"
      style={{
        ['--offset-x' as any]: `${offsetX}px`,
      }}
    >
      <YStack alignItems="center" gap="$1">
        <Text fontSize={64} lineHeight={64}>{emoji}</Text>
        <Text 
          fontSize="$4" 
          fontWeight="700"
          style={{
            background: 'linear-gradient(90deg, #ff0000, #ff7f00, #ffff00, #00ff00, #0000ff, #4b0082, #9400d3)',
            backgroundSize: '200% 100%',
            WebkitBackgroundClip: 'text',
            WebkitTextFillColor: 'transparent',
            backgroundClip: 'text',
            animation: 'rainbow 2s linear infinite',
            filter: 'drop-shadow(0 0 8px rgba(255,255,255,0.9))',
          }}
        >
          {sender}
        </Text>
      </YStack>
      <style>
        {`
          .emoji-float {
            animation: floatUp 3s ease-out forwards;
            transform: translateX(var(--offset-x));
          }
          
          @keyframes floatUp {
            0% {
              transform: translateY(0) translateX(var(--offset-x));
              opacity: 1;
            }
            80% {
              opacity: 1;
            }
            100% {
              transform: translateY(-80vh) translateX(var(--offset-x));
              opacity: 0;
            }
          }
          
          @keyframes rainbow {
            0% {
              background-position: 0% 50%;
            }
            100% {
              background-position: 200% 50%;
            }
          }
        `}
      </style>
    </YStack>
  )
}
