'use client'

import React, { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { useApp } from '../provider/AppProvider'
import { SplashScreen } from '../components/SplashScreen'
import { HomeScreen } from '../features/home/screen'
import { LoginScreen } from 'app/features/auth'

interface AppInitializerProps {
  children?: React.ReactNode
}

export const AppInitializer: React.FC<AppInitializerProps> = ({ children }) => {
  const { isAuthenticated, isLoading: authLoading, user } = useAuth()
  const { isGroupsLoading, currentGroup, userGroups } = useApp()
  const [initMessage, setInitMessage] = useState("Inizializzazione...")

  // Determina lo stato di caricamento complessivo
  const isInitializing = authLoading || (isAuthenticated && isGroupsLoading)

  // Aggiorna il messaggio di caricamento in base allo stato
  useEffect(() => {
    if (authLoading) {
      setInitMessage("Verifica autenticazione...")
    } else if (isAuthenticated && isGroupsLoading) {
      setInitMessage("Caricamento dati utente...")
    } else if (isAuthenticated && userGroups.length === 0) {
      setInitMessage("Configurazione gruppo...")
    } else {
      setInitMessage("Completamento...")
    }
  }, [authLoading, isAuthenticated, isGroupsLoading, userGroups.length])

  // Mostra splash screen durante l'inizializzazione
  if (isInitializing) {
    return <SplashScreen message={initMessage} />
  }

  // Se non autenticato, mostra la home screen (che include il welcome)
  if (!isAuthenticated) {
    return <LoginScreen />
  }

  // Se autenticato ma senza gruppi, mostra messaggio appropriato
  if (isAuthenticated && userGroups.length === 0) {
    return (
      <SplashScreen 
        message="Nessun gruppo trovato. Contatta l'amministratore." 
        showSpinner={false}
      />
    )
  }

  // Tutto caricato correttamente, mostra l'app normale
  return children ? <>{children}</> : <HomeScreen />
}