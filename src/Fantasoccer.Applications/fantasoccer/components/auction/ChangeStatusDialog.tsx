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
                    <Select.Item index={1} value="1">
                      <Select.ItemText>Infortunato/Estero</Select.ItemText>
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
                  Importo vendita (+ guadagno / - perdita):
                </Label>
                <Input 
                  id="revenue-input"
                  placeholder="Es: 50 o -30" 
                  value={revenue} 
                  onChangeText={setRevenue} 
                  keyboardType="numeric" 
                  size="$4" 
                />
                <Text fontSize="$2" color="$color10">
                  Lascia vuoto o 0 se nessun importo da registrare
                </Text>
              </YStack>
            )}
            
            <Card padding="$3" backgroundColor="$blue2" borderRadius="$3">
              <YStack gap="$2">
                <Text fontSize="$3" fontWeight="600" color="$blue11">Spiegazione stati:</Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Infortunato/Estero:</Text> Venduto per un girone (torna secondo girone)
                </Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Scartato:</Text> Giocatore rimosso per scelta dell&apos;allenatore
                </Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Venduto:</Text> Venduto ad un altro giocatore (con/senza prezzo)
                </Text>
                <Text fontSize="$2" color="$blue11">
                  • <Text fontWeight="600">Rimosso:</Text> Giocatore rimosso definitivamente
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
                  if (needsRevenue && revenue) {
                    const parsed = parseInt(revenue, 10)
                    if (!isNaN(parsed)) revenueValue = parsed
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
