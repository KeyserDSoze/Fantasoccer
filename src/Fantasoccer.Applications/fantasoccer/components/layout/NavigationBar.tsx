'use client'

import React, { useState } from 'react'
import { Platform } from 'react-native'
import {
  Button,
  Sheet,
  XStack,
  YStack,
  Text,
  Separator,
  ScrollView,
  useMedia,
} from 'tamagui'
import {
  Menu,
  X,
  Home,
  Trophy,
  Calendar,
  Zap,
  Users,
  UserCheck,
  DollarSign,
  Star,
  Settings,
  Shield,
  ShieldCheck,
  User,
  TrendingUp,
  PlayCircle,
  Gavel,
} from '@tamagui/lucide-icons'
import { useAuth } from 'providers/AuthProvider'
import { useApp } from 'providers/AppProvider'
import { hasRole, IdentityRole } from '../../types/identity'
import { SwitchThemeButton } from './SwitchThemeButton'
import { useNavigation } from '@react-navigation/native'
import type { RootNavigatorParamList } from 'navigation'

interface NavigationBarProps {
  onNavigate?: (path: string) => void
}

type NavigationRouteName = keyof RootNavigatorParamList

type IconType = typeof Home

type NavigationBaseItem = {
  label: string
  icon: IconType
  description: string
}

type NavigationRouteItem = NavigationBaseItem & {
  type: 'route'
  route: NavigationRouteName
}

type NavigationLinkItem = NavigationBaseItem & {
  type: 'link'
  path: string
}

type NavigationItem = NavigationRouteItem | NavigationLinkItem

type NavigationSection = {
  section: string
  items: NavigationItem[]
}

// Helper function to convert year number to season string
const getSeasonFromYear = (yearNumber: number): string => {
  // Year 14 = 2025/26, Year 13 = 2024/25, etc.
  // Base calculation: 2025 - 14 = 2011, so 2011 + yearNumber = startYear
  const baseYear = 2011
  const startYear = baseYear + yearNumber
  const endYear = startYear + 1
  
  // return `${startYear}/${String(endYear).slice(-2)} (${yearNumber})`
  return `${startYear}/${String(endYear).slice(-2)}`
}

// Navigation items structure based on prompt_frontendapp routing
const navigationItems: NavigationSection[] = [
  {
    section: 'Main',
    items: [
      { type: 'route', route: 'Home', label: 'Home', icon: Home, description: 'Serie A matches of current round' },
      { type: 'route', route: 'Ranking', label: 'Classifica', icon: Trophy, description: 'Classifica della tua lega' },
      { type: 'route', route: 'Calendar', label: 'Calendario', icon: Calendar, description: 'Calendario della lega' },
      { type: 'link', path: '/live', label: 'Live', icon: Zap, description: 'Calendario e risultati in tempo reale' },
    ],
  },
  {
    section: 'Lega',
    items: [
      { type: 'link', path: '/my-team', label: 'My Team', icon: Users, description: 'Gestisci la formazione per il turno attuale' },
      { type: 'link', path: '/teams', label: 'Teams', icon: Users, description: 'Tutte le squadre della tua lega' },
    ],
  },
  {
    section: 'Giocatori',
    items: [
      { type: 'link', path: '/players', label: 'Tutti i giocatori', icon: UserCheck, description: 'Elenco dei giocatori di Serie A' },
      { type: 'link', path: '/free-players', label: 'Giocatori liberi', icon: User, description: 'Giocatori disponibili nella tua lega' },
    ],
  },
  {
    section: 'Mercato',
    items: [
      { type: 'link', path: '/market', label: 'Mercato', icon: DollarSign, description: 'Inizia un nuovo scambio' },
      { type: 'link', path: '/market/trades', label: 'Scambi Attivi', icon: TrendingUp, description: 'Offerte di scambio attive' },
      { type: 'link', path: '/market/trades/history', label: 'Storico Scambi', icon: Calendar, description: 'Scambi passati' },
    ],
  },
  {
    section: 'Serie A',
    items: [
      { type: 'link', path: '/seriea/standings', label: 'Classifica Serie A', icon: Trophy, description: 'Classifica ufficiale della Serie A' },
      { type: 'link', path: '/seriea/calendar', label: 'Calendario Serie A', icon: Calendar, description: 'Calendario ufficiale della Serie A' },
    ],
  },
  {
    section: 'Other',
    items: [
      { type: 'link', path: '/hall-of-fame', label: 'Hall of Fame', icon: Star, description: 'Vincitori e stagioni passate' },
      { type: 'link', path: '/auction', label: 'Auction', icon: Gavel, description: 'Asta live' },
      { type: 'route', route: 'Settings', label: 'Settings', icon: Settings, description: 'Impostazioni utente' },
    ],
  },
]

// Admin items (shown only to admins)
const adminItems: NavigationSection[] = [
  {
    section: 'Admin',
    items: [
      { type: 'link', path: '/admin', label: 'Admin Dashboard', icon: Shield, description: 'Admin dashboard' },
      { type: 'link', path: '/admin/groups', label: 'Manage Groups', icon: Users, description: 'Manage groups and leagues' },
      { type: 'link', path: '/admin/auction', label: 'Auction Control', icon: Gavel, description: 'Auction control panel' },
    ],
  },
]

// Super Admin items (shown only to super admins)
const superAdminItems: NavigationSection[] = [
  {
    section: 'Super Admin',
    items: [
      { type: 'link', path: '/superadmin', label: 'Super Admin', icon: ShieldCheck, description: 'Super admin dashboard' },
      { type: 'link', path: '/superadmin/users', label: 'Manage Users', icon: Users, description: 'User management' },
      { type: 'link', path: '/superadmin/logs', label: 'Logs', icon: PlayCircle, description: 'System logs' },
      { type: 'link', path: '/superadmin/errors', label: 'Error Reports', icon: X, description: 'Error tracking' },
    ],
  },
]

export const NavigationBar: React.FC<NavigationBarProps> = ({ onNavigate }) => {
  const [isOpen, setIsOpen] = useState(false)
  const [showGroupSelector, setShowGroupSelector] = useState(false)
  const { user, logout } = useAuth()
  const { 
    currentGroup, 
    userGroups,
    selectedLeagueId,
    selectedYear,
    setCurrentGroup,
    setSelectedLeague,
    setSelectedYear,
    getAvailableLeagues,
    getAvailableYears
  } = useApp()
  const media = useMedia()
  const isDesktop = !media.sm
  const navigation = useNavigation()
  // Check user roles for admin sections - find current user's role in current group
  const currentUserInGroup = currentGroup?.users.find(u => u.username === user?.username)
  const isAdmin = currentUserInGroup && hasRole(currentUserInGroup.role, IdentityRole.Admin)
  // For now, super admin logic would need backend implementation
  const isSuperAdmin = currentUserInGroup && hasRole(currentUserInGroup.role, IdentityRole.SuperAdmin)

  // Responsive sheet settings
  const snapPointsMode = isDesktop ? 'fit' : 'percent'
  const sheetProps = {
    modal: true,
    open: isOpen,
    onOpenChange: setIsOpen,
    snapPointsMode: snapPointsMode as 'fit' | 'percent',
    snapPoints: isDesktop ? [800] : [90],
    moveOnKeyboardChange: false,
    dismissOnSnapToBottom: !isDesktop,
    disableDrag: isDesktop,
  }

  // Handle navigation
  const handleNavigate = (item: NavigationItem) => {
    if (item.type === 'route') {
      ;(navigation as { navigate: (routeName: NavigationRouteName) => void }).navigate(item.route)
    } else if (onNavigate) {
      onNavigate(item.path)
    } else {
      console.warn(`NavigationBar: no navigation handler provided for path "${item.path}"`)
    }
    setIsOpen(false)
  }

  // Render navigation section
  const renderSection = (section: NavigationSection) => (
    <YStack key={section.section} gap="$2" marginBottom="$4">
      <Text fontSize="$6" fontWeight="bold" color="$color12" marginBottom="$2">
        {section.section}
      </Text>
      {section.items.map((item) => {
        const IconComponent = item.icon
        return (
          <Button
            key={item.type === 'route' ? item.route : item.path}
            variant="outlined"
            justifyContent="flex-start"
            paddingHorizontal="$4"
            paddingVertical="$3"
            onPress={() => handleNavigate(item)}
            borderRadius="$4"
            backgroundColor="transparent"
            hoverStyle={{ backgroundColor: '$color3' }}
            pressStyle={{ backgroundColor: '$color4' }}
          >
            <XStack gap="$3" alignItems="center" flex={1}>
              <IconComponent size="$1" color="$color11" />
              <YStack flex={1} alignItems="flex-start">
                <Text fontSize="$4" fontWeight="500" color="$color12">
                  {item.label}
                </Text>
                <Text fontSize="$2" color="$color10" opacity={0.8}>
                  {item.description}
                </Text>
              </YStack>
            </XStack>
          </Button>
        )
      })}
    </YStack>
  )

  return (
    <>
      {/* Hamburger Menu Button */}
      <Button
        size="$4"
        circular
        backgroundColor="transparent"
        borderColor="transparent"
        onPress={() => setIsOpen(true)}
        hoverStyle={{ backgroundColor: '$color3' }}
        pressStyle={{ backgroundColor: '$color4' }}
      >
        <Menu size="$1.5" color="$color12" />
      </Button>

      {/* Navigation Sheet */}
      <Sheet {...sheetProps}>
        <Sheet.Overlay 
          backgroundColor="rgba(0, 0, 0, 0.5)" 
          animation="lazy" 
          enterStyle={{ opacity: 0 }}
          exitStyle={{ opacity: 0 }}
        />
        
        <Sheet.Handle backgroundColor="$color8" />
        
        <Sheet.Frame
          backgroundColor="$background"
          borderTopLeftRadius={isDesktop ? 0 : '$6'}
          borderTopRightRadius={isDesktop ? 0 : '$6'}
          padding="$0"
          width="100%"
          alignSelf="center"
          height={isDesktop ? '100vh' : '90vh'}
          overflow="hidden"
        >
          <ScrollView 
            showsVerticalScrollIndicator={false}
            bounces={true}
            alwaysBounceVertical={false}
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={{ 
              flexGrow: 1,
              paddingBottom: 50 // Extra space at bottom for safe scrolling
            }}
            style={{ flex: 1 }}
          >
            <YStack padding="$4" gap="$4">
              {/* Header */}
              <XStack justifyContent="space-between" alignItems="center" marginBottom="$4">
                 {user && (<Text fontSize="$8" fontWeight="bold" color="$blue10">
                  Ciao {user.username}
                </Text>)}
                <Button
                  size="$3"
                  circular
                  backgroundColor="transparent"
                  borderColor="transparent"
                  onPress={() => setIsOpen(false)}
                >
                  <X size="$1" color="$color11" />
                </Button>
              </XStack>

              {/* User Info */}
              {user && (
                <YStack 
                  backgroundColor="$color2" 
                  padding="$3" 
                  borderRadius="$4" 
                  marginBottom="$4"
                >
                  <Text fontSize="$3" color="$color10">
                    {user.email}
                  </Text>
                  
                  {/* Current Group Info */}
                  {currentGroup && (
                    <YStack gap="$2" marginTop="$2">
                      <Text fontSize="$3" color="$blue10">
                        📊 {currentGroup.name}
                      </Text>
                      
                      {/* Show current league and year if available */}
                      {selectedLeagueId && (
                        <Text fontSize="$2" color="$color11">
                          🏆 {getAvailableLeagues().find(l => l.id === selectedLeagueId)?.name || 'Unknown League'}
                          {selectedYear && ` • Anno ${getSeasonFromYear(selectedYear)}`}
                        </Text>
                      )}
                    </YStack>
                  )}
                  
                  {/* Group/League Selector Toggle */}
                  {(userGroups.length > 1 || (currentGroup && currentGroup.leagues.length > 1)) && (
                    <Button
                      size="$2"
                      variant="outlined"
                      marginTop="$3"
                      onPress={() => setShowGroupSelector(!showGroupSelector)}
                    >
                      <Text fontSize="$2">
                        {showGroupSelector ? '← Nascondi' : '⚙️ Cambia Gruppo/Leghe/Anno'}
                      </Text>
                    </Button>
                  )}
                  
                  {/* Group/League Selector */}
                  {showGroupSelector && (
                    <YStack gap="$2" marginTop="$3" padding="$2" backgroundColor="$color3" borderRadius="$3">
                      {/* Group Selector */}
                      {userGroups.length > 1 && (
                        <YStack gap="$1">
                          <Text fontSize="$2" fontWeight="bold" color="$color12">Groups:</Text>
                          {userGroups.map((group) => (
                            <Button
                              key={group.id}
                              size="$1"
                              variant={currentGroup?.id === group.id ? "outlined" : "outlined"}
                              borderColor={currentGroup?.id === group.id ? "$blue8" : "transparent"}
                              backgroundColor={currentGroup?.id === group.id ? "$blue2" : "transparent"}
                              onPress={async () => {
                                try {
                                  await setCurrentGroup(group.id)
                                } catch (error) {
                                  console.error('Error switching group:', error)
                                }
                              }}
                              justifyContent="flex-start"
                            >
                              <Text fontSize="$1" color={currentGroup?.id === group.id ? "$blue11" : "$color11"}>
                                {group.name}
                              </Text>
                            </Button>
                          ))}
                        </YStack>
                      )}
                      
                      {/* League Selector */}
                      {currentGroup && currentGroup.leagues.length > 1 && (
                        <YStack gap="$1" marginTop="$2">
                          <Text fontSize="$2" fontWeight="bold" color="$color12">Leghe:</Text>
                          {currentGroup.leagues.map((league) => (
                            <Button
                              key={league.id}
                              size="$1"
                              variant={selectedLeagueId === league.id ? "outlined" : "outlined"}
                              borderColor={selectedLeagueId === league.id ? "$green8" : "transparent"}
                              backgroundColor={selectedLeagueId === league.id ? "$green2" : "transparent"}
                              onPress={async () => {
                                try {
                                  await setSelectedLeague(league.id)
                                } catch (error) {
                                  console.error('Error switching league:', error)
                                }
                              }}
                              justifyContent="flex-start"
                            >
                              <Text fontSize="$1" color={selectedLeagueId === league.id ? "$green11" : "$color11"}>
                                🏆 {league.name} {league.isMain ? '(Principale)' : ''}
                              </Text>
                            </Button>
                          ))}
                        </YStack>
                      )}
                      
                      {/* Year Selector */}
                      {selectedLeagueId && getAvailableYears().length > 1 && (
                        <YStack gap="$1" marginTop="$2">
                          <Text fontSize="$2" fontWeight="bold" color="$color12">Anni:</Text>
                          <XStack gap="$1" flexWrap="wrap">
                            {getAvailableYears().sort((a, b) => b - a).map((year) => (
                              <Button
                                key={year}
                                size="$1"
                                variant={selectedYear === year ? "outlined" : "outlined"}
                                borderColor={selectedYear === year ? "orange" : "transparent"}
                                backgroundColor={selectedYear === year ? "orange" : "transparent"}
                                onPress={async () => {
                                  try {
                                    await setSelectedYear(year)
                                  } catch (error) {
                                    console.error('Error switching year:', error)
                                  }
                                }}
                                minWidth="$3"
                              >
                                <Text fontSize="$1" color={selectedYear === year ? "orange" : "$color11"}>
                                  {getSeasonFromYear(year)}
                                </Text>
                              </Button>
                            ))}
                          </XStack>
                        </YStack>
                      )}
                    </YStack>
                  )}
                </YStack>
              )}

              <Separator />

              {/* Main Navigation */}
              {navigationItems.map(renderSection)}

              {/* Admin Navigation */}
              {isAdmin && (
                <>
                  <Separator />
                  {adminItems.map(renderSection)}
                </>
              )}

              {/* Super Admin Navigation */}
              {isSuperAdmin && (
                <>
                  <Separator />
                  {superAdminItems.map(renderSection)}
                </>
              )}

              <Separator />

              {/* Settings Section */}
              <YStack gap="$3" marginBottom="$4">
                <Text fontSize="$6" fontWeight="bold" color="$color12">
                  Settings
                </Text>
                
                {/* Theme Toggle */}
                <XStack alignItems="center" justifyContent="space-between" paddingHorizontal="$4">
                  <Text fontSize="$4" color="$color12">Theme</Text>
                  <SwitchThemeButton />
                </XStack>

                {/* Logout Button */}
                <Button
                  variant="outlined"
                  borderColor="$red8"
                  color="$red10"
                  onPress={logout}
                  marginTop="$2"
                >
                  <Text color="$red10" fontWeight="500">Logout</Text>
                </Button>
              </YStack>
            </YStack>
          </ScrollView>
        </Sheet.Frame>
      </Sheet>
    </>
  )
}