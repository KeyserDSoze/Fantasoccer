// ID Token decoder utility for debugging and analysis
// Helps understand what claims are available in Microsoft ID tokens

export interface IDTokenClaims {
  // Standard OpenID Connect claims
  sub: string           // Subject (user ID)
  aud: string          // Audience (client ID)
  iss: string          // Issuer (Microsoft authority)
  iat: number          // Issued at (timestamp)
  exp: number          // Expires at (timestamp)
  tid: string          // Tenant ID
  
  // Microsoft-specific claims
  email?: string       // User email
  name?: string        // Display name
  preferred_username?: string  // UPN or email
  given_name?: string  // First name
  family_name?: string // Last name
  
  // Additional claims that may be present
  roles?: string[]     // User roles
  groups?: string[]    // Group memberships
  ver?: string         // Token version
  nonce?: string       // Nonce for replay protection
  
  // Other potential claims
  [key: string]: any
}

export class IDTokenDecoder {
  /**
   * Decode a JWT ID token without verification
   * WARNING: This is for debugging only - tokens should be verified on the backend
   */
  static decode(idToken: string): IDTokenClaims | null {
    try {
      // JWT has 3 parts: header.payload.signature
      const parts = idToken.split('.')
      if (parts.length !== 3) {
        throw new Error('Invalid JWT format')
      }

      // Decode the payload (middle part)
      const payload = parts[1]
      
      // Add padding if needed for base64 decoding
      const paddedPayload = payload + '='.repeat((4 - payload.length % 4) % 4)
      
      // Decode from base64url
      const decodedPayload = atob(paddedPayload.replace(/-/g, '+').replace(/_/g, '/'))
      
      // Parse JSON
      const claims = JSON.parse(decodedPayload) as IDTokenClaims
      
      return claims
    } catch (error) {
      console.error('❌ Failed to decode ID token:', error)
      return null
    }
  }

  /**
   * Get human-readable token info for debugging
   */
  static getTokenInfo(idToken: string): string {
    const claims = this.decode(idToken)
    if (!claims) {
      return 'Failed to decode token'
    }

    const expiresAt = new Date(claims.exp * 1000)
    const issuedAt = new Date(claims.iat * 1000)
    const isExpired = Date.now() > claims.exp * 1000

    return `
🆔 ID Token Information:
├─ Subject (sub): ${claims.sub}
├─ Email: ${claims.email || 'N/A'}
├─ Name: ${claims.name || 'N/A'}
├─ Preferred Username: ${claims.preferred_username || 'N/A'}
├─ Tenant ID (tid): ${claims.tid}
├─ Audience (aud): ${claims.aud}
├─ Issuer (iss): ${claims.iss}
├─ Issued At: ${issuedAt.toISOString()}
├─ Expires At: ${expiresAt.toISOString()}
├─ Is Expired: ${isExpired ? '❌ YES' : '✅ NO'}
├─ Token Version: ${claims.ver || 'N/A'}
└─ Additional Claims: ${Object.keys(claims).filter(k => 
    !['sub', 'email', 'name', 'preferred_username', 'tid', 'aud', 'iss', 'iat', 'exp', 'ver'].includes(k)
  ).join(', ') || 'None'}
    `.trim()
  }

  /**
   * Extract key user information from ID token
   */
  static extractUserInfo(idToken: string): {
    id: string
    email: string
    name: string
    displayName: string
    tenantId: string
  } | null {
    const claims = this.decode(idToken)
    if (!claims) {
      return null
    }

    return {
      id: claims.sub,
      email: claims.email || claims.preferred_username || '',
      name: claims.name || claims.given_name || claims.preferred_username || '',
      displayName: claims.name || `${claims.given_name || ''} ${claims.family_name || ''}`.trim() || claims.preferred_username || '',
      tenantId: claims.tid,
    }
  }

  /**
   * Validate basic ID token structure and claims
   */
  static validate(idToken: string, expectedClientId?: string): {
    isValid: boolean
    errors: string[]
    warnings: string[]
  } {
    const errors: string[] = []
    const warnings: string[] = []

    const claims = this.decode(idToken)
    if (!claims) {
      errors.push('Failed to decode token')
      return { isValid: false, errors, warnings }
    }

    // Check required claims
    if (!claims.sub) errors.push('Missing subject (sub) claim')
    if (!claims.aud) errors.push('Missing audience (aud) claim')
    if (!claims.iss) errors.push('Missing issuer (iss) claim')
    if (!claims.exp) errors.push('Missing expiration (exp) claim')
    if (!claims.iat) errors.push('Missing issued at (iat) claim')

    // Check expiration
    if (claims.exp && Date.now() > claims.exp * 1000) {
      errors.push('Token is expired')
    }

    // Check audience if provided
    if (expectedClientId && claims.aud !== expectedClientId) {
      errors.push(`Audience mismatch: expected ${expectedClientId}, got ${claims.aud}`)
    }

    // Check issuer
    if (claims.iss && !claims.iss.includes('login.microsoftonline.com')) {
      warnings.push('Issuer is not Microsoft')
    }

    // Check tenant
    if (!claims.tid) {
      warnings.push('Missing tenant ID (tid) claim')
    }

    // Check user info
    if (!claims.email && !claims.preferred_username) {
      warnings.push('No email or username claim found')
    }

    return {
      isValid: errors.length === 0,
      errors,
      warnings
    }
  }
}

// Export for easy access
export const idTokenDecoder = IDTokenDecoder