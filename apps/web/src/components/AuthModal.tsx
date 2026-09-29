import { useState } from 'react'
import { useAuth } from '../context/AuthContext'
import { Mail, Lock, User as UserIcon, LogIn, UserPlus, AlertCircle, Eye, EyeOff, ShieldCheck, Check } from 'lucide-react'

interface AuthModalProps {
  isOpen: boolean
  onClose?: () => void
  initialMode?: 'login' | 'register'
}

export function AuthModal({ isOpen, onClose, initialMode = 'login' }: AuthModalProps) {
  const { login, register, error, clearError, isLoading } = useAuth()
  const [mode, setMode] = useState<'login' | 'register'>(initialMode)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [clientErrors, setClientErrors] = useState<Record<string, string>>({})
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  if (!isOpen) return null

  const validate = (): boolean => {
    const errs: Record<string, string> = {}

    if (mode === 'register') {
      if (!name.trim()) {
        errs.name = 'Họ và tên không được để trống'
      }
    }

    if (!email.trim()) {
      errs.email = 'Email không được để trống'
    } else {
      const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
      if (!emailRegex.test(email.trim())) {
        errs.email = 'Địa chỉ email không đúng định dạng'
      }
    }

    if (!password) {
      errs.password = 'Mật khẩu không được để trống'
    } else if (mode === 'register' && password.length < 6) {
      errs.password = 'Mật khẩu đăng ký phải có ít nhất 6 ký tự'
    }

    setClientErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    clearError()
    setSuccessMessage(null)

    if (!validate()) return

    try {
      if (mode === 'login') {
        await login(email.trim(), password)
        if (onClose) onClose()
      } else {
        await register(name.trim(), email.trim(), password)
        setSuccessMessage('Đăng ký tài khoản thành công! Đang chuyển hướng vào hệ thống...')
        setTimeout(() => {
          if (onClose) onClose()
        }, 800)
      }
    } catch {
      // Error handled by AuthContext
    }
  }

  const handleQuickDemo = (demoEmail: string, demoPass: string) => {
    setEmail(demoEmail)
    setPassword(demoPass)
    setClientErrors({})
    clearError()
  }

  return (
    <div className="modal-backdrop">
      <div
        className="modal-container"
        style={{
          maxWidth: '460px',
          background: 'linear-gradient(180deg, #101625 0%, #0a0d14 100%)',
          border: '1px solid rgba(99, 102, 241, 0.25)',
          boxShadow: '0 25px 60px -15px rgba(0, 0, 0, 0.8), 0 0 40px rgba(99, 102, 241, 0.15)',
        }}
      >
        {/* Brand Banner */}
        <div style={{ textAlign: 'center', padding: '2rem 1.75rem 1rem' }}>
          <div
            style={{
              width: '54px',
              height: '54px',
              borderRadius: 'var(--radius-lg)',
              background: 'linear-gradient(135deg, var(--primary) 0%, var(--accent-cyan) 100%)',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              boxShadow: '0 0 25px var(--primary-glow)',
              marginBottom: '1rem',
            }}
          >
            <ShieldCheck size={28} />
          </div>
          <h2 style={{ fontSize: '1.4rem', fontWeight: 800, color: 'white', letterSpacing: '-0.02em' }}>
            NexaOps Platform
          </h2>
          <p style={{ fontSize: '0.825rem', color: 'var(--text-secondary)', marginTop: '0.25rem' }}>
            Xác thực phiên làm việc an toàn với JWT Bearer & Multi-Tenant RBAC
          </p>

          {/* Mode Switcher Tabs */}
          <div
            style={{
              display: 'flex',
              background: 'rgba(255, 255, 255, 0.04)',
              borderRadius: 'var(--radius-md)',
              padding: '0.3rem',
              marginTop: '1.25rem',
              border: '1px solid var(--border-subtle)',
            }}
          >
            <button
              type="button"
              onClick={() => {
                setMode('login')
                clearError()
                setClientErrors({})
              }}
              style={{
                flex: 1,
                padding: '0.55rem',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                background: mode === 'login' ? 'var(--primary)' : 'transparent',
                color: mode === 'login' ? 'white' : 'var(--text-secondary)',
                fontWeight: 600,
                fontSize: '0.85rem',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              Đăng Nhập
            </button>
            <button
              type="button"
              onClick={() => {
                setMode('register')
                clearError()
                setClientErrors({})
              }}
              style={{
                flex: 1,
                padding: '0.55rem',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                background: mode === 'register' ? 'var(--primary)' : 'transparent',
                color: mode === 'register' ? 'white' : 'var(--text-secondary)',
                fontWeight: 600,
                fontSize: '0.85rem',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              Đăng Ký Mới
            </button>
          </div>
        </div>

        {/* Form Body */}
        <div style={{ padding: '0.5rem 1.75rem 1.75rem' }}>
          {/* Error Alert */}
          {error && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.6rem',
                padding: '0.75rem 1rem',
                borderRadius: 'var(--radius-md)',
                background: 'rgba(244, 63, 94, 0.12)',
                border: '1px solid rgba(244, 63, 94, 0.3)',
                color: '#fb7185',
                fontSize: '0.825rem',
                marginBottom: '1rem',
              }}
            >
              <AlertCircle size={16} style={{ flexShrink: 0 }} />
              <span>{error}</span>
            </div>
          )}

          {/* Success Alert */}
          {successMessage && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.6rem',
                padding: '0.75rem 1rem',
                borderRadius: 'var(--radius-md)',
                background: 'rgba(16, 185, 129, 0.12)',
                border: '1px solid rgba(16, 185, 129, 0.3)',
                color: '#34d399',
                fontSize: '0.825rem',
                marginBottom: '1rem',
              }}
            >
              <Check size={16} style={{ flexShrink: 0 }} />
              <span>{successMessage}</span>
            </div>
          )}

          <form onSubmit={handleSubmit}>
            {/* Name Field (Register only) */}
            {mode === 'register' && (
              <div className="input-group">
                <label className="input-label">Họ và tên *</label>
                <div style={{ position: 'relative' }}>
                  <input
                    type="text"
                    className="input-control"
                    placeholder="Nguyễn Văn A"
                    style={{ paddingLeft: '2.5rem' }}
                    value={name}
                    onChange={(e) => {
                      setName(e.target.value)
                      if (clientErrors.name) setClientErrors({ ...clientErrors, name: '' })
                    }}
                  />
                  <UserIcon
                    size={16}
                    color="var(--text-muted)"
                    style={{ position: 'absolute', left: '0.85rem', top: '50%', transform: 'translateY(-50%)' }}
                  />
                </div>
                {clientErrors.name && (
                  <span style={{ fontSize: '0.75rem', color: 'var(--accent-rose)', marginTop: '0.2rem' }}>
                    {clientErrors.name}
                  </span>
                )}
              </div>
            )}

            {/* Email Field */}
            <div className="input-group">
              <label className="input-label">Địa chỉ Email *</label>
              <div style={{ position: 'relative' }}>
                <input
                  type="email"
                  className="input-control"
                  placeholder="admin@nexaops.com"
                  style={{ paddingLeft: '2.5rem' }}
                  value={email}
                  onChange={(e) => {
                    setEmail(e.target.value)
                    if (clientErrors.email) setClientErrors({ ...clientErrors, email: '' })
                  }}
                />
                <Mail
                  size={16}
                  color="var(--text-muted)"
                  style={{ position: 'absolute', left: '0.85rem', top: '50%', transform: 'translateY(-50%)' }}
                />
              </div>
              {clientErrors.email && (
                <span style={{ fontSize: '0.75rem', color: 'var(--accent-rose)', marginTop: '0.2rem' }}>
                  {clientErrors.email}
                </span>
              )}
            </div>

            {/* Password Field */}
            <div className="input-group">
              <label className="input-label">
                <span>Mật khẩu *</span>
                {mode === 'register' && (
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Tối thiểu 6 ký tự</span>
                )}
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  type={showPassword ? 'text' : 'password'}
                  className="input-control"
                  placeholder="••••••••"
                  style={{ paddingLeft: '2.5rem', paddingRight: '2.5rem' }}
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value)
                    if (clientErrors.password) setClientErrors({ ...clientErrors, password: '' })
                  }}
                />
                <Lock
                  size={16}
                  color="var(--text-muted)"
                  style={{ position: 'absolute', left: '0.85rem', top: '50%', transform: 'translateY(-50%)' }}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{
                    position: 'absolute',
                    right: '0.75rem',
                    top: '50%',
                    transform: 'translateY(-50%)',
                    background: 'none',
                    border: 'none',
                    color: 'var(--text-muted)',
                    cursor: 'pointer',
                    padding: '0.2rem',
                  }}
                >
                  {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
              {clientErrors.password && (
                <span style={{ fontSize: '0.75rem', color: 'var(--accent-rose)', marginTop: '0.2rem' }}>
                  {clientErrors.password}
                </span>
              )}
            </div>

            <button
              type="submit"
              className="btn btn-primary"
              disabled={isLoading}
              style={{ width: '100%', justifyContent: 'center', marginTop: '1rem', padding: '0.75rem' }}
            >
              {isLoading ? (
                <span>Đang xử lý xác thực...</span>
              ) : mode === 'login' ? (
                <>
                  <LogIn size={16} /> Đăng Nhập Hệ Thống
                </>
              ) : (
                <>
                  <UserPlus size={16} /> Hoàn Tất Đăng Ký
                </>
              )}
            </button>
          </form>

          {/* Quick Demo Helper */}
          {mode === 'login' && (
            <div
              style={{
                marginTop: '1.5rem',
                padding: '0.75rem',
                borderRadius: 'var(--radius-md)',
                background: 'rgba(255, 255, 255, 0.02)',
                border: '1px dashed var(--border-subtle)',
                textAlign: 'center',
              }}
            >
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.4rem' }}>
                Gợi ý tài khoản thử nghiệm nhanh (Quick Fill):
              </div>
              <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'center' }}>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  style={{ fontSize: '0.75rem', padding: '0.25rem 0.55rem' }}
                  onClick={() => handleQuickDemo('admin@nexaops.com', 'Admin@123')}
                >
                  admin@nexaops.com
                </button>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  style={{ fontSize: '0.75rem', padding: '0.25rem 0.55rem' }}
                  onClick={() => handleQuickDemo('owner@nexaops.com', 'Owner@123')}
                >
                  owner@nexaops.com
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
