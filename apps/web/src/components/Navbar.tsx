import React, { useState } from 'react'
import type { Company } from '../types'
import { useAuth } from '../context/AuthContext'
import {
  Layers,
  LayoutDashboard,
  Building2,
  GitBranch,
  FileSpreadsheet,
  ShieldCheck,
  ChevronDown,
  LogOut,
  LogIn,
  Check,
  Plus,
  Users,
} from 'lucide-react'

interface NavbarProps {
  currentTab: string
  setCurrentTab: (tab: string) => void
  activeCompany: Company | null
  companies: Company[]
  onSelectCompany: (company: Company) => void
  onOpenAuth: () => void
}

export const Navbar: React.FC<NavbarProps> = ({
  currentTab,
  setCurrentTab,
  activeCompany,
  companies,
  onSelectCompany,
  onOpenAuth,
}) => {
  const { user, isAuthenticated, logout } = useAuth()
  const [isCompanyDropdownOpen, setIsCompanyDropdownOpen] = useState(false)
  const [isUserDropdownOpen, setIsUserDropdownOpen] = useState(false)

  const navItems = [
    { id: 'dashboard', label: 'Dashboard', icon: <LayoutDashboard size={17} /> },
    { id: 'companies', label: 'Tổ Chức', icon: <Building2 size={17} /> },
    { id: 'departments', label: 'Phòng Ban', icon: <GitBranch size={17} /> },
    { id: 'members', label: 'Thành Viên', icon: <Users size={17} /> },
    { id: 'forms', label: 'Biểu Mẫu', icon: <FileSpreadsheet size={17} /> },
    { id: 'audit', label: 'Nhật Ký', icon: <ShieldCheck size={17} /> },
  ]

  const userInitials = user?.name
    ? user.name
        .split(' ')
        .map((p) => p[0])
        .slice(0, 2)
        .join('')
        .toUpperCase()
    : 'U'

  return (
    <nav className="navbar">
      <div className="container nav-container">
        {/* Brand */}
        <a href="#dashboard" className="brand" onClick={() => setCurrentTab('dashboard')}>
          <div className="brand-icon">
            <Layers size={22} color="white" />
          </div>
          <span>
            Nexa<span style={{ color: 'var(--accent-cyan)' }}>Ops</span>
          </span>
        </a>

        {/* Links */}
        <div className="nav-links">
          {navItems.map((item) => (
            <button
              key={item.id}
              className={`nav-item ${currentTab === item.id ? 'active' : ''}`}
              onClick={() => setCurrentTab(item.id)}
              style={{ background: 'none', border: 'none', cursor: 'pointer' }}
            >
              {item.icon}
              {item.label}
            </button>
          ))}
        </div>

        {/* Right Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.85rem' }}>
          {/* Tenant Switcher Dropdown */}
          {isAuthenticated && (
            <div style={{ position: 'relative' }}>
              <button
                type="button"
                onClick={() => {
                  setIsCompanyDropdownOpen(!isCompanyDropdownOpen)
                  setIsUserDropdownOpen(false)
                }}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                  padding: '0.45rem 0.85rem',
                  borderRadius: 'var(--radius-md)',
                  background: 'rgba(255, 255, 255, 0.04)',
                  border: '1px solid var(--border-subtle)',
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                  color: 'white',
                }}
              >
                <Building2 size={15} color="var(--primary-light)" />
                <span
                  style={{
                    fontWeight: 600,
                    color: 'var(--text-primary)',
                    maxWidth: '160px',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  {activeCompany?.name || 'Chọn tổ chức'}
                </span>
                <ChevronDown size={14} color="var(--text-muted)" />
              </button>

              {/* Company dropdown menu */}
              {isCompanyDropdownOpen && (
                <div
                  style={{
                    position: 'absolute',
                    top: 'calc(100% + 8px)',
                    right: 0,
                    width: '240px',
                    background: 'var(--bg-secondary)',
                    border: '1px solid var(--border-hover)',
                    borderRadius: 'var(--radius-md)',
                    boxShadow: '0 12px 30px rgba(0, 0, 0, 0.6)',
                    padding: '0.5rem',
                    zIndex: 200,
                  }}
                >
                  <div
                    style={{
                      fontSize: '0.725rem',
                      fontWeight: 700,
                      color: 'var(--text-muted)',
                      textTransform: 'uppercase',
                      padding: '0.4rem 0.6rem',
                    }}
                  >
                    Danh sách tổ chức ({companies.length})
                  </div>

                  <div style={{ maxHeight: '180px', overflowY: 'auto' }}>
                    {companies.map((comp) => {
                      const isSelected = activeCompany?.id === comp.id
                      return (
                        <div
                          key={comp.id}
                          onClick={() => {
                            onSelectCompany(comp)
                            setIsCompanyDropdownOpen(false)
                          }}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '0.5rem 0.6rem',
                            borderRadius: 'var(--radius-sm)',
                            cursor: 'pointer',
                            fontSize: '0.85rem',
                            color: isSelected ? 'var(--primary-light)' : 'var(--text-primary)',
                            background: isSelected ? 'rgba(99, 102, 241, 0.12)' : 'transparent',
                          }}
                        >
                          <div style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {comp.name}
                          </div>
                          {isSelected && <Check size={14} color="var(--primary-light)" />}
                        </div>
                      )
                    })}
                  </div>

                  <div
                    style={{
                      borderTop: '1px solid var(--border-subtle)',
                      marginTop: '0.4rem',
                      paddingTop: '0.4rem',
                    }}
                  >
                    <button
                      type="button"
                      onClick={() => {
                        setIsCompanyDropdownOpen(false)
                        setCurrentTab('companies')
                      }}
                      style={{
                        width: '100%',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.4rem',
                        padding: '0.45rem 0.6rem',
                        borderRadius: 'var(--radius-sm)',
                        background: 'none',
                        border: 'none',
                        color: 'var(--accent-cyan)',
                        fontSize: '0.8rem',
                        cursor: 'pointer',
                      }}
                    >
                      <Plus size={14} /> Quản lý / Tạo mới công ty
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* User Profile or Login Button */}
          {isAuthenticated ? (
            <div style={{ position: 'relative' }}>
              <button
                type="button"
                onClick={() => {
                  setIsUserDropdownOpen(!isUserDropdownOpen)
                  setIsCompanyDropdownOpen(false)
                }}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                  background: 'none',
                  border: 'none',
                  cursor: 'pointer',
                  padding: 0,
                }}
              >
                <div
                  style={{
                    width: '36px',
                    height: '36px',
                    borderRadius: 'var(--radius-md)',
                    background: 'linear-gradient(135deg, var(--accent-purple), var(--primary))',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontWeight: 700,
                    fontSize: '0.85rem',
                    color: 'white',
                    boxShadow: '0 0 10px rgba(168, 85, 247, 0.3)',
                  }}
                >
                  {userInitials}
                </div>
              </button>

              {/* User Dropdown */}
              {isUserDropdownOpen && (
                <div
                  style={{
                    position: 'absolute',
                    top: 'calc(100% + 8px)',
                    right: 0,
                    width: '220px',
                    background: 'var(--bg-secondary)',
                    border: '1px solid var(--border-hover)',
                    borderRadius: 'var(--radius-md)',
                    boxShadow: '0 12px 30px rgba(0, 0, 0, 0.6)',
                    padding: '0.75rem',
                    zIndex: 200,
                  }}
                >
                  <div style={{ paddingBottom: '0.6rem', borderBottom: '1px solid var(--border-subtle)' }}>
                    <div style={{ fontWeight: 700, color: 'white', fontSize: '0.9rem' }}>{user?.name}</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{user?.email}</div>
                    <span
                      style={{
                        display: 'inline-block',
                        fontSize: '0.65rem',
                        fontWeight: 700,
                        textTransform: 'uppercase',
                        padding: '0.15rem 0.4rem',
                        borderRadius: '4px',
                        background: 'rgba(99, 102, 241, 0.15)',
                        color: 'var(--primary-light)',
                        marginTop: '0.35rem',
                      }}
                    >
                      Vai trò: {user?.role}
                    </span>
                  </div>

                  <button
                    type="button"
                    onClick={() => {
                      setIsUserDropdownOpen(false)
                      logout()
                    }}
                    style={{
                      width: '100%',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '0.5rem',
                      padding: '0.55rem 0.5rem',
                      marginTop: '0.5rem',
                      borderRadius: 'var(--radius-sm)',
                      background: 'none',
                      border: 'none',
                      color: 'var(--accent-rose)',
                      fontSize: '0.85rem',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    <LogOut size={15} /> Đăng Xuất (Sign out)
                  </button>
                </div>
              )}
            </div>
          ) : (
            <button className="btn btn-primary btn-sm" onClick={onOpenAuth}>
              <LogIn size={14} /> Đăng Nhập
            </button>
          )}
        </div>
      </div>
    </nav>
  )
}
