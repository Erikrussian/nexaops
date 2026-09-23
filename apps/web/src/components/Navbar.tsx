import React from 'react'
import {
  Layers,
  LayoutDashboard,
  Building2,
  GitBranch,
  FileSpreadsheet,
  ShieldCheck,
  Bell,
  ChevronDown,
} from 'lucide-react'

interface NavbarProps {
  currentTab: string
  setCurrentTab: (tab: string) => void
  currentCompany: string
}

export const Navbar: React.FC<NavbarProps> = ({
  currentTab,
  setCurrentTab,
  currentCompany,
}) => {
  const navItems = [
    { id: 'dashboard', label: 'Dashboard', icon: <LayoutDashboard size={17} /> },
    { id: 'companies', label: 'Companies', icon: <Building2 size={17} /> },
    { id: 'departments', label: 'Departments', icon: <GitBranch size={17} /> },
    { id: 'forms', label: 'Dynamic Forms', icon: <FileSpreadsheet size={17} /> },
    { id: 'audit', label: 'Audit Trail', icon: <ShieldCheck size={17} /> },
  ]

  return (
    <nav className="navbar">
      <div className="container nav-container">
        {/* Brand */}
        <a href="#dashboard" className="brand" onClick={() => setCurrentTab('dashboard')}>
          <div className="brand-icon">
            <Layers size={22} color="white" />
          </div>
          <span>Nexa<span style={{ color: 'var(--accent-cyan)' }}>Ops</span></span>
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
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          {/* Tenant Switcher Badge */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.5rem',
              padding: '0.4rem 0.8rem',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(255, 255, 255, 0.04)',
              border: '1px solid var(--border-subtle)',
              fontSize: '0.85rem',
              cursor: 'pointer',
            }}
          >
            <Building2 size={15} color="var(--primary-light)" />
            <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{currentCompany}</span>
            <ChevronDown size={14} color="var(--text-muted)" />
          </div>

          {/* Notification Button */}
          <button
            style={{
              width: '36px',
              height: '36px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(255, 255, 255, 0.04)',
              border: '1px solid var(--border-subtle)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--text-secondary)',
              cursor: 'pointer',
            }}
          >
            <Bell size={17} />
          </button>

          {/* User Avatar */}
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
            AD
          </div>
        </div>
      </div>
    </nav>
  )
}
