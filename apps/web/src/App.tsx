import { useState } from 'react'
import { Navbar } from './components/Navbar'
import { Dashboard } from './components/Dashboard'
import { Building2, GitBranch, FileSpreadsheet, ShieldCheck } from 'lucide-react'

export function App() {
  const [currentTab, setCurrentTab] = useState<string>('dashboard')
  const currentCompany = 'NexaOps Global Ltd'

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Navbar
        currentTab={currentTab}
        setCurrentTab={setCurrentTab}
        currentCompany={currentCompany}
      />

      <main style={{ flex: 1 }}>
        {currentTab === 'dashboard' && <Dashboard />}

        {currentTab === 'companies' && (
          <div className="container animate-fade-in" style={{ padding: '3rem 1.5rem' }}>
            <div className="glass-card" style={{ padding: '2.5rem', textAlign: 'center' }}>
              <div
                style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: 'var(--radius-lg)',
                  background: 'rgba(99, 102, 241, 0.1)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'var(--primary-light)',
                  marginBottom: '1rem',
                }}
              >
                <Building2 size={32} />
              </div>
              <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                Quản Trị Tổ Chức & Công Ty
              </h2>
              <p style={{ color: 'var(--text-secondary)', maxWidth: '520px', margin: '0 auto 1.5rem' }}>
                Hỗ trợ thiết lập thông tin tenant, chuyển giao quyền sở hữu công ty (Ownership Transfer) bảo mật cao và phân quyền nhân sự.
              </p>
              <button className="btn btn-primary" onClick={() => setCurrentTab('dashboard')}>
                Trở về Dashboard
              </button>
            </div>
          </div>
        )}

        {currentTab === 'departments' && (
          <div className="container animate-fade-in" style={{ padding: '3rem 1.5rem' }}>
            <div className="glass-card" style={{ padding: '2.5rem', textAlign: 'center' }}>
              <div
                style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: 'var(--radius-lg)',
                  background: 'rgba(6, 182, 212, 0.1)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'var(--accent-cyan)',
                  marginBottom: '1rem',
                }}
              >
                <GitBranch size={32} />
              </div>
              <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                Cơ Cấu Phòng Ban Phân Cấp
              </h2>
              <p style={{ color: 'var(--text-secondary)', maxWidth: '520px', margin: '0 auto 1.5rem' }}>
                Mô hình tổ chức dạng cây phân cấp (Tree Hierarchy) với xác thực bảo mật chặn xóa phòng ban cha khi còn phòng ban con.
              </p>
              <button className="btn btn-primary" onClick={() => setCurrentTab('dashboard')}>
                Trở về Dashboard
              </button>
            </div>
          </div>
        )}

        {currentTab === 'forms' && (
          <div className="container animate-fade-in" style={{ padding: '3rem 1.5rem' }}>
            <div className="glass-card" style={{ padding: '2.5rem', textAlign: 'center' }}>
              <div
                style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: 'var(--radius-lg)',
                  background: 'rgba(168, 85, 247, 0.1)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'var(--accent-purple)',
                  marginBottom: '1rem',
                }}
              >
                <FileSpreadsheet size={32} />
              </div>
              <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                Trình Xây Dựng Biểu Mẫu Động (Dynamic Form Builder)
              </h2>
              <p style={{ color: 'var(--text-secondary)', maxWidth: '520px', margin: '0 auto 1.5rem' }}>
                Thiết kế biểu mẫu không cần code, lưu trữ schema trên PostgreSQL JSONB và tự động xác thực trường dữ liệu bằng Dynamic Validation Engine.
              </p>
              <button className="btn btn-primary" onClick={() => setCurrentTab('dashboard')}>
                Trở về Dashboard
              </button>
            </div>
          </div>
        )}

        {currentTab === 'audit' && (
          <div className="container animate-fade-in" style={{ padding: '3rem 1.5rem' }}>
            <div className="glass-card" style={{ padding: '2.5rem', textAlign: 'center' }}>
              <div
                style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: 'var(--radius-lg)',
                  background: 'rgba(16, 185, 129, 0.1)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'var(--accent-emerald)',
                  marginBottom: '1rem',
                }}
              >
                <ShieldCheck size={32} />
              </div>
              <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                Nhật Ký Kiểm Toán Toàn Hệ Thống (Audit Trail)
              </h2>
              <p style={{ color: 'var(--text-secondary)', maxWidth: '520px', margin: '0 auto 1.5rem' }}>
                Lưu vết 100% mọi hành động nhạy cảm trên hệ thống (chuyển nhượng quyền sở hữu, thêm thành viên, duyệt đơn) phục vụ kiểm toán an toàn thông tin.
              </p>
              <button className="btn btn-primary" onClick={() => setCurrentTab('dashboard')}>
                Trở về Dashboard
              </button>
            </div>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer
        style={{
          borderTop: '1px solid var(--border-subtle)',
          padding: '1.5rem 0',
          background: 'rgba(10, 13, 20, 0.95)',
          marginTop: 'auto',
        }}
      >
        <div className="container" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
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

export default App
