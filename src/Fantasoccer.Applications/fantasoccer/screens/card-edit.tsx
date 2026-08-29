import React, { useState, useCallback, useEffect, useMemo } from 'react'
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
  Button,
  useMedia,
  TextArea,
  Label,
  Switch,
  ScrollView
} from 'tamagui'
import { Save, X, Upload, Image as ImageIcon, AlertCircle } from '@tamagui/lucide-icons-2'
import { useToastController } from '@tamagui/toast'
import { useRoute, useNavigation, RouteProp } from '@react-navigation/native'
import type { RootStackParamList } from '../navigation'
import cardService from 'services/cardService'
import {
  Card as CardType,
  CardType as CardTypeEnum,
  CardCondition,
  CardConditionForPlayers,
  CardConditionSummatory,
  CardConditionForPlayersValues,
  CardConditionSummatoryPlayers,
  getCardTypeLabel
} from 'types/card'
import { FantaSoccerRole } from 'types/team'

export default function CardEditScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'
  const toast = useToastController()
  const navigation = useNavigation()
  const route = useRoute<RouteProp<RootStackParamList, 'CardEdit'>>()

  const cardId = route.params?.cardId || null
  const isEditMode = !!cardId

  // State
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [uploadingImage, setUploadingImage] = useState(false)

  // Form fields - Basic
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [type, setType] = useState<CardTypeEnum>(CardTypeEnum.Strategy)
  const [points, setPoints] = useState('0')
  const [negativePoints, setNegativePoints] = useState('0')
  const [enemyPoints, setEnemyPoints] = useState('0')

  // Flags per abilitare condizioni
  const [hasMyCondition, setHasMyCondition] = useState(false)
  const [hasEnemyCondition, setHasEnemyCondition] = useState(false)

  // Image
  const [selectedImage, setSelectedImage] = useState<File | null>(null)
  const [imagePreview, setImagePreview] = useState<string | null>(null)

  // Load existing card
  useEffect(() => {
    if (isEditMode && cardId) {
      loadCard(cardId)
    }
  }, [cardId, isEditMode])

  const loadCard = async (id: string) => {
    try {
      setLoading(true)
      const card = await cardService.getCardById(id)
      if (card) {
        setName(card.name)
        setDescription(card.description)
        setType(card.type)
        setPoints(card.points.toString())
        setNegativePoints(card.negativePoints.toString())
        setEnemyPoints(card.enemyPoints.toString())
        setHasMyCondition(!!card.myCondition)
        setHasEnemyCondition(!!card.enemyCondition)
        
        // Load existing image
        if (id) {
          setImagePreview(cardService.getCardImageUrl(id))
        }
      }
    } catch (err: any) {
      toast.show('Errore', {
        message: err?.message ?? 'Impossibile caricare la carta',
        variant: 'error'
      })
    } finally {
      setLoading(false)
    }
  }

  // Image handler
  const handleImageSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    // Validate file
    const maxSize = 5 * 1024 * 1024 // 5MB
    if (file.size > maxSize) {
      toast.show('Errore', {
        message: 'Immagine troppo grande. Massimo 5MB',
        variant: 'error'
      })
      return
    }

    const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
    if (!allowedTypes.includes(file.type)) {
      toast.show('Errore', {
        message: 'Formato non supportato. Usa JPEG, PNG, GIF o WebP',
        variant: 'error'
      })
      return
    }

    setSelectedImage(file)

    // Create preview
    const reader = new FileReader()
    reader.onload = (e) => {
      setImagePreview(e.target?.result as string)
    }
    reader.readAsDataURL(file)
  }

  // Validation
  const validate = useCallback((): string | null => {
    if (!name.trim()) return 'Il nome è obbligatorio'
    if (!description.trim()) return 'La descrizione è obbligatoria'
    if (isNaN(parseFloat(points))) return 'I punti devono essere un numero valido'
    if (isNaN(parseFloat(negativePoints))) return 'I punti negativi devono essere un numero valido'
    if (isNaN(parseFloat(enemyPoints))) return 'I punti avversario devono essere un numero valido'
    return null
  }, [name, description, points, negativePoints, enemyPoints])

  // Save handler
  const handleSave = async () => {
    const validationError = validate()
    if (validationError) {
      toast.show('Validazione fallita', {
        message: validationError,
        variant: 'error'
      })
      return
    }

    try {
      setSaving(true)

      // Build card object
      const cardData: Omit<CardType, 'id'> = {
        name: name.trim(),
        description: description.trim(),
        type: type,
        points: parseFloat(points),
        negativePoints: parseFloat(negativePoints),
        enemyPoints: parseFloat(enemyPoints),
        myCondition: hasMyCondition ? buildEmptyCondition() : null,
        enemyCondition: hasEnemyCondition ? buildEmptyCondition() : null
      }

      let savedCard: CardType
      if (isEditMode && cardId) {
        // Update existing
        savedCard = await cardService.updateCard(cardId, { ...cardData, id: cardId })
        toast.show('Successo', {
          message: 'Carta aggiornata con successo',
          variant: 'success'
        })
      } else {
        // Create new
        savedCard = await cardService.createCard(cardData)
        toast.show('Successo', {
          message: 'Carta creata con successo',
          variant: 'success'
        })
      }

      // Upload image if selected
      if (selectedImage && savedCard.id) {
        try {
          setUploadingImage(true)
          await cardService.uploadCardImage(savedCard.id, selectedImage)
          toast.show('Successo', {
            message: 'Immagine caricata con successo',
            variant: 'success'
          })
        } catch (err: any) {
          toast.show('Attenzione', {
            message: 'Carta salvata ma errore nel caricamento immagine: ' + (err?.message ?? 'Errore sconosciuto'),
            variant: 'warning'
          })
        } finally {
          setUploadingImage(false)
        }
      }

      // Navigate back
      navigation.goBack()
    } catch (err: any) {
      toast.show('Errore', {
        message: err?.message ?? 'Impossibile salvare la carta',
        variant: 'error'
      })
    } finally {
      setSaving(false)
    }
  }

  // Helper to build empty condition
  const buildEmptyCondition = (): CardCondition => {
    return {
      rolesInvolved: [FantaSoccerRole.All],
      conditionForPlayer: null,
      condition: null
    }
  }

  return (
    <ScreenContent title={isEditMode ? 'Modifica Carta' : 'Crea Nuova Carta'}>
      <View style={{ flex: 1, width: '100%', alignItems: 'center' }}>
        <ScrollView width="100%" showsVerticalScrollIndicator={false}>
          <YStack
            padding="$4"
            gap="$4"
            width={containerWidth}
            maxWidth="100%"
            alignSelf="center"
          >
            {/* Header */}
            <XStack justifyContent="space-between" alignItems="center" flexWrap="wrap" gap="$3">
              <H3>{isEditMode ? '✏️ Modifica Carta' : '➕ Crea Nuova Carta'}</H3>
              <XStack gap="$2">
                <Button
                  size="$4"
                  variant="outlined"
                  icon={<X size={20} />}
                  onPress={() => navigation.goBack()}
                >
                  Annulla
                </Button>
                <Button
                  size="$4"
                  backgroundColor="$green10"
                  icon={saving || uploadingImage ? <Spinner size="small" color="white" /> : <Save size={20} />}
                  onPress={handleSave}
                  disabled={saving || uploadingImage}
                >
                  {saving ? 'Salvataggio...' : uploadingImage ? 'Upload immagine...' : 'Salva'}
                </Button>
              </XStack>
            </XStack>

            <Separator />

            {loading ? (
              <Card padding="$6" alignItems="center">
                <Spinner size="large" />
                <Text marginTop="$3" color="$color11">Caricamento...</Text>
              </Card>
            ) : (
              <YStack gap="$4">
                {/* Basic Info Section */}
                <Card padding="$4" backgroundColor="$color2">
                  <YStack gap="$3">
                    <H4>📋 Informazioni Base</H4>
                    <Separator />

                    {/* Name */}
                    <YStack gap="$2">
                      <Label htmlFor="name">Nome Carta *</Label>
                      <Input
                        id="name"
                        placeholder="Es: Attacco Fulmineo"
                        value={name}
                        onChangeText={setName}
                        size="$4"
                      />
                    </YStack>

                    {/* Description */}
                    <YStack gap="$2">
                      <Label htmlFor="description">Descrizione *</Label>
                      <TextArea
                        id="description"
                        placeholder="Descrivi l'effetto della carta..."
                        value={description}
                        onChangeText={setDescription}
                        size="$4"
                        numberOfLines={4}
                      />
                    </YStack>

                    {/* Type */}
                    <YStack gap="$2">
                      <Label htmlFor="type">Tipologia *</Label>
                      <XStack gap="$2" flexWrap="wrap">
                        {[
                          CardTypeEnum.Strategy,
                          CardTypeEnum.Goalkeeper,
                          CardTypeEnum.Defensor,
                          CardTypeEnum.Midfielder,
                          CardTypeEnum.Forward,
                          CardTypeEnum.Special
                        ].map((t) => (
                          <Button
                            key={t}
                            size="$3"
                            variant={type === t ? 'outlined' : 'outlined'}
                            backgroundColor={type === t ? '$blue4' : 'transparent'}
                            borderColor={type === t ? '$blue8' : '$borderColor'}
                            onPress={() => setType(t)}
                          >
                            {getCardTypeLabel(t)}
                          </Button>
                        ))}
                      </XStack>
                    </YStack>

                    {/* Points */}
                    <XStack gap="$3" flexWrap="wrap">
                      <YStack flex={1} minWidth={150} gap="$2">
                        <Label htmlFor="points">Punti Positivi *</Label>
                        <Input
                          id="points"
                          placeholder="0"
                          value={points}
                          onChangeText={setPoints}
                          keyboardType="numeric"
                          size="$4"
                        />
                        <Text fontSize="$1" color="$color10">Punti guadagnati se la carta viene soddisfatta</Text>
                      </YStack>
                      <YStack flex={1} minWidth={150} gap="$2">
                        <Label htmlFor="negativePoints">Punti Negativi *</Label>
                        <Input
                          id="negativePoints"
                          placeholder="0"
                          value={negativePoints}
                          onChangeText={setNegativePoints}
                          keyboardType="numeric"
                          size="$4"
                        />
                        <Text fontSize="$1" color="$color10">Punti persi se la carta NON viene soddisfatta</Text>
                      </YStack>
                      <YStack flex={1} minWidth={150} gap="$2">
                        <Label htmlFor="enemyPoints">Punti Avversario *</Label>
                        <Input
                          id="enemyPoints"
                          placeholder="0"
                          value={enemyPoints}
                          onChangeText={setEnemyPoints}
                          keyboardType="numeric"
                          size="$4"
                        />
                        <Text fontSize="$1" color="$color10">Punti dati all&apos;avversario se soddisfatti</Text>
                      </YStack>
                    </XStack>
                  </YStack>
                </Card>

                {/* Image Section */}
                <Card padding="$4" backgroundColor="$color2">
                  <YStack gap="$3">
                    <H4>🖼️ Immagine Carta</H4>
                    <Separator />

                    <YStack gap="$3" alignItems="center">
                      {imagePreview && (
                        <Card
                          width={200}
                          height={200}
                          overflow="hidden"
                          backgroundColor="$color3"
                          borderWidth={2}
                          borderColor="$borderColor"
                        >
                          <img
                            src={imagePreview}
                            alt="Preview"
                            style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                          />
                        </Card>
                      )}

                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/gif,image/webp"
                        onChange={handleImageSelect}
                        style={{ display: 'none' }}
                        id="image-upload"
                      />
                      <label htmlFor="image-upload">
                        <Button
                          size="$4"
                          icon={<Upload size={20} />}
                          onPress={() => document.getElementById('image-upload')?.click()}
                          pointerEvents="none"
                        >
                          {imagePreview ? 'Cambia Immagine' : 'Carica Immagine'}
                        </Button>
                      </label>

                      <Text fontSize="$2" color="$color10">
                        Formati supportati: JPEG, PNG, GIF, WebP (max 5MB)
                      </Text>
                    </YStack>
                  </YStack>
                </Card>

                {/* Conditions Section */}
                <Card padding="$4" backgroundColor="$color2">
                  <YStack gap="$3">
                    <H4>⚙️ Condizioni</H4>
                    <Separator />

                    <Card padding="$3" backgroundColor="$yellow2" borderColor="$yellow8">
                      <XStack gap="$2" alignItems="center">
                        <AlertCircle size={20} color="$yellow11" />
                        <Text fontSize="$3" color="$yellow11" flex={1}>
                          La configurazione avanzata delle condizioni sarà disponibile in una prossima versione.
                          Per ora puoi abilitare/disabilitare la presenza di condizioni.
                        </Text>
                      </XStack>
                    </Card>

                    {/* My Condition Toggle */}
                    <XStack gap="$3" alignItems="center" justifyContent="space-between">
                      <YStack flex={1}>
                        <Text fontSize="$4" fontWeight="600">Condizione Mia Squadra</Text>
                        <Text fontSize="$2" color="$color10">
                          Attiva per aggiungere condizioni sui tuoi giocatori
                        </Text>
                      </YStack>
                      <Switch
                        checked={hasMyCondition}
                        onCheckedChange={setHasMyCondition}
                        size="$4"
                        backgroundColor={hasMyCondition ? '$green9' : '$color6'}
                        borderColor={hasMyCondition ? '$green10' : '$color8'}
                      >
                        <Switch.Thumb transition="quick" backgroundColor="$color12" />
                      </Switch>
                    </XStack>

                    {/* Enemy Condition Toggle */}
                    <XStack gap="$3" alignItems="center" justifyContent="space-between">
                      <YStack flex={1}>
                        <Text fontSize="$4" fontWeight="600">Condizione Squadra Avversaria</Text>
                        <Text fontSize="$2" color="$color10">
                          Attiva per aggiungere condizioni sui giocatori avversari
                        </Text>
                      </YStack>
                      <Switch
                        checked={hasEnemyCondition}
                        onCheckedChange={setHasEnemyCondition}
                        size="$4"
                        backgroundColor={hasEnemyCondition ? '$green9' : '$color6'}
                        borderColor={hasEnemyCondition ? '$green10' : '$color8'}
                      >
                        <Switch.Thumb transition="quick" backgroundColor="$color12" />
                      </Switch>
                    </XStack>
                  </YStack>
                </Card>

                {/* Save Button (mobile) */}
                {media.sm && (
                  <Button
                    size="$5"
                    backgroundColor="$green10"
                    icon={saving || uploadingImage ? <Spinner size="small" color="white" /> : <Save size={24} />}
                    onPress={handleSave}
                    disabled={saving || uploadingImage}
                  >
                    {saving ? 'Salvataggio...' : uploadingImage ? 'Upload immagine...' : 'Salva Carta'}
                  </Button>
                )}
              </YStack>
            )}
          </YStack>
        </ScrollView>
      </View>
    </ScreenContent>
  )
}
