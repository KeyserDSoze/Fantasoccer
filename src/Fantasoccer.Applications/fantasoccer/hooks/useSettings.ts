import { useState, useCallback } from 'react'
import { settingsService, ChangeTeamNameParams, UploadLogoParams, CopyLogoParams } from 'services/settingsService'

export interface UseSettingsResult {
  // Team logo
  uploadingLogo: boolean
  uploadLogoError: string | null
  uploadLogo: (params: UploadLogoParams) => Promise<boolean>
  copyingLogo: boolean
  copyLogoError: string | null
  copyLogoFromPreviousYear: (params: CopyLogoParams) => Promise<boolean>

  // Team name
  changingTeamName: boolean
  changeTeamNameError: string | null
  changeTeamNameMessages: string[]
  changeTeamNameProgress: string | null
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
  const [copyingLogo, setCopyingLogo] = useState(false)
  const [copyLogoError, setCopyLogoError] = useState<string | null>(null)

  // Team name state
  const [changingTeamName, setChangingTeamName] = useState(false)
  const [changeTeamNameError, setChangeTeamNameError] = useState<string | null>(null)
  const [changeTeamNameMessages, setChangeTeamNameMessages] = useState<string[]>([])
  const [changeTeamNameProgress, setChangeTeamNameProgress] = useState<string | null>(null)

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

  // Copy logo from previous year
  const copyLogoFromPreviousYear = useCallback(async (params: CopyLogoParams): Promise<boolean> => {
    setCopyingLogo(true)
    setCopyLogoError(null)

    try {
      const success = await settingsService.copyLogoFromPreviousYear(params)
      
      if (!success) {
        setCopyLogoError('Impossibile copiare il logo. Verifica che esista un logo per l\'anno precedente.')
      }

      return success
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Errore sconosciuto durante la copia del logo'
      setCopyLogoError(errorMessage)
      console.error('Error copying logo:', error)
      return false
    } finally {
      setCopyingLogo(false)
    }
  }, [])

  // Change team name
  const changeTeamName = useCallback(async (params: ChangeTeamNameParams): Promise<boolean> => {
    setChangingTeamName(true)
    setChangeTeamNameError(null)
    setChangeTeamNameMessages([])
    setChangeTeamNameProgress(null)

    try {
      // Iterate over streaming messages from the server
      // settingsService.changeTeamNameStream yields lines as they arrive
      const stream = settingsService.changeTeamNameStream(params)
      for await (const line of stream) {
        // push message into messages
        setChangeTeamNameMessages((prev) => [...prev, line])
        setChangeTeamNameProgress(line)
        
        // Force React to process this update immediately before next iteration
        // This ensures UI updates in real-time as messages arrive
        await new Promise(resolve => setTimeout(resolve, 0))

        // if server prefixes with error: treat as error and stop
        if (line.toLowerCase().startsWith('error:')) {
          const msg = line.substring('error:'.length).trim() || line
          setChangeTeamNameError(msg)
          setChangingTeamName(false)
          return false
        }
      }

      // If we have messages but none prefixed with error, success
      setChangingTeamName(false)
      return true
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Errore sconosciuto durante il cambio del nome'
      setChangeTeamNameError(errorMessage)
      setChangeTeamNameMessages((prev) => [...prev, errorMessage])
      console.error('Error changing team name (stream):', error)
      setChangingTeamName(false)
      return false
    } finally {
      setChangeTeamNameProgress(null)
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
    copyingLogo,
    copyLogoError,
    copyLogoFromPreviousYear,

    // Team name
    changingTeamName,
    changeTeamNameError,
    changeTeamNameMessages,
    changeTeamNameProgress,
    changeTeamName,

    // Username
    updatingUsername,
    updateUsernameError,
    updateUsername,
  }
}
