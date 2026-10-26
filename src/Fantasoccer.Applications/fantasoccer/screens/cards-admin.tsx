import React, { useMemo, useState, useCallback } from 'react'
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
  Select,
  Sheet
} from 'tamagui'
import { Search, Plus, Edit3, Trash2, Image as ImageIcon, Sparkles } from '@tamagui/lucide-icons-2'
import { useToastController } from '@tamagui/toast'
import { useFocusEffect, useNavigation } from '@react-navigation/native'
import type { NavigationProp } from '../navigation'
import cardService from 'services/cardService'
import { Card as CardType, CardType as CardTypeEnum, getCardTypeLabel, getCardTypeColor, getCardTypeBgColor } from 'types/card'

export default function CardsAdminScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 1200 : '100%'
  const toast = useToastController()
  const navigation = useNavigation<NavigationProp>()

  // State
  const [cards, setCards] = useState<CardType[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [typeFilter, setTypeFilter] = useState<CardTypeEnum | 'all'>('all')
  const [refreshing, setRefreshing] = useState(false)

  // Load cards
  const loadCards = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const fetchedCards = await cardService.getAllCards()
      setCards(fetchedCards)
    } catch (err: any) {
      setError(err?.message ?? 'Errore nel caricamento delle carte')
      toast.show('Errore', {
        message: 'Impossibile caricare le carte',
        variant: 'error'
      })
    } finally {
      setLoading(false)
    }
  }, [toast])

  // Initial load
  useFocusEffect(useCallback(() => { void loadCards() }, [loadCards]))

  // Refresh handler
  const handleRefresh = useCallback(async () => {
    setRefreshing(true)
    await loadCards()
    setRefreshing(false)
  }, [loadCards])

  // Filter cards
  const filteredCards = useMemo(() => {
    let result = cards

    // Filter by search query
    if (searchQuery) {
      const query = searchQuery.toLowerCase()
      result = result.filter(card =>
        card.name.toLowerCase().includes(query) ||
        card.description.toLowerCase().includes(query)
      )
    }

    // Filter by type
    if (typeFilter !== 'all') {
      result = result.filter(card => card.type === typeFilter)
    }

    return result
  }, [cards, searchQuery, typeFilter])

  // Stats
  const stats = useMemo(() => {
    return {
      total: cards.length,
      strategy: cards.filter(c => c.type === CardTypeEnum.Strategy).length,
      goalkeeper: cards.filter(c => c.type === CardTypeEnum.Goalkeeper).length,
      defensor: cards.filter(c => c.type === CardTypeEnum.Defensor).length,
      midfielder: cards.filter(c => c.type === CardTypeEnum.Midfielder).length,
      forward: cards.filter(c => c.type === CardTypeEnum.Forward).length,
      special: cards.filter(c => c.type === CardTypeEnum.Special).length
    }
  }, [cards])

  // Delete card handler
  const handleDeleteCard = useCallback(async (cardId: string, cardName: string) => {
    if (!confirm(`Sei sicuro di voler eliminare la carta "${cardName}"?`)) {
      return
    }

    try {
      await cardService.deleteCard(cardId, cards.find(card => card.id === cardId)?.version)
      toast.show('Successo', {
        message: 'Carta eliminata con successo',
        variant: 'success'
      })
      await loadCards()
    } catch (err: any) {
      toast.show('Errore', {
        message: err?.message ?? 'Impossibile eliminare la carta',
        variant: 'error'
      })
    }
  }, [toast, loadCards, cards])

  return (
    <ScreenContent title="Gestione Carte">
      <View style={{ flex: 1, width: '100%', alignItems: 'center' }}>
        <YStack
          padding="$4"
          gap="$4"
          width={containerWidth}
          maxWidth="100%"
        >
          {/* Header */}
          <XStack justifyContent="space-between" alignItems="center" flexWrap="wrap" gap="$3">
            <YStack gap="$2">
              <H3>🃏 Gestione Carte Fantacalcio</H3>
              <Text fontSize="$3" color="$color11">
                Crea e gestisci le carte speciali del gioco
              </Text>
            </YStack>
            <Button
              size="$4"
              backgroundColor="$green10"
              icon={<Plus size={20} />}
              onPress={() => navigation.navigate('CardEdit', { cardId: null })}
            >
              Crea Nuova Carta
            </Button>
          </XStack>

          <Separator />

          {/* Stats Cards */}
          <XStack gap="$3" flexWrap="wrap">
            <Card flex={1} minWidth={150} padding="$3" backgroundColor="$blue2" borderColor="$blue8">
              <YStack gap="$2">
                <Text fontSize="$2" color="$blue11">Totale Carte</Text>
                <Text fontSize="$8" fontWeight="bold" color="$blue11">{stats.total}</Text>
              </YStack>
            </Card>
            <Card flex={1} minWidth={150} padding="$3" backgroundColor="$green2" borderColor="$green8">
              <YStack gap="$2">
                <Text fontSize="$2" color="$green11">Strategia</Text>
                <Text fontSize="$6" fontWeight="bold" color="$green11">{stats.strategy}</Text>
              </YStack>
            </Card>
            <Card flex={1} minWidth={150} padding="$3" backgroundColor="$yellow2" borderColor="$yellow8">
              <YStack gap="$2">
                <Text fontSize="$2" color="$yellow11">Portiere</Text>
                <Text fontSize="$6" fontWeight="bold" color="$yellow11">{stats.goalkeeper}</Text>
              </YStack>
            </Card>
            <Card flex={1} minWidth={150} padding="$3" backgroundColor="$purple2" borderColor="$purple8">
              <YStack gap="$2">
                <Text fontSize="$2" color="$purple11">Speciali</Text>
                <Text fontSize="$6" fontWeight="bold" color="$purple11">{stats.special}</Text>
              </YStack>
            </Card>
          </XStack>

          {/* Filters */}
          <Card padding="$3" backgroundColor="$color2">
            <YStack gap="$3">
              <H4>🔍 Filtri</H4>
              <XStack gap="$3" flexWrap="wrap">
                {/* Search */}
                <YStack flex={1} minWidth={250} gap="$2">
                  <Text fontSize="$2" color="$color11">Cerca per nome</Text>
                  <Input
                    placeholder="Nome o descrizione..."
                    value={searchQuery}
                    onChangeText={setSearchQuery}
                    size="$3"
                  />
                </YStack>

                {/* Type filter */}
                <YStack flex={1} minWidth={200} gap="$2">
                  <Text fontSize="$2" color="$color11">Tipologia</Text>
                  <Select
                    value={typeFilter.toString()}
                    onValueChange={(value) => setTypeFilter(value === 'all' ? 'all' : parseInt(value) as CardTypeEnum)}
                    size="$3"
                  >
                    <Select.Trigger>
                      <Select.Value placeholder="Tutte" />
                    </Select.Trigger>
                    <Select.Content>
                      <Select.ScrollUpButton />
                      <Select.Viewport>
                        <Select.Item index={0} value="all">
                          <Select.ItemText>Tutte le tipologie</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={1} value={CardTypeEnum.Strategy.toString()}>
                          <Select.ItemText>{getCardTypeLabel(CardTypeEnum.Strategy)}</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={2} value={CardTypeEnum.Goalkeeper.toString()}>
                          <Select.ItemText>{getCardTypeLabel(CardTypeEnum.Goalkeeper)}</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={3} value={CardTypeEnum.Defensor.toString()}>
                          <Select.ItemText>{getCardTypeLabel(CardTypeEnum.Defensor)}</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={4} value={CardTypeEnum.Midfielder.toString()}>
                          <Select.ItemText>{getCardTypeLabel(CardTypeEnum.Midfielder)}</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={5} value={CardTypeEnum.Forward.toString()}>
                          <Select.ItemText>{getCardTypeLabel(CardTypeEnum.Forward)}</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={6} value={CardTypeEnum.Special.toString()}>
                          <Select.ItemText>{getCardTypeLabel(CardTypeEnum.Special)}</Select.ItemText>
                        </Select.Item>
                      </Select.Viewport>
                      <Select.ScrollDownButton />
                    </Select.Content>
                  </Select>
                </YStack>

                {/* Refresh button */}
                <Button
                  size="$3"
                  onPress={handleRefresh}
                  disabled={refreshing}
                  marginTop="auto"
                >
                  {refreshing ? <Spinner size="small" /> : 'Aggiorna'}
                </Button>
              </XStack>
            </YStack>
          </Card>

          {/* Loading state */}
          {loading && (
            <Card padding="$6" alignItems="center">
              <Spinner size="large" />
              <Text marginTop="$3" color="$color11">Caricamento carte...</Text>
            </Card>
          )}

          {/* Error state */}
          {error && !loading && (
            <Card padding="$4" backgroundColor="$red2" borderColor="$red8">
              <Text color="$red11">{error}</Text>
            </Card>
          )}

          {/* Cards list */}
          {!loading && !error && (
            <>
              <XStack justifyContent="space-between" alignItems="center">
                <Text fontSize="$4" fontWeight="600" color="$color12">
                  {filteredCards.length} {filteredCards.length === 1 ? 'carta trovata' : 'carte trovate'}
                </Text>
              </XStack>

              {filteredCards.length === 0 ? (
                <Card padding="$6" alignItems="center">
                  <Sparkles size={48} color="$color9" />
                  <Text marginTop="$3" color="$color11">Nessuna carta trovata</Text>
                </Card>
              ) : (
                <YStack gap="$3">
                  {filteredCards.map((card) => (
                    <Card key={card.id} padding="$4" backgroundColor="$background" borderWidth={2} borderColor="$borderColor">
                      <YStack gap="$3">
                        {/* Card header */}
                        <XStack justifyContent="space-between" alignItems="flex-start" gap="$3">
                          <YStack flex={1} gap="$2">
                            <XStack gap="$2" alignItems="center" flexWrap="wrap">
                              <Text fontSize="$6" fontWeight="bold" color="$color12">
                                {card.name}
                              </Text>
                              <XStack
                                paddingHorizontal="$2"
                                paddingVertical="$1"
                                backgroundColor={getCardTypeBgColor(card.type)}
                                borderRadius="$2"
                              >
                                <Text
                                  fontSize="$2"
                                  fontWeight="600"
                                  color={getCardTypeColor(card.type)}
                                >
                                  {getCardTypeLabel(card.type)}
                                </Text>
                              </XStack>
                            </XStack>
                            <Text fontSize="$3" color="$color11">
                              {card.description}
                            </Text>
                          </YStack>

                          {/* Actions */}
                          <XStack gap="$2">
                            <Button
                              size="$3"
                              circular
                              backgroundColor="$blue4"
                              icon={<Edit3 size={16} color="$blue11" />}
                              onPress={() => navigation.navigate('CardEdit', { cardId: card.id })}
                            />
                            <Button
                              size="$3"
                              circular
                              backgroundColor="$purple4"
                              icon={<ImageIcon size={16} color="$purple11" />}
                              onPress={() => {
                                // TODO: Open image upload modal
                                toast.show('Coming Soon', {
                                  message: 'Funzionalità di upload immagine in arrivo',
                                  variant: 'info'
                                })
                              }}
                            />
                            <Button
                              size="$3"
                              circular
                              backgroundColor="$red4"
                              icon={<Trash2 size={16} color="$red11" />}
                              onPress={() => handleDeleteCard(card.id, card.name)}
                            />
                          </XStack>
                        </XStack>

                        <Separator />

                        {/* Card details */}
                        <XStack gap="$4" flexWrap="wrap">
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Punti</Text>
                            <Text fontSize="$5" fontWeight="600" color="$green11">
                              +{card.points}
                            </Text>
                          </YStack>
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Punti Negativi</Text>
                            <Text fontSize="$5" fontWeight="600" color="$orange11">
                              {card.negativePoints > 0 ? `-${card.negativePoints}` : card.negativePoints}
                            </Text>
                          </YStack>
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Punti Avversario</Text>
                            <Text fontSize="$5" fontWeight="600" color="$red11">
                              {card.enemyPoints > 0 ? `+${card.enemyPoints}` : card.enemyPoints}
                            </Text>
                          </YStack>
                          {card.myCondition && (
                            <YStack gap="$1">
                              <Text fontSize="$2" color="$color10">Condizione Mia</Text>
                              <Text fontSize="$4" color="$blue11">
                                ✓ Configurata
                              </Text>
                            </YStack>
                          )}
                          {card.enemyCondition && (
                            <YStack gap="$1">
                              <Text fontSize="$2" color="$color10">Condizione Avversario</Text>
                              <Text fontSize="$4" color="$orange11">
                                ✓ Configurata
                              </Text>
                            </YStack>
                          )}
                        </XStack>
                      </YStack>
                    </Card>
                  ))}
                </YStack>
              )}
            </>
          )}
        </YStack>
      </View>
    </ScreenContent>
  )
}
