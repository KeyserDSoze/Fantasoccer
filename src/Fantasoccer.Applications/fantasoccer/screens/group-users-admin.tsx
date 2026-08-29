import React, { useState, useCallback, useMemo } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { 
  YStack, 
  XStack, 
  Text, 
  H3,
  H4,
  Card, 
  Spinner, 
  Separator,
  Button,
  Input,
  useMedia
} from 'tamagui'
import { useApp } from 'providers/AppProvider'
import { groupService } from 'services/groupService'
import { appIdentityService } from 'services/appIdentityService'
import { IdentityRole, hasRole } from 'types/identity'
import { UserOfAGroup } from 'types/group'
import { 
  Users as UsersIcon,
  UserPlus,
  UserMinus,
  Shield,
  Eye,
  Star,
  CheckCircle,
  AlertCircle,
  Mail
} from '@tamagui/lucide-icons-2'

// Email regex pattern
const EMAIL_PATTERN = /^[\w-\.]+@([\w-]+\.)+[\w-]{2,6}$/

export default function GroupUsersAdminScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const { currentGroup, refreshGroups } = useApp()

  const [newEmail, setNewEmail] = useState('')
  const [adding, setAdding] = useState(false)
  const [updating, setUpdating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [updatingUserId, setUpdatingUserId] = useState<string | null>(null)

  // Validate email
  const isEmailValid = useMemo(() => {
    if (!newEmail) return false
    return EMAIL_PATTERN.test(newEmail)
  }, [newEmail])

  // Add new user to group
  const addUser = useCallback(async () => {
    if (!currentGroup || !newEmail || !isEmailValid) return

    setAdding(true)
    setError(null)
    setSuccess(null)

    try {
      // Check if user already exists in group
      if (currentGroup.users.some(u => u.email === newEmail)) {
        setError(`L'utente ${newEmail} è già nel gruppo`)
        return
      }

      // Check if user exists in AppIdentity
      let user = await appIdentityService.getAppIdentity(newEmail)
      
      if (!user) {
        // Create new AppIdentity if doesn't exist
        user = {
          email: newEmail,
          username: newEmail,
          state: {
            raw: {}
          },
          groups: [
            {
              id: currentGroup.id,
              name: currentGroup.name,
              role: IdentityRole.Reader,
              raw: { i: currentGroup.id, n: currentGroup.name, r: IdentityRole.Reader }
            }
          ],
          raw: {
            e: newEmail,
            u: newEmail,
            s: {},
            g: [{ i: currentGroup.id, n: currentGroup.name, r: IdentityRole.Reader }]
          }
        }

        await appIdentityService.insertAppIdentity(user)
      } else {
        // Add group to existing user if not already present
        const hasGroup = user.groups.some(g => g.id === currentGroup.id)
        if (!hasGroup) {
          user.groups.push({
            id: currentGroup.id,
            name: currentGroup.name,
            role: IdentityRole.Reader,
            raw: { i: currentGroup.id, n: currentGroup.name, r: IdentityRole.Reader }
          })
          await appIdentityService.updateAppIdentity(user.email, user)
        }
      }

      // Add user to group
      const updatedGroup = {
        ...currentGroup,
        users: [
          ...currentGroup.users,
          {
            email: newEmail,
            username: user.username || newEmail,
            role: IdentityRole.Reader
          }
        ]
      }

      await groupService.updateGroup(currentGroup.id, updatedGroup)
      
      setSuccess(`Utente ${newEmail} aggiunto con successo`)
      setNewEmail('')
      
      // Refresh groups
      await refreshGroups()
      
    } catch (err) {
      setError(`Errore nell'aggiunta dell'utente: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setAdding(false)
    }
  }, [currentGroup, newEmail, isEmailValid, refreshGroups])

  // Remove user from group
  const removeUser = useCallback(async (user: UserOfAGroup) => {
    if (!currentGroup) return

    setUpdatingUserId(user.email)
    setUpdating(true)
    setError(null)
    setSuccess(null)

    try {
      // Remove user from group
      const updatedGroup = {
        ...currentGroup,
        users: currentGroup.users.filter(u => u.email !== user.email)
      }

      await groupService.updateGroup(currentGroup.id, updatedGroup)

      // Update user's AppIdentity to remove this group
      const userIdentity = await appIdentityService.getAppIdentity(user.email)
      if (userIdentity) {
        userIdentity.groups = userIdentity.groups.filter(g => g.id !== currentGroup.id)
        await appIdentityService.updateAppIdentity(user.email, userIdentity)
      }

      setSuccess(`Utente ${user.email} rimosso con successo`)
      
      // Refresh groups
      await refreshGroups()
      
    } catch (err) {
      setError(`Errore nella rimozione dell'utente: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setUpdating(false)
      setUpdatingUserId(null)
    }
  }, [currentGroup, refreshGroups])

  // Toggle role for user
  const toggleRole = useCallback(async (user: UserOfAGroup, role: IdentityRole) => {
    if (!currentGroup) return

    setUpdatingUserId(user.email)
    setUpdating(true)
    setError(null)
    setSuccess(null)

    try {
      // Toggle role flag
      const currentRole = user.role
      const hasRoleFlag = hasRole(currentRole, role)
      const newRole = hasRoleFlag 
        ? currentRole & ~role  // Remove role
        : currentRole | role   // Add role

      // Update user in group
      const updatedGroup = {
        ...currentGroup,
        users: currentGroup.users.map(u => 
          u.email === user.email 
            ? { ...u, role: newRole }
            : u
        )
      }

      await groupService.updateGroup(currentGroup.id, updatedGroup)

      // Update user's AppIdentity
      const userIdentity = await appIdentityService.getAppIdentity(user.email)
      if (userIdentity) {
        const groupIndex = userIdentity.groups.findIndex(g => g.id === currentGroup.id)
        if (groupIndex !== -1) {
          userIdentity.groups[groupIndex].role = newRole
          userIdentity.groups[groupIndex].raw.r = newRole
        }
        await appIdentityService.updateAppIdentity(user.email, userIdentity)
      }

      setSuccess(`Ruolo aggiornato per ${user.email}`)
      
      // Refresh groups
      await refreshGroups()
      
    } catch (err) {
      setError(`Errore nell'aggiornamento del ruolo: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setUpdating(false)
      setUpdatingUserId(null)
    }
  }, [currentGroup, refreshGroups])

  if (!currentGroup) {
    return (
      <View style={{ flex: 1 }}>
        <ScreenContent path="screens/group-users-admin.tsx" title="Gestione Utenti">
          <YStack padding="$4" alignItems="center" gap="$3">
            <AlertCircle size="$4" color="$orange10" />
            <Text fontSize="$5" color="$color11">
              Nessun gruppo selezionato
            </Text>
            <Text fontSize="$3" color="$color10" textAlign="center">
              Seleziona un gruppo per gestire gli utenti
            </Text>
          </YStack>
        </ScreenContent>
      </View>
    )
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/group-users-admin.tsx" title="Gestione Utenti">
        <YStack
          width={containerWidth}
          maxWidth={1200}
          gap="$4"
          alignItems="stretch"
          alignSelf="center"
        >
          {/* Header */}
          <Card
            padding="$4"
            backgroundColor="$blue3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$blue7"
          >
            <XStack alignItems="center" gap="$3" marginBottom="$2">
              <UsersIcon size="$2" color="$blue10" />
              <H3 color="$blue11">Gestione Utenti del Gruppo</H3>
            </XStack>
            <Text fontSize="$3" color="$blue10">
              Aggiungi, rimuovi e gestisci i ruoli degli utenti nel gruppo <Text fontWeight="bold">{currentGroup.name}</Text>.
            </Text>
          </Card>

          {/* Success Message */}
          {success && (
            <Card padding="$3" backgroundColor="$green3" borderColor="$green7" borderWidth="$0.5">
              <XStack alignItems="center" gap="$2">
                <CheckCircle size="$1" color="$green11" />
                <Text color="$green11" fontWeight="600">{success}</Text>
              </XStack>
            </Card>
          )}

          {/* Error Message */}
          {error && (
            <Card padding="$3" backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5">
              <XStack alignItems="center" gap="$2">
                <AlertCircle size="$1" color="$red11" />
                <Text color="$red11" fontWeight="600">{error}</Text>
              </XStack>
            </Card>
          )}

          {/* Add User Section */}
          <Card padding="$4" backgroundColor="$color2" borderRadius="$4">
            <H4 marginBottom="$3" color="$color12">
              <UserPlus size="$1" style={{ marginRight: 8 }} />
              Aggiungi Nuovo Utente
            </H4>
            
            <YStack gap="$3">
              <Input
                placeholder="email@esempio.com"
                value={newEmail}
                onChangeText={setNewEmail}
                keyboardType="email-address"
                autoCapitalize="none"
                autoCorrect={false}
                size="$4"
                borderColor={newEmail && !isEmailValid ? '$red8' : '$borderColor'}
              />
              
              {newEmail && !isEmailValid && (
                <Text fontSize="$2" color="$red11">
                  Email non valida
                </Text>
              )}

              <Button
                size="$4"
                backgroundColor="$blue9"
                onPress={addUser}
                disabled={!isEmailValid || adding}
                icon={adding ? <Spinner size="small" /> : <UserPlus size="$1" />}
              >
                {adding ? 'Aggiunta in corso...' : 'Aggiungi Utente'}
              </Button>
            </YStack>
          </Card>

          {/* Users List */}
          <Card padding="$4" backgroundColor="$color2" borderRadius="$4">
            <H4 marginBottom="$3" color="$color12">
              <UsersIcon size="$1" style={{ marginRight: 8 }} />
              Utenti del Gruppo ({currentGroup.users.length})
            </H4>

            {currentGroup.users.length === 0 ? (
              <Text fontSize="$3" color="$color10" textAlign="center" paddingVertical="$6">
                Nessun utente nel gruppo
              </Text>
            ) : (
              <YStack gap="$2">
                {currentGroup.users.map((user, index) => (
                  <Card
                    key={user.email}
                    padding="$3"
                    backgroundColor="$color1"
                    borderRadius="$3"
                    borderWidth="$0.5"
                    borderColor="$borderColor"
                  >
                    <YStack gap="$3">
                      {/* User Info */}
                      <XStack alignItems="center" gap="$3" justifyContent="space-between">
                        <YStack flex={1} gap="$1">
                          <Text fontSize="$4" fontWeight="600" color="$color12">
                            {user.username}
                          </Text>
                          <XStack alignItems="center" gap="$2">
                            <Mail size="$0.75" color="$color11" />
                            <Text fontSize="$2" color="$color11">
                              {user.email}
                            </Text>
                          </XStack>
                        </YStack>

                        <Button
                          size="$2"
                          variant="outlined"
                          borderColor="$red8"
                          color="$red11"
                          onPress={() => removeUser(user)}
                          disabled={updating && updatingUserId === user.email}
                          icon={updating && updatingUserId === user.email ? <Spinner size="small" /> : <UserMinus size="$0.75" />}
                        >
                          Rimuovi
                        </Button>
                      </XStack>

                      <Separator />

                      {/* Roles */}
                      <YStack gap="$2">
                        <Text fontSize="$2" color="$color11" fontWeight="600">
                          Ruoli:
                        </Text>

                        <XStack gap="$2" flexWrap="wrap">
                          {/* Super Admin */}
                          <Button
                            size="$2"
                            variant={hasRole(user.role, IdentityRole.SuperAdmin) ? 'outlined' : 'outlined'}
                            backgroundColor={hasRole(user.role, IdentityRole.SuperAdmin) ? '$purple3' : 'transparent'}
                            borderColor={hasRole(user.role, IdentityRole.SuperAdmin) ? '$purple8' : '$borderColor'}
                            color={hasRole(user.role, IdentityRole.SuperAdmin) ? '$purple11' : '$color11'}
                            onPress={() => toggleRole(user, IdentityRole.SuperAdmin)}
                            disabled={updating && updatingUserId === user.email}
                            icon={<Star size="$0.75" />}
                          >
                            Super Admin
                          </Button>

                          {/* Admin */}
                          <Button
                            size="$2"
                            variant={hasRole(user.role, IdentityRole.Admin) ? 'outlined' : 'outlined'}
                            backgroundColor={hasRole(user.role, IdentityRole.Admin) ? '$blue3' : 'transparent'}
                            borderColor={hasRole(user.role, IdentityRole.Admin) ? '$blue8' : '$borderColor'}
                            color={hasRole(user.role, IdentityRole.Admin) ? '$blue11' : '$color11'}
                            onPress={() => toggleRole(user, IdentityRole.Admin)}
                            disabled={updating && updatingUserId === user.email}
                            icon={<Shield size="$0.75" />}
                          >
                            Admin
                          </Button>

                          {/* Participant */}
                          <Button
                            size="$2"
                            variant={hasRole(user.role, IdentityRole.Participant) ? 'outlined' : 'outlined'}
                            backgroundColor={hasRole(user.role, IdentityRole.Participant) ? '$green3' : 'transparent'}
                            borderColor={hasRole(user.role, IdentityRole.Participant) ? '$green8' : '$borderColor'}
                            color={hasRole(user.role, IdentityRole.Participant) ? '$green11' : '$color11'}
                            onPress={() => toggleRole(user, IdentityRole.Participant)}
                            disabled={updating && updatingUserId === user.email}
                            icon={<UsersIcon size="$0.75" />}
                          >
                            Partecipante
                          </Button>

                          {/* Reader */}
                          <Button
                            size="$2"
                            variant={hasRole(user.role, IdentityRole.Reader) ? 'outlined' : 'outlined'}
                            backgroundColor={hasRole(user.role, IdentityRole.Reader) ? '$gray3' : 'transparent'}
                            borderColor={hasRole(user.role, IdentityRole.Reader) ? '$gray8' : '$borderColor'}
                            color={hasRole(user.role, IdentityRole.Reader) ? '$gray11' : '$color11'}
                            onPress={() => toggleRole(user, IdentityRole.Reader)}
                            disabled={updating && updatingUserId === user.email}
                            icon={<Eye size="$0.75" />}
                          >
                            Visitatore
                          </Button>
                        </XStack>
                      </YStack>
                    </YStack>
                  </Card>
                ))}
              </YStack>
            )}
          </Card>
        </YStack>
      </ScreenContent>
    </View>
  )
}
