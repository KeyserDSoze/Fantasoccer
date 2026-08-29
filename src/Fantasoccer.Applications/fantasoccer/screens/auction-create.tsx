// Auction Create Screen - Create new auction and share link
import { useState, useCallback } from 'react'
import { View, ScrollView, Share, Linking, Platform } from 'react-native'
import { Button, Card, Input, Separator, Text, XStack, YStack, useMedia, RadioGroup, Label } from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { auctionService } from 'services/auctionService'
import { 
  AuctionType, 
  AuctionKind, 
  AuctionTypeLabels, 
  AuctionTypeDescriptions,
  AuctionKindLabels,
  AuctionKindDescriptions
} from 'types/auction'
import { Gavel, Share2, Play, Copy, Check } from '@tamagui/lucide-icons-2'
import { getWebUrl, toWebPath } from 'configurations/webConfig'

export default function AuctionCreateScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  const [selectedType, setSelectedType] = useState<AuctionType>(AuctionType.Normal)
  const [selectedKind, setSelectedKind] = useState<AuctionKind>(AuctionKind.Starting)
  const [auctionId, setAuctionId] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  // Create auction
  const handleCreateAuction = useCallback(async () => {
    setLoading(true)
    setError(null)
    setSuccess(null)
    setAuctionId(null)

    try {
      const id = await auctionService.createAuction(selectedType, selectedKind)
      
      if (!id || id.trim() === '') {
        setError('Non sei autorizzato a creare l\'asta. Solo gli admin/super admin possono crearla per la lega principale del gruppo corrente.')
        return
      }

      setAuctionId(id)
      setSuccess('Asta creata con successo!')
    } catch (err) {
      setError(`Errore nella creazione dell'asta: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [selectedType, selectedKind])

  // Generate auction URL
  const getAuctionUrl = useCallback((id: string): string => {
    if (Platform.OS === 'web') {
      return getWebUrl(`/auction/${id}`)
    }
    return `https://fanta.zone/auction/${id}`
  }, [])

  // Copy link to clipboard
  const handleCopyLink = useCallback(async () => {
    if (!auctionId) return

    const url = getAuctionUrl(auctionId)
    
    if (Platform.OS === 'web') {
      await navigator.clipboard.writeText(url)
    } else {
      // React Native doesn't have clipboard in core, would need @react-native-clipboard/clipboard
      // For now, show the URL
    }

    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }, [auctionId, getAuctionUrl])

  // Share via WhatsApp
  const handleShareWhatsApp = useCallback(async () => {
    if (!auctionId) return

    const url = getAuctionUrl(auctionId)
    const message = `🎯 Unisciti all'asta! ${url}`

    if (Platform.OS === 'web') {
      const whatsappUrl = `https://wa.me/?text=${encodeURIComponent(message)}`
      window.open(whatsappUrl, '_blank')
    } else {
      const whatsappUrl = `whatsapp://send?text=${encodeURIComponent(message)}`
      const canOpen = await Linking.canOpenURL(whatsappUrl)
      
      if (canOpen) {
        await Linking.openURL(whatsappUrl)
      } else {
        // Fallback to generic share
        await handleShare()
      }
    }
  }, [auctionId, getAuctionUrl])

  // Generic share
  const handleShare = useCallback(async () => {
    if (!auctionId) return

    const url = getAuctionUrl(auctionId)
    const message = `🎯 Unisciti all'asta!`

    try {
      await Share.share({
        message: `${message}\n${url}`,
        url: url,
        title: 'Asta Fantasoccer'
      })
    } catch (err) {
      console.error('Error sharing:', err)
    }
  }, [auctionId, getAuctionUrl])

  // Start auction (navigate to live page)
  const handleStartAuction = useCallback(() => {
    if (!auctionId) return
    
    if (Platform.OS === 'web') {
      window.location.href = toWebPath(`/auction/${auctionId}`)
    } else {
      // React Navigation would handle this
      // navigation.navigate('AuctionLive', { auctionId })
    }
  }, [auctionId])

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/auction-create.tsx" title="Crea Asta">
        <ScrollView>
          <YStack padding="$4" gap="$6" alignItems="center">
            <YStack width={containerWidth} gap="$4">
              
              {/* Header Info */}
              <Card padding="$4" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8">
                <YStack gap="$2">
                  <XStack gap="$2" alignItems="center">
                    <Gavel size={24} color="$blue11" />
                    <Text fontSize="$5" fontWeight="700" color="$blue11">
                      Crea Nuova Asta
                    </Text>
                  </XStack>
                  <Text fontSize="$3" color="$blue10">
                    Solo admin e super admin possono creare aste per la lega principale del gruppo corrente.
                  </Text>
                </YStack>
              </Card>

              {/* Messages */}
              {error && (
                <Card padding="$3" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
                  <Text color="$red11" fontSize="$3">{error}</Text>
                </Card>
              )}
              {success && (
                <Card padding="$3" backgroundColor="$green2" borderColor="$green8" borderWidth="$0.5">
                  <Text color="$green11" fontSize="$3">{success}</Text>
                </Card>
              )}

              {/* Auction Type Selection */}
              {!auctionId && (
                <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                  <YStack gap="$4">
                    
                    {/* Auction Kind Selection */}
                    <YStack gap="$3">
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        Tipologia Asta
                      </Text>

                      <RadioGroup
                        value={selectedKind.toString()}
                        onValueChange={(val) => setSelectedKind(Number(val) as AuctionKind)}
                        gap="$3"
                      >
                        {Object.values(AuctionKind)
                          .filter(v => typeof v === 'number')
                          .map((kind) => (
                            <XStack key={kind} gap="$3" alignItems="flex-start">
                              <RadioGroup.Item value={kind.toString()} id={`kind-${kind}`} size="$4">
                                <RadioGroup.Indicator />
                              </RadioGroup.Item>
                              <YStack flex={1} gap="$1">
                                <Label htmlFor={`kind-${kind}`} fontSize="$4" fontWeight="600" color="$color11">
                                  {AuctionKindLabels[kind as AuctionKind]}
                                </Label>
                                <Text fontSize="$2" color="$color10">
                                  {AuctionKindDescriptions[kind as AuctionKind]}
                                </Text>
                              </YStack>
                            </XStack>
                          ))}
                      </RadioGroup>
                    </YStack>

                    <Separator />

                    {/* Auction Type Selection */}
                    <YStack gap="$3">
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        Modalità di Ordinamento
                      </Text>

                      <RadioGroup
                        value={selectedType.toString()}
                        onValueChange={(val) => setSelectedType(Number(val) as AuctionType)}
                        gap="$3"
                      >
                        {Object.values(AuctionType)
                          .filter(v => typeof v === 'number')
                          .map((type) => (
                            <XStack key={type} gap="$3" alignItems="flex-start">
                              <RadioGroup.Item value={type.toString()} id={`type-${type}`} size="$4">
                                <RadioGroup.Indicator />
                              </RadioGroup.Item>
                              <YStack flex={1} gap="$1">
                                <Label htmlFor={`type-${type}`} fontSize="$4" fontWeight="600" color="$color11">
                                  {AuctionTypeLabels[type as AuctionType]}
                                </Label>
                                <Text fontSize="$2" color="$color10">
                                  {AuctionTypeDescriptions[type as AuctionType]}
                                </Text>
                              </YStack>
                            </XStack>
                          ))}
                      </RadioGroup>
                    </YStack>

                    <Button
                      icon={Gavel}
                      onPress={handleCreateAuction}
                      disabled={loading}
                      theme="blue"
                      size="$5"
                    >
                      {loading ? 'Creazione in corso...' : 'Crea Asta'}
                    </Button>
                  </YStack>
                </Card>
              )}

              {/* Auction Created - Share & Start */}
              {auctionId && (
                <>
                  <Separator />

                  <Card padding="$4" backgroundColor="$green2" borderRadius="$4" borderWidth="$0.5" borderColor="$green8">
                    <YStack gap="$4">
                      <Text fontSize="$5" fontWeight="700" color="$green11">
                        ✅ Asta Creata!
                      </Text>

                      <YStack gap="$2">
                        <Text fontSize="$3" fontWeight="600" color="$green11">
                          ID Asta:
                        </Text>
                        <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
                          <Text fontSize="$3" color="$color11" selectable>
                            {auctionId}
                          </Text>
                        </Card>
                      </YStack>

                      <YStack gap="$2">
                        <Text fontSize="$3" fontWeight="600" color="$green11">
                          Link Asta:
                        </Text>
                        <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
                          <Text fontSize="$2" color="$color11" selectable>
                            {getAuctionUrl(auctionId)}
                          </Text>
                        </Card>
                      </YStack>
                    </YStack>
                  </Card>

                  {/* Action Buttons */}
                  <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                    <YStack gap="$3">
                      <Text fontSize="$4" fontWeight="600" color="$color11">
                        Condividi l&apos;asta con i partecipanti
                      </Text>

                      <Button
                        icon={copied ? Check : Copy}
                        onPress={handleCopyLink}
                        theme={copied ? "green" : undefined}
                        variant="outlined"
                      >
                        {copied ? 'Link Copiato!' : 'Copia Link'}
                      </Button>

                      <Button
                        icon={Share2}
                        onPress={handleShareWhatsApp}
                        theme="green"
                        variant="outlined"
                      >
                        Condividi su WhatsApp
                      </Button>

                      <Button
                        icon={Share2}
                        onPress={handleShare}
                        theme="blue"
                        variant="outlined"
                      >
                        Condividi
                      </Button>

                      <Separator />

                      <Button
                        icon={Play}
                        onPress={handleStartAuction}
                        theme="orange"
                        size="$5"
                      >
                        Avvia Asta
                      </Button>
                    </YStack>
                  </Card>

                  {/* Info */}
                  <Card padding="$3" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8">
                    <Text fontSize="$2" color="$blue10">
                      💡 <Text fontWeight="600">Suggerimento:</Text> Condividi il link con tutti i partecipanti prima di avviare l&apos;asta.
                      Potranno unirsi in qualsiasi momento durante l&apos;asta.
                    </Text>
                  </Card>
                </>
              )}
            </YStack>
          </YStack>
        </ScrollView>
      </ScreenContent>
    </View>
  )
}
