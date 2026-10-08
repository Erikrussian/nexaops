import { useState, useEffect, useMemo, useCallback } from 'react'
import type { Company, CompanyMember, Department } from '../types'
import { api } from '../services/api'
import { useAuth } from '../context/AuthContext'
import {
  Users,
  UserPlus,
  Mail,
  ShieldCheck,
  Briefcase,
  User as UserIcon,
  Crown,
  Search,
  RefreshCw,
  Copy,
  Check,
  Trash2,
  Edit2,
  Clock,
  CheckCircle2,
  AlertCircle,
  Building2,
  Ticket,
  X,
} from 'lucide-react'

interface CompanyMembersManagerProps {
  activeCompany: Company | null
}

// Fallback mock members for offline/guest display
const INITIAL_MOCK_MEMBERS: CompanyMember[] = [
  {
    id: 'mem_01',
    companyId: 'comp_01',
    userId: 'usr_01',
    userName: 'Nguyễn Văn Quản Trị',
    email: 'admin@nexaops.com',
    role: 'OWNER',
    status: 'ACTIVE',
    createdAt: '2026-08-01T08:00:00Z',
    updatedAt: '2026-08-01T08:00:00Z',
  },
  {
    id: 'mem_02',
    companyId: 'comp_01',
    userId: 'usr_02',
    userName: 'Trần Thị Thu Thảo',
    email: 'thao.tt@nexaops.com',
    role: 'ADMIN',
    status: 'ACTIVE',
    departmentId: 'dept_root_1',
    createdAt: '2026-08-15T09:30:00Z',
    updatedAt: '2026-08-15T09:30:00Z',
  },
  {
    id: 'mem_03',
    companyId: 'comp_01',
    userId: 'usr_03',
    userName: 'Đặng Tuấn Anh',
    email: 'anh.dt@nexaops.com',
    role: 'MANAGER',
    status: 'ACTIVE',
    departmentId: 'dept_sub_1_1',
    createdAt: '2026-09-01T10:15:00Z',
    updatedAt: '2026-09-01T10:15:00Z',
  },
  {
    id: 'mem_04',
    companyId: 'comp_01',
    email: 'lan.hoang@partner.com',
    role: 'MEMBER',
    status: 'INVITED',
    departmentId: 'dept_sub_1_1',
    inviteExpiresAt: '2026-10-15T23:59:59Z',
    createdAt: '2026-10-01T14:20:00Z',
    updatedAt: '2026-10-01T14:20:00Z',
  },
]

export function CompanyMembersManager({ activeCompany }: CompanyMembersManagerProps) {
  const { user, isAuthenticated } = useAuth()

  // Main state
  const [members, setMembers] = useState<CompanyMember[]>([])
  const [departments, setDepartments] = useState<Department[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successToast, setSuccessToast] = useState<string | null>(null)

  // Filters & search
  const [searchQuery, setSearchQuery] = useState('')
  const [roleFilter, setRoleFilter] = useState<string>('ALL')
  const [statusFilter, setStatusFilter] = useState<string>('ALL')
  const [deptFilter, setDeptFilter] = useState<string>('ALL')

  // Modals state
  const [isInviteOpen, setIsInviteOpen] = useState(false)
  const [isAcceptOpen, setIsAcceptOpen] = useState(false)
  const [editingMember, setEditingMember] = useState<CompanyMember | null>(null)
  const [deletingMember, setDeletingMember] = useState<CompanyMember | null>(null)

  // Invite Form State
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState<number>(3) // 1: Admin, 2: Manager, 3: Member
  const [inviteDeptId, setInviteDeptId] = useState<string>('')
  const [inviteLoading, setInviteLoading] = useState(false)
  const [inviteResult, setInviteResult] = useState<{ inviteLink: string; email: string } | null>(null)
  const [copiedLink, setCopiedLink] = useState(false)

  // Join by Token State
  const [tokenInput, setTokenInput] = useState('')
  const [tokenLoading, setTokenLoading] = useState(false)
  const [tokenError, setTokenError] = useState<string | null>(null)

  // Edit Member Form State
  const [editRole, setEditRole] = useState<number>(3)
  const [editStatus, setEditStatus] = useState<number>(0)
  const [editDeptId, setEditDeptId] = useState<string>('')
  const [editLoading, setEditLoading] = useState(false)
  const [editError, setEditError] = useState<string | null>(null)

  // Delete Action State
  const [deleteLoading, setDeleteLoading] = useState(false)

  // Auto-clear success toast
  useEffect(() => {
    if (successToast) {
      const timer = setTimeout(() => setSuccessToast(null), 4000)
      return () => clearTimeout(timer)
    }
  }, [successToast])

  // Load members and departments from server
  const loadData = useCallback(async () => {
    if (!activeCompany) {
      setMembers([])
      setDepartments([])
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      if (isAuthenticated) {
        const [membersData, deptsData] = await Promise.all([
          api.getCompanyMembers(activeCompany.id).catch(() => INITIAL_MOCK_MEMBERS),
          api.getDepartments(activeCompany.id).catch(() => []),
        ])
        setMembers(membersData && membersData.length > 0 ? membersData : INITIAL_MOCK_MEMBERS)
        setDepartments(deptsData || [])
      } else {
        setMembers(INITIAL_MOCK_MEMBERS)
        setDepartments([])
      }
    } catch (err: unknown) {
      console.warn('Failed to fetch company members, falling back to mock:', err)
      setMembers(INITIAL_MOCK_MEMBERS)
    } finally {
      setIsLoading(false)
    }
  }, [activeCompany, isAuthenticated])

  useEffect(() => {
    loadData()
  }, [loadData])

  // Department name helper
  const departmentMap = useMemo(() => {
    const map = new Map<string, string>()
    departments.forEach((d) => map.set(d.id, d.name))
    return map
  }, [departments])

  // Filtered members list
  const filteredMembers = useMemo(() => {
    return members.filter((m) => {
      // Query filter
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase()
        const name = (m.user?.name || m.userName || '').toLowerCase()
        const email = m.email.toLowerCase()
        const deptName = (m.departmentId ? departmentMap.get(m.departmentId) || '' : '').toLowerCase()
        if (!name.includes(q) && !email.includes(q) && !deptName.includes(q)) {
          return false
        }
      }

      // Role filter
      if (roleFilter !== 'ALL') {
        const normalizedRole = m.role?.toUpperCase()
        if (normalizedRole !== roleFilter) return false
      }

      // Status filter
      if (statusFilter !== 'ALL') {
        const normalizedStatus = m.status?.toUpperCase()
        if (normalizedStatus !== statusFilter) return false
      }

      // Dept filter
      if (deptFilter !== 'ALL') {
        if (deptFilter === 'NONE') {
          if (m.departmentId) return false
        } else {
          if (m.departmentId !== deptFilter) return false
        }
      }

      return true
    })
  }, [members, searchQuery, roleFilter, statusFilter, deptFilter, departmentMap])

  // Metrics
  const metrics = useMemo(() => {
    const total = members.length
    const active = members.filter((m) => m.status?.toUpperCase() === 'ACTIVE').length
    const pending = members.filter((m) => m.status?.toUpperCase() === 'INVITED').length
    const assignedDepts = new Set(members.filter((m) => m.departmentId).map((m) => m.departmentId)).size
    return { total, active, pending, assignedDepts }
  }, [members])

  // Handle Invite Member
  const handleInviteSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeCompany || !inviteEmail.trim()) return

    setInviteLoading(true)
    setError(null)

    try {
      const res = await api.inviteMember(activeCompany.id, {
        email: inviteEmail.trim(),
        role: inviteRole,
        departmentId: inviteDeptId ? inviteDeptId : undefined,
      })

      setInviteResult({
        inviteLink: res.inviteLink || `http://localhost:3000/auth/invite?token=sample_token`,
        email: inviteEmail.trim(),
      })
      setSuccessToast(`Đã gửi lời mời tham gia thành công tới ${inviteEmail}!`)
      loadData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gửi lời mời thất bại. Vui lòng kiểm tra lại.'
      setError(msg)
    } finally {
      setInviteLoading(false)
    }
  }

  // Handle Copy Invite Link
  const handleCopyLink = (link: string) => {
    navigator.clipboard.writeText(link)
    setCopiedLink(true)
    setTimeout(() => setCopiedLink(false), 2500)
  }

  // Handle Accept Token Submit
  const handleAcceptToken = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!tokenInput.trim()) return

    setTokenLoading(true)
    setTokenError(null)

    try {
      const res = await api.acceptInvite({ inviteToken: tokenInput.trim() })
      setSuccessToast(res.message || 'Gia nhập tổ chức thành công!')
      setIsAcceptOpen(false)
      setTokenInput('')
      loadData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Mã lời mời không hợp lệ hoặc đã hết hạn.'
      setTokenError(msg)
    } finally {
      setTokenLoading(false)
    }
  }

  // Open Edit Modal
  const handleOpenEdit = (m: CompanyMember) => {
    setEditingMember(m)
    // Convert role to number: 1: Admin, 2: Manager, 3: Member
    const r = m.role?.toUpperCase()
    if (r === 'ADMIN') setEditRole(1)
    else if (r === 'MANAGER') setEditRole(2)
    else setEditRole(3)

    // Status: 0: Active, 2: Suspended
    const s = m.status?.toUpperCase()
    setEditStatus(s === 'SUSPENDED' || s === 'INACTIVE' ? 2 : 0)

    setEditDeptId(m.departmentId || '')
    setEditError(null)
  }

  // Submit Edit Member
  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!editingMember) return

    setEditLoading(true)
    setEditError(null)

    try {
      await api.updateMember(editingMember.id, {
        role: editRole,
        status: editStatus,
        departmentId: editDeptId ? editDeptId : undefined,
      })

      setSuccessToast('Đã cập nhật thông tin thành viên thành công!')
      setEditingMember(null)
      loadData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Cập nhật thất bại'
      setEditError(msg)
    } finally {
      setEditLoading(false)
    }
  }

  // Submit Remove Member
  const handleConfirmDelete = async () => {
    if (!deletingMember) return
    setDeleteLoading(true)

    try {
      await api.removeMember(deletingMember.id)
      setSuccessToast(`Đã xóa thành viên khỏi công ty thành công.`)
      setDeletingMember(null)
      loadData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Xóa thành viên thất bại'
      setError(msg)
    } finally {
      setDeleteLoading(false)
    }
  }

  // Format date helper
  const formatDate = (isoString?: string) => {
    if (!isoString) return '-'
    try {
      const d = new Date(isoString)
      return d.toLocaleDateString('vi-VN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
      })
    } catch {
      return isoString
    }
  }

  // Role visual badge helper
  const renderRoleBadge = (role: string) => {
    const r = role?.toUpperCase()
    switch (r) {
      case 'OWNER':
        return (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.35rem',
              padding: '0.25rem 0.65rem',
              borderRadius: '9999px',
              fontSize: '0.75rem',
              fontWeight: 700,
              background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.25) 0%, rgba(217, 119, 6, 0.15) 100%)',
              color: '#fbbf24',
              border: '1px solid rgba(245, 158, 11, 0.35)',
              boxShadow: '0 0 10px rgba(245, 158, 11, 0.2)',
            }}
          >
            <Crown size={12} /> Chủ Sở Hữu (Owner)
          </span>
        )
      case 'ADMIN':
        return (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.35rem',
              padding: '0.25rem 0.65rem',
              borderRadius: '9999px',
              fontSize: '0.75rem',
              fontWeight: 700,
              background: 'rgba(99, 102, 241, 0.18)',
              color: '#a5b4fc',
              border: '1px solid rgba(99, 102, 241, 0.3)',
            }}
          >
            <ShieldCheck size={12} /> Quản Trị Viên (Admin)
          </span>
        )
      case 'MANAGER':
        return (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.35rem',
              padding: '0.25rem 0.65rem',
              borderRadius: '9999px',
              fontSize: '0.75rem',
              fontWeight: 600,
              background: 'rgba(6, 182, 212, 0.15)',
              color: '#38bdf8',
              border: '1px solid rgba(6, 182, 212, 0.3)',
            }}
          >
            <Briefcase size={12} /> Quản Lý (Manager)
          </span>
        )
      default:
        return (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.35rem',
              padding: '0.25rem 0.65rem',
              borderRadius: '9999px',
              fontSize: '0.75rem',
              fontWeight: 500,
              background: 'rgba(148, 163, 184, 0.12)',
              color: 'var(--text-secondary)',
              border: '1px solid var(--border-subtle)',
            }}
          >
            <UserIcon size={12} /> Thành Viên (Member)
          </span>
        )
    }
  }

  // Status visual badge helper
  const renderStatusBadge = (status: string, expiresAt?: string) => {
    const s = status?.toUpperCase()
    if (s === 'ACTIVE') {
      return (
        <span
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '0.4rem',
            padding: '0.2rem 0.6rem',
            borderRadius: '9999px',
            fontSize: '0.75rem',
            fontWeight: 600,
            background: 'rgba(16, 185, 129, 0.15)',
            color: '#34d399',
            border: '1px solid rgba(16, 185, 129, 0.25)',
          }}
        >
          <span
            style={{
              width: '6px',
              height: '6px',
              borderRadius: '50%',
              backgroundColor: '#10b981',
              boxShadow: '0 0 6px #10b981',
            }}
          />
          Hoạt Động
        </span>
      )
    }
    if (s === 'INVITED') {
      return (
        <span
          title={expiresAt ? `Hết hạn: ${formatDate(expiresAt)}` : undefined}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '0.4rem',
            padding: '0.2rem 0.6rem',
            borderRadius: '9999px',
            fontSize: '0.75rem',
            fontWeight: 600,
            background: 'rgba(245, 158, 11, 0.15)',
            color: '#fbbf24',
            border: '1px solid rgba(245, 158, 11, 0.25)',
          }}
        >
          <Clock size={11} /> Đang Chờ Duyệt
        </span>
      )
    }
    return (
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: '0.4rem',
          padding: '0.2rem 0.6rem',
          borderRadius: '9999px',
          fontSize: '0.75rem',
          fontWeight: 600,
          background: 'rgba(239, 68, 68, 0.12)',
          color: '#f87171',
          border: '1px solid rgba(239, 68, 68, 0.25)',
        }}
      >
        <span
          style={{
            width: '6px',
            height: '6px',
            borderRadius: '50%',
            backgroundColor: '#ef4444',
          }}
        />
        Đình Chỉ
      </span>
    )
  }

  if (!activeCompany) {
    return (
      <div className="container animate-fade-in" style={{ padding: '3.5rem 1.5rem' }}>
        <div className="glass-card" style={{ padding: '3.5rem 2rem', textAlign: 'center', maxWidth: '600px', margin: '0 auto' }}>
          <Building2 size={48} color="var(--primary-light)" style={{ margin: '0 auto 1.25rem' }} />
          <h3 style={{ fontSize: '1.4rem', fontWeight: 800, color: 'white', marginBottom: '0.75rem' }}>
            Chưa Chọn Tổ Chức
          </h3>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', lineHeight: 1.6 }}>
            Vui lòng chọn hoặc tạo một công ty / tổ chức từ menu thanh điều hướng trên cùng để quản lý danh sách thành viên, phân quyền và phòng ban.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="container animate-fade-in" style={{ padding: '2rem 1.5rem 4rem' }}>
      {/* Toast Notification */}
      {successToast && (
        <div
          style={{
            position: 'fixed',
            top: '5rem',
            right: '2rem',
            zIndex: 1000,
            background: 'linear-gradient(135deg, rgba(16, 185, 129, 0.95) 0%, rgba(5, 150, 105, 0.95) 100%)',
            color: 'white',
            padding: '0.85rem 1.25rem',
            borderRadius: 'var(--radius-md)',
            boxShadow: '0 10px 25px -5px rgba(16, 185, 129, 0.4)',
            display: 'flex',
            alignItems: 'center',
            gap: '0.6rem',
            fontSize: '0.875rem',
            fontWeight: 600,
            animation: 'slideIn 0.3s cubic-bezier(0.16, 1, 0.3, 1)',
          }}
        >
          <CheckCircle2 size={18} />
          {successToast}
        </div>
      )}

      {/* Global Error Banner */}
      {error && (
        <div
          style={{
            background: 'rgba(239, 68, 68, 0.12)',
            border: '1px solid rgba(239, 68, 68, 0.3)',
            borderRadius: 'var(--radius-md)',
            padding: '0.85rem 1.25rem',
            color: '#fca5a5',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '1.5rem',
            fontSize: '0.875rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <AlertCircle size={18} color="#ef4444" />
            <span>{error}</span>
          </div>
          <button
            onClick={() => setError(null)}
            style={{ background: 'none', border: 'none', color: '#fca5a5', cursor: 'pointer' }}
          >
            <X size={16} />
          </button>
        </div>
      )}

      {/* Header & Action Bar */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: '1.25rem',
          marginBottom: '2rem',
        }}
      >
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', marginBottom: '0.4rem' }}>
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.35rem',
                fontSize: '0.75rem',
                fontWeight: 700,
                textTransform: 'uppercase',
                letterSpacing: '0.05em',
                color: 'var(--accent-cyan)',
                background: 'rgba(6, 182, 212, 0.12)',
                padding: '0.2rem 0.6rem',
                borderRadius: 'var(--radius-sm)',
              }}
            >
              <Building2 size={12} /> {activeCompany.name}
            </span>
            <span style={{ color: 'var(--text-muted)', fontSize: '0.8rem' }}>• Đội ngũ nhân sự</span>
          </div>
          <h1 style={{ fontSize: '1.85rem', fontWeight: 800, color: 'white', letterSpacing: '-0.02em' }}>
            Quản Lý Thành Viên & Phân Quyền
          </h1>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', marginTop: '0.2rem' }}>
            Mời nhân sự, cấu trúc vai trò (Owner, Admin, Manager, Member) và liên kết phòng ban theo mô hình bảo mật NexaOps.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
          <button
            className="btn btn-secondary"
            onClick={() => {
              setTokenError(null)
              setTokenInput('')
              setIsAcceptOpen(true)
            }}
            style={{ fontSize: '0.875rem' }}
          >
            <Ticket size={16} /> Nhập Mã Lời Mời
          </button>

          <button
            className="btn btn-primary"
            onClick={() => {
              setInviteResult(null)
              setInviteEmail('')
              setInviteRole(3)
              setInviteDeptId('')
              setIsInviteOpen(true)
            }}
            style={{ fontSize: '0.875rem' }}
          >
            <UserPlus size={16} /> Mời Thành Viên Mới
          </button>
        </div>
      </div>

      {/* Metrics Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))',
          gap: '1rem',
          marginBottom: '2rem',
        }}
      >
        <div className="glass-card" style={{ padding: '1.25rem 1.5rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', fontWeight: 600 }}>TỔNG THÀNH VIÊN</span>
            <div style={{ padding: '0.4rem', borderRadius: '8px', background: 'rgba(99, 102, 241, 0.15)', color: 'var(--primary-light)' }}>
              <Users size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.85rem', fontWeight: 800, color: 'white' }}>{metrics.total}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
            Đã đăng ký trong tổ chức
          </div>
        </div>

        <div className="glass-card" style={{ padding: '1.25rem 1.5rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', fontWeight: 600 }}>ĐANG HOẠT ĐỘNG</span>
            <div style={{ padding: '0.4rem', borderRadius: '8px', background: 'rgba(16, 185, 129, 0.15)', color: '#34d399' }}>
              <CheckCircle2 size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.85rem', fontWeight: 800, color: '#34d399' }}>{metrics.active}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
            {metrics.total > 0 ? Math.round((metrics.active / metrics.total) * 100) : 0}% tỷ lệ kích hoạt
          </div>
        </div>

        <div className="glass-card" style={{ padding: '1.25rem 1.5rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', fontWeight: 600 }}>LỜI MỜI CHỜ DUYỆT</span>
            <div style={{ padding: '0.4rem', borderRadius: '8px', background: 'rgba(245, 158, 11, 0.15)', color: '#fbbf24' }}>
              <Clock size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.85rem', fontWeight: 800, color: '#fbbf24' }}>{metrics.pending}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
            Chưa kích hoạt qua token
          </div>
        </div>

        <div className="glass-card" style={{ padding: '1.25rem 1.5rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', fontWeight: 600 }}>PHÒNG BAN LIÊN KẾT</span>
            <div style={{ padding: '0.4rem', borderRadius: '8px', background: 'rgba(6, 182, 212, 0.15)', color: 'var(--accent-cyan)' }}>
              <Briefcase size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.85rem', fontWeight: 800, color: 'var(--accent-cyan)' }}>{metrics.assignedDepts}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
            Phòng ban có nhân sự trực thuộc
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div
        className="glass-card"
        style={{
          padding: '1rem 1.25rem',
          marginBottom: '1.5rem',
          display: 'flex',
          gap: '1rem',
          alignItems: 'center',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
        }}
      >
        <div style={{ display: 'flex', gap: '0.75rem', flex: 1, minWidth: '280px', flexWrap: 'wrap' }}>
          {/* Search Box */}
          <div style={{ position: 'relative', flex: 1, minWidth: '220px' }}>
            <Search
              size={16}
              style={{
                position: 'absolute',
                left: '0.85rem',
                top: '50%',
                transform: 'translateY(-50%)',
                color: 'var(--text-muted)',
              }}
            />
            <input
              type="text"
              placeholder="Tìm theo họ tên, email hoặc phòng ban..."
              className="form-control"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                paddingLeft: '2.4rem',
                height: '38px',
                fontSize: '0.875rem',
                background: 'rgba(10, 13, 20, 0.6)',
              }}
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                style={{
                  position: 'absolute',
                  right: '0.75rem',
                  top: '50%',
                  transform: 'translateY(-50%)',
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  cursor: 'pointer',
                }}
              >
                <X size={14} />
              </button>
            )}
          </div>

          {/* Role Filter */}
          <select
            className="form-control"
            value={roleFilter}
            onChange={(e) => setRoleFilter(e.target.value)}
            style={{ width: '150px', height: '38px', fontSize: '0.85rem', background: 'rgba(10, 13, 20, 0.6)' }}
          >
            <option value="ALL">Tất cả vai trò</option>
            <option value="OWNER">Owner (Chủ)</option>
            <option value="ADMIN">Admin (Quản trị)</option>
            <option value="MANAGER">Manager (Quản lý)</option>
            <option value="MEMBER">Member (Thành viên)</option>
          </select>

          {/* Status Filter */}
          <select
            className="form-control"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{ width: '150px', height: '38px', fontSize: '0.85rem', background: 'rgba(10, 13, 20, 0.6)' }}
          >
            <option value="ALL">Tất cả trạng thái</option>
            <option value="ACTIVE">Hoạt động</option>
            <option value="INVITED">Chờ kích hoạt</option>
            <option value="SUSPENDED">Đình chỉ</option>
          </select>

          {/* Department Filter */}
          {departments.length > 0 && (
            <select
              className="form-control"
              value={deptFilter}
              onChange={(e) => setDeptFilter(e.target.value)}
              style={{ width: '180px', height: '38px', fontSize: '0.85rem', background: 'rgba(10, 13, 20, 0.6)' }}
            >
              <option value="ALL">Tất cả phòng ban</option>
              <option value="NONE">Chưa phân bổ</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          )}
        </div>

        {/* Refresh & Count */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
            Hiển thị <strong>{filteredMembers.length}</strong> / {members.length}
          </span>
          <button
            className="btn btn-secondary btn-sm"
            onClick={loadData}
            disabled={isLoading}
            title="Tải lại danh sách"
            style={{ padding: '0.4rem 0.6rem' }}
          >
            <RefreshCw size={14} className={isLoading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* Members Table */}
      <div className="glass-card" style={{ padding: 0, overflow: 'hidden' }}>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
            <thead>
              <tr
                style={{
                  borderBottom: '1px solid var(--border-subtle)',
                  background: 'rgba(16, 22, 34, 0.8)',
                  color: 'var(--text-secondary)',
                  fontSize: '0.75rem',
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                }}
              >
                <th style={{ padding: '0.9rem 1.25rem' }}>Nhân Sự / Email</th>
                <th style={{ padding: '0.9rem 1rem' }}>Phòng Ban</th>
                <th style={{ padding: '0.9rem 1rem' }}>Vai Trò</th>
                <th style={{ padding: '0.9rem 1rem' }}>Trạng Thái</th>
                <th style={{ padding: '0.9rem 1rem' }}>Ngày Tham Gia</th>
                <th style={{ padding: '0.9rem 1.25rem', textAlign: 'right' }}>Thao Tác</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && members.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
                    <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem' }}>
                      <RefreshCw size={16} className="animate-spin" color="var(--primary)" />
                      <span>Đang tải danh sách thành viên...</span>
                    </div>
                  </td>
                </tr>
              ) : filteredMembers.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: '3.5rem 1rem', textAlign: 'center' }}>
                    <Users size={36} color="var(--text-muted)" style={{ margin: '0 auto 0.75rem' }} />
                    <div style={{ fontWeight: 600, color: 'white', marginBottom: '0.25rem' }}>
                      Không tìm thấy thành viên phù hợp
                    </div>
                    <div style={{ color: 'var(--text-secondary)', fontSize: '0.8rem', maxWidth: '360px', margin: '0 auto 1rem' }}>
                      Thử điều chỉnh từ khóa tìm kiếm hoặc bỏ bớt các bộ lọc vai trò, trạng thái.
                    </div>
                    {(searchQuery || roleFilter !== 'ALL' || statusFilter !== 'ALL' || deptFilter !== 'ALL') && (
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => {
                          setSearchQuery('')
                          setRoleFilter('ALL')
                          setStatusFilter('ALL')
                          setDeptFilter('ALL')
                        }}
                      >
                        Xóa tất cả bộ lọc
                      </button>
                    )}
                  </td>
                </tr>
              ) : (
                filteredMembers.map((m) => {
                  const isCurrentAuthUser =
                    Boolean(user && ((m.userId && m.userId === user.id) || m.email.toLowerCase() === user.email.toLowerCase()))
                  const isOwner = m.role?.toUpperCase() === 'OWNER'
                  const departmentName = m.departmentId ? departmentMap.get(m.departmentId) : null
                  const displayName = m.user?.name || m.userName || m.email.split('@')[0]
                  const initials = displayName
                    .split(' ')
                    .map((n) => n[0])
                    .slice(0, 2)
                    .join('')
                    .toUpperCase()

                  return (
                    <tr
                      key={m.id}
                      style={{
                        borderBottom: '1px solid var(--border-subtle)',
                        transition: 'background var(--transition-fast)',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.02)')}
                      onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                    >
                      {/* Name & Email */}
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                          <div
                            style={{
                              width: '38px',
                              height: '38px',
                              borderRadius: '50%',
                              background: isOwner
                                ? 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)'
                                : 'linear-gradient(135deg, #6366f1 0%, #4338ca 100%)',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              fontSize: '0.85rem',
                              fontWeight: 700,
                              color: 'white',
                              flexShrink: 0,
                            }}
                          >
                            {initials}
                          </div>
                          <div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                              <span style={{ fontWeight: 600, color: 'white' }}>{displayName}</span>
                              {isCurrentAuthUser && (
                                <span
                                  style={{
                                    fontSize: '0.65rem',
                                    padding: '0.1rem 0.4rem',
                                    borderRadius: '4px',
                                    background: 'rgba(99, 102, 241, 0.2)',
                                    color: 'var(--primary-light)',
                                    fontWeight: 700,
                                  }}
                                >
                                  Bạn
                                </span>
                              )}
                            </div>
                            <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>{m.email}</div>
                          </div>
                        </div>
                      </td>

                      {/* Department */}
                      <td style={{ padding: '1rem' }}>
                        {departmentName ? (
                          <span
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '0.35rem',
                              padding: '0.2rem 0.55rem',
                              borderRadius: '6px',
                              fontSize: '0.8rem',
                              background: 'rgba(255, 255, 255, 0.04)',
                              border: '1px solid var(--border-subtle)',
                              color: 'var(--text-primary)',
                            }}
                          >
                            <Building2 size={12} color="var(--primary-light)" />
                            {departmentName}
                          </span>
                        ) : (
                          <span style={{ color: 'var(--text-muted)', fontSize: '0.8rem', fontStyle: 'italic' }}>
                            Chưa phân bổ
                          </span>
                        )}
                      </td>

                      {/* Role */}
                      <td style={{ padding: '1rem' }}>{renderRoleBadge(m.role)}</td>

                      {/* Status */}
                      <td style={{ padding: '1rem' }}>{renderStatusBadge(m.status, m.inviteExpiresAt)}</td>

                      {/* Created At */}
                      <td style={{ padding: '1rem', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                        {formatDate(m.createdAt)}
                        {m.invitedBy && (
                          <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>
                            Mời bởi {m.invitedBy.name}
                          </div>
                        )}
                      </td>

                      {/* Actions */}
                      <td style={{ padding: '1rem 1.25rem', textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', gap: '0.4rem' }}>
                          {/* If invited, quick copy invite token / link action */}
                          {m.status?.toUpperCase() === 'INVITED' && (
                            <button
                              className="btn btn-secondary btn-sm"
                              onClick={() => {
                                const sampleLink = `http://localhost:3000/auth/invite?token=invite_${m.id}`
                                handleCopyLink(sampleLink)
                                setSuccessToast('Đã sao chép liên kết mời thành viên vào bộ nhớ tạm!')
                              }}
                              title="Sao chép link mời"
                              style={{ padding: '0.35rem 0.5rem' }}
                            >
                              <Copy size={13} />
                            </button>
                          )}

                          {/* Edit Role/Dept (Hidden for Owner or if current user cannot edit) */}
                          <button
                            className="btn btn-secondary btn-sm"
                            onClick={() => handleOpenEdit(m)}
                            title="Chỉnh sửa vai trò / phòng ban"
                            disabled={isOwner}
                            style={{
                              padding: '0.35rem 0.5rem',
                              opacity: isOwner ? 0.4 : 1,
                              cursor: isOwner ? 'not-allowed' : 'pointer',
                            }}
                          >
                            <Edit2 size={13} />
                          </button>

                          {/* Remove Member */}
                          <button
                            className="btn btn-sm"
                            onClick={() => setDeletingMember(m)}
                            title={isOwner ? 'Không thể xóa chủ sở hữu' : 'Xóa thành viên khỏi tổ chức'}
                            disabled={isOwner}
                            style={{
                              padding: '0.35rem 0.5rem',
                              background: 'rgba(239, 68, 68, 0.1)',
                              border: '1px solid rgba(239, 68, 68, 0.25)',
                              color: '#ef4444',
                              opacity: isOwner ? 0.3 : 1,
                              cursor: isOwner ? 'not-allowed' : 'pointer',
                            }}
                          >
                            <Trash2 size={13} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* ----------------- INVITE MEMBER MODAL ----------------- */}
      {isInviteOpen && (
        <div className="modal-overlay">
          <div
            className="modal-container animate-scale-up"
            style={{ maxWidth: '520px', width: '100%', padding: '2rem' }}
          >
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '1.25rem',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                <div
                  style={{
                    padding: '0.5rem',
                    borderRadius: '8px',
                    background: 'rgba(99, 102, 241, 0.15)',
                    color: 'var(--primary-light)',
                  }}
                >
                  <UserPlus size={20} />
                </div>
                <div>
                  <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>Mời Thành Viên Mới</h3>
                  <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    Tổ chức: <strong>{activeCompany.name}</strong>
                  </div>
                </div>
              </div>
              <button
                onClick={() => setIsInviteOpen(false)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={18} />
              </button>
            </div>

            {inviteResult ? (
              // Success Screen with Invite Link
              <div>
                <div
                  style={{
                    padding: '1.25rem',
                    borderRadius: 'var(--radius-md)',
                    background: 'rgba(16, 185, 129, 0.1)',
                    border: '1px solid rgba(16, 185, 129, 0.25)',
                    marginBottom: '1.5rem',
                    textAlign: 'center',
                  }}
                >
                  <CheckCircle2 size={36} color="#10b981" style={{ margin: '0 auto 0.75rem' }} />
                  <h4 style={{ color: 'white', fontWeight: 700, marginBottom: '0.35rem' }}>
                    Lời Mời Đã Được Tạo Thành Công!
                  </h4>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
                    Một liên kết mời kích hoạt đã được tạo cho email <strong>{inviteResult.email}</strong>.
                  </p>
                </div>

                <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                  <label className="form-label" style={{ fontSize: '0.8rem' }}>
                    ĐƯỜNG DẪN THAM GIA TRỰC TIẾP (CÓ HIỆU LỰC 7 NGÀY)
                  </label>
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <input
                      type="text"
                      className="form-control"
                      readOnly
                      value={inviteResult.inviteLink}
                      style={{ fontSize: '0.85rem', fontFamily: 'var(--font-mono)' }}
                    />
                    <button
                      className="btn btn-secondary"
                      onClick={() => handleCopyLink(inviteResult.inviteLink)}
                      style={{ flexShrink: 0 }}
                    >
                      {copiedLink ? <Check size={16} color="#10b981" /> : <Copy size={16} />}
                      {copiedLink ? 'Đã Chép' : 'Sao Chép'}
                    </button>
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                  <button
                    className="btn btn-secondary"
                    onClick={() => {
                      setInviteResult(null)
                      setInviteEmail('')
                    }}
                  >
                    Mời thêm người khác
                  </button>
                  <button className="btn btn-primary" onClick={() => setIsInviteOpen(false)}>
                    Hoàn tất
                  </button>
                </div>
              </div>
            ) : (
              // Form Input Screen
              <form onSubmit={handleInviteSubmit}>
                <div className="form-group" style={{ marginBottom: '1.25rem' }}>
                  <label className="form-label">
                    Email nhân sự <span style={{ color: 'var(--accent-rose)' }}>*</span>
                  </label>
                  <div style={{ position: 'relative' }}>
                    <Mail
                      size={16}
                      style={{
                        position: 'absolute',
                        left: '0.85rem',
                        top: '50%',
                        transform: 'translateY(-50%)',
                        color: 'var(--text-muted)',
                      }}
                    />
                    <input
                      type="email"
                      required
                      placeholder="vd: nhanvien@nexaops.com"
                      className="form-control"
                      value={inviteEmail}
                      onChange={(e) => setInviteEmail(e.target.value)}
                      style={{ paddingLeft: '2.5rem' }}
                    />
                  </div>
                  <small style={{ color: 'var(--text-muted)', fontSize: '0.75rem', marginTop: '0.35rem', display: 'block' }}>
                    Hệ thống sẽ ghi nhận lời mời bảo mật và gán vai trò khi người dùng xác nhận.
                  </small>
                </div>

                {/* Role selection radio grid */}
                <div className="form-group" style={{ marginBottom: '1.25rem' }}>
                  <label className="form-label">Vai trò trong tổ chức</label>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '0.65rem' }}>
                    <div
                      onClick={() => setInviteRole(1)}
                      style={{
                        padding: '0.85rem',
                        borderRadius: 'var(--radius-md)',
                        border: `1px solid ${inviteRole === 1 ? 'var(--primary)' : 'var(--border-subtle)'}`,
                        background: inviteRole === 1 ? 'rgba(99, 102, 241, 0.12)' : 'rgba(255, 255, 255, 0.02)',
                        cursor: 'pointer',
                        transition: 'all 0.15s ease',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.2rem' }}>
                        <ShieldCheck size={16} color="var(--primary-light)" />
                        <span style={{ fontWeight: 700, color: 'white', fontSize: '0.875rem' }}>
                          Quản Trị Viên (Admin)
                        </span>
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                        Toàn quyền cấu hình phòng ban, biểu mẫu, thành viên và kiểm duyệt toàn hệ thống.
                      </div>
                    </div>

                    <div
                      onClick={() => setInviteRole(2)}
                      style={{
                        padding: '0.85rem',
                        borderRadius: 'var(--radius-md)',
                        border: `1px solid ${inviteRole === 2 ? 'var(--accent-cyan)' : 'var(--border-subtle)'}`,
                        background: inviteRole === 2 ? 'rgba(6, 182, 212, 0.12)' : 'rgba(255, 255, 255, 0.02)',
                        cursor: 'pointer',
                        transition: 'all 0.15s ease',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.2rem' }}>
                        <Briefcase size={16} color="var(--accent-cyan)" />
                        <span style={{ fontWeight: 700, color: 'white', fontSize: '0.875rem' }}>
                          Trưởng Bộ Phận (Manager)
                        </span>
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                        Phụ trách phòng ban, phê duyệt hồ sơ và giám sát các quy trình nội bộ.
                      </div>
                    </div>

                    <div
                      onClick={() => setInviteRole(3)}
                      style={{
                        padding: '0.85rem',
                        borderRadius: 'var(--radius-md)',
                        border: `1px solid ${inviteRole === 3 ? 'var(--accent-emerald)' : 'var(--border-subtle)'}`,
                        background: inviteRole === 3 ? 'rgba(16, 185, 129, 0.12)' : 'rgba(255, 255, 255, 0.02)',
                        cursor: 'pointer',
                        transition: 'all 0.15s ease',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.2rem' }}>
                        <UserIcon size={16} color="#34d399" />
                        <span style={{ fontWeight: 700, color: 'white', fontSize: '0.875rem' }}>
                          Thành Viên (Member)
                        </span>
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                        Nhập liệu biểu mẫu, nộp đơn từ và theo dõi tiến độ xử lý hồ sơ cá nhân.
                      </div>
                    </div>
                  </div>
                </div>

                {/* Optional Department assignment */}
                {departments.length > 0 && (
                  <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                    <label className="form-label">Chỉ định phòng ban (Tùy chọn)</label>
                    <select
                      className="form-control"
                      value={inviteDeptId}
                      onChange={(e) => setInviteDeptId(e.target.value)}
                    >
                      <option value="">-- Chưa chỉ định phòng ban --</option>
                      {departments.map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.name} {d.code ? `(${d.code})` : ''}
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => setIsInviteOpen(false)}
                    disabled={inviteLoading}
                  >
                    Hủy bỏ
                  </button>
                  <button type="submit" className="btn btn-primary" disabled={inviteLoading || !inviteEmail.trim()}>
                    {inviteLoading ? (
                      <>
                        <RefreshCw size={14} className="animate-spin" /> Đang tạo lời mời...
                      </>
                    ) : (
                      <>
                        <UserPlus size={16} /> Gửi Lời Mời
                      </>
                    )}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}

      {/* ----------------- JOIN VIA TOKEN MODAL ----------------- */}
      {isAcceptOpen && (
        <div className="modal-overlay">
          <div
            className="modal-container animate-scale-up"
            style={{ maxWidth: '480px', width: '100%', padding: '2rem' }}
          >
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '1.25rem',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                <div
                  style={{
                    padding: '0.5rem',
                    borderRadius: '8px',
                    background: 'rgba(6, 182, 212, 0.15)',
                    color: 'var(--accent-cyan)',
                  }}
                >
                  <Ticket size={20} />
                </div>
                <div>
                  <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>Nhập Mã Lời Mời</h3>
                  <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    Tham gia tổ chức bằng mã được cấp
                  </div>
                </div>
              </div>
              <button
                onClick={() => setIsAcceptOpen(false)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={18} />
              </button>
            </div>

            {tokenError && (
              <div
                style={{
                  background: 'rgba(239, 68, 68, 0.12)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '0.65rem 0.85rem',
                  color: '#fca5a5',
                  fontSize: '0.8rem',
                  marginBottom: '1rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.4rem',
                }}
              >
                <AlertCircle size={15} color="#ef4444" />
                <span>{tokenError}</span>
              </div>
            )}

            <form onSubmit={handleAcceptToken}>
              <div className="form-group" style={{ marginBottom: '1.25rem' }}>
                <label className="form-label">
                  Mã lời mời (Token) <span style={{ color: 'var(--accent-rose)' }}>*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="Dán token lời mời, vd: 9a7b-3c..."
                  className="form-control"
                  value={tokenInput}
                  onChange={(e) => setTokenInput(e.target.value)}
                  style={{ fontFamily: 'var(--font-mono)' }}
                />
                <small style={{ color: 'var(--text-muted)', fontSize: '0.75rem', marginTop: '0.35rem', display: 'block' }}>
                  Lưu ý: Bạn phải đăng nhập đúng tài khoản email được Quản trị viên chỉ định khi tạo lời mời.
                </small>
              </div>

              <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsAcceptOpen(false)}
                  disabled={tokenLoading}
                >
                  Đóng
                </button>
                <button type="submit" className="btn btn-primary" disabled={tokenLoading || !tokenInput.trim()}>
                  {tokenLoading ? (
                    <>
                      <RefreshCw size={14} className="animate-spin" /> Đang kiểm tra...
                    </>
                  ) : (
                    <>
                      <CheckCircle2 size={16} /> Xác Nhận Gia Nhập
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ----------------- EDIT MEMBER MODAL ----------------- */}
      {editingMember && (
        <div className="modal-overlay">
          <div
            className="modal-container animate-scale-up"
            style={{ maxWidth: '480px', width: '100%', padding: '2rem' }}
          >
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '1.25rem',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                <div
                  style={{
                    padding: '0.5rem',
                    borderRadius: '8px',
                    background: 'rgba(99, 102, 241, 0.15)',
                    color: 'var(--primary-light)',
                  }}
                >
                  <Edit2 size={20} />
                </div>
                <div>
                  <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>Cập Nhật Thành Viên</h3>
                  <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    {editingMember.email}
                  </div>
                </div>
              </div>
              <button
                onClick={() => setEditingMember(null)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={18} />
              </button>
            </div>

            {editError && (
              <div
                style={{
                  background: 'rgba(239, 68, 68, 0.12)',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '0.65rem 0.85rem',
                  color: '#fca5a5',
                  fontSize: '0.8rem',
                  marginBottom: '1rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.4rem',
                }}
              >
                <AlertCircle size={15} color="#ef4444" />
                <span>{editError}</span>
              </div>
            )}

            <form onSubmit={handleSaveEdit}>
              {/* Role Select */}
              <div className="form-group" style={{ marginBottom: '1.25rem' }}>
                <label className="form-label">Vai trò</label>
                <select
                  className="form-control"
                  value={editRole}
                  onChange={(e) => setEditRole(Number(e.target.value))}
                >
                  <option value={1}>Quản Trị Viên (Admin)</option>
                  <option value={2}>Quản Lý (Manager)</option>
                  <option value={3}>Thành Viên (Member)</option>
                </select>
              </div>

              {/* Status Select */}
              <div className="form-group" style={{ marginBottom: '1.25rem' }}>
                <label className="form-label">Trạng thái tài khoản</label>
                <select
                  className="form-control"
                  value={editStatus}
                  onChange={(e) => setEditStatus(Number(e.target.value))}
                >
                  <option value={0}>Hoạt Động (Active)</option>
                  <option value={2}>Đình Chỉ (Suspended)</option>
                </select>
              </div>

              {/* Department Select */}
              {departments.length > 0 && (
                <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                  <label className="form-label">Phòng ban trực thuộc</label>
                  <select
                    className="form-control"
                    value={editDeptId}
                    onChange={(e) => setEditDeptId(e.target.value)}
                  >
                    <option value="">-- Chưa phân bổ phòng ban --</option>
                    {departments.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name} {d.code ? `(${d.code})` : ''}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setEditingMember(null)}
                  disabled={editLoading}
                >
                  Hủy
                </button>
                <button type="submit" className="btn btn-primary" disabled={editLoading}>
                  {editLoading ? (
                    <>
                      <RefreshCw size={14} className="animate-spin" /> Đang lưu...
                    </>
                  ) : (
                    'Lưu Thay Đổi'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ----------------- REMOVE MEMBER MODAL ----------------- */}
      {deletingMember && (
        <div className="modal-overlay">
          <div
            className="modal-container animate-scale-up"
            style={{ maxWidth: '450px', width: '100%', padding: '2rem' }}
          >
            <div style={{ textAlign: 'center', marginBottom: '1.25rem' }}>
              <div
                style={{
                  width: '52px',
                  height: '52px',
                  borderRadius: '50%',
                  background: 'rgba(239, 68, 68, 0.15)',
                  color: '#ef4444',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  margin: '0 auto 1rem',
                }}
              >
                <Trash2 size={24} />
              </div>
              <h3 style={{ fontSize: '1.25rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                Xóa Thành Viên Khỏi Tổ Chức?
              </h3>
              <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', lineHeight: 1.5 }}>
                Bạn có chắc chắn muốn xóa{' '}
                <strong style={{ color: 'white' }}>
                  {deletingMember.user?.name || deletingMember.userName || deletingMember.email}
                </strong>{' '}
                khỏi <strong>{activeCompany.name}</strong>? Hành động này sẽ thu hồi toàn bộ quyền truy cập phòng ban và dữ liệu biểu mẫu.
              </p>
            </div>

            <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'center' }}>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setDeletingMember(null)}
                disabled={deleteLoading}
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                className="btn"
                onClick={handleConfirmDelete}
                disabled={deleteLoading}
                style={{
                  background: 'linear-gradient(135deg, #ef4444 0%, #b91c1c 100%)',
                  color: 'white',
                  fontWeight: 600,
                  border: 'none',
                }}
              >
                {deleteLoading ? (
                  <>
                    <RefreshCw size={14} className="animate-spin" /> Đang xóa...
                  </>
                ) : (
                  'Xác Nhận Xóa'
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
