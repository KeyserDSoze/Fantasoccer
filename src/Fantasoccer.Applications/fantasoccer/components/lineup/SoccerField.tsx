import { memo, useMemo, useState, useEffect } from 'react'
import { YStack, XStack, Text, ScrollView, Button } from 'tamagui'
import { useToastController } from '@tamagui/toast'
import { Save } from '@tamagui/lucide-icons'
import type { GameWrapper, LiveTeam, LivePlayer } from 'types/game'
import { FantaSoccerRole, Team } from 'types/team'
import { Role } from 'types/group'
import { PlayerCard, PLAYER_CARD_HEIGHT } from './PlayerCard'
import { PlayerSwapModal } from './PlayerSwapModal'
import { PlayerViewModal } from './PlayerViewModal'
import { useResponsiveField } from '../../hooks/useResponsiveField'
import { useAuth } from '../../providers/AuthProvider'
import { canRemovePlayer, getAvailableFieldPosition, getAvailableBenchPosition } from '../../utils/teamValidation'
import gameService from '../../services/gameService'

// SVG field background as a React component with responsive orientation
interface SoccerFieldSVGProps {
  orientation: 'horizontal' | 'vertical'
}

const SoccerFieldSVG = memo(({ orientation }: SoccerFieldSVGProps) => {
  // Desktop: campo orizzontale tradizionale (1400x900)
  if (orientation === 'horizontal') {
    return (
      <svg 
        width="100%" 
        height="100%" 
        viewBox="0 0 1400 900" 
        xmlns="http://www.w3.org/2000/svg" 
        aria-label="Campo da calcio - Desktop"
      >
        <g id="campo-orizzontale">
          <rect fill="#15803d" x="0" y="0" width="1400" height="900"/>
          <rect stroke="#ffffff" strokeWidth="8" fill="none" x="40" y="40" width="1320" height="820"/>
          <line stroke="#ffffff" strokeWidth="6" x1="700" y1="40" x2="700" y2="860"/>
          <circle stroke="#ffffff" strokeWidth="6" fill="none" cx="700" cy="450" r="90"/>
          <circle fill="#ffffff" cx="700" cy="450" r="6"/>
          <rect stroke="#ffffff" strokeWidth="6" fill="none" x="40" y="250" width="200" height="400"/>
          <rect stroke="#ffffff" strokeWidth="6" fill="none" x="40" y="340" width="80" height="220"/>
          <circle fill="#ffffff" cx="200" cy="450" r="6"/>
          <path stroke="#ffffff" strokeWidth="5" fill="none" d="M 240 380 A 60 60 0 0 1 240 520"/>
          <rect stroke="#ffffff" strokeWidth="6" fill="none" x="1160" y="250" width="200" height="400"/>
          <rect stroke="#ffffff" strokeWidth="6" fill="none" x="1280" y="340" width="80" height="220"/>
          <circle fill="#ffffff" cx="1200" cy="450" r="6"/>
          <path stroke="#ffffff" strokeWidth="5" fill="none" d="M 1160 520 A 60 60 0 0 1 1160 380"/>
          <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="0" y="390" width="40" height="120"/>
          <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="1360" y="390" width="40" height="120"/>
        </g>
      </svg>
    )
  }

  // Mobile: campo verticale nativo (900x1800) - più alto per maggiore spaziatura
  return (
    <svg 
      width="100%" 
      height="100%" 
      viewBox="0 0 900 1800" 
      xmlns="http://www.w3.org/2000/svg" 
      aria-label="Campo da calcio - Mobile"
    >
      <g id="campo-verticale">
        <rect fill="#15803d" x="0" y="0" width="900" height="1800"/>
        <rect stroke="#ffffff" strokeWidth="8" fill="none" x="40" y="40" width="820" height="1720"/>
        <line stroke="#ffffff" strokeWidth="6" x1="35" y1="900" x2="865" y2="900"/>
        <circle stroke="#ffffff" strokeWidth="6" fill="none" cx="450" cy="900" r="90"/>
        <circle fill="#ffffff" cx="450" cy="900" r="6"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="250" y="40" width="400" height="200"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="340" y="40" width="220" height="80"/>
        <circle fill="#ffffff" cx="450" cy="200" r="6"/>
        <path stroke="#ffffff" strokeWidth="5" fill="none" d="M 380 240 A 60 60 0 0 1 520 240"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="250" y="1560" width="400" height="200"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="none" x="340" y="1680" width="220" height="80"/>
        <circle fill="#ffffff" cx="450" cy="1600" r="6"/>
        <path stroke="#ffffff" strokeWidth="5" fill="none" d="M 520 1560 A 60 60 0 0 1 380 1560"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="390" y="0" width="120" height="40"/>
        <rect stroke="#ffffff" strokeWidth="6" fill="rgba(255,255,255,0.05)" x="390" y="1760" width="120" height="40"/>
      </g>
    </svg>
  )
})

export interface SoccerFieldProps {
  gameWrapper?: GameWrapper | null
  groupId?: string // ID del gruppo per salvare il team
  onRefresh?: () => void | Promise<void> // Callback per ricaricare i dati dopo il salvataggio
  style?: any
}

function pickHomeAwayTeams(gameWrapper?: GameWrapper | null): { home?: LiveTeam; away?: LiveTeam } {
  const teams = gameWrapper?.teams ?? []
  if (!teams.length) return {}

  // Try to match by game home/away names
  const game = gameWrapper?.game
  if (game) {
    const homeName = (game.home ?? '').toLowerCase()
    const awayName = (game.away ?? '').toLowerCase()
    const foundHome = teams.find(t => (t.name ?? '').toLowerCase() === homeName)
    const foundAway = teams.find(t => (t.name ?? '').toLowerCase() === awayName)
    if (foundHome || foundAway) return { home: foundHome ?? teams[0], away: foundAway ?? teams[1] ?? teams[0] }
  }

  // Fallback to first/second
  return { home: teams[0], away: teams[1] }
}

export const SoccerField = memo(({ gameWrapper, groupId, onRefresh, style }: SoccerFieldProps) => {
  const { width, height, isMobile, orientation, cardScale } = useResponsiveField()
  const { user } = useAuth()
  const toast = useToastController()
  
  // Stato editing
  const [selectedPlayer, setSelectedPlayer] = useState<LivePlayer | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [viewModalOpen, setViewModalOpen] = useState(false) // Modal view-only per info giocatore
  const [editingTeamIndex, setEditingTeamIndex] = useState<number | null>(null) // 0 = home, 1 = away
  const [isSaving, setIsSaving] = useState(false)
  
  // Team locali modificabili (inizializzati da gameWrapper, aggiornati durante editing)
  const [localTeams, setLocalTeams] = useState<{ home?: LiveTeam; away?: LiveTeam }>({})
  const [hasLocalChanges, setHasLocalChanges] = useState(false)
  
  const { home: initialHome, away: initialAway } = useMemo(() => pickHomeAwayTeams(gameWrapper), [gameWrapper])
  
  // Inizializza localTeams quando gameWrapper cambia
  useEffect(() => {
    setLocalTeams({ home: initialHome, away: initialAway })
  }, [initialHome, initialAway])
  
  // Usa i team locali (modificati) o quelli iniziali
  const home = localTeams.home ?? initialHome
  const away = localTeams.away ?? initialAway
  // Removed debug log
  const homeStarters: LivePlayer[] = (home?.players ?? []).filter(p => 
    p.currentPosition >= FantaSoccerRole.GoalKeeper && p.currentPosition <= FantaSoccerRole.Forward
  )
  const awayStarters: LivePlayer[] = (away?.players ?? []).filter(p => 
    p.currentPosition >= FantaSoccerRole.GoalKeeper && p.currentPosition <= FantaSoccerRole.Forward
  )

  // Panchine - giocatori backup (riserve) ordinati per currentPosition
  const homeBench: LivePlayer[] = (home?.players ?? [])
    .filter(p => 
      p.currentPosition >= FantaSoccerRole.BackupGoalKeeper && p.currentPosition <= FantaSoccerRole.SecondBackupForward
    )
    .sort((a, b) => a.currentPosition - b.currentPosition)
    
  const awayBench: LivePlayer[] = (away?.players ?? [])
    .filter(p => 
      p.currentPosition >= FantaSoccerRole.BackupGoalKeeper && p.currentPosition <= FantaSoccerRole.SecondBackupForward
    )
    .sort((a, b) => a.currentPosition - b.currentPosition)

  // Tribuna - giocatori non utilizzati
  const homeTribuneRaw: LivePlayer[] = (home?.players ?? []).filter(p => p.currentPosition === FantaSoccerRole.Tribune)
  const awayTribuneRaw: LivePlayer[] = (away?.players ?? []).filter(p => p.currentPosition === FantaSoccerRole.Tribune)

  // Ordinamento tribuna per ruolo principale e poi per nome
  const ROLE_SORT_ORDER: Role[] = [Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward, Role.Undefined]
  const sortTribune = (players: LivePlayer[]) => {
    return [...players].sort((a, b) => {
      const roleA = (a.current as any)?.role ?? Role.Undefined
      const roleB = (b.current as any)?.role ?? Role.Undefined
      const idxA = ROLE_SORT_ORDER.indexOf(roleA)
      const idxB = ROLE_SORT_ORDER.indexOf(roleB)
      if (idxA !== idxB) return idxA - idxB
      const nameA = (a.current as any)?.name ?? ''
      const nameB = (b.current as any)?.name ?? ''
      return nameA.localeCompare(nameB, 'it-IT', { sensitivity: 'base' })
    })
  }
  const homeTribune = sortTribune(homeTribuneRaw)
  const awayTribune = sortTribune(awayTribuneRaw)

  // Determina quale team è modificabile (owner === email)
  const canEdit = gameWrapper?.canEdit ?? false
  const userEmail = user?.email ?? ''
  const homeIsMyTeam = canEdit && (home?.owner ?? '').toLowerCase() === userEmail.toLowerCase()
  const awayIsMyTeam = canEdit && (away?.owner ?? '').toLowerCase() === userEmail.toLowerCase()

  // Determina se almeno una partita è iniziata (per mostrare view modal invece di edit modal)
  const hasGameStarted = useMemo(() => {
    const players = [...(home?.players ?? []), ...(away?.players ?? [])]
    // Se almeno un giocatore ha una partita iniziata, mostra il view modal
    return players.some(p => {
      const realGame = p.realGame
      if (!realGame?.date) return false
      const now = new Date()
      return now >= realGame.date
    })
  }, [home, away])

  // Funzione per gestire click su un giocatore
  const handlePlayerClick = (player: LivePlayer, teamIndex: number) => {
    const isMyTeam = teamIndex === 0 ? homeIsMyTeam : awayIsMyTeam
    
    // Se almeno una partita è iniziata, mostra sempre il modal di visualizzazione
    if (hasGameStarted) {
      setSelectedPlayer(player)
      setViewModalOpen(true)
      return
    }
    
    // Altrimenti, se canEdit e isMyTeam, mostra il modal di editing
    if (canEdit && isMyTeam) {
      setSelectedPlayer(player)
      setEditingTeamIndex(teamIndex)
      setModalOpen(true)
    }
  }

  // Callback modale: swap tra giocatori
  const handleSwap = (targetPlayer: LivePlayer) => {
    if (!selectedPlayer || editingTeamIndex === null) return

    // Trova il team da modificare
    const currentTeam = editingTeamIndex === 0 ? home : away
    if (!currentTeam) return

    // Crea una copia aggiornata dei giocatori con posizioni scambiate
    const updatedPlayers = currentTeam.players.map(p => {
      const pName = (p.current as any)?.name
      const selectedName = (selectedPlayer.current as any)?.name
      const targetName = (targetPlayer.current as any)?.name

      if (pName === selectedName) {
        // Scambia posizione con targetPlayer
        return { ...p, currentPosition: targetPlayer.currentPosition }
      } else if (pName === targetName) {
        // Scambia posizione con selectedPlayer
        return { ...p, currentPosition: selectedPlayer.currentPosition }
      }
      return p
    })

    // Aggiorna lo stato locale del team
    const updatedTeam = { ...currentTeam, players: updatedPlayers }
    setLocalTeams(prev => ({
      ...prev,
      [editingTeamIndex === 0 ? 'home' : 'away']: updatedTeam
    }))
    setHasLocalChanges(true)

    setModalOpen(false)
    setSelectedPlayer(null)
    setEditingTeamIndex(null)
  }

  // Callback modale: rimuovi in tribuna
  const handleRemove = () => {
    if (!selectedPlayer || editingTeamIndex === null) return

    // Trova il team da modificare
    const currentTeam = editingTeamIndex === 0 ? home : away
    if (!currentTeam) return

    // Crea una copia aggiornata dei giocatori con il selezionato in tribuna
    const updatedPlayers = currentTeam.players.map(p => {
      const pName = (p.current as any)?.name
      const selectedName = (selectedPlayer.current as any)?.name

      if (pName === selectedName) {
        return { ...p, currentPosition: FantaSoccerRole.Tribune }
      }
      return p
    })

    // Aggiorna lo stato locale del team
    const updatedTeam = { ...currentTeam, players: updatedPlayers }
    setLocalTeams(prev => ({
      ...prev,
      [editingTeamIndex === 0 ? 'home' : 'away']: updatedTeam
    }))
    setHasLocalChanges(true)

    setModalOpen(false)
    setSelectedPlayer(null)
    setEditingTeamIndex(null)
  }

  // Callback modale: aggiungi giocatore in campo (dalla tribuna o panchina)
  const handleAddToField = (player: LivePlayer) => {
    if (editingTeamIndex === null) return

    const currentTeam = editingTeamIndex === 0 ? home : away
    if (!currentTeam) return

    // Trova posizione disponibile in campo per il ruolo del giocatore
    const playerRole = (player.current as any)?.role ?? Role.Undefined
    const targetPosition = getAvailableFieldPosition(playerRole, currentTeam.players)
    
    if (targetPosition === undefined) {
      toast.show('❌ Nessuno spazio disponibile in campo per questo ruolo', { variant: 'error' })
      return
    }

    // Aggiorna la posizione del giocatore
    const updatedPlayers = currentTeam.players.map(p => {
      const pName = (p.current as any)?.name
      const playerName = (player.current as any)?.name
      if (pName === playerName) {
        return { ...p, currentPosition: targetPosition }
      }
      return p
    })

    const updatedTeam = { ...currentTeam, players: updatedPlayers }
    setLocalTeams(prev => ({
      ...prev,
      [editingTeamIndex === 0 ? 'home' : 'away']: updatedTeam
    }))
    setHasLocalChanges(true)

    setModalOpen(false)
    setSelectedPlayer(null)
    setEditingTeamIndex(null)
  }

  // Callback modale: aggiungi giocatore in panchina (dalla tribuna)
  const handleAddToBench = (player: LivePlayer) => {
    if (editingTeamIndex === null) return

    const currentTeam = editingTeamIndex === 0 ? home : away
    if (!currentTeam) return

    // Trova posizione disponibile in panchina per il ruolo del giocatore
    const playerRole = (player.current as any)?.role ?? Role.Undefined
    const targetPosition = getAvailableBenchPosition(playerRole, currentTeam.players)
    
    if (targetPosition === undefined) {
      toast.show('❌ Nessuno spazio disponibile in panchina per questo ruolo', { variant: 'error' })
      return
    }

    // Aggiorna la posizione del giocatore
    const updatedPlayers = currentTeam.players.map(p => {
      const pName = (p.current as any)?.name
      const playerName = (player.current as any)?.name
      if (pName === playerName) {
        return { ...p, currentPosition: targetPosition }
      }
      return p
    })

    const updatedTeam = { ...currentTeam, players: updatedPlayers }
    setLocalTeams(prev => ({
      ...prev,
      [editingTeamIndex === 0 ? 'home' : 'away']: updatedTeam
    }))
    setHasLocalChanges(true)

    setModalOpen(false)
    setSelectedPlayer(null)
    setEditingTeamIndex(null)
  }

  // Funzione per salvare il team modificato
  const handleSave = async () => {
    if (!groupId || !hasLocalChanges) return

    // Determina quale team è stato modificato
    const teamToSave = homeIsMyTeam ? home : awayIsMyTeam ? away : null
    if (!teamToSave) {
      toast.show('Errore: team non trovato', { variant: 'error' })
      return
    }

    setIsSaving(true)
    try {
      const teamData: Team = {
        name: teamToSave.name,
        owner: teamToSave.owner,
        additionalOwners: teamToSave.additionalOwners,
        lastUpdate: new Date(),
        revenueMoney: 0,
        moneyFromRank: 0,
        players: teamToSave.players.map(p => ({
          ...(p.current as any),
          position: p.currentPosition
        }))
      }

      const response = await gameService.saveTeam(groupId, teamData)
      
      if (response?.i) {
        toast.show('✅ Formazione salvata con successo!', { variant: 'success' })
        setHasLocalChanges(false)
        
        // Ricarica i dati dal server per confermare le modifiche
        if (onRefresh) {
          await onRefresh()
        }
      } else {
        toast.show(`Errore: ${response?.m || 'Salvataggio fallito'}`, { variant: 'error' })
      }
    } catch (error: any) {
      console.error('Errore salvataggio team:', error)
      toast.show(`❌ Errore: ${error?.message || 'Salvataggio fallito'}`, { variant: 'error' })
    } finally {
      setIsSaving(false)
    }
  }

  // Determina se il giocatore selezionato può essere rimosso
  const selectedCanRemove = useMemo(() => {
    if (!selectedPlayer || editingTeamIndex === null) return false
    const teamPlayers = editingTeamIndex === 0 ? (home?.players ?? []) : (away?.players ?? [])
    return canRemovePlayer(selectedPlayer, teamPlayers)
  }, [selectedPlayer, editingTeamIndex, home, away])

  // Tutti i giocatori del team in editing per la modale
  const allPlayersForModal = useMemo(() => {
    if (editingTeamIndex === null) return []
    return editingTeamIndex === 0 ? (home?.players ?? []) : (away?.players ?? [])
  }, [editingTeamIndex, home, away])

  // Funzione per raggruppare giocatori della panchina per ruolo
  const groupBenchByRole = (benchPlayers: LivePlayer[]) => {
    const groups = {
      goalkeepers: benchPlayers.filter(p => p.currentPosition === FantaSoccerRole.BackupGoalKeeper),
      defenders: benchPlayers.filter(p =>
        p.currentPosition === FantaSoccerRole.FirstBackupDefensor ||
        p.currentPosition === FantaSoccerRole.SecondBackupDefensor
      ).sort((a, b) => a.currentPosition - b.currentPosition),
      midfielders: benchPlayers.filter(p =>
        p.currentPosition === FantaSoccerRole.FirstBackupMidfielder ||
        p.currentPosition === FantaSoccerRole.SecondBackupMidfielder
      ).sort((a, b) => a.currentPosition - b.currentPosition),
      forwards: benchPlayers.filter(p =>
        p.currentPosition === FantaSoccerRole.FirstBackupForward ||
        p.currentPosition === FantaSoccerRole.SecondBackupForward
      ).sort((a, b) => a.currentPosition - b.currentPosition)
    }
    return groups
  }

  const homeBenchGroups = groupBenchByRole(homeBench)
  const awayBenchGroups = groupBenchByRole(awayBench)

  /**
   * Componente riutilizzabile per sezioni panchina
   */
  const renderBenchSection = (
    players: LivePlayer[],
    sectionKey: string,
    roleLabel: string,
    backgroundColor: string,
    roleIcon: string,
    teamIndex: number, // 0 = home, 1 = away
    isMyTeam: boolean // se è il mio team
  ) => {
    if (players.length === 0) return null

    const hexToRgba = (hex: string, alpha: number = 0.1) => {
      const r = parseInt(hex.slice(1, 3), 16)
      const g = parseInt(hex.slice(3, 5), 16)
      const b = parseInt(hex.slice(5, 7), 16)
      return `rgba(${r}, ${g}, ${b}, ${alpha})`
    }

    const scaledCardHeight = PLAYER_CARD_HEIGHT * cardScale

    return (
      <XStack
        gap={8}
        backgroundColor={hexToRgba(backgroundColor)}
        borderRadius={8}
        padding={12}
        alignItems="stretch"
      >
        {/* Badge ruolo verticale con altezza uguale alla card scalata */}
        <YStack
          minWidth={60}
          height={scaledCardHeight}
          justifyContent="center"
          alignItems="center"
          backgroundColor={backgroundColor}
            borderRadius={8}
          paddingHorizontal={8}
          paddingVertical={2}
        >
          <Text
            fontSize={14}
            fontWeight="700"
            color="#fff"
            style={{
              writingMode: 'vertical-lr',
              textOrientation: 'mixed'
            }}
          >
            {roleIcon} {roleLabel.substring(0, 3)}
          </Text>
        </YStack>
        {/* Cards giocatori */}
        <XStack
          gap={gapBetweenCards}
          flexWrap="wrap"
          flex={1}
          alignItems="flex-start"
          style={{
            transform: `scale(${cardScale})`,
            transformOrigin: 'left top',
            height: scaledCardHeight
          }}
        >
          {players.map((player, i) => (
            <PlayerCard 
              key={`${sectionKey}-${i}`} 
              player={player}
              onPress={() => handlePlayerClick(player, teamIndex)}
            />
          ))}
        </XStack>
      </XStack>
    )
  }

  // Group players by FantaSoccerRole numeric enum (titolari: 0..3)
  function groupByPosition(players: LivePlayer[]) {
    const groups: { GK: LivePlayer[]; DEF: LivePlayer[]; MID: LivePlayer[]; FWD: LivePlayer[] } = { GK: [], DEF: [], MID: [], FWD: [] }
    players.forEach(p => {
      // position can be on player.position or player.current.position depending on shape
      const pos = p.currentPosition
      switch (pos) {
        case FantaSoccerRole.GoalKeeper:
          groups.GK.push(p)
          break
        case FantaSoccerRole.Defensor:
          groups.DEF.push(p)
          break
        case FantaSoccerRole.Midfielder:
          groups.MID.push(p)
          break
        case FantaSoccerRole.Forward:
          groups.FWD.push(p)
          break
      }
    })
    return groups
  }

  const homeGroups = groupByPosition(homeStarters)
  const awayGroups = groupByPosition(awayStarters)

  // Stripes order: home GK, home DEF, home MID, home FWD, then mirrored away FWD, MID, DEF, GK
  const stripes: LivePlayer[][] = [
    homeGroups.GK,
    homeGroups.DEF,
    homeGroups.MID,
    homeGroups.FWD,
    awayGroups.FWD,
    awayGroups.MID,
    awayGroups.DEF,
    awayGroups.GK,
  ]
  const paddingBottomForRole = 9
  const gapBetweenCards = 8
  
  // Mostra pulsante salva se c'è almeno un team modificabile e ci sono modifiche
  const showSaveButton = (homeIsMyTeam || awayIsMyTeam) && hasLocalChanges

  return (
    <YStack gap={16} width="100%" {...style}>
      {/* PULSANTE SALVA FORMAZIONE */}
      {showSaveButton && (
        <XStack justifyContent="center" paddingHorizontal={16}>
          <Button
            backgroundColor="$green10"
            color="#fff"
            size="$5"
            fontWeight="700"
            icon={<Save size={20} />}
            onPress={handleSave}
            disabled={isSaving}
            opacity={isSaving ? 0.6 : 1}
            minWidth={200}
          >
            {isSaving ? 'Salvataggio...' : '💾 Salva Formazione'}
          </Button>
        </XStack>
      )}

      {/* CAMPO DA CALCIO */}
      <YStack 
        position="relative" 
        width="100%" 
        height={height}
        maxWidth="100%"
      >
        {/* SVG field background with responsive orientation */}
        <SoccerFieldSVG orientation={orientation} />

        {/* 8 equal containers positioned absolutely over the field */}
        {stripes.map((playersInStripe, stripeIndex) => {
          // Determina se questa stripe appartiene a home (0-3) o away (4-7)
          const isHomeStripe = stripeIndex < 4
          const teamIndex = isHomeStripe ? 0 : 1
          const isMyTeam = isHomeStripe ? homeIsMyTeam : awayIsMyTeam
          
          if (isMobile) {
            // Mobile: vertical layout with horizontal rows
            const topPercent = stripeIndex * 12.5
            
            return (
              <XStack
                key={`stripe-${stripeIndex}`}
                position="absolute"
                top={`${topPercent}%`}
                left={0}
                width="100%"
                height="12.5%"
                justifyContent="center"
                alignItems="center"
                pointerEvents="none"
              >
                <XStack 
                  pointerEvents="auto" 
                  gap={gapBetweenCards}
                  alignItems="center" 
                  justifyContent="center"
                  style={{
                    transform: `scale(${cardScale})`,
                    transformOrigin: 'center center'
                  }}
                >
                  {playersInStripe.map((p, i) => (
                    <PlayerCard 
                      key={`${p.current?.name ?? ''}-${i}`} 
                      player={p}
                      onPress={() => handlePlayerClick(p, teamIndex)}
                    />
                  ))}
                </XStack>
              </XStack>
            )
          } else {
            // Desktop/Tablet: horizontal layout with vertical columns
            const leftPercent = stripeIndex * 12.5
            
            return (
              <YStack
                key={`stripe-${stripeIndex}`}
                position="absolute"
                left={`${leftPercent}%`}
                top={0}
                width="12.5%"
                height="100%"
                justifyContent="center"
                alignItems="center"
                pointerEvents="none"
              >
                <YStack 
                  pointerEvents="auto" 
                  gap={gapBetweenCards}
                  alignItems="center" 
                  justifyContent="center"
                  style={{
                    transform: `scale(${cardScale})`,
                    transformOrigin: 'center center'
                  }}
                >
                  {playersInStripe.map((p, i) => (
                    <PlayerCard 
                      key={`${p.current?.name ?? ''}-${i}`} 
                      player={p}
                      onPress={() => handlePlayerClick(p, teamIndex)}
                    />
                  ))}
                </YStack>
              </YStack>
            )
          }
        })}
      </YStack>
      {/* PANCHINE ORGANIZZATE PER RUOLO */}
      {isMobile ? (
        <YStack gap={32}>
          {/* Panchina Casa (stacked) */}
          <YStack gap={12}>
            <YStack 
              backgroundColor="#16a34a" 
              paddingHorizontal={16} 
              paddingVertical={8} 
              borderRadius={8}
              alignItems="center"
            >
              <Text fontSize={16} fontWeight="700" color="#fff">
                🏠 PANCHINA {home?.name?.toUpperCase() || 'CASA'}
              </Text>
            </YStack>
            {renderBenchSection(homeBenchGroups.goalkeepers, 'home-gk', 'PORTIERI', '#f59e0b', '🥅', 0, homeIsMyTeam)}
            {renderBenchSection(homeBenchGroups.defenders, 'home-def', 'DIFENSORI', '#3b82f6', '🛡️', 0, homeIsMyTeam)}
            {renderBenchSection(homeBenchGroups.midfielders, 'home-mid', 'CENTROCAMPISTI', '#10b981', '⚽', 0, homeIsMyTeam)}
            {renderBenchSection(homeBenchGroups.forwards, 'home-fwd', 'ATTACCANTI', '#ef4444', '🎯', 0, homeIsMyTeam)}
            {homeBench.length === 0 && (
              <Text fontSize={14} color="$gray10" fontStyle="italic" textAlign="center">
                Nessun giocatore in panchina
              </Text>
            )}
          </YStack>
          {/* Separator */}
          <YStack height={1} backgroundColor="#334155" opacity={0.5} />
          {/* Panchina Trasferta (stacked) */}
          <YStack gap={12}>
            <YStack 
              backgroundColor="#dc2626" 
              paddingHorizontal={16} 
              paddingVertical={8} 
              borderRadius={8}
              alignItems="center"
            >
              <Text fontSize={16} fontWeight="700" color="#fff">
                ✈️ PANCHINA {away?.name?.toUpperCase() || 'TRASFERTA'}
              </Text>
            </YStack>
            {renderBenchSection(awayBenchGroups.goalkeepers, 'away-gk', 'PORTIERI', '#f59e0b', '🥅', 1, awayIsMyTeam)}
            {renderBenchSection(awayBenchGroups.defenders, 'away-def', 'DIFENSORI', '#3b82f6', '🛡️', 1, awayIsMyTeam)}
            {renderBenchSection(awayBenchGroups.midfielders, 'away-mid', 'CENTROCAMPISTI', '#10b981', '⚽', 1, awayIsMyTeam)}
            {renderBenchSection(awayBenchGroups.forwards, 'away-fwd', 'ATTACCANTI', '#ef4444', '🎯', 1, awayIsMyTeam)}
            {awayBench.length === 0 && (
              <Text fontSize={14} color="$gray10" fontStyle="italic" textAlign="center">
                Nessun giocatore in panchina
              </Text>
            )}
          </YStack>
        </YStack>
      ) : (
        <XStack gap={16} flex={1} justifyContent="space-between">
          {/* Panchina Casa (desktop side) */}
          <YStack flex={1} gap={12}>
            <YStack 
              backgroundColor="#16a34a" 
              paddingHorizontal={16} 
              paddingVertical={8} 
              borderRadius={8}
              alignItems="center"
            >
              <Text fontSize={16} fontWeight="700" color="#fff">
                🏠 PANCHINA {home?.name?.toUpperCase() || 'CASA'}
              </Text>
            </YStack>
            {renderBenchSection(homeBenchGroups.goalkeepers, 'home-gk', 'PORTIERI', '#f59e0b', '🥅', 0, homeIsMyTeam)}
            {renderBenchSection(homeBenchGroups.defenders, 'home-def', 'DIFENSORI', '#3b82f6', '🛡️', 0, homeIsMyTeam)}
            {renderBenchSection(homeBenchGroups.midfielders, 'home-mid', 'CENTROCAMPISTI', '#10b981', '⚽', 0, homeIsMyTeam)}
            {renderBenchSection(homeBenchGroups.forwards, 'home-fwd', 'ATTACCANTI', '#ef4444', '🎯', 0, homeIsMyTeam)}
            {homeBench.length === 0 && (
              <Text fontSize={14} color="$gray10" fontStyle="italic" textAlign="center">
                Nessun giocatore in panchina
              </Text>
            )}
          </YStack>
          {/* Panchina Trasferta (desktop side) */}
            <YStack flex={1} gap={12}>
              <YStack 
                backgroundColor="#dc2626" 
                paddingHorizontal={16} 
                paddingVertical={8} 
                borderRadius={8}
                alignItems="center"
              >
                <Text fontSize={16} fontWeight="700" color="#fff">
                  ✈️ PANCHINA {away?.name?.toUpperCase() || 'TRASFERTA'}
                </Text>
              </YStack>
              {renderBenchSection(awayBenchGroups.goalkeepers, 'away-gk', 'PORTIERI', '#f59e0b', '🥅', 1, awayIsMyTeam)}
              {renderBenchSection(awayBenchGroups.defenders, 'away-def', 'DIFENSORI', '#3b82f6', '🛡️', 1, awayIsMyTeam)}
              {renderBenchSection(awayBenchGroups.midfielders, 'away-mid', 'CENTROCAMPISTI', '#10b981', '⚽', 1, awayIsMyTeam)}
              {renderBenchSection(awayBenchGroups.forwards, 'away-fwd', 'ATTACCANTI', '#ef4444', '🎯', 1, awayIsMyTeam)}
              {awayBench.length === 0 && (
                <Text fontSize={14} color="$gray10" fontStyle="italic" textAlign="center">
                  Nessun giocatore in panchina
                </Text>
              )}
            </YStack>
        </XStack>
      )}

      {/* TRIBUNA - sempre due righe separate */}
      {(homeTribune.length > 0 || awayTribune.length > 0) && (
        <YStack gap={24}>
          {/* Tribuna Home */}
          <YStack gap={8}>
            <YStack
              backgroundColor="#6b7280"
              paddingHorizontal={16}
              paddingVertical={8}
              borderRadius={8}
              alignItems="center"
            >
              <Text fontSize={16} fontWeight="700" color="#fff">
                🏟️ TRIBUNA {home?.name?.toUpperCase() || 'CASA'}
              </Text>
            </YStack>
            <YStack backgroundColor="rgba(107,114,128,0.15)" borderRadius={8} padding={12}>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <XStack
                  gap={gapBetweenCards}
                  paddingHorizontal={4}
                  alignItems="flex-start"
                  style={{
                    transform: `scale(${cardScale})`,
                    transformOrigin: 'left top',
                    height: PLAYER_CARD_HEIGHT * cardScale
                  }}
                >
                  {homeTribune.map((player, i) => (
                    <PlayerCard 
                      key={`home-tribune-${i}`} 
                      player={player}
                      onPress={() => handlePlayerClick(player, 0)}
                    />
                  ))}
                  {homeTribune.length === 0 && (
                    <Text fontSize={12} color="$gray10" fontStyle="italic">
                      Nessuno in tribuna
                    </Text>
                  )}
                </XStack>
              </ScrollView>
            </YStack>
          </YStack>
          {/* Separator */}
          <YStack height={1} backgroundColor="#334155" opacity={0.5} />
          {/* Tribuna Away */}
          <YStack gap={8}>
            <YStack
              backgroundColor="#6b7280"
              paddingHorizontal={16}
              paddingVertical={8}
              borderRadius={8}
              alignItems="center"
            >
              <Text fontSize={16} fontWeight="700" color="#fff">
                🏟️ TRIBUNA {away?.name?.toUpperCase() || 'TRASFERTA'}
              </Text>
            </YStack>
            <YStack backgroundColor="rgba(107,114,128,0.15)" borderRadius={8} padding={12}>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <XStack
                  gap={gapBetweenCards}
                  paddingHorizontal={4}
                  alignItems="flex-start"
                  style={{
                    transform: `scale(${cardScale})`,
                    transformOrigin: 'left top',
                    height: PLAYER_CARD_HEIGHT * cardScale
                  }}
                >
                  {awayTribune.map((player, i) => (
                    <PlayerCard 
                      key={`away-tribune-${i}`} 
                      player={player}
                      onPress={() => handlePlayerClick(player, 1)}
                    />
                  ))}
                  {awayTribune.length === 0 && (
                    <Text fontSize={12} color="$gray10" fontStyle="italic">
                      Nessuno in tribuna
                    </Text>
                  )}
                </XStack>
              </ScrollView>
            </YStack>
          </YStack>
        </YStack>
      )}

      {/* MODALE PER EDITING */}
      <PlayerSwapModal
        isOpen={modalOpen}
        onClose={() => {
          setModalOpen(false)
          setSelectedPlayer(null)
          setEditingTeamIndex(null)
        }}
        selectedPlayer={selectedPlayer}
        allPlayers={allPlayersForModal}
        onSwap={handleSwap}
        onRemove={selectedCanRemove ? handleRemove : undefined}
        canRemove={selectedCanRemove}
        onAddToField={handleAddToField}
        onAddToBench={handleAddToBench}
      />

      {/* MODALE PER VISUALIZZAZIONE INFO GIOCATORE */}
      <PlayerViewModal
        isOpen={viewModalOpen}
        onClose={() => {
          setViewModalOpen(false)
          setSelectedPlayer(null)
        }}
        player={selectedPlayer}
      />
    </YStack>
  )
})

export default SoccerField

