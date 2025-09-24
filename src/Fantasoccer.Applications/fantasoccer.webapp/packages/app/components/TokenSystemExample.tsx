'use client'

// Example component showing the new token system usage
import React, { useState } from 'react'
import { View, Text, Button, StyleSheet, ScrollView } from 'react-native'
import { useAuth } from '../auth/AuthProvider'
import { apiRequest } from '../services/apiRequestHelper'
import { authService } from '../services/authService'
import { getUserRoleNames, IdentityRole, hasRole } from '../types/identity'
import { tokenStorageService } from '../services/tokenStorageService'

export const TokenSystemExample: React.FC = () => {
  const { isAuthenticated, user, login, logout } = useAuth()
  const [apiResult, setApiResult] = useState<string>('')
  const [loading, setLoading] = useState(false)

  const testApiCall = async () => {
    setLoading(true)
    try {
      // Example of using the new API helper
      const result = await apiRequest.get('/api/Authentication/Social/User')
      setApiResult(JSON.stringify(result, null, 2))
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : 'Unknown error'
      setApiResult(`Error: ${errorMsg}`)
      
      // Handle special errors
      if (errorMsg === 'UNAUTHORIZED' || errorMsg === 'REFRESH_FAILED') {
        console.log('User needs to re-login')
        // AuthProvider will handle this automatically
      }
    } finally {
      setLoading(false)
    }
  }

  const testRefreshToken = async () => {
    setLoading(true)
    try {
      const tokenData = await tokenStorageService.loadToken()
      if (tokenData) {
        const newTokens = await authService.refreshToken(tokenData.refreshToken)
        setApiResult(`Refresh successful! New token expires in: ${newTokens.expiresIn} seconds`)
        
        // Update storage
        await tokenStorageService.saveToken({
          tokenType: newTokens.tokenType,
          accessToken: newTokens.accessToken,
          refreshToken: newTokens.refreshToken,
          expiresAt: Date.now() + (newTokens.expiresIn * 1000),
        })
      } else {
        setApiResult('No token found to refresh')
      }
    } catch (error) {
      setApiResult(`Refresh failed: ${error instanceof Error ? error.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }

  const showTokenInfo = async () => {
    try {
      const tokenData = await tokenStorageService.loadToken()
      if (tokenData) {
        const expiresAt = new Date(tokenData.expiresAt)
        const timeLeft = Math.max(0, Math.floor((tokenData.expiresAt - Date.now()) / 1000))
        
        setApiResult(`
Token Information:
├─ Type: ${tokenData.tokenType}
├─ Access Token: ${tokenData.accessToken.substring(0, 50)}...
├─ Refresh Token: ${tokenData.refreshToken.substring(0, 50)}...
├─ Expires At: ${expiresAt.toISOString()}
├─ Time Left: ${timeLeft} seconds
└─ Is Valid: ${timeLeft > 0 ? '✅' : '❌'}
        `)
      } else {
        setApiResult('No token stored')
      }
    } catch (error) {
      setApiResult(`Error getting token info: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  if (!isAuthenticated) {
    return (
      <View style={styles.container}>
        <Text style={styles.title}>🔐 Token System Authentication</Text>
        <Text style={styles.subtitle}>Please login to test the new token system</Text>
        
        <View style={styles.buttonContainer}>
          <Button title="Login with OAuth" onPress={login} />
        </View>

        <View style={styles.infoContainer}>
          <Text style={styles.infoTitle}>🆕 New Token System Features:</Text>
          <Text style={styles.infoText}>
            • OAuth authorization code flow{'\n'}
            • PKCE for security{'\n'}
            • Automatic token refresh{'\n'}
            • Bearer token authentication{'\n'}
            • Structured token storage{'\n'}
            • Error handling with re-login
          </Text>
        </View>
      </View>
    )
  }

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.title}>✅ Authenticated!</Text>
      
      <View style={styles.userInfo}>
        <Text style={styles.subtitle}>User Information:</Text>
        <Text style={styles.userText}>Username: {user?.username}</Text>
        <Text style={styles.userText}>Email: {user?.email}</Text>
        <Text style={styles.userText}>Working Email: {user?.state?.workingEmail}</Text>
        <Text style={styles.userText}>Current Group: {user?.groups?.[0]?.name}</Text>
        <Text style={styles.userText}>Year: {user?.state?.year}</Text>
        <Text style={styles.userText}>Groups: {user?.groups?.length || 0}</Text>
      </View>

      {user?.groups && user.groups.length > 0 && (
        <View style={styles.groupsInfo}>
          <Text style={styles.subtitle}>Groups & Roles:</Text>
          {user.groups.map((group, index) => (
            <View key={group.id} style={styles.groupItem}>
              <Text style={styles.groupName}>🏆 {group.name}</Text>
              <Text style={styles.groupRole}>
                Roles: {getUserRoleNames(group.role).join(', ')}
              </Text>
              <Text style={styles.groupRole}>
                Admin: {hasRole(group.role, IdentityRole.Admin) ? '✅' : '❌'}
              </Text>
            </View>
          ))}
        </View>
      )}

      <View style={styles.buttonContainer}>
        <Button 
          title="Test API Call (with auto-refresh)" 
          onPress={testApiCall}
          disabled={loading}
        />
        
        <Button 
          title="Test Manual Refresh Token" 
          onPress={testRefreshToken}
          disabled={loading}
        />
        
        <Button 
          title="Show Token Info" 
          onPress={showTokenInfo}
          disabled={loading}
        />
        
        <Button 
          title="Logout" 
          onPress={logout}
          disabled={loading}
        />
      </View>

      {apiResult && (
        <View style={styles.resultContainer}>
          <Text style={styles.resultTitle}>📄 API Result:</Text>
          <Text style={styles.resultText}>{apiResult}</Text>
        </View>
      )}

      <View style={styles.infoContainer}>
        <Text style={styles.infoTitle}>🔧 How it works:</Text>
        <Text style={styles.infoText}>
          1. OAuth code → Backend exchange{'\n'}
          2. Backend returns: tokenType, accessToken, refreshToken, expiresIn{'\n'}
          3. All API calls use Bearer accessToken{'\n'}
          4. On 401 error → Auto refresh with refreshToken{'\n'}
          5. Refresh calls: provider=0, code=refreshToken{'\n'}
          6. If refresh fails → Redirect to login
        </Text>
      </View>
    </ScrollView>
  )
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 20,
    backgroundColor: '#f8f9fa',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 20,
    color: '#333',
  },
  subtitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 15,
    color: '#666',
  },
  userInfo: {
    backgroundColor: '#e8f5e8',
    padding: 15,
    borderRadius: 8,
    marginBottom: 20,
  },
  userText: {
    fontSize: 14,
    marginBottom: 5,
    color: '#333',
  },
  buttonContainer: {
    gap: 10,
    marginBottom: 20,
  },
  resultContainer: {
    backgroundColor: '#f0f8ff',
    padding: 15,
    borderRadius: 8,
    marginBottom: 20,
  },
  resultTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 10,
    color: '#0066cc',
  },
  resultText: {
    fontSize: 12,
    fontFamily: 'monospace',
    color: '#333',
    backgroundColor: '#fff',
    padding: 10,
    borderRadius: 4,
  },
  infoContainer: {
    backgroundColor: '#fff3e0',
    padding: 15,
    borderRadius: 8,
    marginTop: 10,
  },
  infoTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 10,
    color: '#f57c00',
  },
  infoText: {
    fontSize: 12,
    lineHeight: 18,
    color: '#f57c00',
  },
  groupsInfo: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 8,
    marginBottom: 20,
  },
  groupItem: {
    backgroundColor: '#fff',
    padding: 10,
    borderRadius: 6,
    marginBottom: 10,
    borderLeftWidth: 3,
    borderLeftColor: '#007bff',
  },
  groupName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#007bff',
    marginBottom: 5,
  },
  groupRole: {
    fontSize: 14,
    color: '#6c757d',
    marginBottom: 2,
  },
})