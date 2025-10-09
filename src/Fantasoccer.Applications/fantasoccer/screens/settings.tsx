import { useCallback, useMemo, useState } from 'react'
import { AlertTriangle, Check, Upload, User, Users } from '@tamagui/lucide-icons'
import {
  Button,
  Card,
  H3,
  H4,
  Input,
  Paragraph,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack,
  useMedia,
} from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { TeamLogo } from 'components/team/TeamLogo'
import { useApp } from 'providers/AppProvider'
import { useAuth } from 'providers/AuthProvider'
import { useSettings } from 'hooks/useSettings'
import { useGroup } from 'hooks/useGroup'
import { getActualFantacalcioYear } from 'utils/timezoneHelpers'
import * as ImagePicker from 'expo-image-picker'

export default function SettingsScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 800 : '100%'

  const { currentGroup } = useApp()
  const { user } = useAuth()
  const userEmail = user?.email ?? null
  const groupId = currentGroup?.id ?? null

  // Get current year
  const currentYear = getActualFantacalcioYear()

  // Get user's team info from the group
  const { group, loading: groupLoading, refetch: refetchGroup } = useGroup(groupId)

  // Find user's team in the current year's baskets
  const userTeamInfo = useMemo(() => {
    if (!group || !userEmail) return null

    // Search through all baskets for the user's team
    for (const basket of group.baskets) {
      const yearData = basket.years?.find((y) => y.year === currentYear)
      if (!yearData) continue

      const team = yearData.teams?.find(
        (t) => t.owner === userEmail || t.additionalOwners?.includes(userEmail)
      )

      if (team) {
        return {
          basketId: basket.id,
          teamName: team.name,
          owner: team.owner,
          year: currentYear,
        }
      }
    }

    return null
  }, [group, userEmail, currentYear])

  const {
    uploadingLogo,
    uploadLogoError,
    uploadLogo,
    changingTeamName,
    changeTeamNameError,
    changeTeamNameMessages,
    changeTeamNameProgress,
    changeTeamName,
    updatingUsername,
    updateUsernameError,
    updateUsername,
  } = useSettings()

  // Local state for inputs
  const [newTeamName, setNewTeamName] = useState('')
  const [newUsername, setNewUsername] = useState('')
  const [teamNameSuccess, setTeamNameSuccess] = useState(false)
  const [usernameSuccess, setUsernameSuccess] = useState(false)
  const [logoSuccess, setLogoSuccess] = useState(false)

  // Handle logo upload
  const handleLogoUpload = useCallback(async () => {
    if (!groupId || !userTeamInfo) return

    try {
      // Request permission to access media library
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync()
      
      if (status !== 'granted') {
        alert('Permesso negato per accedere alla galleria')
        return
      }

      // Launch image picker
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        aspect: [1, 1],
        quality: 0.8,
      })

      if (result.canceled) return

      const imageUri = result.assets[0].uri

      // Convert image to blob for web, or use file for native
      let imageBlob: Blob | File

      if (typeof window !== 'undefined' && 'fetch' in window) {
        // Web: fetch the image and convert to blob
        const response = await fetch(imageUri)
        imageBlob = await response.blob()
      } else {
        // React Native: create a file-like object
        const filename = imageUri.split('/').pop() || 'logo.png'
        imageBlob = {
          uri: imageUri,
          name: filename,
          type: 'image/png',
        } as any
      }

      const success = await uploadLogo({
        groupId,
        year: userTeamInfo.year,
        owner: userTeamInfo.owner,
        basketId: userTeamInfo.basketId,
        image: imageBlob,
      })

      if (success) {
        setLogoSuccess(true)
        setTimeout(() => setLogoSuccess(false), 3000)
      }
    } catch (error) {
      console.error('Error selecting/uploading logo:', error)
    }
  }, [groupId, userTeamInfo, uploadLogo])

  // Handle team name change
  const handleChangeTeamName = useCallback(async () => {
    if (!groupId || !userTeamInfo || !newTeamName.trim()) return

    const success = await changeTeamName({
      groupId,
      owner: userTeamInfo.owner,
      newName: newTeamName.trim(),
      basketId: userTeamInfo.basketId,
    })

    if (success) {
      // Refresh group so userTeamInfo picks up the new name
      try {
        await refetchGroup()
      } catch (e) {
        // ignore refetch errors
      }
      setTeamNameSuccess(true)
      setNewTeamName('')
      setTimeout(() => setTeamNameSuccess(false), 3000)
    }
  }, [groupId, userTeamInfo, newTeamName, changeTeamName, refetchGroup])

  // Handle username update
  const handleUpdateUsername = useCallback(async () => {
    if (!newUsername.trim()) return

    const success = await updateUsername(newUsername.trim())

    if (success) {
      setUsernameSuccess(true)
      setNewUsername('')
      setTimeout(() => setUsernameSuccess(false), 3000)
    }
  }, [newUsername, updateUsername])

  const isLoading = groupLoading

  return (
    <ScreenContent path="screens/settings.tsx" title="Impostazioni">
      <YStack
        width={containerWidth}
        maxWidth={800}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
        paddingBottom="$8"
      >
        {isLoading ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor"
          >
            <XStack alignItems="center" gap="$3" justifyContent="center">
              <Spinner size="small" color="$blue10" />
              <Text color="$color10">Caricamento impostazioni...</Text>
            </XStack>
          </Card>
        ) : !currentGroup ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor"
          >
            <Text color="$color12" fontWeight="600" marginBottom="$2">
              Nessun gruppo selezionato
            </Text>
            <Paragraph color="$color10">
              Seleziona un gruppo dalla barra laterale per gestire le impostazioni.
            </Paragraph>
          </Card>
        ) : !userTeamInfo ? (
          <Card
            padding="$4"
            backgroundColor="$yellow3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$yellow7"
          >
            <XStack alignItems="center" gap="$2">
              <AlertTriangle size="$1.5" color="$yellow10" />
              <Text color="$yellow11" fontWeight="600">
                Nessuna squadra trovata
              </Text>
            </XStack>
            <Paragraph marginTop="$2" color="$yellow11">
              Non hai una squadra registrata in questo gruppo per l'anno corrente ({currentYear}).
            </Paragraph>
          </Card>
        ) : (
          <YStack gap="$6">
            {/* Team Logo Section */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
            >
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <Upload size="$1.5" color="$blue10" />
                  <H3 color="$color12">Logo Squadra</H3>
                </XStack>

                <Separator />

                <YStack gap="$3" alignItems="center">
                  {groupId && (
                    <TeamLogo
                      groupId={groupId}
                      year={userTeamInfo.year.toString()}
                      ownerEmail={userTeamInfo.owner}
                      size={media.gtSm ? 120 : 96}
                    />
                  )}

                  <Text fontSize="$5" color="$color11" textAlign="center">
                    {userTeamInfo.teamName}
                  </Text>

                  <Button
                    icon={Upload}
                    onPress={handleLogoUpload}
                    disabled={uploadingLogo}
                    theme="blue"
                    size="$4"
                  >
                    {uploadingLogo ? 'Caricamento...' : 'Carica Nuovo Logo'}
                  </Button>

                  {uploadLogoError && (
                    <XStack
                      padding="$2"
                      backgroundColor="$red3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center"
                    >
                      <AlertTriangle size="$1" color="$red10" />
                      <Text color="$red11" fontSize="$2">
                        {uploadLogoError}
                      </Text>
                    </XStack>
                  )}

                  {logoSuccess && (
                    <XStack
                      padding="$2"
                      backgroundColor="$green3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center"
                    >
                      <Check size="$1" color="$green10" />
                      <Text color="$green11" fontSize="$2">
                        Logo caricato con successo!
                      </Text>
                    </XStack>
                  )}
                </YStack>
              </YStack>
            </Card>

            {/* Team Name Section */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
            >
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <Users size="$1.5" color="$blue10" />
                  <H3 color="$color12">Nome Squadra</H3>
                </XStack>

                <Separator />

                <YStack gap="$3">
                  <YStack gap="$2">
                    <Text fontSize="$3" color="$color11">
                      Nome attuale: <Text fontWeight="600">{userTeamInfo.teamName}</Text>
                    </Text>
                    <Text fontSize="$2" color="$color10">
                      Il nome deve essere tra 3 e 30 caratteri e unico nel gruppo.
                    </Text>
                  </YStack>

                  <XStack gap="$2" flexWrap="wrap">
                    <Input
                      flex={1}
                      minWidth={200}
                      placeholder="Nuovo nome squadra"
                      value={newTeamName}
                      onChangeText={setNewTeamName}
                      disabled={changingTeamName}
                    />
                    <Button
                      icon={Check}
                      onPress={handleChangeTeamName}
                      disabled={changingTeamName || !newTeamName.trim()}
                      theme="blue"
                    >
                      {changingTeamName ? 'Aggiornamento...' : 'Cambia Nome'}
                    </Button>
                  </XStack>

                  {changingTeamName && changeTeamNameProgress && (
                    <XStack alignItems="center" gap="$2" marginTop="$2">
                      <Spinner size="small" color="$blue10" />
                      <Text color="$color11">{changeTeamNameProgress}</Text>
                    </XStack>
                  )}

                  {changeTeamNameError && (
                    <XStack
                      padding="$2"
                      backgroundColor="$red3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center"
                    >
                      <AlertTriangle size="$1" color="$red10" />
                      <Text color="$red11" fontSize="$2">
                        {changeTeamNameError}
                      </Text>
                    </XStack>
                  )}

                  {teamNameSuccess && (
                    <XStack
                      padding="$2"
                      backgroundColor="$green3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center"
                    >
                      <Check size="$1" color="$green10" />
                      <Text color="$green11" fontSize="$2">
                        Nome squadra aggiornato con successo!
                      </Text>
                    </XStack>
                  )}

                  {/* Durante l'operazione mostriamo solo l'ultimo messaggio di progresso (gestito da changeTeamNameProgress)
                      Dopo il completamento possiamo mostrare l'ultimo messaggio ricevuto se necessario. */}
                  {!changingTeamName && changeTeamNameMessages.length > 0 && !changeTeamNameError && (
                    <YStack
                      padding="$3"
                      backgroundColor="$blue3"
                      borderRadius="$3"
                      gap="$1"
                    >
                      <Text color="$blue11" fontSize="$2">
                        {changeTeamNameMessages[changeTeamNameMessages.length - 1]}
                      </Text>
                    </YStack>
                  )}
                </YStack>
              </YStack>
            </Card>

            {/* Username Section */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
            >
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <User size="$1.5" color="$blue10" />
                  <H3 color="$color12">Username</H3>
                </XStack>

                <Separator />

                <YStack gap="$3">
                  <Text fontSize="$2" color="$color10">
                    Lo username deve essere tra 3 e 20 caratteri e può contenere solo lettere, numeri e
                    underscore (_).
                  </Text>

                  <XStack gap="$2" flexWrap="wrap">
                    <Input
                      flex={1}
                      minWidth={200}
                      placeholder="Nuovo username"
                      value={newUsername}
                      onChangeText={setNewUsername}
                      disabled={updatingUsername}
                      autoCapitalize="none"
                      autoCorrect={false}
                    />
                    <Button
                      icon={Check}
                      onPress={handleUpdateUsername}
                      disabled={updatingUsername || !newUsername.trim()}
                      theme="blue"
                    >
                      {updatingUsername ? 'Aggiornamento...' : 'Cambia Username'}
                    </Button>
                  </XStack>

                  {updateUsernameError && (
                    <XStack
                      padding="$2"
                      backgroundColor="$red3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center"
                    >
                      <AlertTriangle size="$1" color="$red10" />
                      <Text color="$red11" fontSize="$2">
                        {updateUsernameError}
                      </Text>
                    </XStack>
                  )}

                  {usernameSuccess && (
                    <XStack
                      padding="$2"
                      backgroundColor="$green3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center"
                    >
                      <Check size="$1" color="$green10" />
                      <Text color="$green11" fontSize="$2">
                        Username aggiornato con successo!
                      </Text>
                    </XStack>
                  )}
                </YStack>
              </YStack>
            </Card>
          </YStack>
        )}
      </YStack>
    </ScreenContent>
  )
}
