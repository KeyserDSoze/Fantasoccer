import React from 'react'
import { View, Text } from 'react-native'
import { SoccerBallSVG } from '../lineup/icons/SoccerBallSVG'

interface BonusIconProps {
  size?: number
  color?: string
}

// Icona per Good Defense (muro arancione tipo firewall)
export const GoodDefenseIcon: React.FC<BonusIconProps> = ({ 
  size = 24, 
  color = '#ea580c' 
}) => (
  <Text style={{ 
    fontSize: size, 
    color: color,
    textAlign: 'center',
    lineHeight: size
  }}>
    🧱
  </Text>
)

// Icona per Good People (stella)
export const GoodPeopleIcon: React.FC<BonusIconProps> = ({ 
  size = 24, 
  color = '#eab308' 
}) => (
  <Text style={{ 
    fontSize: size, 
    color: color,
    textAlign: 'center',
    lineHeight: size
  }}>
    ⭐
  </Text>
)

// Icona per Own Goal (pallone rosso e bianco)
export const OwnGoalIcon: React.FC<BonusIconProps> = ({ 
  size = 24
}) => (
  <SoccerBallSVG 
    size={size}
    primaryColor="#ffffff"
    secondaryColor="#dc2626"
  />
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
  iconSize = 24
}) => (
  <View style={{ flexDirection: 'row', gap: 6, alignItems: 'center' }}>
    {hasGoodDefense && <GoodDefenseIcon size={iconSize} />}
    {hasGoodPeople && <GoodPeopleIcon size={iconSize} />}
    {hasOwnGoal && <OwnGoalIcon size={iconSize} />}
  </View>
)