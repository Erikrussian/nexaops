import { useState } from 'react'
import { AuthProvider, useAuth } from './context/AuthContext'
import { CompanyProvider, useCompany } from './context/CompanyContext'
import { Navbar } from './components/Navbar'
import { Dashboard } from './components/Dashboard'
import { FormsManager } from './components/FormsManager'
import { CompaniesManager } from './components/CompaniesManager'
import { DepartmentsManager } from './components/DepartmentsManager'
import { CompanyMembersManager } from './components/CompanyMembersManager'
import { AuditLogsManager } from './components/AuditLogsManager'
import { AuthModal } from './components/AuthModal'
import { LogIn, Sparkles, Building2, ShieldAlert } from 'lucide-react'

function AppContent() {
  const { isAuthenticated, isInitializing } = useAuth()
  const { companies, activeCompany, selectCompany, refreshCompanies, hasPermission } = useCompany()
  const [currentTab, setCurrentTab] = useState<string>('dashboard')
  const [isAuthModalOpen, setIsAuthModalOpen] = useState(false)

  // Show auth modal on tab navigation if unauthenticated
  const handleTabChange = (tab: string) => {
    if (!isAuthenticated && (tab === 'companies' || tab === 'audit' || tab === 'members' || tab === 'departments')) {
      setIsAuthModalOpen(true)
      return
    }
    setCurrentTab(tab)
  }

  if (isInitializing) {
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
          Đang khôi phục phiên làm việc bảo mật NexaOps...
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
        onSelectCompany={selectCompany}
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
                Bạn đang ở chế độ xem khách. Đăng nhập hoặc đăng ký để quản lý tổ chức thật và lưu trữ dữ liệu an toàn trên PostgreSQL.
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
        {currentTab === 'dashboard' && (
          <Dashboard activeCompany={activeCompany} onNavigateTab={handleTabChange} />
        )}

        {currentTab === 'forms' && <FormsManager activeCompany={activeCompany} />}

        {currentTab === 'companies' && (
          isAuthenticated ? (
            <CompaniesManager
              activeCompany={activeCompany}
              onSelectCompany={selectCompany}
              onCompanyUpdated={refreshCompanies}
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

        {currentTab === 'members' && <CompanyMembersManager activeCompany={activeCompany} />}

        {currentTab === 'audit' && (
          isAuthenticated && !hasPermission('MANAGER') ? (
            <div className="container animate-fade-in" style={{ padding: '3.5rem 1.5rem' }}>
              <div className="glass-card" style={{ padding: '3rem', textAlign: 'center', maxWidth: '560px', margin: '0 auto' }}>
                <ShieldAlert size={44} color="var(--accent-rose)" style={{ margin: '0 auto 1rem' }} />
                <h3 style={{ fontSize: '1.3rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                  Quyền Truy Cập Bị Hạn Chế
                </h3>
                <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginBottom: '1.5rem' }}>
                  Nhật ký kiểm toán (Audit Logs) chỉ dành cho người dùng có vai trò Quản trị viên (Admin), Quản lý (Manager) hoặc Chủ sở hữu (Owner) của tổ chức.
                </p>
                <button className="btn btn-secondary" onClick={() => setCurrentTab('dashboard')}>
                  Quay lại Dashboard
                </button>
              </div>
            </div>
          ) : (
            <AuditLogsManager activeCompany={activeCompany} />
          )
        )}
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
            <span style={{ color: 'var(--accent-emerald)', fontWeight: 600 }}>74/74 Tests Passed</span>
          </div>
        </div>
      </footer>
    </div>
  )
}

export function App() {
  return (
    <AuthProvider>
      <CompanyProvider>
        <AppContent />
      </CompanyProvider>
    </AuthProvider>
  )
}

export default App
