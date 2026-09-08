import React, { useEffect, useState } from 'react'
import { Image } from 'tamagui'
import { View } from 'react-native'
import { getTeamLogoUrl } from '../../utils/teamLogo'

interface TeamLogoProps {
  groupId: string
  year: number | string
  ownerEmail: string
  size?: number
  fallbackColor?: string
  cacheBuster?: number // Optional timestamp to force cache refresh
}

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
  
  // Use provided cacheBuster or generate one based on current day (updates daily)
  const effectiveCacheBuster = cacheBuster ?? Math.floor(Date.now() / (1000 * 60 * 60 * 24))
  const logoUrl = `${getTeamLogoUrl(groupId, Number(year), ownerEmail)}?t=${effectiveCacheBuster}`

  useEffect(() => {
    setImageError(false)
  }, [logoUrl])
  
  if (imageError) {
    return <FallbackLogo size={size} color={fallbackColor} />
  }
  
  return (
    <Image
      src={logoUrl}
      width={size}
      height={size}
      objectFit="cover"
      alt={`Logo ${ownerEmail}`}
      onError={() => setImageError(true)}
    />
  )
}
