import type { Metadata } from 'next'
import { NextTamaguiProvider } from 'app/provider/NextTamaguiProvider'
import { NavigationBar } from 'app/components/NavigationBar'

export const metadata: Metadata = {
  title: 'Fantasoccer | La tua lega fantacalcio',
  description: 'Crea la tua squadra, sfida i tuoi amici e vinci il campionato nel miglior fantacalcio d\'Italia',
  keywords: ['fantasoccer', 'fantacalcio', 'calcio', 'serie a', 'lega', 'squadra'],
  authors: [{ name: 'Fantasoccer Team' }],
  icons: {
    icon: [
      { url: '/favicon.ico', sizes: '16x16 32x32', type: 'image/x-icon' },
      { url: '/favicon.svg', type: 'image/svg+xml' }
    ],
    apple: [
      { url: '/apple-touch-icon.svg', type: 'image/svg+xml' },
      { url: '/favicon.svg', type: 'image/svg+xml' }
    ],
    shortcut: '/favicon.ico'
  },
  // Mobile optimization
  viewport: {
    width: 'device-width',
    initialScale: 1,
    maximumScale: 1,
    userScalable: false,
    viewportFit: 'cover' // For iPhone X+ safe areas
  },
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: '#ffffff' },
    { media: '(prefers-color-scheme: dark)', color: '#000000' }
  ],
  appleWebApp: {
    capable: true,
    statusBarStyle: 'default',
    title: 'Fantasoccer'
  },
  formatDetection: {
    telephone: false,
    date: false,
    address: false,
    email: false,
    url: false
  },
  openGraph: {
    title: 'Fantasoccer | La tua lega fantacalcio',
    description: 'Crea la tua squadra, sfida i tuoi amici e vinci il campionato',
    type: 'website',
    locale: 'it_IT',
  },
  twitter: {
    card: 'summary',
    title: 'Fantasoccer | La tua lega fantacalcio',
    description: 'Crea la tua squadra, sfida i tuoi amici e vinci il campionato',
  },
  // PWA manifest
  manifest: '/manifest.json'
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    // You can use `suppressHydrationWarning` to avoid the warning about mismatched content during hydration in dev mode
    <html lang="it" suppressHydrationWarning>
      <head>
        {/* Additional mobile optimizations */}
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="default" />
        <meta name="apple-mobile-web-app-title" content="Fantasoccer" />
        <meta name="msapplication-TileColor" content="#1e40af" />
        <meta name="msapplication-tap-highlight" content="no" />
        
        {/* Preload critical resources */}
        <link rel="preload" href="/favicon.svg" as="image" type="image/svg+xml" />
        
        {/* DNS prefetch for external resources */}
        <link rel="dns-prefetch" href="//fasostorage.blob.core.windows.net" />
        
        {/* PWA theme color for different schemes */}
        <meta name="theme-color" content="#1e40af" media="(prefers-color-scheme: light)" />
        <meta name="theme-color" content="#000000" media="(prefers-color-scheme: dark)" />
      </head>
      <body>
        <NextTamaguiProvider>
          <>
            {/* Persistent header/navigation - kept outside children so it isn't remounted on child route changes */}
            <NavigationBar />

            {/* Main app body - children are wrapped by Provider/AppInitializer inside NextTamaguiProvider
                AppInitializer will show the SplashScreen until initialization completes, so the splash
                is preserved. */}
            <main id="app-root">{children}</main>
          </>
        </NextTamaguiProvider>
      </body>
    </html>
  )
}
