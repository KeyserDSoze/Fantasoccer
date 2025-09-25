'use client'

import React, { useMemo } from 'react'
import { 
  XStack, 
  YStack, 
  Text, 
  ScrollView,
  styled,
  View,
  Image,
  useTheme
} from 'tamagui'
import { RankedTeam, EnhancedRankedTeam } from '../types/rank'
import { getTeamLogoProps } from '../utils/teamLogo'

// Types
export interface RankingTableColumn {
  key: string
  title: string
  shortTitle?: string  // For mobile
  width?: number
  minWidth?: number
  align?: 'left' | 'center' | 'right'
  render?: (team: RankedTeam | EnhancedRankedTeam, index: number) => React.ReactNode
}

export interface RankingTableProps {
  teams: (RankedTeam | EnhancedRankedTeam)[]
  columns?: RankingTableColumn[]
  groupId: string
  year: number
  showPosition?: boolean
  isLoading?: boolean
  emptyMessage?: string
  maxHeight?: number
  stickyFirstColumn?: boolean
}

// Styled components
const TableContainer = styled(View, {
  flex: 1,
  backgroundColor: '$background',
  borderRadius: '$4',
  overflow: 'hidden',
  borderWidth: 1,
  borderColor: '$borderColor',
})

const HeaderRow = styled(XStack, {
  backgroundColor: '$gray2',
  borderBottomWidth: 1,
  borderBottomColor: '$borderColor',
  minHeight: 48,
})

const DataRow = styled(XStack, {
  borderBottomWidth: 1,
  borderBottomColor: '$borderColorLighter',
  minHeight: 56,
  backgroundColor: '$background',
  hoverStyle: {
    backgroundColor: '$gray1',
  },
  
  variants: {
    odd: {
      true: {
        backgroundColor: '$gray0',
        hoverStyle: {
          backgroundColor: '$gray1',
        },
      }
    }
  } as const,
})

const Cell = styled(View, {
  paddingHorizontal: '$3',
  paddingVertical: '$2',
  justifyContent: 'center',
  borderRightWidth: 1,
  borderRightColor: '$borderColorLighter',
  
  variants: {
    align: {
      left: { alignItems: 'flex-start' },
      center: { alignItems: 'center' },
      right: { alignItems: 'flex-end' },
    },
    header: {
      true: {
        backgroundColor: '$gray2',
        borderRightColor: '$borderColor',
      }
    },
    sticky: {
      true: {
        position: 'sticky',
        left: 0,
        zIndex: 2,
        backgroundColor: '$background',
        borderRightWidth: 2,
        borderRightColor: '$borderColor',
        shadowColor: '$shadowColor',
        shadowOffset: { width: 2, height: 0 },
        shadowOpacity: 0.1,
        shadowRadius: 4,
      }
    }
  } as const,
})

const StickyHeaderCell = styled(Cell, {
  backgroundColor: '$gray2',
  position: 'sticky',
  left: 0,
  zIndex: 3,
  borderRightWidth: 2,
  borderRightColor: '$borderColor',
  shadowColor: '$shadowColor',
  shadowOffset: { width: 2, height: 0 },
  shadowOpacity: 0.1,
  shadowRadius: 4,
})

// Default columns configuration
const getDefaultColumns = (): RankingTableColumn[] => [
  {
    key: 'position',
    title: 'Pos',
    shortTitle: 'P',
    width: 50,
    align: 'center',
    render: (team, index) => (
      <Text fontSize="$4" fontWeight="600" color="$color">
        {'position' in team && team.position ? team.position : index + 1}
      </Text>
    )
  },
  {
    key: 'points',
    title: 'Punti',
    shortTitle: 'P',
    width: 70,
    align: 'center',
    render: (team) => (
      <Text fontSize="$4" fontWeight="600" color="$blue10">
        {team.point}
      </Text>
    )
  },
  {
    key: 'valuePoints',
    title: 'VP',
    shortTitle: 'VP',
    width: 70,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$color">
        {team.valuePoint}
      </Text>
    )
  },
  {
    key: 'victories',
    title: 'V',
    shortTitle: 'V',
    width: 50,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$green10">
        {team.victories}
      </Text>
    )
  },
  {
    key: 'draws',
    title: 'D',
    shortTitle: 'D',
    width: 50,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$yellow10">
        {team.draws}
      </Text>
    )
  },
  {
    key: 'defeats',
    title: 'De',
    shortTitle: 'De',
    width: 50,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$red10">
        {team.defeats}
      </Text>
    )
  },
  {
    key: 'goals',
    title: 'G',
    shortTitle: 'G',
    width: 50,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$color">
        {team.goal}
      </Text>
    )
  },
  {
    key: 'sufferedGoals',
    title: 'SG',
    shortTitle: 'SG',
    width: 50,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$color">
        {team.sufferedGoal}
      </Text>
    )
  },
  {
    key: 'sufferedValuePoints',
    title: 'SV',
    shortTitle: 'SV',
    width: 70,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$color">
        {team.sufferedValuePoint}
      </Text>
    )
  },
  {
    key: 'money',
    title: 'M',
    shortTitle: 'M',
    width: 60,
    align: 'center',
    render: (team) => (
      <Text fontSize="$3" color="$color">
        {team.money}
      </Text>
    )
  }
]

// Team name and logo column (always first and sticky)
const getTeamNameColumn = (groupId: string, year: number): RankingTableColumn => ({
  key: 'team',
  title: 'Squadra',
  shortTitle: 'Squadra',
  minWidth: 180,
  width: 200,
  align: 'left',
  render: (team) => (
    <XStack space="$2" alignItems="center" flex={1}>
      <Image
        {...getTeamLogoProps({
          groupId,
          year,
          email: team.owner,
          size: 32
        })}
      />
      <YStack flex={1} minWidth={0}>
        <Text 
          fontSize="$4" 
          fontWeight="600" 
          color="$color"
          numberOfLines={1}
          ellipsizeMode="tail"
        >
          {team.name}
        </Text>
        <Text 
          fontSize="$2" 
          color="$gray10"
          numberOfLines={1}
          ellipsizeMode="tail"
        >
          {team.owner}
        </Text>
      </YStack>
    </XStack>
  )
})

export function RankingTable({
  teams,
  columns,
  groupId,
  year,
  showPosition = true,
  isLoading = false,
  emptyMessage = 'Nessuna squadra trovata',
  maxHeight = 600,
  stickyFirstColumn = true
}: RankingTableProps) {
  const theme = useTheme()

  // Memoize columns configuration
  const tableColumns = useMemo(() => {
    const teamColumn = getTeamNameColumn(groupId, year)
    const defaultColumns = getDefaultColumns()
    const customColumns = columns || defaultColumns

    // Filter position column if not needed
    const filteredColumns = showPosition 
      ? customColumns 
      : customColumns.filter(col => col.key !== 'position')

    return [teamColumn, ...filteredColumns]
  }, [groupId, year, columns, showPosition])

  // Loading state
  if (isLoading) {
    return (
      <TableContainer>
        <YStack padding="$4" alignItems="center" justifyContent="center" minHeight={200}>
          <Text color="$gray10">Caricamento...</Text>
        </YStack>
      </TableContainer>
    )
  }

  // Empty state
  if (teams.length === 0) {
    return (
      <TableContainer>
        <YStack padding="$4" alignItems="center" justifyContent="center" minHeight={200}>
          <Text color="$gray10">{emptyMessage}</Text>
        </YStack>
      </TableContainer>
    )
  }

  return (
    <TableContainer>
      <ScrollView 
        horizontal={true}
        showsHorizontalScrollIndicator={true}
        maxHeight={maxHeight}
      >
        <YStack minWidth="100%">
          {/* Header */}
          <HeaderRow>
            {tableColumns.map((column, index) => {
              const isFirstColumn = index === 0
              const CellComponent = (isFirstColumn && stickyFirstColumn) ? StickyHeaderCell : Cell
              
              return (
                <CellComponent
                  key={column.key}
                  header
                  sticky={isFirstColumn && stickyFirstColumn}
                  align={column.align || 'left'}
                  width={column.width}
                  minWidth={column.minWidth || column.width}
                >
                  <Text fontSize="$3" fontWeight="600" color="$color" textAlign={column.align}>
                    {column.shortTitle || column.title}
                  </Text>
                </CellComponent>
              )
            })}
          </HeaderRow>

          {/* Data Rows */}
          <ScrollView 
            maxHeight={maxHeight - 48}
            showsVerticalScrollIndicator={true}
          >
            {teams.map((team, teamIndex) => (
              <DataRow key={`${team.owner}-${teamIndex}`} odd={teamIndex % 2 === 1}>
                {tableColumns.map((column, columnIndex) => {
                  const isFirstColumn = columnIndex === 0
                  const CellComponent = Cell
                  
                  return (
                    <CellComponent
                      key={`${team.owner}-${column.key}`}
                      sticky={isFirstColumn && stickyFirstColumn}
                      align={column.align || 'left'}
                      width={column.width}
                      minWidth={column.minWidth || column.width}
                    >
                      {column.render ? column.render(team, teamIndex) : (
                        <Text fontSize="$3" color="$color">
                          {(team as any)[column.key] || '-'}
                        </Text>
                      )}
                    </CellComponent>
                  )
                })}
              </DataRow>
            ))}
          </ScrollView>
        </YStack>
      </ScrollView>
    </TableContainer>
  )
}

// Export utility functions for custom columns
export { getDefaultColumns, getTeamNameColumn }