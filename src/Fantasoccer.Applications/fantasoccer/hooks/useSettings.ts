import { useState, useCallback } from 'react'
import { settingsService, ChangeTeamNameParams, UploadLogoParams } from 'services/settingsService'

export interface UseSettingsResult {
  // Team logo
  uploadingLogo: boolean
  uploadLogoError: string | null
  uploadLogo: (params: UploadLogoParams) => Promise<boolean>

  // Team name
  changingTeamName: boolean
  changeTeamNameError: string | null
  changeTeamNameMessages: string[]
  changeTeamName: (params: ChangeTeamNameParams) => Promise<boolean>

  // Username
  updatingUsername: boolean
  updateUsernameError: string | null
  updateUsername: (newUsername: string) => Promise<boolean>
}

export function useSettings(): UseSettingsResult {
  // Team logo state
  const [uploadingLogo, setUploadingLogo] = useState(false)
  const [uploadLogoError, setUploadLogoError] = useState<string | null>(null)

  // Team name state
  const [changingTeamName, setChangingTeamName] = useState(false)
  const [changeTeamNameError, setChangeTeamNameError] = useState<string | null>(null)
  const [changeTeamNameMessages, setChangeTeamNameMessages] = useState<string[]>([])

  // Username state
  const [updatingUsername, setUpdatingUsername] = useState(false)
  const [updateUsernameError, setUpdateUsernameError] = useState<string | null>(null)

  // Upload team logo
  const uploadLogo = useCallback(async (params: UploadLogoParams): Promise<boolean> => {
    setUploadingLogo(true)
    setUploadLogoError(null)

    try {
      const success = await settingsService.uploadLogo(params)
      
      if (!success) {
        setUploadLogoError('Impossibile caricare il logo. Verifica di essere il proprietario della squadra.')
      }

      return success
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Errore sconosciuto durante il caricamento del logo'
      setUploadLogoError(errorMessage)
      console.error('Error uploading logo:', error)
      return false
    } finally {
      setUploadingLogo(false)
    }
  }, [])

  // Change team name
  const changeTeamName = useCallback(async (params: ChangeTeamNameParams): Promise<boolean> => {
    setChangingTeamName(true)
    setChangeTeamNameError(null)
    setChangeTeamNameMessages([])

    try {
      const response = await settingsService.changeTeamName(params)
      setChangeTeamNameMessages(response.messages)

      if (!response.success) {
        setChangeTeamNameError(response.messages.join(', '))
      }

      return response.success
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Errore sconosciuto durante il cambio del nome'
      setChangeTeamNameError(errorMessage)
      setChangeTeamNameMessages([errorMessage])
      console.error('Error changing team name:', error)
      return false
    } finally {
      setChangingTeamName(false)
    }
  }, [])

  // Update username
  const updateUsername = useCallback(async (newUsername: string): Promise<boolean> => {
    setUpdatingUsername(true)
    setUpdateUsernameError(null)

    try {
      const success = await settingsService.updateUsername(newUsername)

      if (!success) {
        setUpdateUsernameError('Impossibile aggiornare lo username. Verifica che sia valido (3-20 caratteri, solo lettere, numeri e underscore).')
      }

      return success
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Errore sconosciuto durante l\'aggiornamento dello username'
      setUpdateUsernameError(errorMessage)
      console.error('Error updating username:', error)
      return false
    } finally {
      setUpdatingUsername(false)
    }
  }, [])

  return {
    // Team logo
    uploadingLogo,
    uploadLogoError,
    uploadLogo,

    // Team name
    changingTeamName,
    changeTeamNameError,
    changeTeamNameMessages,
    changeTeamName,

    // Username
    updatingUsername,
    updateUsernameError,
    updateUsername,
  }
}
