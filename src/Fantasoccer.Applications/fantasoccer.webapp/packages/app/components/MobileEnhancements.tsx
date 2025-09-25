'use client'

import { useEffect } from 'react'

/**
 * Service Worker registration component
 * Handles PWA functionality registration
 */
export function ServiceWorkerRegistration() {
  useEffect(() => {
    // Only register service worker in production and if supported
    if (
      typeof window !== 'undefined' &&
      'serviceWorker' in navigator &&
      process.env.NODE_ENV === 'production'
    ) {
      // Register service worker
      navigator.serviceWorker
        .register('/sw.js', {
          scope: '/',
          updateViaCache: 'none'
        })
        .then((registration) => {
          console.log('SW registered: ', registration)
          
          // Check for updates
          registration.addEventListener('updatefound', () => {
            const newWorker = registration.installing
            if (newWorker) {
              newWorker.addEventListener('statechange', () => {
                if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                  // New version available
                  console.log('New version available')
                  // Could show a toast here to inform user
                }
              })
            }
          })
        })
        .catch((registrationError) => {
          console.log('SW registration failed: ', registrationError)
        })

      // Handle service worker messages
      navigator.serviceWorker.addEventListener('message', (event) => {
        console.log('Message from SW:', event.data)
      })

      // Handle when service worker becomes ready
      navigator.serviceWorker.ready.then((registration) => {
        console.log('SW ready:', registration)
      })
    }
  }, [])

  return null // This component doesn't render anything
}

/**
 * Install prompt for PWA
 * Shows native install banner when available
 */
export function PWAInstallPrompt() {
  useEffect(() => {
    let deferredPrompt: any = null

    const handleBeforeInstallPrompt = (event: Event) => {
      // Prevent the mini-infobar from appearing on mobile
      event.preventDefault()
      // Stash the event so it can be triggered later
      deferredPrompt = event
      
      // Update UI to notify the user they can install the PWA
      console.log('PWA install prompt available')
      
      // You could show a custom install button here
      // showInstallPromotion()
    }

    const handleAppInstalled = () => {
      // Hide the app-provided install promotion
      console.log('PWA was installed')
      deferredPrompt = null
    }

    // Listen for the beforeinstallprompt event
    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    
    // Listen for the appinstalled event
    window.addEventListener('appinstalled', handleAppInstalled)

    // Cleanup
    return () => {
      window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
      window.removeEventListener('appinstalled', handleAppInstalled)
    }
  }, [])

  return null
}

/**
 * Combined mobile enhancements component
 */
export function MobileEnhancements() {
  return (
    <>
      <ServiceWorkerRegistration />
      <PWAInstallPrompt />
    </>
  )
}