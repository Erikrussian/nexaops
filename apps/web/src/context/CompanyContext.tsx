import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react'
import type { Company, CompanyMember } from '../types'
import { api } from '../services/api'
import { useAuth } from './AuthContext'

export type CompanyRole = 'OWNER' | 'ADMIN' | 'MANAGER' | 'MEMBER'

const ROLE_HIERARCHY: Record<CompanyRole, number> = {
  OWNER: 4,
  ADMIN: 3,
  MANAGER: 2,
  MEMBER: 1,
}

interface CompanyContextType {
  companies: Company[]
  activeCompany: Company | null
  userRole: CompanyRole | null
  isLoading: boolean
  error: string | null
  selectCompany: (company: Company | null) => void
  refreshCompanies: () => Promise<void>
  hasPermission: (minimumRole: CompanyRole) => boolean
}

const CompanyContext = createContext<CompanyContextType | undefined>(undefined)

const STORAGE_KEY = 'nexaops_active_company_id'

export function CompanyProvider({ children }: { children: ReactNode }) {
  const { user, isAuthenticated } = useAuth()
  const [companies, setCompanies] = useState<Company[]>([])
  const [activeCompany, setActiveCompany] = useState<Company | null>(null)
  const [userRole, setUserRole] = useState<CompanyRole | null>(null)
  const [isLoading, setIsLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  // Determine user role in active company
  const resolveUserRole = useCallback(
    async (company: Company): Promise<CompanyRole | null> => {
      if (!user) return null

      // If user is owner of the company
      if (company.ownerId === user.id) {
        return 'OWNER'
      }

      // Check member list from API to determine assigned role
      try {
        const members = await api.getCompanyMembers(company.id)
        const myMember = members.find(
          (m: CompanyMember) =>
            m.userId === user.id ||
            m.email.toLowerCase() === user.email.toLowerCase() ||
            m.userEmail?.toLowerCase() === user.email.toLowerCase()
        )

        if (!myMember) return null

        const rawRole = String(myMember.role).toUpperCase()
        if (rawRole === 'OWNER' || rawRole === '0') return 'OWNER'
        if (rawRole === 'ADMIN' || rawRole === '1') return 'ADMIN'
        if (rawRole === 'MANAGER' || rawRole === '2') return 'MANAGER'
        return 'MEMBER'
      } catch {
        return null
      }
    },
    [user]
  )

  const selectCompany = useCallback(
    async (company: Company | null) => {
      // Clear previous workspace state immediately to avoid cross-tenant contamination
      setActiveCompany(company)
      setUserRole(null)

      if (company) {
        sessionStorage.setItem(STORAGE_KEY, company.id)
        const role = await resolveUserRole(company)
        setUserRole(role)
      } else {
        sessionStorage.removeItem(STORAGE_KEY)
      }
    },
    [resolveUserRole]
  )

  const refreshCompanies = useCallback(async () => {
    if (!isAuthenticated) {
      setCompanies([])
      setActiveCompany(null)
      setUserRole(null)
      sessionStorage.removeItem(STORAGE_KEY)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      const data = await api.getMyCompanies()
      setCompanies(data)

      const savedId = sessionStorage.getItem(STORAGE_KEY)
      const matched = data.find((c) => c.id === savedId)

      if (matched) {
        await selectCompany(matched)
      } else if (data.length > 0) {
        await selectCompany(data[0])
      } else {
        await selectCompany(null)
      }
    } catch (err: any) {
      setError(err.message || 'Không thể tải danh sách tổ chức')
    } finally {
      setIsLoading(false)
    }
  }, [isAuthenticated, selectCompany])

  useEffect(() => {
    refreshCompanies()
  }, [refreshCompanies])

  const hasPermission = useCallback(
    (minimumRole: CompanyRole): boolean => {
      if (!userRole) return false
      return ROLE_HIERARCHY[userRole] >= ROLE_HIERARCHY[minimumRole]
    },
    [userRole]
  )

  const value: CompanyContextType = {
    companies,
    activeCompany,
    userRole,
    isLoading,
    error,
    selectCompany,
    refreshCompanies,
    hasPermission,
  }

  return <CompanyContext.Provider value={value}>{children}</CompanyContext.Provider>
}

export function useCompany(): CompanyContextType {
  const context = useContext(CompanyContext)
  if (!context) {
    throw new Error('useCompany must be used within a CompanyProvider')
  }
  return context
}
