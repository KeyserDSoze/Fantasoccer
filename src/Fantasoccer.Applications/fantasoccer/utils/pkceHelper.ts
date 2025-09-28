// PKCE Helper for cross-platform Microsoft OAuth
// Supports Web, Android, and iOS with fixed code_verifier for backend compatibility

export class PKCEHelper {
  // Fixed code_verifier as requested for backend compatibility
  private static readonly FIXED_CODE_VERIFIER = "19cfc47c216dacba8ca23eeee817603e2ba34fe0976378662ba31688ed302fa9"
  
  /**
   * Get the fixed code verifier
   * This is used during the token exchange with the backend
   */
  static getCodeVerifier(): string {
    return this.FIXED_CODE_VERIFIER
  }
  
  /**
   * Generate code challenge from the fixed code verifier
   * Uses SHA256 and base64url encoding as per RFC 7636
   */
  static async getCodeChallenge(): Promise<string> {
    try {
      const encoder = new TextEncoder()
      const data = encoder.encode(this.FIXED_CODE_VERIFIER)
      
      // Use Web Crypto API if available (Web and modern React Native)
      if (typeof crypto !== 'undefined' && crypto.subtle) {
        const digest = await crypto.subtle.digest('SHA-256', data)
        return this.base64URLEncode(new Uint8Array(digest))
      }
      
      // Fallback for environments without crypto.subtle
      throw new Error('Web Crypto API not available - need polyfill for PKCE')
      
    } catch (error) {
      console.error('❌ PKCE code challenge generation failed:', error)
      throw new Error(`Failed to generate code challenge: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }
  
  /**
   * Base64URL encode as per RFC 4648 Section 5
   * Removes padding and replaces + with - and / with _
   */
  private static base64URLEncode(buffer: Uint8Array): string {
    const base64 = btoa(String.fromCharCode(...buffer))
    return base64
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=/g, '')
  }
  
  /**
   * Generate Microsoft OAuth authorize URL with PKCE
   * Supports both web and mobile redirect URIs
   */
  static async buildAuthorizeUrl(config: {
    clientId: string
    authority: string
    redirectUri: string
    scopes?: string[]
    state?: string
  }): Promise<string> {
    const codeChallenge = await this.getCodeChallenge()
    const scopes = config.scopes?.join(' ') || 'openid profile email'
    const state = config.state || this.generateState()
    
    const params = new URLSearchParams({
      client_id: config.clientId,
      response_type: 'code',
      redirect_uri: config.redirectUri,
      scope: scopes,
      state: state,
      code_challenge: codeChallenge,
      code_challenge_method: 'S256',
      response_mode: 'query'
    })
    
    const authorizeUrl = `${config.authority}/oauth2/v2.0/authorize?${params.toString()}`
    
    console.log('🔗 Generated OAuth authorize URL:')
    console.log('📍 Authority:', config.authority)
    console.log('🎯 Client ID:', config.clientId)
    console.log('🔄 Redirect URI:', config.redirectUri)
    console.log('🔐 Code Challenge:', codeChallenge)
    console.log('🎲 State:', state)
    console.log('🌐 Full URL:', authorizeUrl)
    
    return authorizeUrl
  }
  
  /**
   * Generate random state parameter for OAuth security
   */
  private static generateState(): string {
    const array = new Uint8Array(16)
    if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
      crypto.getRandomValues(array)
    } else {
      // Fallback for environments without crypto.getRandomValues
      for (let i = 0; i < array.length; i++) {
        array[i] = Math.floor(Math.random() * 256)
      }
    }
    return this.base64URLEncode(array)
  }
  
  /**
   * Validate that the environment supports PKCE
   * Returns true if crypto APIs are available
   */
  static isSupported(): boolean {
    return typeof crypto !== 'undefined' && 
           typeof crypto.subtle !== 'undefined' &&
           typeof TextEncoder !== 'undefined'
  }
  
  /**
   * Get PKCE implementation info for debugging
   */
  static getInfo() {
    return {
      codeVerifier: this.FIXED_CODE_VERIFIER,
      codeVerifierLength: this.FIXED_CODE_VERIFIER.length,
      isSupported: this.isSupported(),
      cryptoAvailable: typeof crypto !== 'undefined',
      subtleAvailable: typeof crypto !== 'undefined' && typeof crypto.subtle !== 'undefined',
      textEncoderAvailable: typeof TextEncoder !== 'undefined'
    }
  }
}

// Export for easy access
export const pkceHelper = PKCEHelper