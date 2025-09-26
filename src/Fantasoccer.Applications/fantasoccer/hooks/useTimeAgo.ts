// Hook per mostrare il tempo trascorso dall'ultimo aggiornamento
import { useState, useEffect } from 'react'

export function useTimeAgo(date: Date | null) {
  const [timeAgo, setTimeAgo] = useState<string>('Mai')

  useEffect(() => {
    if (!date) {
      setTimeAgo('Mai')
      return
    }

    const updateTimeAgo = () => {
      const now = new Date()
      const diffMs = now.getTime() - date.getTime()
      const diffSeconds = Math.floor(diffMs / 1000)
      
      if (diffSeconds < 60) {
        setTimeAgo(`${diffSeconds}s fa`)
      } else if (diffSeconds < 3600) {
        setTimeAgo(`${Math.floor(diffSeconds / 60)}m fa`)
      } else {
        setTimeAgo(date.toLocaleTimeString('it-IT', { 
          hour: '2-digit', 
          minute: '2-digit' 
        }))
      }
    }

    // Aggiorna subito
    updateTimeAgo()

    // Aggiorna ogni secondo
    const interval = setInterval(updateTimeAgo, 1000)

    return () => clearInterval(interval)
  }, [date])

  return timeAgo
}