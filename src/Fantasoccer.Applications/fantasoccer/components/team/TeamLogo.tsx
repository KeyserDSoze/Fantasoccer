import React, { useState } from 'react'
import { Image } from 'tamagui'
import { View } from 'react-native'

interface TeamLogoProps {
  groupId: string
  year: number | string
  ownerEmail: string
  size?: number
  fallbackColor?: string
  cacheBuster?: number // Optional timestamp to force cache refresh
}

const BASE_URL = 'https://fasostorage.blob.core.windows.net/logos'

// SVG fallback logo
const FallbackLogo: React.FC<{ size: number; color: string }> = ({ size, color }) => (
  <View
    style={{
      width: size,
      height: size,
      backgroundColor: color,
      borderRadius: size / 2,
      justifyContent: 'center',
      alignItems: 'center',
      borderWidth: 2,
      borderColor: '#ffffff',
    }}
  >
    <View
      style={{
        width: size * 0.6,
        height: size * 0.6,
        backgroundColor: '#ffffff',
        borderRadius: (size * 0.6) / 2,
        justifyContent: 'center',
        alignItems: 'center',
      }}
    >
      <View
        style={{
          width: size * 0.3,
          height: size * 0.3,
          backgroundColor: color,
          borderRadius: (size * 0.3) / 2,
        }}
      />
    </View>
  </View>
)

export const TeamLogo: React.FC<TeamLogoProps> = ({
  groupId,
  year,
  ownerEmail,
  size = 40,
  fallbackColor = '#2563eb',
  cacheBuster
}) => {
  const [imageError, setImageError] = useState(false)
  
  const logoUrl = cacheBuster 
    ? `${BASE_URL}/${groupId}/${year}/${ownerEmail}.png?t=${cacheBuster}`
    : `${BASE_URL}/${groupId}/${year}/${ownerEmail}.png`
  
  if (imageError) {
    return <FallbackLogo size={size} color={fallbackColor} />
  }
  
  return (
    <Image
      source={{ uri: logoUrl }}
      width={size}
      height={size}
      onError={() => setImageError(true)}
    />
  )
}