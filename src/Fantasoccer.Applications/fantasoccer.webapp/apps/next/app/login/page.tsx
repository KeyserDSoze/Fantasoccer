import { LoginScreen } from 'app/features/auth'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Accedi | Fantasoccer',
  description: 'Accedi al tuo account Fantasoccer per gestire la tua squadra e le tue leghe',
}

export default function Login() {
  return <LoginScreen />
}