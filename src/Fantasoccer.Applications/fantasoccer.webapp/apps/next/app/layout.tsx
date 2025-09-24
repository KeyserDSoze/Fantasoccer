import type { Metadata } from 'next'
import { NextTamaguiProvider } from 'app/provider/NextTamaguiProvider'

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
  }
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    // You can use `suppressHydrationWarning` to avoid the warning about mismatched content during hydration in dev mode
    <html lang="en" suppressHydrationWarning>
      <body>
        <NextTamaguiProvider>{children}</NextTamaguiProvider>
      </body>
    </html>
  )
}
