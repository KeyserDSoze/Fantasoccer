'use client'

import React, { useState } from 'react'
import { Platform } from 'react-native'
import {
  Button,
  Card,
  Sheet,
  XStack,
  YStack,
  Text,
  Separator,
  ScrollView,
  useMedia,
  Spinner,
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
  UserCog,
  DollarSign,
  Star,
  Settings,
  ShieldCheck,
  TrendingUp,
  PlayCircle,
  Gavel,
  BookOpen,
  Package,
  HelpCircle,
  Bell,
  FileText,
  Sparkles,
} from '@tamagui/lucide-icons-2'
import { useAuth } from 'providers/AuthProvider'
import { useApp } from 'providers/AppProvider'
import { hasRole, IdentityRole } from '../../types/identity'
import { SwitchThemeButton } from './SwitchThemeButton'
import { useNavigation } from '@react-navigation/native'
import type { RootStackParamList } from 'navigation'
import { APP_VERSION } from 'config/version'
import { formatSeasonFromYear } from 'utils/season'
import { useMainLeagueMarketActivity } from 'providers/MarketActivityProvider'

interface NavigationBarProps {
  onNavigate?: (path: string) => void
}

type NavigationRouteName = keyof RootStackParamList

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

// Navigation items structure based on prompt_frontendapp routing
const navigationItems: NavigationSection[] = [
  {
    section: 'Main',
    items: [
      { type: 'route', route: 'Home', label: 'Home', icon: Home, description: 'Classifica e giornata di Serie A' },
      { type: 'route', route: 'Ranking', label: 'Classifica', icon: Trophy, description: 'Classifica della tua lega' },
      { type: 'route', route: 'Calendar', label: 'Calendario', icon: Calendar, description: 'Calendario della lega' },
      { type: 'route', route: 'Live', label: 'Live', icon: Zap, description: 'Calendario e risultati in tempo reale' },
    ],
  },
  {
    section: 'Lega',
    items: [
      { type: 'route', route: 'Edit', label: 'La mia formazione', icon: Users, description: 'Gestisci la formazione per il turno attuale' },
      { type: 'route', route: 'TeamSettings', label: 'Impostazioni squadra', icon: UserCog, description: 'Gestisci logo, nome e immagini dei giocatori' },
      { type: 'route', route: 'Teams', label: 'Le squadre', icon: Users, description: 'Tutte le squadre della tua lega' },
    ],
  },
  {
    section: 'Giocatori',
    items: [
      { type: 'route', route: 'Players', label: 'Tutti i giocatori', icon: UserCheck, description: 'Statistiche complete dei giocatori di Serie A' },
    ],
  },
  {
    section: 'Mercato',
    items: [
      { type: 'route', route: 'Market', label: 'Crea Scambio', icon: DollarSign, description: 'Proponi un nuovo scambio' },
      { type: 'route', route: 'MarketTrades', label: 'Scambi', icon: TrendingUp, description: 'Vota scambi attivi e vedi lo storico' },
    ],
  },
  {
    section: 'Altro',
    items: [
      { type: 'route', route: 'HallOfFame', label: 'Hall of Fame', icon: Star, description: 'Vincitori e statistiche di sempre' },
      { type: 'route', route: 'Rules', label: 'Regolamento', icon: BookOpen, description: 'Regole e impostazioni della lega' },
      { type: 'route', route: 'Info', label: 'Guida', icon: HelpCircle, description: 'Guida al sito' },
      { type: 'route', route: 'McpGuide', label: 'MCP e ChatGPT', icon: Sparkles, description: 'Collega un assistente AI a Fantasoccer' },
      { type: 'route', route: 'Settings', label: 'Impostazioni', icon: Settings, description: 'Impostazioni utente' },
      { type: 'route', route: 'PushNotification', label: 'Notifiche Push', icon: Bell, description: 'Gestisci le notifiche push' },
      { type: 'route', route: 'PatchNotes', label: 'Patch Notes', icon: FileText, description: 'Novità e aggiornamenti dell\'app' },
    ],
  },
]

// Admin items (shown only to admins)
const adminItems: NavigationSection[] = [
  {
    section: 'Gestione Gruppo (Admin)',
    items: [
      { type: 'route', route: 'AuctionCreate', label: 'Asta', icon: Gavel, description: 'Crea e gestisci aste di fantacalcio' },
    ],
  },
]

// Super Admin items (shown only to super admins)
const superAdminItems: NavigationSection[] = [
  {
    section: 'Gestione Gruppo (Super Admin)',
    items: [
      { type: 'route', route: 'GroupUsersAdmin', label: 'Utenti', icon: UserCog, description: 'Gestisci utenti e ruoli del gruppo corrente' },
      { type: 'route', route: 'GroupBasketsAdmin', label: 'Basket', icon: Package, description: 'Gestisci basket e teams per anno del gruppo' },
      { type: 'route', route: 'GroupLeagueAdmin', label: 'Leghe', icon: Trophy, description: 'Gestisci leghe, settings e ricalcoli del gruppo' },
    ],
  },
  {
    section: 'Super Admin',
    items: [
      { type: 'link', path: '/superadmin', label: 'Super Admin', icon: ShieldCheck, description: 'Super admin dashboard' },
      { type: 'route', route: 'Users', label: 'Gestione Utenti', icon: Users, description: 'Visualizza tutti gli utenti e i loro gruppi' },
      { type: 'route', route: 'Logs', label: 'Log di Piattaforma', icon: PlayCircle, description: 'Log e errori delle ultime 24 ore per job' },
      { type: 'route', route: 'SerieAAdmin', label: 'Gestione Serie A', icon: Calendar, description: 'Gestisci partite rinviate nel calendario Serie A' },
      { type: 'route', route: 'CardsAdmin', label: 'Gestione Carte', icon: Sparkles, description: 'Crea e gestisci carte speciali del gioco' },
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
    hasLoadedGroups,
    isGroupsLoading,
    groupsError,
    refreshGroups,
    selectedLeagueId,
    selectedYear,
    setCurrentGroup,
    setSelectedLeague,
    setSelectedYear,
    getAvailableLeagues,
    getAvailableYears,
    superAdminMode,
    toggleSuperAdminMode
  } = useApp()
  const media = useMedia()
  const isDesktop = !media.sm
  const navigation = useNavigation()
  const marketActivity = useMainLeagueMarketActivity()
  const menuScrollStyle = {
    flex: 1,
    ...(Platform.OS === 'web' ? { overscrollBehavior: 'contain' as const } : {}),
  }
  
  // Check user roles for admin sections - find current user's role in current group
  const currentUserInGroup = currentGroup?.users.find(u => u.email === user?.email)
  
  const isSuperAdmin = currentUserInGroup && hasRole(currentUserInGroup.role, IdentityRole.SuperAdmin)
  const isAdmin = isSuperAdmin || currentUserInGroup && hasRole(currentUserInGroup.role, IdentityRole.Admin)
  const selectedAnnualLeague = currentGroup?.leagues.find(l => l.id === selectedLeagueId)?.years.find(y => y.year === selectedYear)
  const openingCompetitionEnabled = currentGroup?.leagues.find(l => l.id === selectedLeagueId)?.isMain === true && selectedAnnualLeague?.settings.openingCompetition.enabled === true
  // For now, super admin logic would need backend implementation

  // Responsive sheet settings
  const snapPointsMode = isDesktop ? 'fit' : 'percent'
  const sheetProps = {
    modal: true,
    open: isOpen,
    onOpenChange: setIsOpen,
    snapPointsMode: snapPointsMode as 'fit' | 'percent',
    snapPoints: isDesktop ? undefined : [90],
    moveOnKeyboardChange: false,
    dismissOnSnapToBottom: false,
    dismissOnOverlayPress: true,
    // Menu swipes must only scroll content, never drag or dismiss the navigation sheet.
    disableDrag: true,
  }

  // Handle navigation
  const handleNavigate = async (item: NavigationItem) => {
    if (item.type === 'route') {
      if (item.route === 'Live') {
        // Also restore the menu preference when Live is already the active screen.
        navigation.navigate('Live', {
          leagueId: selectedLeagueId ?? undefined,
          groupId: currentGroup?.id,
        })
      } else if (item.route === 'MarketTrades' && marketActivity.mainLeagueId) {
        (navigation as { navigate: (routeName: NavigationRouteName, params?: Record<string, unknown>) => void }).navigate(
          item.route,
          {
            groupId: currentGroup?.id,
            leagueId: marketActivity.mainLeagueId,
            year: marketActivity.year,
          }
        )
      } else {
        ;(navigation as { navigate: (routeName: NavigationRouteName) => void }).navigate(item.route)
      }
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
        const hasActiveMarkets = item.type === 'route' && item.route === 'MarketTrades' && marketActivity.activeMarketCount > 0
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
              {hasActiveMarkets && (
                <YStack
                  width={20}
                  height={20}
                  borderRadius={10}
                  backgroundColor="$red10"
                  alignItems="center"
                  justifyContent="center"
                  aria-label="Ci sono scambi attivi nella lega principale">
                  <Text color="white" fontSize={13} fontWeight="900" lineHeight={16}>!</Text>
                </YStack>
              )}
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
        position="relative"
        backgroundColor="transparent"
        borderColor="transparent"
        onPress={() => setIsOpen(true)}
        hoverStyle={{ backgroundColor: '$color3' }}
        pressStyle={{ backgroundColor: '$color4' }}
      >
        <Menu size="$1.5" color="$color12" />
        {marketActivity.activeMarketCount > 0 && (
          <YStack
            position="absolute"
            top={1}
            right={1}
            width={17}
            height={17}
            borderRadius={9}
            backgroundColor="$red10"
            alignItems="center"
            justifyContent="center"
            aria-label="Ci sono scambi attivi nella lega principale">
            <Text color="white" fontSize={12} fontWeight="900" lineHeight={14}>!</Text>
          </YStack>
        )}
      </Button>

      {/* Navigation Sheet */}
      <Sheet {...sheetProps}>
        <Sheet.Overlay 
          backgroundColor="rgba(0, 0, 0, 0.5)" 
          transition="lazy"
          enterStyle={{ opacity: 0 }}
          exitStyle={{ opacity: 0 }}
        />
        
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
            bounces={false}
            alwaysBounceVertical={false}
            overScrollMode="never"
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={{ 
              flexGrow: 1,
              paddingBottom: 50 // Extra space at bottom for safe scrolling
            }}
            style={menuScrollStyle}
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
                  aria-label="Chiudi menu di navigazione"
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
                          {selectedYear && ` • Anno ${formatSeasonFromYear(selectedYear)}`}
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
                                backgroundColor={"transparent"}
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
                                  {formatSeasonFromYear(year)}
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

              {!hasLoadedGroups || isGroupsLoading ? (
                <YStack alignItems="center" gap="$3" padding="$5">
                  <Spinner />
                  <Text color="$color10">Caricamento dei tuoi gruppi…</Text>
                </YStack>
              ) : groupsError ? (
                <Card padding="$4" gap="$3">
                  <Text color="$red10">Non riesco a caricare i gruppi. {groupsError}</Text>
                  <Button variant="outlined" onPress={() => void refreshGroups()}>Riprova</Button>
                </Card>
              ) : userGroups.length === 0 ? (
                <Card padding="$4" gap="$3">
                  <Text fontSize="$5" fontWeight="bold" color="$color12">Non fai ancora parte di un gruppo</Text>
                  <Text color="$color10">Le funzioni di lega, formazioni e mercato appariranno quando sarai invitato in un gruppo.</Text>
                  <Button variant="outlined" onPress={() => {
                    (navigation as { navigate: (routeName: NavigationRouteName) => void }).navigate('GroupWorkspace')
                    setIsOpen(false)
                  }}>Gestione gruppi</Button>
                </Card>
              ) : (
                <>
                  {/* Game navigation is available only to accounts with group membership. */}
                  {navigationItems.map((section) => renderSection(section))}
                  {openingCompetitionEnabled && renderSection({ section: 'Competizioni', items: [{ type: 'route', route: 'OpeningCompetition', label: 'Competizione d’apertura', icon: Trophy, description: 'Classifica e premi della competizione' }] })}

                  {isAdmin && <><Separator />{adminItems.map(renderSection)}</>}

                  {isSuperAdmin && (
                    <>
                      <Separator />
                      <YStack gap="$3" marginBottom="$4" padding="$3" backgroundColor="$purple2" borderRadius="$4">
                        <Text fontSize="$5" fontWeight="bold" color="$purple11">🛡️ Modalità Super Admin</Text>
                        <Text fontSize="$2" color="$color11">Attiva questa modalità per modificare le formazioni di tutte le squadre durante le partite live.</Text>
                        <Button size="$3" backgroundColor={superAdminMode ? "$purple10" : "$purple4"}
                          borderColor={superAdminMode ? "$purple10" : "$purple8"} onPress={toggleSuperAdminMode} pressStyle={{ opacity: 0.8 }}>
                          <Text color={superAdminMode ? "white" : "$purple11"} fontWeight="600">
                            {superAdminMode ? '✓ Modalità Attiva' : 'Attiva Modalità'}
                          </Text>
                        </Button>
                      </YStack>
                      {superAdminItems.map(renderSection)}
                    </>
                  )}
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

                {/* Version Info */}
                <XStack justifyContent="center" marginTop="$4" paddingTop="$3" borderTopWidth={1} borderTopColor="$borderColor">
                  <Text fontSize="$2" color="$color9">
                    v{APP_VERSION}
                  </Text>
                </XStack>
              </YStack>
            </YStack>
          </ScrollView>
        </Sheet.Frame>
      </Sheet>
    </>
  )
}
