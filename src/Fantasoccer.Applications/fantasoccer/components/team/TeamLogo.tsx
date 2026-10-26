import React, { useEffect, useState } from 'react'
import { Image } from 'tamagui'
import { View } from 'react-native'
import { useApp } from '../../providers/AppProvider'

interface TeamLogoProps {
  groupId: string
  year: number | string
  ownerEmail: string
  size?: number
  fallbackColor?: string
  cacheBuster?: number // Optional timestamp to force cache refresh
  logoUrl?: string | null
  teamId?: string
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
  cacheBuster,
  logoUrl: explicitUrl,
  teamId
}) => {
  const [imageError, setImageError] = useState(false)
  const { userGroups } = useApp()
  const metadata = userGroups.find(group => group.id === groupId)?.baskets.flatMap(basket => basket.years)
    .filter(season => season.year === Number(year)).flatMap(season => season.teams)
    .find(team => teamId ? team.teamId === teamId : team.owner.toLowerCase() === ownerEmail.toLowerCase())
  
  // Use provided cacheBuster or generate one based on current day (updates daily)
  const effectiveCacheBuster = cacheBuster ?? Math.floor(Date.now() / (1000 * 60 * 60 * 24))
  const source = explicitUrl ?? metadata?.logoUrl
  const logoUrl = source ? `${source}${source.includes('?') ? '&' : '?'}t=${effectiveCacheBuster}` : null

  useEffect(() => {
    setImageError(false)
  }, [logoUrl])
  
  if (imageError || !logoUrl) {
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
