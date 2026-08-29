import React, { useMemo, useState } from 'react'
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
  Input,
  useMedia
} from 'tamagui'
import { useAllIdentities } from 'hooks/useAppIdentity'
import { IdentityRole, getUserRoleNames } from 'types/identity'
import { Users as UsersIcon, Search, Shield, Mail, Calendar } from '@tamagui/lucide-icons-2'

export default function UsersScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const { identities, loading, error, refetch } = useAllIdentities({
    refreshInterval: 30000 // Auto-refresh ogni 30 secondi
  })

  const [searchQuery, setSearchQuery] = useState('')

  // Filtra utenti in base alla ricerca
  const filteredIdentities = useMemo(() => {
    if (!searchQuery) return identities

    const query = searchQuery.toLowerCase()
    return identities.filter(identity => 
      identity.email.toLowerCase().includes(query) ||
      identity.username?.toLowerCase().includes(query) ||
      identity.groups.some(group => group.name.toLowerCase().includes(query))
    )
  }, [identities, searchQuery])

  // Statistiche rapide
  const stats = useMemo(() => {
    const totalUsers = identities.length
    const usersWithGroups = identities.filter(i => i.groups.length > 0).length
    const superAdmins = identities.filter(i => 
      i.groups.some(g => (g.role & IdentityRole.SuperAdmin) === IdentityRole.SuperAdmin)
    ).length
    const admins = identities.filter(i => 
      i.groups.some(g => (g.role & IdentityRole.Admin) === IdentityRole.Admin)
    ).length

    return { totalUsers, usersWithGroups, superAdmins, admins }
  }, [identities])

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/users.tsx" title="Gestione Utenti">
        <YStack
          width={containerWidth}
          maxWidth={1200}
          gap="$4"
          alignItems="stretch"
          alignSelf="center"
        >
          {/* Header con info */}
          <Card
            padding="$4"
            backgroundColor="$blue3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$blue7"
          >
            <XStack alignItems="center" gap="$3" marginBottom="$2">
              <UsersIcon size="$2" color="$blue10" />
              <H3 color="$blue11">Utenti Registrati</H3>
            </XStack>
            <Text fontSize="$3" color="$blue10">
              Visualizzazione completa di tutti gli utenti del sistema e relative informazioni.
            </Text>
          </Card>

          {/* Statistiche rapide */}
          <XStack gap="$3" flexWrap="wrap">
            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={150}>
              <Text fontSize="$2" color="$color11">Totale Utenti</Text>
              <Text fontSize="$7" fontWeight="bold" color="$blue10">{stats.totalUsers}</Text>
            </Card>
            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={150}>
              <Text fontSize="$2" color="$color11">Con Gruppi</Text>
              <Text fontSize="$7" fontWeight="bold" color="$green10">{stats.usersWithGroups}</Text>
            </Card>
            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={150}>
              <Text fontSize="$2" color="$color11">Super Admin</Text>
              <Text fontSize="$7" fontWeight="bold" color="$purple10">{stats.superAdmins}</Text>
            </Card>
            <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={150}>
              <Text fontSize="$2" color="$color11">Admin</Text>
              <Text fontSize="$7" fontWeight="bold" color="$orange10">{stats.admins}</Text>
            </Card>
          </XStack>

          {/* Barra di ricerca */}
          <Card padding="$3" backgroundColor="$color2">
            <XStack gap="$2" alignItems="center">
              <Search size="$1" color="$color11" />
              <Input
                flex={1}
                placeholder="Cerca per email, username o gruppo..."
                value={searchQuery}
                onChangeText={setSearchQuery}
                backgroundColor="$color1"
                borderColor="$borderColor"
              />
            </XStack>
          </Card>

          {/* Lista utenti */}
          {loading ? (
            <Card padding="$6" alignItems="center" gap="$3">
              <Spinner size="large" color="$blue10" />
              <Text color="$color10">Caricamento utenti...</Text>
            </Card>
          ) : error ? (
            <Card padding="$4" backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5">
              <Text color="$red11" fontWeight="600">Errore: {error}</Text>
            </Card>
          ) : filteredIdentities.length === 0 ? (
            <Card padding="$6" alignItems="center">
              <Text color="$color11" fontSize="$4">
                {searchQuery ? 'Nessun utente trovato' : 'Nessun utente registrato'}
              </Text>
            </Card>
          ) : (
            <YStack gap="$3">
              <XStack justifyContent="space-between" alignItems="center">
                <Text fontSize="$3" color="$color11">
                  Visualizzati {filteredIdentities.length} di {identities.length} utenti
                </Text>
              </XStack>

              {filteredIdentities.map(identity => (
                <UserCard key={identity.email} identity={identity} />
              ))}
            </YStack>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}

// ============================================
// USER CARD COMPONENT
// ============================================

interface UserCardProps {
  identity: AppIdentity
}

function UserCard({ identity }: UserCardProps) {
  const media = useMedia()
  const isMobile = !media.gtMd

  // Determina il ruolo più alto
  const highestRole = useMemo(() => {
    let maxRole: IdentityRole = IdentityRole.None
    identity.groups.forEach(group => {
      if ((group.role & IdentityRole.SuperAdmin) === IdentityRole.SuperAdmin) {
        maxRole = IdentityRole.SuperAdmin
      } else if ((maxRole & IdentityRole.SuperAdmin) !== IdentityRole.SuperAdmin && (group.role & IdentityRole.Admin) === IdentityRole.Admin) {
        maxRole = IdentityRole.Admin
      } else if (maxRole === IdentityRole.None && (group.role & IdentityRole.Participant) === IdentityRole.Participant) {
        maxRole = IdentityRole.Participant
      }
    })
    return maxRole
  }, [identity.groups])

  const roleColor = useMemo(() => {
    if ((highestRole & IdentityRole.SuperAdmin) === IdentityRole.SuperAdmin) return '$purple10'
    if ((highestRole & IdentityRole.Admin) === IdentityRole.Admin) return '$orange10'
    if ((highestRole & IdentityRole.Participant) === IdentityRole.Participant) return '$blue10'
    return '$color11'
  }, [highestRole])

  const roleLabel = useMemo(() => {
    if ((highestRole & IdentityRole.SuperAdmin) === IdentityRole.SuperAdmin) return 'Super Admin'
    if ((highestRole & IdentityRole.Admin) === IdentityRole.Admin) return 'Admin'
    if ((highestRole & IdentityRole.Participant) === IdentityRole.Participant) return 'Partecipante'
    return 'Nessun ruolo'
  }, [highestRole])

  return (
    <Card
      padding="$4"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$borderColor"
      gap="$3"
    >
      {/* Header - Email e Username */}
      <YStack gap="$2">
        <XStack alignItems="center" gap="$2" flexWrap="wrap">
          <Mail size="$1" color="$color11" />
          <Text fontSize="$4" fontWeight="bold" color="$color12">
            {identity.email}
          </Text>
          {identity.username && (
            <Text fontSize="$3" color="$color11">
              (@{identity.username})
            </Text>
          )}
        </XStack>

        <XStack alignItems="center" gap="$2">
          <Shield size="$1" color={roleColor} />
          <Text fontSize="$3" color={roleColor} fontWeight="600">
            {roleLabel}
          </Text>
        </XStack>
      </YStack>

      <Separator />

      {/* Stato Corrente */}
      {(identity.state.groupId || identity.state.leagueId || identity.state.year !== undefined) && (
        <YStack gap="$2">
          <H4 fontSize="$3" color="$color11">Stato Corrente</H4>
          <YStack gap="$1" paddingLeft="$3">
            {identity.state.groupId && (
              <Text fontSize="$2" color="$color10">
                📁 Gruppo: {identity.state.groupId}
              </Text>
            )}
            {identity.state.leagueId && (
              <Text fontSize="$2" color="$color10">
                🏆 Lega: {identity.state.leagueId}
              </Text>
            )}
            {identity.state.year !== undefined && (
              <Text fontSize="$2" color="$color10">
                📅 Anno: {identity.state.year}
              </Text>
            )}
            {identity.state.meOrLeaderEmail && (
              <Text fontSize="$2" color="$color10">
                👤 Email di lavoro: {identity.state.meOrLeaderEmail}
              </Text>
            )}
          </YStack>
        </YStack>
      )}

      {/* Gruppi */}
      {identity.groups.length > 0 && (
        <YStack gap="$2">
          <H4 fontSize="$3" color="$color11">
            Gruppi ({identity.groups.length})
          </H4>
          <YStack gap="$2">
            {identity.groups.map(group => (
              <Card
                key={group.id}
                padding="$2"
                backgroundColor="$color1"
                borderRadius="$2"
              >
                <XStack justifyContent="space-between" alignItems="center" flexWrap="wrap" gap="$2">
                  <YStack gap="$1" flex={1}>
                    <Text fontSize="$3" fontWeight="600" color="$color12">
                      {group.name}
                    </Text>
                    <Text fontSize="$1" color="$color11">
                      ID: {group.id}
                    </Text>
                  </YStack>
                  <XStack gap="$1" flexWrap="wrap">
                    {getUserRoleNames(group.role).map(roleName => (
                      <Card
                        key={roleName}
                        padding="$1"
                        paddingHorizontal="$2"
                        backgroundColor={
                          roleName === 'SuperAdmin' ? '$purple3' :
                          roleName === 'Admin' ? '$orange3' :
                          roleName === 'Participant' ? '$blue3' :
                          '$color3'
                        }
                        borderRadius="$2"
                      >
                        <Text
                          fontSize="$1"
                          color={
                            roleName === 'SuperAdmin' ? '$purple11' :
                            roleName === 'Admin' ? '$orange11' :
                            roleName === 'Participant' ? '$blue11' :
                            '$color11'
                          }
                          fontWeight="600"
                        >
                          {roleName}
                        </Text>
                      </Card>
                    ))}
                  </XStack>
                </XStack>
              </Card>
            ))}
          </YStack>
        </YStack>
      )}

      {identity.groups.length === 0 && (
        <Card padding="$3" backgroundColor="$yellow3" borderRadius="$3">
          <Text fontSize="$2" color="$yellow11">
            ⚠️ Utente non assegnato a nessun gruppo
          </Text>
        </Card>
      )}
    </Card>
  )
}

// Import AppIdentity type
import type { AppIdentity } from 'types/identity'
