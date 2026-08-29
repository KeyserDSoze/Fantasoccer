import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, Info } from '@tamagui/lucide-icons-2'
import {
  Card,
  H3,
  H4,
  Paragraph,
  Text,
  XStack,
  YStack,
  Separator,
  useMedia
} from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { Role } from 'types/group'
import { PlayerCard } from 'components/lineup/PlayerCard'
import { GoodDefenseIcon, GoodPeopleIcon, OwnGoalIcon } from 'components/icons/BonusIcons'
import { IconFactory } from 'components/lineup/icons'
import { type LivePlayer } from 'types/game'
import { Behaviour } from 'types/serieA'
import { Chance, ChanceType as ChanceTypeEnum } from 'types/chance'
import { FantaSoccerRole, PlayerInTeamStatus } from 'types/team'

// Dati di esempio per PlayerCard con voto (post-partita)
const createExamplePlayerWithVote = (): LivePlayer => ({
  current: {
    name: 'Destro',
    team: { name: 'Roma', id: 'roma-id' } as any,
    role: Role.Forward,
    active: true,
    visible: true,
    position: 0,
    price: 10,
    status: PlayerInTeamStatus.Active,
    isActive: true
  },
  currentPosition: FantaSoccerRole.Forward,
  vote: {
    role: Role.Forward,
    value: 7.5,
    isFinal: true,
    hasVote: true,
    isOut: false,
    goals: 2,
    penalties: 0,
    assists: 1,
    stoppedPenalties: 0,
    sufferedGoals: 0,
    wrongedPenalties: 0,
    ownGoals: 0,
    status: Behaviour.YellowCard,
    manOfTheMatch: true,
    injury: false
  },
  finalValue: { value: 10.5, special: false },
  realGame: {
    home: { name: 'Roma', abbreviation: 'ROM' },
    away: { name: 'Milan', abbreviation: 'MIL' },
    date: new Date('2025-10-04T15:00:00'),
    homeGoals: 3,
    awayGoals: 1,
    delayed: false
  },
  chance: null
} as unknown as LivePlayer)

// Dati di esempio per PlayerCard con chance (pre-partita)
const createExamplePlayerWithChance = (): LivePlayer => ({
  current: {
    name: 'Destro',
    team: { name: 'Roma', id: 'roma-id' } as any,
    role: Role.Forward,
    active: true,
    visible: true,
    position: 0,
    price: 10,
    status: PlayerInTeamStatus.Active,
    isActive: true
  },
  currentPosition: FantaSoccerRole.Forward,
  vote: null,
  finalValue: null,
  realGame: {
    home: { name: 'Roma', abbreviation: 'ROM' },
    away: { name: 'Juventus', abbreviation: 'JUV' },
    date: new Date('2095-10-06T20:45:00'),
    homeGoals: null,
    awayGoals: null,
    delayed: false
  },
  chance: {
    fantagazzetta: true,
    gazzetta: false,
    mediaset: true,
    sky: false,
    status: ChanceTypeEnum.Injury,
    description: 'Troppo forte per giocare',
    positiveness: 75,
    trend: 4, // Excellent
    lastGame: null
  } as Chance
} as unknown as LivePlayer)

export default function InfoScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

  return (
    <ScreenContent path="screens/info.tsx" title="Informazioni">
      <YStack
        width={containerWidth}
        maxWidth={960}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
      >
            {/* Sezione Spiegazione Icone */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
              gap="$4"
            >
              <XStack alignItems="center" gap="$2">
                <Info size="$1" color="$blue10" />
                <H3 color="$color12">Guida alle Icone</H3>
              </XStack>
              <Separator />

              <Paragraph fontSize="$3" color="$color11">
                Le card dei giocatori mostrano diverse informazioni a seconda del momento della partita. 
                Ecco una guida completa alle icone e ai colori utilizzati.
              </Paragraph>

              {/* Card di esempio con voto (post-partita) */}
              <YStack gap="$3">
                <H4 color="$color11">📊 Dopo la Partita - Con Voto</H4>
                <Text fontSize="$3" color="$color10">
                  Quando la partita è terminata, la card mostra il voto finale e i bonus/malus ottenuti:
                </Text>
                
                <XStack justifyContent="center" padding="$3" backgroundColor="$color3" borderRadius="$3">
                  <PlayerCard player={createExamplePlayerWithVote()} />
                </XStack>

                <YStack gap="$2" paddingLeft="$2">
                  <H4 fontSize="$3" color="$color11">🎨 Colori Badge Voto (in alto a sinistra):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#16a34a" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Verde: voto finale confermato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#f59e0b" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Arancione: voto live (partita in corso, lampeggiante)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#dc2626" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Rosso: giocatore uscito dal campo</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#6b7280" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Grigio: non ha giocato abbastanza (voto non valido)</Text>
                    </XStack>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">⚡ Icone Eventi (lato destro):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="goal" size={16} />
                      <Text fontSize="$2" color="$color10">Gol segnato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="penalty" size={16} />
                      <Text fontSize="$2" color="$color10">Rigore segnato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="assist" size={16} />
                      <Text fontSize="$2" color="$color10">Assist fornito</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="cleanSheet" size={16} />
                      <Text fontSize="$2" color="$color10">Porta inviolata (clean sheet) - solo portieri che non hanno subito gol</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="stoppedPenalty" size={16} />
                      <Text fontSize="$2" color="$color10">Rigore parato (portieri)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="sufferedGoal" size={16} />
                      <Text fontSize="$2" color="$color10">Gol subito (portieri/chi sta in porta)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="wrongedPenalty" size={16} />
                      <Text fontSize="$2" color="$color10">Rigore sbagliato</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="ownGoal" size={16} />
                      <Text fontSize="$2" color="$color10">Autogol</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="yellowCard" size={16} />
                      <Text fontSize="$2" color="$color10">Cartellino giallo</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="redCard" size={16} />
                      <Text fontSize="$2" color="$color10">Cartellino rosso (espulsione)</Text>
                    </XStack>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">🌟 Effetti Speciali:</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <Text fontSize="$2" color="$color10">
                      🏆 <Text fontWeight="600">Bordo arcobaleno animato</Text>: Migliore in campo (Man of the Match)
                    </Text>
                    <Text fontSize="$2" color="$color10">
                      ⚡ <Text fontWeight="600">Voto lampeggiante</Text>: Partita in corso (aggiornamento live ogni 10 secondi)
                    </Text>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">🎨 Colore Bordo Inferiore (nome giocatore):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#16a34a" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Verde: giocatore titolare (posizioni 0-3)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#f59e0b" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Arancione: prima panchina (posizioni 4, prime riserve)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#3b82f6" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Blu: seconda panchina (seconde riserve)</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#dc2626" borderRadius={4} />
                      <Text fontSize="$2" color="$color10">Rosso: tribuna (non schierato)</Text>
                    </XStack>
                  </YStack>
                </YStack>
              </YStack>

              <Separator />

              {/* Card di esempio con chance (pre-partita) */}
              <YStack gap="$3">
                <H4 color="$color11">🔮 Prima della Partita - Con Probabilità</H4>
                <Text fontSize="$3" color="$color10">
                  Prima che inizi la partita, la card mostra le probabilità di schieramento e la forma del giocatore:
                </Text>
                
                <XStack justifyContent="center" padding="$3" backgroundColor="$color3" borderRadius="$3">
                  <PlayerCard player={createExamplePlayerWithChance()} />
                </XStack>

                <YStack gap="$2" paddingLeft="$2">
                  <H4 fontSize="$3" color="$color11">📈 Badge Forma (in alto a sinistra):</H4>
                  <Text fontSize="$2" color="$color10" paddingLeft="$3">
                    Mostra la forma del giocatore con un numero che può andare da -5 a +25 (in base ai bonus dell&apos;ultima partita) e una freccia direzionale che rappresenta le ultime 5 partite che ha giocato il giocatore:
                  </Text>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#dc2626" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↓</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Rosso con freccia giù: forma molto negativa</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#f97316" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↘</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Arancione con freccia obliqua giù: forma negativa</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#facc15" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#000">→</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Giallo con freccia orizzontale: forma neutra</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#4ade80" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↗</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Verde chiaro con freccia obliqua su: forma positiva</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <YStack width={20} height={20} backgroundColor="#16a34a" borderRadius={4} alignItems="center" justifyContent="center">
                        <Text fontSize="$1" color="#fff">↑</Text>
                      </YStack>
                      <Text fontSize="$2" color="$color10">Verde scuro con freccia su: forma eccellente</Text>
                    </XStack>
                  </YStack>

                  <H4 fontSize="$3" color="$color11" marginTop="$2">📰 Badge Probabilità Titolarità (lato destro):</H4>
                  <YStack gap="$1" paddingLeft="$3">
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$green10">F</Text>
                      <Text fontSize="$2" color="$color10">Verde: probabile titolare secondo Fantacalcio.it</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$red10">F</Text>
                      <Text fontSize="$2" color="$color10">Rosso: probabile panchinaro secondo Fantacalcio.it</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$green10">G</Text>
                      <Text fontSize="$2" color="$color10">Verde: probabile titolare secondo Gazzetta dello Sport</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text fontSize="$3" fontWeight="600" color="$red10">G</Text>
                      <Text fontSize="$2" color="$color10">Rosso: probabile panchinaro secondo Gazzetta dello Sport</Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <IconFactory type="injury" size={10} />
                      <Text fontSize="$2" color="$color10">Triangolo viola: giocatore infortunato</Text>
                    </XStack>
                  </YStack>
                </YStack>
              </YStack>

              <Separator />

              {/* Bonus squadra nel risultato partita */}
              <YStack gap="$3">
                <H4 color="$color11">🏆 Bonus Squadra nel Risultato Partita</H4>
                <Text fontSize="$3" color="$color10">
                  Nella schermata del risultato della partita, accanto al punteggio di ogni squadra, possono apparire le seguenti icone bonus:
                </Text>

                <YStack gap="$2" paddingLeft="$3">
                  <XStack alignItems="center" gap="$2">
                    <GoodDefenseIcon size={20} />
                    <Text fontSize="$2" color="$color10">
                      <Text fontWeight="600">Difesa forte</Text>: bonus per difesa che non ha subito molti gol (muro arancione)
                    </Text>
                  </XStack>
                  <XStack alignItems="center" gap="$2">
                    <GoodPeopleIcon size={20} />
                    <Text fontSize="$2" color="$color10">
                      <Text fontWeight="600">Brava gente</Text>: bonus per squadra con pochi cartellini (stella gialla)
                    </Text>
                  </XStack>
                  <XStack alignItems="center" gap="$2">
                    <IconFactory type="ownGoal" size={20} />
                    <Text fontSize="$2" color="$color10">
                      <Text fontWeight="600">Autogol</Text>: malus per squadra che ha segnato un autogol decisivo (pallone rosso e bianco)
                    </Text>
                  </XStack>
                </YStack>

                <Text fontSize="$2" color="$color11" fontStyle="italic" paddingLeft="$3">
                  Nota: l&apos;autogol viene mostrato solo se entrambe le squadre hanno meno di 66 punti e la differenza è di almeno 6 punti. Questa regola può variare in base alla lega e alle opzioni di lega che sono state impostate, puoi vedere la sezione regole per capire come funziona la tua lega.
                </Text>
              </YStack>
            </Card>
          </YStack>
    </ScreenContent>
  )
}
