import React, { useState } from 'react'
import { Platform } from 'react-native'
import {
  Button,
  Sheet,
  XStack,
  YStack,
  Text,
  Separator,
  SwitchThemeButton,
  ScrollView,
  useMedia,
} from '@my/ui'
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
import { useAuth } from '../auth/AuthProvider'
import { hasRole, IdentityRole } from '../services/authService'
import { useAppNavigation } from '../hooks/useAppNavigation'

interface NavigationBarProps {
  onNavigate?: (path: string) => void
}

// Navigation items structure based on prompt_frontendapp routing
const navigationItems = [
  {
    section: 'Main',
    items: [
      { path: '/', label: 'Home', icon: Home, description: 'Serie A matches of current round' },
      { path: '/standings', label: 'Fantasy Standings', icon: Trophy, description: 'Fantasy league standings' },
      { path: '/calendar', label: 'Fantasy Calendar', icon: Calendar, description: 'Fantasy league calendar' },
      { path: '/live', label: 'Live', icon: Zap, description: 'Live matches and fantasy points' },
    ],
  },
  {
    section: 'League',
    items: [
      { path: '/teams', label: 'Teams', icon: Users, description: 'Teams in current fantasy league' },
    ],
  },
  {
    section: 'Players',
    items: [
      { path: '/players', label: 'All Players', icon: UserCheck, description: 'Serie A players list' },
      { path: '/free-players', label: 'Free Players', icon: User, description: 'Available free players' },
    ],
  },
  {
    section: 'Market',
    items: [
      { path: '/market', label: 'Market Hub', icon: DollarSign, description: 'Trading marketplace' },
      { path: '/market/trades', label: 'Current Trades', icon: TrendingUp, description: 'Active trade offers' },
      { path: '/market/trades/history', label: 'Trade History', icon: Calendar, description: 'Past trades' },
    ],
  },
  {
    section: 'Serie A',
    items: [
      { path: '/seriea/standings', label: 'Serie A Standings', icon: Trophy, description: 'Official Serie A table' },
      { path: '/seriea/calendar', label: 'Serie A Calendar', icon: Calendar, description: 'Serie A fixtures' },
    ],
  },
  {
    section: 'My Team',
    items: [
      { path: '/my-team', label: 'My Team', icon: Users, description: 'Manage lineup for current round' },
    ],
  },
  {
    section: 'Other',
    items: [
      { path: '/hall-of-fame', label: 'Hall of Fame', icon: Star, description: 'Past winners and seasons' },
      { path: '/auction', label: 'Auction', icon: Gavel, description: 'Live auction screen' },
      { path: '/settings', label: 'Settings', icon: Settings, description: 'User settings' },
    ],
  },
]

// Admin items (shown only to admins)
const adminItems = [
  {
    section: 'Admin',
    items: [
      { path: '/admin', label: 'Admin Dashboard', icon: Shield, description: 'Admin dashboard' },
      { path: '/admin/groups', label: 'Manage Groups', icon: Users, description: 'Manage groups and leagues' },
      { path: '/admin/auction', label: 'Auction Control', icon: Gavel, description: 'Auction control panel' },
    ],
  },
]

// Super Admin items (shown only to super admins)
const superAdminItems = [
  {
    section: 'Super Admin',
    items: [
      { path: '/superadmin', label: 'Super Admin', icon: ShieldCheck, description: 'Super admin dashboard' },
      { path: '/superadmin/users', label: 'Manage Users', icon: Users, description: 'User management' },
      { path: '/superadmin/logs', label: 'Logs', icon: PlayCircle, description: 'System logs' },
      { path: '/superadmin/errors', label: 'Error Reports', icon: X, description: 'Error tracking' },
    ],
  },
]

export const NavigationBar: React.FC<NavigationBarProps> = ({ onNavigate }) => {
  const [isOpen, setIsOpen] = useState(false)
  const { user, logout } = useAuth()
  const { navigate } = useAppNavigation()
  const media = useMedia()

  // Check user roles for admin sections
  const userGroups = user?.groups || []
  const isAdmin = userGroups.some(group => hasRole(group.role, IdentityRole.Admin))
  // For now, super admin logic would need backend implementation
  const isSuperAdmin = userGroups.some(group => hasRole(group.role, IdentityRole.Admin) && group.name === 'SuperAdmin')

  // Responsive sheet settings
  const snapPointsMode = media.gtSm ? 'fit' : 'percent'
  const sheetProps = {
    modal: true,
    open: isOpen,
    onOpenChange: setIsOpen,
    snapPointsMode: snapPointsMode as 'fit' | 'percent',
    snapPoints: media.gtSm ? undefined : [90],
    moveOnKeyboardChange: false,
    dismissOnSnapToBottom: true,
    disableDrag: false,
  }

  // Handle navigation
  const handleNavigate = (path: string) => {
    if (onNavigate) {
      onNavigate(path)
    } else {
      navigate(path)
    }
    setIsOpen(false)
  }

  // Render navigation section
  const renderSection = (section: typeof navigationItems[0]) => (
    <YStack key={section.section} gap="$2" marginBottom="$4">
      <Text fontSize="$6" fontWeight="bold" color="$color12" marginBottom="$2">
        {section.section}
      </Text>
      {section.items.map((item) => {
        const IconComponent = item.icon
        return (
          <Button
            key={item.path}
            variant="ghost"
            justifyContent="flex-start"
            paddingHorizontal="$4"
            paddingVertical="$3"
            onPress={() => handleNavigate(item.path)}
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
          borderTopLeftRadius="$6"
          borderTopRightRadius="$6"
          padding="$0"
          width={media.gtSm ? '25%' : '100%'}
          alignSelf={media.gtSm ? 'flex-start' : 'center'}
          marginLeft={media.gtSm ? '$0' : '$0'}
          height={media.gtSm ? '90%' : '90vh'}
          flex={media.gtSm ? undefined : 1}
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
                <Text fontSize="$8" fontWeight="bold" color="$blue10">
                  Fantasoccer ⚽
                </Text>
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
                  <Text fontSize="$5" fontWeight="600" color="$color12">
                    {user.username}
                  </Text>
                  <Text fontSize="$3" color="$color10">
                    {user.email}
                  </Text>
                  {user.groups?.[0] && (
                    <Text fontSize="$3" color="$blue10" marginTop="$1">
                      📊 {user.groups[0].name} • Year {user.state?.year}
                    </Text>
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