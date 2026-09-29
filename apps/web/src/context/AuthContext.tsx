import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import type { User } from '../types'
import { api } from '../services/api'

interface AuthContextType {
  user: User | null
  token: string | null
  isLoading: boolean
  isAuthenticated: boolean
  error: string | null
  login: (email: string, password: string) => Promise<void>
  register: (name: string, email: string, password: string) => Promise<void>
  logout: () => void
  clearError: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [token, setToken] = useState<string | null>(api.getToken())
  const [isLoading, setIsLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  const logout = useCallback(() => {
    api.clearToken()
    setToken(null)
    setUser(null)
    setError(null)
  }, [])

  // Restore session on mount
  useEffect(() => {
    let isMounted = true

    const initAuth = async () => {
      const storedToken = api.getToken()
      if (!storedToken) {
        if (isMounted) setIsLoading(false)
        return
      }

      try {
        const me = await api.getMe()
        if (isMounted) {
          setUser(me)
          setToken(storedToken)
        }
      } catch (err: any) {
        console.warn('Session restoration failed or token expired:', err.message)
        if (isMounted) {
          logout()
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    initAuth()

    // Listen for 401 Unauthorized globally from api
    const handleUnauthorized = () => {
      logout()
    }
    api.onUnauthorized(handleUnauthorized)

    return () => {
      isMounted = false
      api.removeUnauthorized(handleUnauthorized)
    }
  }, [logout])

  const login = async (email: string, password: string) => {
    setIsLoading(true)
    setError(null)
    try {
      const res = await api.login(email, password)
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

  const value = {
    user,
    token,
    isLoading,
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
