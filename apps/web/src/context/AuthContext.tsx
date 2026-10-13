import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import type { User } from '../types'
import { api } from '../services/api'

interface AuthContextType {
  user: User | null
  token: string | null
  isLoading: boolean
  isInitializing: boolean
  isAuthenticated: boolean
  error: string | null
  login: (email: string, password: string) => Promise<void>
  register: (name: string, email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  clearError: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [token, setToken] = useState<string | null>(api.getToken())
  const [isLoading, setIsLoading] = useState<boolean>(false)
  const [isInitializing, setIsInitializing] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  const logout = useCallback(async () => {
    setIsLoading(true)
    try {
      await api.logout()
    } catch {
      // Ignore network errors during logout
    } finally {
      api.clearToken()
      setToken(null)
      setUser(null)
      setError(null)
      setIsLoading(false)
    }
  }, [])

  // Restore session on mount via HttpOnly cookie refresh token
  useEffect(() => {
    let isMounted = true

    const initAuth = async () => {
      try {
        // Attempt silent refresh using HttpOnly cookie
        const res = await api.refreshToken()
        if (isMounted) {
          api.setToken(res.accessToken)
          setToken(res.accessToken)
          setUser(res.user)
        }
      } catch {
        // No active session or cookie expired
        if (isMounted) {
          api.clearToken()
          setToken(null)
          setUser(null)
        }
      } finally {
        if (isMounted) {
          setIsInitializing(false)
        }
      }
    }

    initAuth()

    // Listen for global 401 Unauthorized from api
    const handleUnauthorized = () => {
      api.clearToken()
      setToken(null)
      setUser(null)
    }
    api.onUnauthorized(handleUnauthorized)

    return () => {
      isMounted = false
      api.removeUnauthorized(handleUnauthorized)
    }
  }, [])

  const login = async (email: string, password: string) => {
    setIsLoading(true)
    setError(null)
    try {
      const res = await api.login(email, password)
      api.setToken(res.accessToken)
      setToken(res.accessToken)
      setUser(res.user)
    } catch (err: any) {
      setError(err.message || 'Đăng nhập không thành công')
      throw err
    } finally {
      setIsLoading(false)
    }
  }

  const register = async (name: string, email: string, password: string) => {
    setIsLoading(true)
    setError(null)
    try {
      const res = await api.register(name, email, password)
      api.setToken(res.accessToken)
      setToken(res.accessToken)
      setUser(res.user)
    } catch (err: any) {
      setError(err.message || 'Đăng ký không thành công')
      throw err
    } finally {
      setIsLoading(false)
    }
  }

  const clearError = () => setError(null)

  const value: AuthContextType = {
    user,
    token,
    isLoading,
    isInitializing,
    isAuthenticated: Boolean(user && token),
    error,
    login,
    register,
    logout,
    clearError,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
