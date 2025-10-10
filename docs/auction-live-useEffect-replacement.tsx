// ============================================
// REPLACE LINES ~474-785 WITH THIS SECTION
// ============================================

useEffect(() => {
  if (!user?.email) {
    console.log('⚠️ No user email, skipping initialization')
    return
  }
  
  // Prevent multiple simultaneous connection attempts
  if (connectionAttemptedRef.current || isConnectingRef.current) {
    console.log('⚠️ Connection already in progress, skipping')
    return
  }
  
  let isSubscribed = true
  setLoading(true)
  setConnectionState('Connecting')
  
  const initializeAuction = async () => {
    if (isConnectingRef.current) return
    
    isConnectingRef.current = true
    connectionAttemptedRef.current = true
    
    try {
      // STEP 1: GET INITIAL STATE VIA HTTP
      console.log('📥 [1/5] Fetching initial auction state via HTTP...')
      const initialState = await auctionService.getAuction(auctionId)
      
      if (!isSubscribed) return
      
      console.log('✅ [1/5] Initial state loaded')
      
      // STEP 2: SET ALL STATE FROM HTTP RESPONSE
      setAuctionState(initialState)
      setTeams(initialState.teams)
      setIsAuctioneer(initialState.isAuctioneer)
      setSalesHistory(initialState.wrapper.winnings)
      setOfferTimeoutSeconds(initialState.wrapper.secondsPerAuction)
      
      if (initialState.wrapper.actual) {
        setCurrentPlayer(initialState.wrapper.actual.player)
        setCurrentBid(initialState.wrapper.actual.price)
        setCurrentBidder(initialState.wrapper.actual.owner)
        setCurrentSubstitute(initialState.wrapper.actual.substituted)
        setFirstOfferHappened(initialState.wrapper.actual.price > 0)
      } else {
        setCurrentPlayer(null)
        setCurrentBid(0)
        setCurrentBidder(null)
        setCurrentSubstitute(null)
        setFirstOfferHappened(false)
      }
      
      setIsLocked(
        initialState.wrapper.status === 0 || 
        initialState.wrapper.status === 1 || 
        initialState.wrapper.status === 3
      )
      
      const myTeamEntity = initialState.teams.find(t => 
        t.key?.email?.toLowerCase() === user.email.toLowerCase()
      )
      
      if (myTeamEntity) {
        setMyTeam(myTeamEntity)
        const cost = TeamHelper.getCost(myTeamEntity.value)
        const budget = (initialState.league?.settings?.startingMoney || 1000) - cost
        setMyTeamBudget(budget)
      }
      
      if (initialState.league?.settings?.startingMoney) {
        setStartingMoney(initialState.league.settings.startingMoney)
      }
      
      // STEP 3: SETUP EVENT HANDLERS
      auctionService.setEventHandlers({
        onConnected: (connectionId) => {
          if (isSubscribed) {
            setConnectionState('Connected')
            reconnectAttemptsRef.current = 0
          }
        },
        
        onReconnecting: () => {
          if (isSubscribed) {
            setConnectionState('Connecting')
            toast.show('🔄 Riconnessione...', { variant: 'info', duration: 2000 })
          }
        },
        
        onReconnected: () => {
          if (isSubscribed) {
            setConnectionState('Connected')
            toast.show('✅ Riconnesso', { variant: 'success', duration: 2000 })
          }
        },
        
        onDisconnected: (error) => {
          if (isSubscribed) {
            setConnectionState('Disconnected')
            if (error?.message?.includes('401')) {
              setConnectionError('Sessione scaduta')
            }
          }
        },
        
        onError: (message) => {
          if (isSubscribed) toast.show(`❌ ${message}`, { variant: 'error', duration: 4000 })
        },
        
        onUserJoined: (email) => {
          if (isSubscribed) toast.show(`✅ ${email} connesso`, { variant: 'success', duration: 2000 })
        },
        
        onUserLeft: (email) => {
          if (isSubscribed) toast.show(`👋 ${email} disconnesso`, { variant: 'info', duration: 2000 })
        },
        
        // NEW: PlayerShown with ActualAuction
        onPlayerShown: (actualAuctionRaw: ActualAuctionRaw) => {
          if (!isSubscribed) return
          
          try {
            const actualAuction = mapRawActualAuction(actualAuctionRaw)
            const roleChanged = currentPlayer && currentPlayer.role !== actualAuction.player.role
            
            setCurrentPlayer(actualAuction.player)
            setCurrentBid(actualAuction.price)
            setCurrentBidder(actualAuction.owner)
            setCurrentSubstitute(actualAuction.substituted)
            setFirstOfferHappened(actualAuction.price > 0)
            
            if (roleChanged) {
              setMySelectedSubstitute(null)
              setAuctioneerSelectedSubstitute(null)
            }
            
            setPlayerSearchQuery('')
            setSelectedPlayerForForce('')
            setIsLocked(false)
            setTempBlockButtons(false)
            
            playBidSound()
            vibrateOnBid()
            toast.show(`🎯 ${actualAuction.player.name} - ${getRoleLabel(actualAuction.player.role)}`, {
              variant: 'success',
              duration: 3000
            })
            
            resetTimer()
          } catch (error) {
            console.error('Error processing PlayerShown:', error)
          }
        },
        
        // NEW: PlayerAssigned with PlayerAssignedMessage
        onPlayerAssigned: (dataRaw: PlayerAssignedMessageRaw) => {
          if (!isSubscribed) return
          
          try {
            const data = mapRawPlayerAssignedMessage(dataRaw)
            
            if (data.playerAssignedInAuction) {
              const a = data.playerAssignedInAuction
              
              playBidSound()
              vibrateOnBid()
              
              toast.show(`🎯 ${a.playerName} → ${a.teamName} (${a.price} €)`, {
                variant: 'success',
                duration: 4000
              })
              
              setSalesHistory(prev => [...prev, {
                playerKey: getPlayerKey(a.playerName),
                owner: a.owner,
                price: a.price
              }])
              
              if (user?.email && a.owner.toLowerCase() === user.email.toLowerCase()) {
                setMySelectedSubstitute(null)
              }
              if (selectedTeamForBid && a.owner.toLowerCase() === selectedTeamForBid.toLowerCase()) {
                setAuctioneerSelectedSubstitute(null)
              }
            }
            
            // Update team from event
            if (data.team) {
              setTeams(prev => {
                const index = prev.findIndex(t => t.key?.email === data.team?.key?.email)
                if (index >= 0) {
                  const updated = [...prev]
                  updated[index] = data.team
                  return updated
                }
                return prev
              })
              
              if (data.team.key?.email?.toLowerCase() === user.email.toLowerCase()) {
                setMyTeam(data.team)
                const cost = TeamHelper.getCost(data.team.value)
                setMyTeamBudget(startingMoney - cost)
              }
            }
            
            setCurrentPlayer(null)
            setCurrentBid(0)
            setCurrentBidder(null)
            setCurrentSubstitute(null)
            setFirstOfferHappened(false)
            setAssignPrice('')
            setTimerRunning(false)
            setTimerExpired(false)
            resetTimer()
          } catch (error) {
            console.error('Error processing PlayerAssigned:', error)
          }
        },
        
        // NEW: TeamUpdated event
        onTeamUpdated: (team) => {
          if (!isSubscribed) return
          
          setTeams(prev => {
            const index = prev.findIndex(t => t.key?.email === team.key?.email)
            if (index >= 0) {
              const updated = [...prev]
              updated[index] = team
              return updated
            }
            return [...prev, team]
          })
          
          if (team.key?.email?.toLowerCase() === user.email.toLowerCase()) {
            setMyTeam(team)
            const cost = TeamHelper.getCost(team.value)
            setMyTeamBudget(startingMoney - cost)
          }
          
          if (selectedTeamDetail && team.key?.email === selectedTeamDetail.key?.email) {
            setSelectedTeamDetail(team)
          }
        },
        
        // NEW: TimerUpdate event
        onTimerUpdate: (seconds) => {
          if (!isSubscribed) return
          
          setOfferTimeoutSeconds(seconds)
          toast.show(`⏱️ Timer: ${seconds}s`, { variant: 'info', duration: 2000 })
        },
        
        onBidPlaced: (data: BidPlacedData) => {
          if (!isSubscribed) return
          
          setCurrentBid(data.Amount)
          setCurrentBidder(data.BidderEmail)
          setCurrentSubstitute(data.Substituted)
          setFirstOfferHappened(true)
          
          resetTimer()
          startTimer()
          setTempBlockButtons(false)
          
          playBidSound()
          vibrateOnBid()
          
          toast.show(`💰 ${data.BidderName || data.BidderEmail}: ${data.Amount} €`, {
            variant: 'info',
            duration: 2000
          })
        },
        
        onAuctionPaused: () => {
          if (isSubscribed) {
            setIsLocked(true)
            toast.show('⏸️ Asta in pausa', { variant: 'warning', duration: 3000 })
          }
        },
        
        onAuctionResumed: () => {
          if (isSubscribed) {
            setIsLocked(false)
            toast.show('▶️ Asta ripresa', { variant: 'success', duration: 3000 })
          }
        },
        
        onAuctionClosed: () => {
          if (isSubscribed) {
            setIsLocked(true)
            setConnectionError('Asta chiusa')
            toast.show('🏁 Asta chiusa', { variant: 'info', duration: 5000 })
          }
        },
        
        onCurrentAuctionReset: () => {
          if (isSubscribed) {
            setCurrentBid(0)
            setCurrentBidder(null)
            setCurrentSubstitute(null)
            setFirstOfferHappened(false)
            resetTimer()
            toast.show('🔄 Reset', { variant: 'info', duration: 2000 })
          }
        },
        
        onTimerReset: () => {
          if (isSubscribed) {
            startTimer()
            toast.show('⏱️ Timer riavviato', { variant: 'info', duration: 2000 })
          }
        }
      })
      
      // STEP 4: CONNECT TO SIGNALR
      console.log('🔌 [4/5] Connecting to SignalR...')
      await auctionService.connect(auctionId, user.email)
      
      if (!isSubscribed) {
        await auctionService.disconnect()
        return
      }
      
      // STEP 5: JOIN AUCTION ROOM
      console.log('📨 [5/5] Joining auction...')
      const joined = await auctionService.joinAuction(auctionId)
      
      if (!joined) {
        if (isSubscribed) {
          setConnectionError('Non autorizzato')
          setConnectionState('Disconnected')
        }
        return
      }
      
      if (!isSubscribed) {
        await auctionService.leaveAuction(auctionId)
        return
      }
      
      console.log('🎉 Initialization complete!')
      setLoading(false)
      isConnectingRef.current = false
      
    } catch (error) {
      console.error('❌ Initialization failed:', error)
      
      if (isSubscribed) {
        const errorMessage = error instanceof Error ? error.message : 'Errore'
        
        if (errorMessage.includes('401') || errorMessage.includes('Unauthorized')) {
          setConnectionError('Sessione scaduta')
          
          setTimeout(async () => {
            const { returnUrlService } = await import('../services/returnUrlService')
            await returnUrlService.saveReturnUrl(`/auction/${auctionId}`)
            
            const { tokenStorageService } = await import('../services/tokenStorageService')
            await tokenStorageService.removeToken()
            
            navigation.navigate('Login' as any)
          }, 2000)
        } else {
          setConnectionError(errorMessage)
          
          if (reconnectAttemptsRef.current < 3) {
            toast.show(`⏰ Riconnessione ${reconnectAttemptsRef.current + 1}/3 in 3s...`, { variant: 'error' })
            setTimeout(() => {
              reconnectAttemptsRef.current += 1
              connectionAttemptedRef.current = false
              isConnectingRef.current = false
              setConnectionTrigger(prev => prev + 1)
            }, 3000)
          }
        }
        
        setConnectionState('Disconnected')
        setLoading(false)
        isConnectingRef.current = false
      }
    }
  }
  
  initializeAuction()
  
  return () => {
    isSubscribed = false
    connectionAttemptedRef.current = false
    isConnectingRef.current = false
    auctionService.leaveAuction(auctionId).catch(console.error)
  }
  
}, [auctionId, user?.email, connectionTrigger])
