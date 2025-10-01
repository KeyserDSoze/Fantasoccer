import React from 'react'
import { View, Text } from 'react-native'

interface BonusIconProps {
  size?: number
  color?: string
}

// Icona per Good Defense (scudo) - versione CSS
export const GoodDefenseIcon: React.FC<BonusIconProps> = ({ 
  size = 20, 
  color = '#10b981' 
}) => (
  <View
    style={{
      width: size,
      height: size,
      backgroundColor: color,
      borderRadius: size * 0.2,
      justifyContent: 'center',
      alignItems: 'center',
      position: 'relative',
    }}
  >
    <Text style={{ 
      color: 'white', 
      fontSize: size * 0.6, 
      fontWeight: 'bold',
      textAlign: 'center',
      lineHeight: size * 0.6
    }}>
      🛡️
    </Text>
  </View>
)

// Icona per Good People (gruppo di persone) - versione CSS
export const GoodPeopleIcon: React.FC<BonusIconProps> = ({ 
  size = 20, 
  color = '#3b82f6' 
}) => (
  <View
    style={{
      width: size,
      height: size,
      backgroundColor: color,
      borderRadius: size * 0.2,
      justifyContent: 'center',
      alignItems: 'center',
    }}
  >
    <Text style={{ 
      color: 'white', 
      fontSize: size * 0.6, 
      fontWeight: 'bold',
      textAlign: 'center',
      lineHeight: size * 0.6
    }}>
      👥
    </Text>
  </View>
)

// Icona per No Own Goal (pallone con check) - versione CSS
export const NoOwnGoalIcon: React.FC<BonusIconProps> = ({ 
  size = 20, 
  color = '#f59e0b' 
}) => (
  <View
    style={{
      width: size,
      height: size,
      backgroundColor: color,
      borderRadius: size * 0.2,
      justifyContent: 'center',
      alignItems: 'center',
    }}
  >
    <Text style={{ 
      color: 'white', 
      fontSize: size * 0.6, 
      fontWeight: 'bold',
      textAlign: 'center',
      lineHeight: size * 0.6
    }}>
      ⚽
    </Text>
  </View>
)

// Wrapper component per mostrare tutti i bonus di una squadra
interface TeamBonusesProps {
  hasGoodDefense?: boolean
  hasGoodPeople?: boolean
  hasOwnGoal?: boolean
  iconSize?: number
}

export const TeamBonuses: React.FC<TeamBonusesProps> = ({
  hasGoodDefense,
  hasGoodPeople,
  hasOwnGoal,
  iconSize = 18
}) => (
  <View style={{ flexDirection: 'row', gap: 4, alignItems: 'center' }}>
    {hasGoodDefense && <GoodDefenseIcon size={iconSize} />}
    {hasGoodPeople && <GoodPeopleIcon size={iconSize} />}
    {hasOwnGoal && <NoOwnGoalIcon size={iconSize} />}
  </View>
)