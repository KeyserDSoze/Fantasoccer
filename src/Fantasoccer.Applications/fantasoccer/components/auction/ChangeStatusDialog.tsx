import { Dialog, Button, Card, Select, Label, Input, Text, XStack, YStack } from 'tamagui'
import { ChevronDown } from '@tamagui/lucide-icons-2'
import { useState } from 'react'

export interface ChangeStatusDialogProps {
  open: boolean
  playerKey: string | null
  playerName: string | null
  owner: string | null
  loading: boolean
  onOpenChange: (open: boolean) => void
  onConfirm: (owner: string, playerKey: string, status: number, revenue: number | null) => Promise<void>
  onCancel: () => void
  // League rule for players who left Serie A (default: half the price, rounded down).
  leftSerieARefundPercent?: number
  leftSerieARefundRoundUp?: boolean
}

export function ChangeStatusDialog({
  open,
  playerKey,
  playerName,
  owner,
  loading,
  onOpenChange,
  onConfirm,
  onCancel,
  leftSerieARefundPercent = 50,
  leftSerieARefundRoundUp = false,
}: ChangeStatusDialogProps) {
  const [statusType, setStatusType] = useState(0)
  const [revenue, setRevenue] = useState('')
  
  if (!playerKey || !playerName || !owner) return null
  
  const needsRevenue = statusType === 3 // Sold status
  
  return (
    <Dialog modal open={open}>
      <Dialog.Portal>
        <Dialog.Overlay 
          key="overlay" 
          transition="quick"
          opacity={0.5} 
          enterStyle={{ opacity: 0 }} 
          exitStyle={{ opacity: 0 }} 
        />
        <Dialog.Content 
          bordered 
          elevation="$2"
          key="content" 
          animateOnly={['transform', 'opacity']} 
          transition={['quick', { opacity: { overshootClamping: true } }]}
          enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} 
          exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} 
          gap="$4" 
          width={500} 
          maxWidth="95%"
        >
          <Dialog.Title fontSize="$6" fontWeight="700">Cambia Stato Giocatore</Dialog.Title>
          <Dialog.Description>{playerName}</Dialog.Description>
          
          <YStack gap="$3">
            <YStack gap="$2">
              <Label htmlFor="status-select" fontSize="$4">Motivo cambio stato:</Label>
              <Select 
                id="status-select" 
                value={statusType.toString()} 
                onValueChange={(val) => setStatusType(Number(val))}
              >
                <Select.Trigger iconAfter={ChevronDown}>
                  <Select.Value />
                </Select.Trigger>
                <Select.Content>
                  <Select.Viewport>
                    <Select.Item index={0} value="0">
                      <Select.ItemText>Attivo (ripristina)</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={1} value="5">
                      <Select.ItemText>Uscito dalla Serie A / infortunato</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={2} value="2">
                      <Select.ItemText>Scartato</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={3} value="3">
                      <Select.ItemText>Venduto ad altro giocatore</Select.ItemText>
                    </Select.Item>
                    <Select.Item index={4} value="4">
                      <Select.ItemText>Rimuovi giocatore</Select.ItemText>
                    </Select.Item>
                  </Select.Viewport>
                </Select.Content>
              </Select>
            </YStack>
            
            {needsRevenue && (
              <YStack gap="$2">
                <Label htmlFor="revenue-input" fontSize="$4">
                  Prezzo di vendita (crediti incassati):
                </Label>
                <Input 
                  id="revenue-input"
                  placeholder="Es: 58" 
                  value={revenue} 
                  onChangeText={setRevenue} 
                  keyboardType="numeric" 
                  size="$4" 
                />
                <Text fontSize="$2" color="$color10">
                  Tornano tutti alla squadra; il prezzo d&apos;acquisto resta tra i costi. Vuoto = 0.
                </Text>
              </YStack>
            )}
            
            <Card padding="$3" backgroundColor="$blue2" borderRadius="$3">
              <YStack gap="$2">
                <Text fontSize="$3" fontWeight="600" color="$blue11">Spiegazione stati:</Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Uscito dalla Serie A / infortunato:</Text> torna il {leftSerieARefundPercent}% del prezzo
                  (arrotondato per {leftSerieARefundRoundUp ? 'eccesso' : 'difetto'}), come da regole della lega
                </Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Scartato:</Text> scelta dell&apos;allenatore, non torna nulla
                </Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Venduto:</Text> torna il prezzo di vendita
                </Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Rimosso:</Text> correzione, come se non fosse mai stato acquistato
                </Text>
              </YStack>
            </Card>
            
            <XStack gap="$2" justifyContent="flex-end">
              <Button 
                variant="outlined" 
                onPress={() => {
                  onCancel()
                  setRevenue('')
                  setStatusType(0)
                  onOpenChange(false)
                }}
              >
                Annulla
              </Button>
              <Button 
                theme="orange" 
                disabled={loading} 
                onPress={async () => {
                  let revenueValue: number | null = null
                  if (needsRevenue) {
                    const parsed = parseInt(revenue || '0', 10)
                    revenueValue = Number.isNaN(parsed) ? 0 : Math.max(0, parsed)
                  }
                  
                  await onConfirm(owner, playerKey, statusType, revenueValue)
                  setRevenue('')
                  setStatusType(0)
                }}
              >
                Conferma
              </Button>
            </XStack>
          </YStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
