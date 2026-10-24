import { Platform } from 'react-native'
import { storage } from './storage'

const TRANSACTION_KEY = 'fantasoccer_oauth_transaction'

export class PKCEHelper {
  private static base64URLEncode(buffer: Uint8Array): string {
    return btoa(String.fromCharCode(...buffer)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '')
  }

  private static async save(value: string): Promise<void> {
    if (Platform.OS === 'web') window.sessionStorage.setItem(TRANSACTION_KEY, value)
    else await storage.setItem(TRANSACTION_KEY, value)
  }

  static async consumeTransaction(state: string | null): Promise<string> {
    const raw = Platform.OS === 'web'
      ? window.sessionStorage.getItem(TRANSACTION_KEY)
      : await storage.getItem(TRANSACTION_KEY)
    if (Platform.OS === 'web') window.sessionStorage.removeItem(TRANSACTION_KEY)
    else await storage.removeItem(TRANSACTION_KEY)
    if (!raw) throw new Error('Login non iniziato in questa sessione. Accedi nuovamente.')
    const transaction = JSON.parse(raw)
    if (!state || transaction.state !== state || typeof transaction.verifier !== 'string' ||
      Date.now() - transaction.createdAt > 10 * 60 * 1000)
      throw new Error('Sessione di login non valida o scaduta. Accedi nuovamente.')
    return transaction.verifier
  }

  static async buildAuthorizeUrl(config: {
    clientId: string
    authority: string
    redirectUri: string
    scopes?: string[]
    state?: string
  }): Promise<string> {
    if (!this.isSupported()) throw new Error('PKCE not supported in this environment')
    const verifier = this.base64URLEncode(crypto.getRandomValues(new Uint8Array(32)))
    const state = config.state || this.base64URLEncode(crypto.getRandomValues(new Uint8Array(32)))
    const challenge = this.base64URLEncode(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))))
    await this.save(JSON.stringify({ verifier, state, createdAt: Date.now() }))
    const params = new URLSearchParams({
      client_id: config.clientId, response_type: 'code', redirect_uri: config.redirectUri,
      scope: config.scopes?.join(' ') || 'openid profile email', state,
      code_challenge: challenge, code_challenge_method: 'S256', response_mode: 'query',
    })
    return `${config.authority}/oauth2/v2.0/authorize?${params.toString()}`
  }

  static isSupported(): boolean {
    return typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function' &&
      typeof crypto.subtle !== 'undefined' && typeof TextEncoder !== 'undefined'
  }
}

export const pkceHelper = PKCEHelper
