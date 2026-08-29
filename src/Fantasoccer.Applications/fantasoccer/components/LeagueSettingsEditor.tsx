import { useCallback, useEffect, useState } from 'react'
import { Button, Card, Input, Label, Separator, Switch, Text, XStack, YStack } from 'tamagui'
import { Plus, Trash2 } from '@tamagui/lucide-icons-2'
import {
  DefaultLeagueSetting,
  Role,
  RoleExtensions,
  type LeagueSetting,
  type VoteLeagueSetting
} from 'types/group'

type NumberSettingKey =
  | 'startingMoney'
  | 'delayedDay'
  | 'cancelledDay'
  | 'pointForFirstGoal'
  | 'pointForNextGoal'
  | 'pointForOwnGoal'
  | 'differencePointForOwnGoal'
  | 'pointForVictory'
  | 'pointForDefeat'
  | 'pointForDraw'
  | 'pointForStrongDefense'
  | 'pointForStrongDefense4'
  | 'pointForStrongDefense5'
  | 'pointForGoodPeople'
  | 'pointForCleanSheet'
  | 'moneyForGoal'
  | 'moneyForSufferedGoal'

type VoteSettingKey = keyof VoteLeagueSetting

interface NumberFieldDefinition {
  key: NumberSettingKey
  label: string
  description?: string
  min?: number
  max?: number
}

interface VoteFieldDefinition {
  key: VoteSettingKey
  label: string
  integer?: boolean
}

interface NumericFieldProps {
  id: string
  label: string
  description?: string
  value: number
  integer?: boolean
  min?: number
  max?: number
  disabled: boolean
  onChange: (value: number) => void
  onValidityChange: (id: string, valid: boolean) => void
}

interface LeagueSettingsEditorProps {
  value: LeagueSetting
  disabled: boolean
  resetKey: string
  onChange: (value: LeagueSetting) => void
  onValidityChange: (valid: boolean) => void
}

const generalFields: NumberFieldDefinition[] = [
  { key: 'startingMoney', label: 'Crediti iniziali', description: 'Budget disponibile a inizio stagione.', min: 25 },
  { key: 'delayedDay', label: 'Ritardo prima giornata', description: 'Numero di giornate di Serie A da saltare.', min: 0, max: 37 },
  { key: 'cancelledDay', label: 'Giornate annullate', description: 'Prime giornate fantasy considerate annullate.', min: 0, max: 38 }
]

const resultFields: NumberFieldDefinition[] = [
  { key: 'pointForVictory', label: 'Punti vittoria' },
  { key: 'pointForDraw', label: 'Punti pareggio' },
  { key: 'pointForDefeat', label: 'Punti sconfitta' },
  { key: 'pointForFirstGoal', label: 'Soglia primo gol', min: 1 },
  { key: 'pointForNextGoal', label: 'Intervallo gol successivi', min: 1 },
  { key: 'pointForOwnGoal', label: 'Soglia autogol', description: 'Distanza dalla soglia del primo gol.', min: 0 },
  { key: 'differencePointForOwnGoal', label: 'Differenza per autogol', min: 0 }
]

const bonusFields: NumberFieldDefinition[] = [
  { key: 'pointForStrongDefense', label: 'Bonus difesa con 3 difensori' },
  { key: 'pointForStrongDefense4', label: 'Bonus difesa con 4 difensori' },
  { key: 'pointForStrongDefense5', label: 'Bonus difesa con 5 difensori' },
  { key: 'pointForGoodPeople', label: 'Bonus fair play' },
  { key: 'pointForCleanSheet', label: 'Bonus porta inviolata', min: 0 },
  { key: 'moneyForGoal', label: 'Crediti per gol segnato' },
  { key: 'moneyForSufferedGoal', label: 'Crediti per gol subito' }
]

const voteFields: VoteFieldDefinition[] = [
  { key: 'goal', label: 'Gol' },
  { key: 'penalty', label: 'Rigore segnato' },
  { key: 'sufferedGoal', label: 'Gol subito' },
  { key: 'stoppedPenalty', label: 'Rigore parato' },
  { key: 'wrongedPenalty', label: 'Rigore sbagliato' },
  { key: 'ownGoal', label: 'Autogol' },
  { key: 'assist', label: 'Assist' },
  { key: 'yellowCard', label: 'Ammonizione' },
  { key: 'redCard', label: 'Espulsione' },
  { key: 'injury', label: 'Infortunio', integer: true },
  { key: 'manOfTheMatch', label: 'Migliore in campo', integer: true }
]

const roles = [Role.Undefined, Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward]

const roleLabel = (role: Role) => role === Role.Undefined
  ? 'Tutti i ruoli (default)'
  : RoleExtensions.asString(role)

const getNumericError = (
  text: string,
  integer: boolean,
  min?: number,
  max?: number
): string | null => {
  const normalized = text.replace(',', '.')
  const pattern = integer ? /^-?\d+$/ : /^-?(?:\d+(?:\.\d*)?|\.\d+)$/
  const parsed = Number(normalized)

  if (!pattern.test(normalized) || !Number.isFinite(parsed)) {
    return integer ? 'Inserisci un numero intero.' : 'Inserisci un numero valido.'
  }
  if (min !== undefined && parsed < min) return `Il valore minimo è ${min}.`
  if (max !== undefined && parsed > max) return `Il valore massimo è ${max}.`
  return null
}

function NumericField({
  id,
  label,
  description,
  value,
  integer = true,
  min,
  max,
  disabled,
  onChange,
  onValidityChange
}: NumericFieldProps) {
  const [text, setText] = useState(String(value))
  const [error, setError] = useState<string | null>(() => getNumericError(String(value), integer, min, max))

  useEffect(() => {
    onValidityChange(id, error === null)
    return () => onValidityChange(id, true)
  }, [id, error, onValidityChange])

  const handleChange = (nextText: string) => {
    setText(nextText)
    const normalized = nextText.replace(',', '.')
    const parsed = Number(normalized)
    const message = getNumericError(nextText, integer, min, max)

    setError(message)
    if (message === null) onChange(parsed)
  }

  return (
    <YStack gap="$1.5" flexGrow={1} flexBasis={280} minWidth={240}>
      <Label htmlFor={id}>{label}</Label>
      {description && <Text fontSize="$2" color="$color10">{description}</Text>}
      <Input
        id={id}
        value={text}
        onChangeText={handleChange}
        onBlur={() => {
          if (error) {
            const persistedText = String(value)
            setText(persistedText)
            setError(getNumericError(persistedText, integer, min, max))
          }
        }}
        keyboardType={min !== undefined && min >= 0 ? (integer ? 'number-pad' : 'decimal-pad') : 'default'}
        borderColor={error ? '$red8' : '$borderColor'}
        disabled={disabled}
      />
      {error && <Text fontSize="$2" color="$red10">{error}</Text>}
    </YStack>
  )
}

export function LeagueSettingsEditor({
  value,
  disabled,
  resetKey,
  onChange,
  onValidityChange
}: LeagueSettingsEditorProps) {
  const [selectedRole, setSelectedRole] = useState<Role>(Role.Undefined)
  const [invalidFields, setInvalidFields] = useState<Set<string>>(new Set())

  const handleValidityChange = useCallback((id: string, valid: boolean) => {
    setInvalidFields(current => {
      const next = new Set(current)
      if (valid) next.delete(id)
      else next.add(id)
      onValidityChange(next.size === 0)
      return next
    })
  }, [onValidityChange])

  const updateNumberSetting = (key: NumberSettingKey, nextValue: number) => {
    onChange({ ...value, [key]: nextValue })
  }

  const defaultVotes = value.votes[Role.Undefined] || DefaultLeagueSetting.votes[Role.Undefined]!
  const selectedVotes = value.votes[selectedRole] || defaultVotes
  const hasRoleOverride = selectedRole === Role.Undefined || value.votes[selectedRole] !== undefined

  const updateVoteSetting = (key: VoteSettingKey, nextValue: number) => {
    onChange({
      ...value,
      votes: {
        ...value.votes,
        [selectedRole]: {
          ...selectedVotes,
          [key]: nextValue
        }
      }
    })
  }

  const renderNumberFields = (fields: NumberFieldDefinition[], prefix: string) => (
    <XStack gap="$3" flexWrap="wrap" alignItems="flex-start">
      {fields.map(field => (
        <NumericField
          key={`${resetKey}-${prefix}-${field.key}`}
          id={`${resetKey}-${prefix}-${field.key}`}
          label={field.label}
          description={field.description}
          value={value[field.key]}
          min={field.min}
          max={field.max}
          disabled={disabled}
          onChange={nextValue => updateNumberSetting(field.key, nextValue)}
          onValidityChange={handleValidityChange}
        />
      ))}
    </XStack>
  )

  return (
    <YStack gap="$4">
      <Card padding="$4" backgroundColor="$color1" borderWidth="$0.5" borderColor="$borderColor">
        <YStack gap="$3">
          <Text fontSize="$4" fontWeight="700" color="$color12">Stagione e classifica</Text>
          <Text fontSize="$2" color="$color10">Budget, avvio della stagione e punti assegnati in classifica.</Text>
          <Separator />
          {renderNumberFields(generalFields, 'general')}
          <XStack gap="$3" flexWrap="wrap">
            <YStack gap="$2" flexGrow={1} flexBasis={280} minWidth={240}>
              <Label>Classifica con fantapunteggio</Label>
              <Switch
                checked={value.rankWithValuePoints}
                onCheckedChange={checked => onChange({ ...value, rankWithValuePoints: checked })}
                disabled={disabled}
              />
              <Text fontSize="$2" color="$color10">Somma il fantapunteggio ai punti di vittoria, pareggio o sconfitta.</Text>
            </YStack>
          </XStack>
        </YStack>
      </Card>

      <Card padding="$4" backgroundColor="$color1" borderWidth="$0.5" borderColor="$borderColor">
        <YStack gap="$3">
          <Text fontSize="$4" fontWeight="700" color="$color12">Risultati e soglie gol</Text>
          <Text fontSize="$2" color="$color10">Conversione del fantapunteggio in gol e punti della classifica.</Text>
          <Separator />
          {renderNumberFields(resultFields, 'results')}
        </YStack>
      </Card>

      <Card padding="$4" backgroundColor="$color1" borderWidth="$0.5" borderColor="$borderColor">
        <YStack gap="$3">
          <Text fontSize="$4" fontWeight="700" color="$color12">Bonus ed economia</Text>
          <Text fontSize="$2" color="$color10">Bonus formazione e crediti maturati con i risultati.</Text>
          <Separator />
          {renderNumberFields(bonusFields, 'bonuses')}
        </YStack>
      </Card>

      <Card padding="$4" backgroundColor="$color1" borderWidth="$0.5" borderColor="$borderColor">
        <YStack gap="$3">
          <Text fontSize="$4" fontWeight="700" color="$color12">Modificatori voto</Text>
          <Text fontSize="$2" color="$color10">
            Il default vale per tutti i ruoli. Gli override sostituiscono il default solo per il ruolo selezionato.
          </Text>
          <Separator />

          <XStack gap="$2" flexWrap="wrap">
            {roles.map(role => (
              <Button
                key={role}
                size="$3"
                variant="outlined"
                borderColor={selectedRole === role ? '$blue8' : '$borderColor'}
                backgroundColor={selectedRole === role ? '$blue2' : 'transparent'}
                onPress={() => setSelectedRole(role)}
                disabled={disabled}
              >
                {roleLabel(role)}
              </Button>
            ))}
          </XStack>

          {selectedRole !== Role.Undefined && !hasRoleOverride && (
            <Card padding="$3" backgroundColor="$blue2" borderColor="$blue7" borderWidth="$0.5">
              <XStack gap="$3" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                <Text color="$blue11" flex={1} minWidth={220}>
                  {roleLabel(selectedRole)} usa i modificatori di default.
                </Text>
                <Button
                  size="$3"
                  icon={Plus}
                  onPress={() => onChange({
                    ...value,
                    votes: {
                      ...value.votes,
                      [selectedRole]: { ...defaultVotes }
                    }
                  })}
                  disabled={disabled}
                  theme="blue"
                >
                  Crea override
                </Button>
              </XStack>
            </Card>
          )}

          {hasRoleOverride && (
            <>
              {selectedRole !== Role.Undefined && (
                <XStack justifyContent="flex-end">
                  <Button
                    size="$3"
                    icon={Trash2}
                    variant="outlined"
                    theme="red"
                    onPress={() => {
                      const votes = { ...value.votes }
                      delete votes[selectedRole]
                      onChange({ ...value, votes })
                    }}
                    disabled={disabled}
                  >
                    Rimuovi override
                  </Button>
                </XStack>
              )}
              <XStack gap="$3" flexWrap="wrap" alignItems="flex-start">
                {voteFields.map(field => (
                  <NumericField
                    key={`${resetKey}-${selectedRole}-${field.key}`}
                    id={`${resetKey}-votes-${selectedRole}-${field.key}`}
                    label={field.label}
                    value={selectedVotes[field.key]}
                    integer={field.integer ?? false}
                    disabled={disabled}
                    onChange={nextValue => updateVoteSetting(field.key, nextValue)}
                    onValidityChange={handleValidityChange}
                  />
                ))}
              </XStack>
            </>
          )}
        </YStack>
      </Card>

      {invalidFields.size > 0 && (
        <Text color="$red10" fontWeight="600">
          Correggi i valori non validi prima di salvare.
        </Text>
      )}

      <Card padding="$3" backgroundColor="$color2" borderColor="$borderColor" borderWidth="$0.5">
        <Text fontSize="$2" color="$color10">
          Formazione avanzata, struttura round, limiti rosa, mercato e carte allenatore vengono preservati e copiati,
          ma non sono mostrati perché non sono ancora applicati dal motore di gioco.
        </Text>
      </Card>
    </YStack>
  )
}
