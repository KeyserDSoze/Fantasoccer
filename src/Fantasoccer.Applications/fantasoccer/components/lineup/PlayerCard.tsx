import React, { useMemo, useState, useEffect } from 'react'
import { Card, Image, Text, XStack, YStack, Button } from 'tamagui'
import {
  Trophy,
  Target,
  Star,
  Umbrella,
  Award,
  Shield,
  AlertTriangle,
  Heart,
  Zap,
  Circle,
  Calendar,
  Clock,
  Users,
  Goal,
  Minus,
  Plus,
  TrendingUp,
  TrendingDown,
  Activity,
  AlertCircle,
  Info,
  CheckCircle,
  XCircle,
  Radio,
  Satellite,
  BarChart,
} from '@tamagui/lucide-icons'

import type { LivePlayer } from 'types/game'
import { getPlayerImageUrlFromName } from 'utils/playerImage'
import type { Vote } from 'types/serieA'
import { Behaviour } from 'types/serieA'
import type { Chance, ChanceType, TrendType } from '../../types/chance'
import { ChanceType as ChanceTypeEnum } from '../../types/chance'
import { FantaSoccerRole } from 'types/team'
import { formatGameDateTime, convertUTCToItalianTimezoneAsNotNullableDate } from 'utils/timezoneHelpers'

// ============================================
// COMPONENTI SVG PERSONALIZZATI PER BONUS/MALUS
// ============================================

// Pallone da calcio riutilizzabile con colori personalizzabili
function SoccerBallSVG({
  primaryColor = '#ffffff',
  secondaryColor = '#000000',
  size = 16
}: {
  primaryColor?: string,
  secondaryColor?: string,
  size?: number
}) {
  const CX = 50, CY = 50;
  const outerR = 48;    // raggio della palla "base"
  const seamW = 2.2;    // spessore cuciture

  const poly = (cx: number, cy: number, r: number, sides: number, rotDeg = 0) => {
    const rot = (Math.PI / 180) * rotDeg;
    const pts = Array.from({ length: sides }, (_, i) => {
      const a = rot + (i * 2 * Math.PI) / sides - Math.PI / 2;
      const x = cx + r * Math.cos(a);
      const y = cy + r * Math.sin(a);
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    }).join(' ');
    return pts;
  };

  const centerPentR = 11.5;
  const ringHexR = 25.5;
  const hexR = 12.3;
  const outerPentR = 40.5;
  const outerPentSize = 10.5;

  const hexCenters = Array.from({ length: 5 }, (_, i) => {
    const ang = (-90 + i * 72) * Math.PI / 180;
    return [CX + ringHexR * Math.cos(ang), CY + ringHexR * Math.sin(ang)] as const;
  });

  const outerPentCenters = Array.from({ length: 5 }, (_, i) => {
    const ang = (-90 + 36 + i * 72) * Math.PI / 180;
    return [CX + outerPentR * Math.cos(ang), CY + outerPentR * Math.sin(ang)] as const;
  });

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 100 100"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <clipPath id="ballClip">
          <circle cx={CX} cy={CY} r={outerR} />
        </clipPath>
      </defs>

      {/* cerchio base */}
      <circle
        cx={CX}
        cy={CY}
        r={outerR}
        fill={primaryColor}
      />

      <g clipPath="url(#ballClip)">
        {/* pentagono centrale */}
        <polygon
          points={poly(CX, CY, centerPentR, 5, 0)}
          fill={secondaryColor}
          stroke={secondaryColor}
          strokeWidth={seamW}
        />

        {/* esagoni */}
        {hexCenters.map(([x, y], idx) => (
          <polygon
            key={`hex-${idx}`}
            points={poly(x, y, hexR, 6, 30)}
            fill={primaryColor}
            stroke={secondaryColor}
            strokeWidth={seamW}
          />
        ))}

        {/* pentagoni esterni */}
        {outerPentCenters.map(([x, y], idx) => (
          <polygon
            key={`opent-${idx}`}
            points={poly(x, y, outerPentSize, 5, 18)}
            fill={secondaryColor}
            stroke={secondaryColor}
            strokeWidth={seamW}
          />
        ))}

        {/* highlights */}
        <path d="M76 18c5 3 9 6 12 10" stroke={primaryColor} strokeWidth={seamW} fill="none" />
        <path d="M18 78c3 4 6 7 10 9" stroke={primaryColor} strokeWidth={seamW} fill="none" />
      </g>

      {/* ✅ contorno esterno visibile */}
      <circle
        cx={CX}
        cy={CY}
        r={outerR + seamW / 2}   // leggermente più grande
        fill="none"
        stroke={secondaryColor == '#ffffff' ? primaryColor : secondaryColor}
        strokeWidth={seamW * 1.5}
      />
    </svg>
  );
}


// Piede con scarpa e pallone per assist
function AssistSVG({
  color = "#000000",
  size = 19,
  strokeWidth = 12, // facoltativo: puoi regolare lo spessore del tratto
  ...props
}) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 200 200"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label="Assist icon"
      {...props}
    >
      {/* Contorno scarpa */}
      <path
        d="M18 122c0-38 18-58 48-64l64-14c5 10 17 17 31 17h31v96H18v-35Z"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        strokeLinejoin="round"
      />

      {/* Foro/occhiello superiore */}
      <circle
        cx="144"
        cy="56"
        r="14"
        stroke={color}
        strokeWidth={strokeWidth}
      />

      {/* Suola */}
      <line
        x1="18"
        y1="150"
        x2="192"
        y2="150"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />

      {/* Tre strisce diagonali */}
      <line
        x1="75"
        y1="68"
        x2="105"
        y2="140"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />
      <line
        x1="96"
        y1="62"
        x2="126"
        y2="140"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />
      <line
        x1="117"
        y1="58"
        x2="147"
        y2="140"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
      />

      {/* “Tasselli” suola */}
      <rect
        x="26"
        y="150"
        width="34"
        height="26"
        rx="4"
        stroke={color}
        strokeWidth={strokeWidth}
      />
      <rect
        x="78"
        y="150"
        width="34"
        height="26"
        rx="4"
        stroke={color}
        strokeWidth={strokeWidth}
      />
      <rect
        x="130"
        y="150"
        width="34"
        height="26"
        rx="4"
        stroke={color}
        strokeWidth={strokeWidth}
      />
    </svg>
  );
}

// Cartellino colorato per comportamenti
function BehaviourCardSVG({ color, size = 20 }: { color: string, size?: number }) {
  return (
    <svg width={size} height={size * 1.4} viewBox="0 0 20 28" fill="none">
      <rect x="2" y="2" width="16" height="24" rx="2" fill={color} stroke="rgba(0,0,0,0.2)" strokeWidth="1"/>
      <rect x="4" y="4" width="12" height="2" rx="1" fill="rgba(255,255,255,0.8)"/>
      <rect x="4" y="8" width="8" height="1" rx="0.5" fill="rgba(255,255,255,0.6)"/>
      <rect x="4" y="11" width="10" height="1" rx="0.5" fill="rgba(255,255,255,0.6)"/>
    </svg>
  )
}

export interface PlayerCardProps {
  player: LivePlayer
}

// ============================================
// CONFIGURAZIONE BONUS/MALUS CON ICONE SVG PERSONALIZZATE
// ============================================

// Configurazione per i comportamenti (cartellini)
const BEHAVIOUR_CONFIG = {
  [Behaviour.YellowCard]: { color: '#fbbf24', label: 'Cartellino Giallo' },
  [Behaviour.RedCard]: { color: '#ef4444', label: 'Cartellino Rosso' },
  [Behaviour.Nothing]: null
}

// Configurazione per i bonus/malus con ordine specifico e icone SVG
const BONUS_MALUS_CONFIG: {
  key: keyof Vote
  component: React.ComponentType<{ size?: number }>
  label: string
  description: string
  order: number
}[] = [
  { key: 'goals', component: () => <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#000000" />, label: 'Gol', description: 'gol segnati', order: 1 },
  { key: 'penalties', component: () => <SoccerBallSVG primaryColor="#000000" secondaryColor="#ffffff" />, label: 'Rigori', description: 'rigori segnati', order: 2 },
  { key: 'assists', component: () => <AssistSVG color="#000" />, label: 'Assist', description: 'assist forniti', order: 3 },
  { key: 'stoppedPenalties', component: () => <SoccerBallSVG primaryColor="#3b82f6" secondaryColor="#ffffff" />, label: 'Parate', description: 'rigori parati', order: 4 },
  { key: 'sufferedGoals', component: () => <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" />, label: 'Gol Subiti', description: 'gol subiti dal portiere', order: 5 },
  { key: 'wrongedPenalties', component: () => <SoccerBallSVG primaryColor="#10b981" secondaryColor="#ffffff" />, label: 'Rig. Sbagliati', description: 'rigori sbagliati', order: 6 },
  { key: 'ownGoals', component: () => <SoccerBallSVG primaryColor="#fbbf24" secondaryColor="#ffffff" />, label: 'Autogol', description: 'autogol segnati', order: 7 },
]

// Mappa per le informazioni di chance
const CHANCE_INFO_MAP = {
  injury: { icon: AlertTriangle, color: '#ef4444', label: 'Infortunato', text: 'INF' },
  fantagazzetta: { icon: null, color: '#16a34a', label: 'Fantagazzetta', text: 'F' },
  gazzetta: { icon: null, color: '#059669', label: 'Gazzetta', text: 'G' },
  mediaset: { icon: Info, color: '#0ea5e9', label: 'Mediaset', text: 'M' },
  sky: { icon: Star, color: '#f59e0b', label: 'Sky', text: 'S' },
  trend: { icon: TrendingUp, color: '#8b5cf6', label: 'Forma', text: 'T' },
}



export function PlayerCard({ player}: PlayerCardProps) {
  const [useFallbackImage, setUseFallbackImage] = useState(false)
  const [showInjuryTooltip, setShowInjuryTooltip] = useState(false)
  const [showGameTooltip, setShowGameTooltip] = useState(false)
  const [showBonusTooltip, setShowBonusTooltip] = useState<string | null>(null)
  const [showChanceTooltip, setShowChanceTooltip] = useState<string | null>(null)
  const [isBlinking, setIsBlinking] = useState(false)

  const current = (player as any).current ?? (player as any)
  const vote: Vote | null | undefined = (player as any).vote
  const chance: Chance | null | undefined = (player as any).chance
  const injury = (player as any).injury || (current && (current as any).injury)
  const realGame = player.realGame

  const name = current?.name ?? 'Giocatore'
  const teamName = current?.team?.name ?? ''

  const { src, fallback } = useMemo(() => getPlayerImageUrlFromName(current?.name), [current?.name])
  const imageUri = useFallbackImage ? fallback : src

  const voteLabel = vote ? (typeof vote.value === 'number' ? vote.value.toFixed(1) : String(vote.value)) : null

  // Determina se è durante una partita live per lampeggiamento voti
  const isGameLive = useMemo(() => {
    if (!realGame?.date || !vote) return false
    const now = new Date()
    const gameStart = convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString())
    const twoHoursAfterStart = new Date(gameStart.getTime() + 2 * 60 * 60 * 1000)
    return now >= gameStart && now <= twoHoursAfterStart
  }, [realGame, vote])

  // Determina se la partita non è ancora iniziata per mostrare chance info
  const isGameNotStarted = useMemo(() => {
    if (!realGame?.date) return true // Se non c'è data, consideriamo non iniziata
    const now = new Date()
    const gameStart = convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString())
    return now < gameStart
  }, [realGame])

  // Logica per determinare il colore del voto e se deve lampeggiare
  const voteDisplayInfo = useMemo(() => {
    if (!vote) return null

    const now = new Date()
    let backgroundColor = '#dc2626' // Rosso di default
    let shouldBlink = false

    // Determina il colore basato su isFinal e hasVote
    // VERDE: voto finale confermato (isFinal=true, hasVote=true)
    // ARANCIONE: voto live ma non finale (isFinal=false, hasVote=true) 
    // GRIGIO: voto non valido - non ha giocato abbastanza (hasVote=false)
    // ROSSO: casi edge/errore
    if (vote.isFinal && vote.hasVote) {
      backgroundColor = '#16a34a' // Verde - voto finale confermato
    } else if (!vote.isFinal && vote.hasVote) {
      backgroundColor = '#f59e0b' // Arancione - voto live ma non finale
    } else if (!vote.hasVote) {
      backgroundColor = '#6b7280' // Grigio - voto non valido (non ha giocato abbastanza)
    }

    // Determina se deve lampeggiare (solo se la partita è live e il giocatore non è uscito)
    // Il lampeggiamento si ferma se il giocatore è uscito (isOut=true)
    if (isGameLive && vote.hasVote && !vote.isOut) {
      shouldBlink = true
    }

    return {
      backgroundColor,
      shouldBlink,
      borderColor: vote.isFinal ? '#fff' : 'rgba(255, 255, 255, 0.7)'
    }
  }, [vote, isGameLive])

  // Effetto per gestire il lampeggiamento
  useEffect(() => {
    let interval: NodeJS.Timeout
    
    if (voteDisplayInfo?.shouldBlink) {
      interval = setInterval(() => {
        setIsBlinking(prev => !prev)
      }, 800) // Lampeggia ogni 800ms
    } else {
      setIsBlinking(false)
    }

    return () => {
      if (interval) clearInterval(interval)
    }
  }, [voteDisplayInfo?.shouldBlink])

  // Sistema organizzato per comportamenti e bonus/malus
  const playerBehaviourAndBonus = useMemo(() => {
    if (!vote) return { behaviour: null, bonusGroups: [], hasManOfTheMatch: false }

    // Comportamenti (cartellini)
    const behaviour = vote.status !== Behaviour.Nothing ? {
      type: vote.status,
      config: BEHAVIOUR_CONFIG[vote.status]
    } : null

    // Bonus/Malus raggruppati per tipo
    const bonusGroups: {
      config: typeof BONUS_MALUS_CONFIG[0]
      count: number
      items: React.ReactNode[]
    }[] = []

    for (const bonusConfig of BONUS_MALUS_CONFIG.sort((a, b) => a.order - b.order)) {
      const count = (vote as any)[bonusConfig.key] ?? 0
      if (count > 0) {
        // Crea array di icone per lo stesso tipo (se count > 1, mostra multiple icone)
        const items: React.ReactNode[] = []
        for (let i = 0; i < Math.min(count, 4); i++) { // Max 4 icone per tipo per evitare overflow
          items.push(
            <bonusConfig.component key={`${bonusConfig.key}-${i}`} size={14} />
          )
        }
        
        bonusGroups.push({
          config: bonusConfig,
          count,
          items
        })
      }
    }

    // Man of the Match
    const hasManOfTheMatch = (vote as any)?.manOfTheMatch ?? false

    return {
      behaviour,
      bonusGroups,
      hasManOfTheMatch
    }
  }, [vote])

  // Informazioni chance 
  const chanceInfo = useMemo(() => {
    if (!chance) return []
    
    const info: { type: keyof typeof CHANCE_INFO_MAP; value: any; label: string }[] = []
    
    // Solo se la partita non è ancora iniziata mostriamo F/G
    if (isGameNotStarted) {
      // Fantagazzetta
      if (chance.fantagazzetta !== undefined) {
        info.push({ 
          type: 'fantagazzetta', 
          value: chance.fantagazzetta,
          label: chance.fantagazzetta ? 'Titolare Fantalcacio.it' : 'Panchina Fantacalcio.it'
        })
      }
      
      // Gazzetta
      if (chance.gazzetta !== undefined) {
        info.push({ 
          type: 'gazzetta', 
          value: chance.gazzetta,
          label: chance.gazzetta ? 'Titolare Gazzetta' : 'Panchina Gazzetta'
        })
      }
    }
    
    // Infortunio sempre visibile
    if (chance.status === ChanceTypeEnum.Injury) {
      info.push({ 
        type: 'injury', 
        value: chance.description || 'Infortunato',
        label: 'Infortunio'
      })
    }
    
    return info
  }, [chance, isGameNotStarted])

  // Funzione per determinare il colore di background del nome basato sulla posizione originale
  const getPlayerNameBackgroundColor = useMemo(() => {
    const originalPosition = current?.position ?? player.currentPosition

    // Titolari (0-3) - Verde (come era)
    if (originalPosition >= FantaSoccerRole.GoalKeeper && originalPosition <= FantaSoccerRole.Forward) {
      return "#16a34a" // Verde per titolari
    }

    // Primi panchinari (4, First Backup roles)
    if (originalPosition === FantaSoccerRole.BackupGoalKeeper ||
        originalPosition === FantaSoccerRole.FirstBackupDefensor ||
        originalPosition === FantaSoccerRole.FirstBackupMidfielder ||
        originalPosition === FantaSoccerRole.FirstBackupForward) {
      return "#f59e0b" // Arancione per primi panchinari
    }

    // Secondi panchinari (Second Backup roles)
    if (originalPosition === FantaSoccerRole.SecondBackupDefensor ||
        originalPosition === FantaSoccerRole.SecondBackupMidfielder ||
        originalPosition === FantaSoccerRole.SecondBackupForward) {
      return "#3b82f6" // Blu per secondi panchinari
    }

    // Tribuna e altri (11 e < 0)
    if (originalPosition === FantaSoccerRole.Tribune || 
        originalPosition < FantaSoccerRole.GoalKeeper) {
      return "#dc2626" // Rosso per tribuna/altri
    }

    // Default fallback
    return "#6b7280" // Grigio per casi non gestiti
  }, [current?.position, player.currentPosition])

  // Game info helper - informazioni complete della partita
  const gameInfo = useMemo(() => {
    if (!realGame) return null
    const actualDate = new Date()
    const gameDate = realGame.date ? convertUTCToItalianTimezoneAsNotNullableDate(realGame.date.toISOString()) : null
    const isPlayed = gameDate && gameDate < actualDate
    
    const homeTeamName = realGame.home?.name || realGame.home
    const awayTeamName = realGame.away?.name || realGame.away
    const playerTeamName = teamName // Squadra del giocatore
    
    // Determina quale squadra è quella del giocatore
    const isPlayerInHomeTeam = String(homeTeamName).toLowerCase().includes(String(playerTeamName).toLowerCase()) ||
                               String(playerTeamName).toLowerCase().includes(String(homeTeamName).toLowerCase())
    
    // Crea versioni con case differenziato
    const homeTeamDisplay = isPlayerInHomeTeam ? 
      String(homeTeamName).toUpperCase() : 
      String(homeTeamName).toLowerCase()
    const awayTeamDisplay = !isPlayerInHomeTeam ? 
      String(awayTeamName).toUpperCase() : 
      String(awayTeamName).toLowerCase()
    
    // Versioni abbreviate con case differenziato
    const homeTeamShort = isPlayerInHomeTeam ? 
      String(homeTeamName).substring(0, 3).toUpperCase() : 
      String(homeTeamName).substring(0, 3).toLowerCase()
    const awayTeamShort = !isPlayerInHomeTeam ? 
      String(awayTeamName).substring(0, 3).toUpperCase() : 
      String(awayTeamName).substring(0, 3).toLowerCase()
    
    if (isPlayed) {
      return {
        type: 'result' as const,
        homeTeam: homeTeamName,
        awayTeam: awayTeamName,
        homeGoals: realGame.homeGoals,
        awayGoals: realGame.awayGoals,
        resultText: `${realGame.homeGoals ?? 0}-${realGame.awayGoals ?? 0}`,
        dateText: formatGameDateTime(gameDate),
        matchText: `${homeTeamDisplay} vs ${awayTeamDisplay}`,
        matchTextShort: `${homeTeamShort} vs ${awayTeamShort}`
      }
    } else {
      return {
        type: 'upcoming' as const,
        homeTeam: homeTeamName,
        awayTeam: awayTeamName,
        homeGoals: null,
        awayGoals: null,
        resultText: null,
        dateText: formatGameDateTime(gameDate),
        matchText: `${homeTeamDisplay} vs ${awayTeamDisplay}`,
        matchTextShort: `${homeTeamShort} vs ${awayTeamShort}`
      }
    }
  }, [realGame, teamName])

  // Dimensioni base fisse - il scaling viene gestito dal contenitore via cardScale
  const cardWidth = 112
  const cardHeight = 150 

  return (
    <Card
      width={cardWidth}
      height={cardHeight}
      padding={0}
      backgroundColor="#fff"
      borderRadius={12}
      borderWidth={2}
      borderColor="#e5e5e5"
      position="relative"
      elevation={4}
      overflow="hidden"
    >
      {/* IMMAGINE GIOCATORE - SFONDO COMPLETO della card con bordi arrotondati */}
      <Image
        source={{ uri: imageUri }}
        width={cardWidth}
        height={cardHeight}
        onError={() => setUseFallbackImage(true)}
        position="absolute"
        top={0}
        left={0}
        zIndex={1}
        borderRadius={12}
      />

      {/* Overlay semitrasparente per migliorare leggibilità */}
      <YStack
        position="absolute"
        top={0}
        left={0}
        right={0}
        bottom={0}
        backgroundColor="rgba(0,0,0,0.1)"
        zIndex={2}
      />
      {/* Vote badge - quadrato in alto a sinistra interno alla card */}
      {vote && voteDisplayInfo && (
        <YStack
          position="absolute"
          top={-4}
          left={-4}
          width={36}
          height={36}
          borderRadius={6}
          backgroundColor={voteDisplayInfo.shouldBlink && isBlinking ? 'rgba(255, 255, 255, 0.3)' : voteDisplayInfo.backgroundColor}
          alignItems="center"
          justifyContent="center"
          zIndex={25}
          borderWidth={2}
          borderColor="rgba(255, 255, 255, 0.9)"
          elevation={8}
          opacity={voteDisplayInfo.shouldBlink && isBlinking ? 0.8 : 1}
          transform={[
            { scale: voteDisplayInfo.shouldBlink && isBlinking ? 1.05 : 1.0 }
          ]}
        >
          <Text fontSize={13} fontWeight="900" color="#fff">
            {voteLabel}
          </Text>
          
          {/* Indicatori di stato - piccoli pallini nell'angolo in basso a destra del quadrato */}
          <XStack position="absolute" bottom={2} right={2} gap={1}>
            {!vote.hasVote && (
              <YStack
                width={4}
                height={4}
                borderRadius={2}
                backgroundColor="#ef4444"
                borderWidth={0.5}
                borderColor="#fff"
              />
            )}
            {!vote.isFinal && vote.hasVote && (
              <YStack
                width={4}
                height={4}
                borderRadius={2}
                backgroundColor="#f59e0b"
                borderWidth={0.5}
                borderColor="#fff"
              />
            )}
            {vote.isOut && (
              <YStack
                width={4}
                height={4}
                borderRadius={2}
                backgroundColor="#6b7280"
                borderWidth={0.5}
                borderColor="#fff"
              />
            )}
          </XStack>
        </YStack>
      )}

      {/* Injury indicator - angolo bottom-right dell'immagine */}
      {injury && (
        <Button
          position="absolute"
          bottom={55}
          right={8}
          width={32}
          height={32}
          borderRadius={16}
          backgroundColor="rgba(220, 38, 38, 0.95)"
          padding={0}
          onPress={() => setShowInjuryTooltip(!showInjuryTooltip)}
          zIndex={15}
          borderWidth={2}
          borderColor="#fff"
          elevation={5}
        >
          <AlertTriangle size={16} color="#fff" />
        </Button>
      )}

      {/* Injury tooltip */}
      {injury && showInjuryTooltip && (
        <YStack
          position="absolute"
          bottom={95}
          right={8}
          backgroundColor="rgba(0, 0, 0, 0.9)"
          padding={8}
          borderRadius={6}
          maxWidth={cardWidth - 16}
          zIndex={20}
          elevation={10}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
        >
          <Text fontSize={9} color="#fff" textAlign="center">
            {injury.reason || 'Infortunato'}
          </Text>
        </YStack>
      )}

      {/* Cartellino comportamentale - sotto il voto a sinistra */}
      {vote && playerBehaviourAndBonus.behaviour && (
        <YStack
          position="absolute"
          top={36}
          left={6}
          zIndex={24}
        >
          <BehaviourCardSVG 
            color={playerBehaviourAndBonus.behaviour.config!.color} 
            size={16}
          />
        </YStack>
      )}

      {/* Area bonus/malus organizzata - a destra */}
      {(vote && (playerBehaviourAndBonus.bonusGroups.length > 0 || chanceInfo.length > 0)) && (
        <YStack
          position="absolute"
          top={-4}
          right={-4}
          maxWidth={48}
          borderRadius={6}
          paddingVertical={6}
          paddingHorizontal={4}
          gap={4}
          alignItems="center"
          zIndex={20}
        >
          {/* Bonus/Malus organizzati per tipo */}
          {playerBehaviourAndBonus.bonusGroups.map((group, groupIndex) => (
            <YStack key={group.config.key} alignItems="center" gap={2}>
              {/* Riga di icone dello stesso tipo */}
              <XStack gap={2} flexWrap="wrap" justifyContent="center" maxWidth={40}>
                {group.items}
              </XStack>
              
              {/* Contatore se ci sono più di 4 elementi */}
              {group.count > 4 && (
                <Text fontSize={8} color="#fff" fontWeight="600">
                  +{group.count - 4}
                </Text>
              )}
              
              {/* Tooltip per gruppo */}
              {showBonusTooltip === group.config.key && (
                <YStack
                  position="absolute"
                  top={0}
                  left={-85}
                  backgroundColor="rgba(0, 0, 0, 0.95)"
                  padding={4}
                  borderRadius={4}
                  zIndex={35}
                  borderWidth={1}
                  borderColor="rgba(255, 255, 255, 0.3)"
                  minWidth={75}
                >
                  <Text fontSize={8} color="#fff" textAlign="center" fontWeight="600">
                    {group.count} {group.config.description}
                  </Text>
                </YStack>
              )}
            </YStack>
          ))}
          
          {/* Separatore se ci sono sia bonus che chance */}
          {playerBehaviourAndBonus.bonusGroups.length > 0 && chanceInfo.length > 0 && (
            <YStack
              width={30}
              height={1}
              backgroundColor="rgba(255, 255, 255, 0.3)"
              marginVertical={2}
            />
          )}
          
          {/* Chance Info Icons - versione compatta */}
          {chanceInfo.map((info, index) => {
            const config = CHANCE_INFO_MAP[info.type]
            const IconComponent = config.icon
            
            return (
              <YStack
                key={`${info.type}-${index}`}
                width={20}
                height={20}
                borderRadius={4}
                backgroundColor={config.color}
                alignItems="center"
                justifyContent="center"
                borderWidth={1}
                borderColor="rgba(255, 255, 255, 0.8)"
                onPress={() => setShowChanceTooltip(showChanceTooltip === `${info.type}-${index}` ? null : `${info.type}-${index}`)}
                pressStyle={{ scale: 0.9 }}
              >
                {IconComponent ? (
                  <IconComponent size={12} color="#fff" />
                ) : (
                  <Text fontSize={10} fontWeight="bold" color="#fff">
                    {config.text}
                  </Text>
                )}
                
                {/* Tooltip compatto per chance */}
                {showChanceTooltip === `${info.type}-${index}` && (
                  <YStack
                    position="absolute"
                    top={0}
                    left={-85}
                    backgroundColor="rgba(0, 0, 0, 0.95)"
                    padding={4}
                    borderRadius={4}
                    zIndex={35}
                    borderWidth={1}
                    borderColor="rgba(255, 255, 255, 0.3)"
                    minWidth={75}
                  >
                    <Text fontSize={8} color="#fff" textAlign="center" fontWeight="600">
                      {info.type === 'fantagazzetta' ? 'Fantagazzetta' : 
                       info.type === 'gazzetta' ? 'Gazzetta' : 
                       config.label}: {typeof info.value === 'boolean' ? (info.value ? 'Titolare' : 'Panchina') : info.value}
                    </Text>
                  </YStack>
                )}
              </YStack>
            )
          })}
        </YStack>
      )}

      {/* Banda informazioni partita Serie A - sopra il nome */}
      {gameInfo && (
        <YStack
          position="absolute"
          bottom={32}
          left={0}
          right={0}
          backgroundColor="rgba(0, 0, 0, 0.85)"
          paddingHorizontal={8}
          paddingVertical={6}
          alignItems="center"
          zIndex={9}
          borderTopWidth={1}
          borderBottomWidth={1}
          borderColor="rgba(255, 255, 255, 0.2)"
          onPress={() => setShowGameTooltip(!showGameTooltip)}
          onHoverIn={() => setShowGameTooltip(true)}
          onHoverOut={() => setShowGameTooltip(false)}
          pressStyle={{ backgroundColor: "rgba(0, 0, 0, 0.95)" }}
        >
          {/* Squadre della partita - versione abbreviata */}
          <Text
            fontSize={11}
            fontWeight="700"
            color="#fff"
            textAlign="center"
            numberOfLines={1}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
          >
            {gameInfo.matchTextShort}
          </Text>
          
          {/* Data o risultato */}
          <Text
            fontSize={9}
            fontWeight="600"
            color={gameInfo.type === 'result' ? "#10b981" : "#60a5fa"}
            textAlign="center"
            marginTop={1}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
            opacity={isGameLive && gameInfo.type === 'result' && isBlinking ? 0.7 : 1}
            transform={[
              { scale: isGameLive && gameInfo.type === 'result' && isBlinking ? 1.05 : 1.0 }
            ]}
          >
            {gameInfo.type === 'result' ? `${gameInfo.resultText}` : gameInfo.dateText}
          </Text>
        </YStack>
      )}

      {/* Tooltip nomi completi partita */}
      {gameInfo && showGameTooltip && (
        <YStack
          position="absolute"
          bottom={85}
          left={8}
          right={8}
          backgroundColor="rgba(0, 0, 0, 0.95)"
          padding={8}
          borderRadius={8}
          alignItems="center"
          zIndex={30}
          elevation={12}
          borderWidth={1}
          borderColor="rgba(255, 255, 255, 0.3)"
        >
          <Text
            fontSize={12}
            fontWeight="700"
            color="#fff"
            textAlign="center"
            numberOfLines={2}
            textShadowColor="rgba(0, 0, 0, 0.8)"
            textShadowOffset={{ width: 1, height: 1 }}
            textShadowRadius={1}
          >
            {gameInfo.matchText}
          </Text>
          {gameInfo.type === 'result' && (
            <Text
              fontSize={10}
              fontWeight="600"
              color="#10b981"
              textAlign="center"
              marginTop={4}
              opacity={isGameLive && isBlinking ? 0.7 : 1}
              transform={[
                { scale: isGameLive && isBlinking ? 1.05 : 1.0 }
              ]}
            >
              {gameInfo.resultText}
            </Text>
          )}
        </YStack>
      )}

      {/* Nome giocatore - footer prominente con colore dinamico basato su posizione originale */}
      <YStack
        position="absolute"
        bottom={0}
        left={0}
        right={0}
        backgroundColor={`${getPlayerNameBackgroundColor}E6`} // Aggiunge trasparenza (90%)
        paddingHorizontal={8}
        paddingVertical={8}
        alignItems="center"
        borderBottomLeftRadius={12}
        borderBottomRightRadius={12}
        zIndex={10}
        borderTopWidth={1}
        borderTopColor="rgba(255, 255, 255, 0.3)"
      >
        <Text
          fontSize={13}
          fontWeight="800"
          color="#fff"
          textAlign="center"
          numberOfLines={1}
          textTransform="uppercase"
          letterSpacing={0.5}
          textShadowColor="rgba(0, 0, 0, 0.5)"
          textShadowOffset={{ width: 1, height: 1 }}
          textShadowRadius={2}
        >
          {name}
        </Text>
      </YStack>
    </Card>
  )
}

export default PlayerCard
