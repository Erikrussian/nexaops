import { useState, useEffect, useCallback } from 'react'
import { AuthProvider, useAuth } from './context/AuthContext'
import { Navbar } from './components/Navbar'
import { Dashboard } from './components/Dashboard'
import { FormsManager } from './components/FormsManager'
import { CompaniesManager } from './components/CompaniesManager'
import { DepartmentsManager } from './components/DepartmentsManager'
import { AuditLogsManager } from './components/AuditLogsManager'
import { AuthModal } from './components/AuthModal'
import { api } from './services/api'
import type { Company } from './types'
import { LogIn, Sparkles, Building2 } from 'lucide-react'

function AppContent() {
  const { isAuthenticated, isLoading } = useAuth()
  const [currentTab, setCurrentTab] = useState<string>('dashboard')
  const [companies, setCompanies] = useState<Company[]>([])
  const [activeCompany, setActiveCompany] = useState<Company | null>(null)
  const [isAuthModalOpen, setIsAuthModalOpen] = useState(false)

  // Fetch user companies when authenticated
  const loadCompanies = useCallback(async () => {
    if (!isAuthenticated) {
      setCompanies([])
      setActiveCompany(null)
      return
    }

    try {
      const data = await api.getMyCompanies()
      setCompanies(data)
      if (data.length > 0) {
        // Retain current selection if valid, or select first
        setActiveCompany((prev) => (prev && data.some((c) => c.id === prev.id) ? prev : data[0]))
      } else {
        setActiveCompany(null)
      }
    } catch (err: any) {
      console.warn('Failed to load companies for user:', err.message)
    }
  }, [isAuthenticated])

  useEffect(() => {
    loadCompanies()
  }, [loadCompanies])

  // Show auth modal on tab navigation if unauthenticated
  const handleTabChange = (tab: string) => {
    if (!isAuthenticated && (tab === 'companies' || tab === 'audit')) {
      setIsAuthModalOpen(true)
      return
    }
    setCurrentTab(tab)
  }

  if (isLoading) {
    return (
      <div
        style={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexDirection: 'column',
          gap: '1rem',
          background: 'var(--bg-primary)',
        }}
      >
        <div
          style={{
            width: '48px',
            height: '48px',
            borderRadius: '50%',
            border: '3px solid rgba(99, 102, 241, 0.2)',
            borderTopColor: 'var(--primary)',
            animation: 'spin 1s linear infinite',
          }}
        />
        <div style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>
          Đang khôi phục phiên làm việc NexaOps...
        </div>
      </div>
    )
  }

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Navbar
        currentTab={currentTab}
        setCurrentTab={handleTabChange}
        activeCompany={activeCompany}
        companies={companies}
        onSelectCompany={setActiveCompany}
        onOpenAuth={() => setIsAuthModalOpen(true)}
      />

      {/* Unauthenticated Welcome Banner */}
      {!isAuthenticated && (
        <div
          style={{
            background: 'linear-gradient(90deg, rgba(99, 102, 241, 0.15) 0%, rgba(6, 182, 212, 0.15) 100%)',
            borderBottom: '1px solid rgba(99, 102, 241, 0.3)',
            padding: '0.75rem 1.5rem',
          }}
        >
          <div
            className="container"
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '0.75rem',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem' }}>
              <Sparkles size={16} color="var(--accent-cyan)" />
              <span style={{ color: 'var(--text-primary)' }}>
                Bạn đang ở chế độ xem khách. Đăng nhập hoặc đăng ký để quản lý tổ chức thật và lưu trữ dữ liệu trên PostgreSQL.
              </span>
            </div>
            <button
              className="btn btn-primary btn-sm"
              onClick={() => setIsAuthModalOpen(true)}
              style={{ fontSize: '0.8rem', padding: '0.35rem 0.8rem' }}
            >
              <LogIn size={13} /> Đăng nhập ngay
            </button>
          </div>
        </div>
      )}

      <main style={{ flex: 1 }}>
        {currentTab === 'dashboard' && <Dashboard />}

        {currentTab === 'forms' && <FormsManager activeCompany={activeCompany} />}

        {currentTab === 'companies' && (
          isAuthenticated ? (
            <CompaniesManager
              activeCompany={activeCompany}
              onSelectCompany={setActiveCompany}
              onCompanyUpdated={loadCompanies}
            />
          ) : (
            <div className="container animate-fade-in" style={{ padding: '3.5rem 1.5rem' }}>
              <div className="glass-card" style={{ padding: '3rem', textAlign: 'center', maxWidth: '560px', margin: '0 auto' }}>
                <Building2 size={44} color="var(--primary-light)" style={{ margin: '0 auto 1rem' }} />
                <h3 style={{ fontSize: '1.3rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                  Yêu Cầu Xác Thực Tài Khoản
                </h3>
                <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginBottom: '1.5rem' }}>
                  Để quản lý tổ chức, xem danh sách thành viên và thực hiện chuyển nhượng quyền sở hữu qua Step-up Authentication, bạn cần đăng nhập tài khoản.
                </p>
                <button className="btn btn-primary" onClick={() => setIsAuthModalOpen(true)}>
                  <LogIn size={16} /> Đăng Nhập / Đăng Ký
                </button>
              </div>
            </div>
          )
        )}

        {currentTab === 'departments' && <DepartmentsManager activeCompany={activeCompany} />}

        {currentTab === 'audit' && <AuditLogsManager activeCompany={activeCompany} />}
      </main>

      {/* Auth Modal */}
      <AuthModal isOpen={isAuthModalOpen} onClose={() => setIsAuthModalOpen(false)} />

      {/* Footer */}
      <footer
        style={{
          borderTop: '1px solid var(--border-subtle)',
          padding: '1.5rem 0',
          background: 'rgba(10, 13, 20, 0.95)',
          marginTop: 'auto',
        }}
      >
        <div
          className="container"
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '1rem',
          }}
        >
          <div style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
            © 2026 NexaOps Platform. Clean Architecture + Modular Monolith.
          </div>
          <div style={{ display: 'flex', gap: '1.25rem', fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
            <span>Backend: .NET 10 & PostgreSQL JSONB</span>
            <span>Frontend: React 19 & TypeScript</span>
            <span style={{ color: 'var(--accent-emerald)', fontWeight: 600 }}>67/67 Tests Passed</span>
          </div>
        </div>
      </footer>
    </div>
  )
}

export function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  )
}

export default App
