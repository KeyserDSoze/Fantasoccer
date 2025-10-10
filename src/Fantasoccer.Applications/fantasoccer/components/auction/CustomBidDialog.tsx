import { Dialog, Button, Input, XStack, YStack } from 'tamagui'
import { useState } from 'react'

export interface CustomBidDialogProps {
  open: boolean
  currentBid: number
  onOpenChange: (open: boolean) => void
  onConfirm: (amount: number) => void
}

export function CustomBidDialog({ open, currentBid, onOpenChange, onConfirm }: CustomBidDialogProps) {
  const [customBidAmount, setCustomBidAmount] = useState('')
  
  return (
    <Dialog modal open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay 
          key="overlay" 
          animation="quick" 
          opacity={0.5} 
          enterStyle={{ opacity: 0 }} 
          exitStyle={{ opacity: 0 }} 
        />
        <Dialog.Content 
          bordered 
          elevate 
          key="content" 
          animateOnly={['transform', 'opacity']} 
          animation={['quick', { opacity: { overshootClamping: true } }]} 
          enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} 
          exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} 
          gap="$4" 
          width={400} 
          maxWidth="90%"
        >
          <Dialog.Title fontSize="$6" fontWeight="700">Offerta Personalizzata</Dialog.Title>
          <Dialog.Description>
            Inserisci l'importo della tua offerta (deve essere superiore a €{currentBid})
          </Dialog.Description>
          
          <YStack gap="$3">
            <Input 
              placeholder="Importo (€)" 
              value={customBidAmount} 
              onChangeText={setCustomBidAmount} 
              keyboardType="numeric" 
              size="$5" 
            />
            
            <XStack gap="$2" justifyContent="flex-end">
              <Dialog.Close asChild>
                <Button variant="outlined">Annulla</Button>
              </Dialog.Close>
              <Button 
                theme="blue" 
                onPress={() => {
                  const amount = parseInt(customBidAmount, 10)
                  if (!isNaN(amount) && amount > 0) {
                    onConfirm(amount)
                    setCustomBidAmount('')
                    onOpenChange(false)
                  }
                }}
              >
                Conferma Offerta
              </Button>
            </XStack>
          </YStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
