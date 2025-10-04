// Example: Using AppIdentity Repository in a Component
// This file demonstrates how to use the new AppIdentity repository

import React from 'react'
import { YStack, Text, Button, Card, Spinner, H3, Separator } from 'tamagui'
import { 
  useAppIdentity, 
  useUserState, 
  useUserGroups 
} from '../hooks/useAppIdentity'
import { IdentityRole } from '../types/identity'

/**
 * Example 1: Display User Identity
 */
export function UserIdentityCard({ userEmail }: { userEmail: string }) {
  const { identity, loading, error, refetch } = useAppIdentity(userEmail, {
    refreshInterval: 60000 // Auto-refresh every minute
  })

  if (loading) {
    return (
      <Card padding="$4">
        <Spinner />
        <Text>Loading identity...</Text>
      </Card>
    )
  }

  if (error) {
    return (
      <Card padding="$4" backgroundColor="$red3">
        <Text color="$red11">Error: {error}</Text>
        <Button onPress={refetch} marginTop="$2">Retry</Button>
      </Card>
    )
  }

  if (!identity) {
    return (
      <Card padding="$4">
        <Text>No identity found</Text>
      </Card>
    )
  }

  return (
    <Card padding="$4" gap="$3">
      <H3>User Identity</H3>
      <Separator />
      
      <YStack gap="$2">
        <Text><Text fontWeight="bold">Email:</Text> {identity.email}</Text>
        <Text><Text fontWeight="bold">Username:</Text> {identity.username || 'N/A'}</Text>
        <Text><Text fontWeight="bold">Groups:</Text> {identity.groups.length}</Text>
        
        {identity.state.groupId && (
          <Text><Text fontWeight="bold">Current Group:</Text> {identity.state.groupId}</Text>
        )}
        
        {identity.state.leagueId && (
          <Text><Text fontWeight="bold">Current League:</Text> {identity.state.leagueId}</Text>
        )}
        
        {identity.state.year !== undefined && (
          <Text><Text fontWeight="bold">Current Year:</Text> {identity.state.year}</Text>
        )}
      </YStack>

      <Button onPress={refetch}>Refresh</Button>
    </Card>
  )
}

/**
 * Example 2: Manage User State
 */
export function UserStateManager({ userEmail }: { userEmail: string }) {
  const { 
    state, 
    loading, 
    error,
    updateGroup,
    updateLeague,
    updateYear,
    refetch
  } = useUserState(userEmail)

  const handleChangeGroup = async () => {
    const newGroupId = 'example-group-id'
    const success = await updateGroup(newGroupId)
    
    if (success) {
      console.log('✅ Group updated successfully')
    } else {
      console.error('❌ Failed to update group')
    }
  }

  const handleChangeYear = async () => {
    const newYear = 2024
    const success = await updateYear(newYear)
    
    if (success) {
      console.log('✅ Year updated successfully')
    } else {
      console.error('❌ Failed to update year')
    }
  }

  if (loading) return <Spinner />
  if (error) return <Text color="$red11">Error: {error}</Text>
  if (!state) return <Text>No state found</Text>

  return (
    <Card padding="$4" gap="$3">
      <H3>User State</H3>
      <Separator />
      
      <YStack gap="$2">
        <Text>Group ID: {state.groupId || 'Not set'}</Text>
        <Text>League ID: {state.leagueId || 'Not set'}</Text>
        <Text>Year: {state.year ?? 'Not set'}</Text>
        <Text>Working Email: {state.meOrLeaderEmail || 'Not set'}</Text>
      </YStack>

      <YStack gap="$2">
        <Button onPress={handleChangeGroup}>Change Group (Example)</Button>
        <Button onPress={handleChangeYear}>Change Year (Example)</Button>
        <Button onPress={refetch}>Refresh</Button>
      </YStack>
    </Card>
  )
}

/**
 * Example 3: Manage User Groups
 */
export function UserGroupsManager({ userEmail }: { userEmail: string }) {
  const { 
    groups, 
    loading, 
    error,
    addGroup,
    removeGroup,
    updateRoleInGroup,
    hasRoleInGroup,
    isAdminInGroup,
    isParticipantInGroup,
    refetch
  } = useUserGroups(userEmail)

  const handleAddGroup = async () => {
    const success = await addGroup({
      id: 'new-group-123',
      name: 'Example New Group',
      role: IdentityRole.Participant
    })
    
    if (success) {
      console.log('✅ Group added successfully')
    } else {
      console.error('❌ Failed to add group')
    }
  }

  const handleRemoveGroup = async (groupId: string) => {
    const success = await removeGroup(groupId)
    
    if (success) {
      console.log('✅ Group removed successfully')
    } else {
      console.error('❌ Failed to remove group')
    }
  }

  const handlePromoteToAdmin = async (groupId: string) => {
    const success = await updateRoleInGroup(
      groupId, 
      IdentityRole.Admin | IdentityRole.Participant
    )
    
    if (success) {
      console.log('✅ Role updated successfully')
    } else {
      console.error('❌ Failed to update role')
    }
  }

  if (loading) return <Spinner />
  if (error) return <Text color="$red11">Error: {error}</Text>

  return (
    <Card padding="$4" gap="$3">
      <H3>User Groups ({groups.length})</H3>
      <Separator />
      
      <YStack gap="$3">
        {groups.map(group => (
          <Card key={group.id} padding="$3" backgroundColor="$color2">
            <YStack gap="$2">
              <Text fontWeight="bold">{group.name}</Text>
              <Text fontSize="$2" color="$color11">ID: {group.id}</Text>
              <Text fontSize="$2" color="$color11">Role: {group.role}</Text>
              
              <YStack gap="$1">
                {isAdminInGroup(group.id) && (
                  <Text fontSize="$2" color="$green10">✓ Admin</Text>
                )}
                {isParticipantInGroup(group.id) && (
                  <Text fontSize="$2" color="$blue10">✓ Participant</Text>
                )}
                {hasRoleInGroup(group.id, IdentityRole.Reader) && (
                  <Text fontSize="$2" color="$color11">✓ Reader</Text>
                )}
              </YStack>

              <YStack gap="$1" marginTop="$2">
                {!isAdminInGroup(group.id) && (
                  <Button 
                    size="$2" 
                    onPress={() => handlePromoteToAdmin(group.id)}
                  >
                    Promote to Admin
                  </Button>
                )}
                <Button 
                  size="$2" 
                  theme="red" 
                  onPress={() => handleRemoveGroup(group.id)}
                >
                  Leave Group
                </Button>
              </YStack>
            </YStack>
          </Card>
        ))}
      </YStack>

      <Button onPress={handleAddGroup}>Add New Group (Example)</Button>
      <Button onPress={refetch}>Refresh</Button>
    </Card>
  )
}

/**
 * Example 4: Complete User Profile Page
 */
export function UserProfilePage({ userEmail }: { userEmail: string }) {
  return (
    <YStack gap="$4" padding="$4" maxWidth={960} alignSelf="center" width="100%">
      <UserIdentityCard userEmail={userEmail} />
      <UserStateManager userEmail={userEmail} />
      <UserGroupsManager userEmail={userEmail} />
    </YStack>
  )
}

/**
 * Example 5: Using with Auth Context
 */
export function UserProfileWithAuth() {
  // Assuming you have an auth context that provides user info
  // const { user } = useAuth()
  
  // For example purposes, using a hardcoded email
  const userEmail = 'user@example.com'

  return <UserProfilePage userEmail={userEmail} />
}

/**
 * Example 6: Conditional Rendering Based on Role
 */
export function AdminOnlyComponent({ userEmail, groupId }: { userEmail: string; groupId: string }) {
  const { groups, isAdminInGroup } = useUserGroups(userEmail)

  const isAdmin = isAdminInGroup(groupId)

  if (!isAdmin) {
    return (
      <Card padding="$4" backgroundColor="$yellow3">
        <Text>You need admin privileges to access this feature</Text>
      </Card>
    )
  }

  return (
    <Card padding="$4" backgroundColor="$green3">
      <Text>Welcome, Admin! You have access to administrative features.</Text>
      {/* Admin-only content here */}
    </Card>
  )
}

/**
 * Example 7: Using Service Directly (without hooks)
 */
export async function directServiceExample() {
  const { appIdentityService } = await import('../services/appIdentityService')
  
  // Get user identity
  const identity = await appIdentityService.getAppIdentity('user@example.com')
  console.log('Identity:', identity)
  
  // Update user group
  const success = await appIdentityService.updateUserGroup(
    'user@example.com',
    'new-group-id'
  )
  console.log('Update success:', success)
  
  // Check if user is admin in a group
  const isAdmin = await appIdentityService.userIsAdminInGroup(
    'user@example.com',
    'group-123'
  )
  console.log('Is admin:', isAdmin)
  
  // Add a new group to user
  await appIdentityService.addUserGroup('user@example.com', {
    id: 'group-456',
    name: 'New Group',
    role: IdentityRole.Participant
  })
  
  // Get all user groups
  const groups = await appIdentityService.getUserGroups('user@example.com')
  console.log('User groups:', groups)
}
