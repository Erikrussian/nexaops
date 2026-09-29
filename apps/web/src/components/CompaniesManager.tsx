import { useState, useEffect } from 'react'
import type { Company, CompanyMember } from '../types'
import { api } from '../services/api'
import { useAuth } from '../context/AuthContext'
import {
  Building2,
  Plus,
  Users,
  KeyRound,
  Trash2,
  ShieldAlert,
  AlertCircle,
  X,
  Crown,
} from 'lucide-react'

interface CompaniesManagerProps {
  activeCompany: Company | null
  onSelectCompany: (company: Company) => void
  onCompanyUpdated?: () => void
}

export function CompaniesManager({ activeCompany, onSelectCompany, onCompanyUpdated }: CompaniesManagerProps) {
  const { user } = useAuth()
  const [companies, setCompanies] = useState<Company[]>([])
  const [isLoading, setIsLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Create Company Modal
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [createName, setCreateName] = useState('')
  const [createSlug, setCreateSlug] = useState('')
  const [createCode, setCreateCode] = useState('')
  const [createDesc, setCreateDesc] = useState('')
  const [createLoading, setCreateLoading] = useState(false)
  const [createError, setCreateError] = useState<string | null>(null)

  // Members Modal
  const [viewingMembersCompany, setViewingMembersCompany] = useState<Company | null>(null)
  const [members, setMembers] = useState<CompanyMember[]>([])
  const [membersLoading, setMembersLoading] = useState(false)

  // Step-up Ownership Transfer Modal
  const [transferCompany, setTransferCompany] = useState<Company | null>(null)
  const [transferMembers, setTransferMembers] = useState<CompanyMember[]>([])
  const [transferTargetId, setTransferTargetId] = useState<string>('')
  const [transferPassword, setTransferPassword] = useState<string>('')
  const [transferRole, setTransferRole] = useState<number>(1) // 1 = Admin
  const [transferLoading, setTransferLoading] = useState(false)
  const [transferError, setTransferError] = useState<string | null>(null)
  const [transferSuccess, setTransferSuccess] = useState<string | null>(null)

  const fetchCompanies = async () => {
    setIsLoading(true)
    setError(null)
    try {
      const data = await api.getMyCompanies()
      setCompanies(data)
      if (!activeCompany && data.length > 0) {
        onSelectCompany(data[0])
      }
    } catch (err: any) {
      setError(err.message || 'Không thể tải danh sách công ty')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    fetchCompanies()
  }, [])

  const handleCreateCompany = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!createName.trim()) {
      setCreateError('Tên công ty không được để trống')
      return
    }

    setCreateLoading(true)
    setCreateError(null)

    try {
      const newComp = await api.createCompany({
        name: createName.trim(),
        slug: createSlug.trim() || undefined,
        code: createCode.trim() || undefined,
        description: createDesc.trim() || undefined,
      })

      setCompanies([newComp, ...companies])
      onSelectCompany(newComp)
      setIsCreateOpen(false)
      setCreateName('')
      setCreateSlug('')
      setCreateCode('')
      setCreateDesc('')
      if (onCompanyUpdated) onCompanyUpdated()
    } catch (err: any) {
      setCreateError(err.message || 'Không thể tạo công ty')
    } finally {
      setCreateLoading(false)
    }
  }

  const handleOpenMembers = async (comp: Company) => {
    setViewingMembersCompany(comp)
    setMembersLoading(true)
    try {
      const list = await api.getCompanyMembers(comp.id)
      setMembers(list)
    } catch (err: any) {
      console.warn('Failed to load members:', err.message)
    } finally {
      setMembersLoading(false)
    }
  }

  const handleOpenTransfer = async (comp: Company) => {
    setTransferCompany(comp)
    setTransferTargetId('')
    setTransferPassword('')
    setTransferRole(1)
    setTransferError(null)
    setTransferSuccess(null)
    setTransferLoading(true)

    try {
      const list = await api.getCompanyMembers(comp.id)
      // Exclude current owner from target selection
      const eligible = list.filter((m) => m.userId !== user?.id && m.status.toUpperCase() === 'ACTIVE')
      setTransferMembers(eligible)
      if (eligible.length > 0 && eligible[0].userId) {
        setTransferTargetId(eligible[0].userId)
      }
    } catch (err: any) {
      setTransferError(err.message || 'Không thể tải danh sách thành viên')
    } finally {
      setTransferLoading(false)
    }
  }

  const handleExecuteTransfer = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!transferCompany) return

    if (!transferTargetId) {
      setTransferError('Vui lòng chọn nhân sự tiếp nhận quyền sở hữu')
      return
    }
    if (!transferPassword) {
      setTransferError('Vui lòng nhập mật khẩu của bạn để xác thực bảo mật')
      return
    }

    setTransferLoading(true)
    setTransferError(null)

    try {
      const updated = await api.transferOwnership(transferCompany.id, {
        newOwnerId: transferTargetId,
        password: transferPassword,
        previousOwnerNewRole: transferRole,
      })

      setTransferSuccess('Chuyển nhượng quyền sở hữu công ty thành công!')
      setCompanies(companies.map((c) => (c.id === updated.id ? updated : c)))
      if (activeCompany?.id === updated.id) {
        onSelectCompany(updated)
      }
      if (onCompanyUpdated) onCompanyUpdated()

      setTimeout(() => {
        setTransferCompany(null)
      }, 1200)
    } catch (err: any) {
      setTransferError(err.message || 'Chuyển nhượng quyền sở hữu thất bại')
    } finally {
      setTransferLoading(false)
    }
  }

  const handleDeleteCompany = async (compId: string) => {
    if (!confirm('Bạn có chắc chắn muốn xóa công ty này? Toàn bộ dữ liệu liên quan sẽ bị xóa vĩnh viễn.')) {
      return
    }

    try {
      await api.deleteCompany(compId)
      const remaining = companies.filter((c) => c.id !== compId)
      setCompanies(remaining)
      if (activeCompany?.id === compId && remaining.length > 0) {
        onSelectCompany(remaining[0])
      }
      if (onCompanyUpdated) onCompanyUpdated()
    } catch (err: any) {
      alert(`Lỗi khi xóa công ty: ${err.message}`)
    }
  }

  return (
    <div className="container animate-fade-in" style={{ padding: '2.5rem 1.5rem 4rem' }}>
      {/* Top Header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '1rem',
          marginBottom: '2rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div
            style={{
              width: '42px',
              height: '42px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(99, 102, 241, 0.15)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--primary-light)',
            }}
          >
            <Building2 size={22} />
          </div>
          <div>
            <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white' }}>
              Quản Trị Tổ Chức & Công Ty (Multi-Tenant Hub)
            </h2>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>
              Quản lý danh sách tenant, cấu trúc thành viên và quy trình Step-up Authentication chuyển nhượng quyền sở hữu
            </p>
          </div>
        </div>

        <button className="btn btn-primary" onClick={() => setIsCreateOpen(true)}>
          <Plus size={16} /> Tạo Công Ty Mới
        </button>
      </div>

      {/* Error alert */}
      {error && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.6rem',
            padding: '1rem',
            borderRadius: 'var(--radius-md)',
            background: 'rgba(244, 63, 94, 0.12)',
            border: '1px solid rgba(244, 63, 94, 0.3)',
            color: '#fb7185',
            marginBottom: '1.5rem',
          }}
        >
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Loading state */}
      {isLoading ? (
        <div className="glass-card" style={{ padding: '3.5rem', textAlign: 'center' }}>
          <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>Đang tải danh sách công ty...</div>
        </div>
      ) : companies.length === 0 ? (
        /* Empty State */
        <div className="glass-card" style={{ padding: '3.5rem', textAlign: 'center' }}>
          <Building2 size={48} color="var(--text-muted)" style={{ margin: '0 auto 1rem' }} />
          <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>
            Bạn chưa thuộc tổ chức hoặc công ty nào
          </h3>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginTop: '0.5rem', marginBottom: '1.5rem' }}>
            Hãy khởi tạo tổ chức đầu tiên của bạn để thiết lập phòng ban, biểu mẫu và quy trình phê duyệt tự động.
          </p>
          <button className="btn btn-primary" onClick={() => setIsCreateOpen(true)}>
            <Plus size={16} /> Tạo Công Ty Đầu Tiên
          </button>
        </div>
      ) : (
        /* Companies Grid */
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))',
            gap: '1.5rem',
          }}
        >
          {companies.map((comp) => {
            const isOwner = user?.id === comp.ownerId
            const isSelected = activeCompany?.id === comp.id

            return (
              <div
                key={comp.id}
                className="glass-card"
                style={{
                  padding: '1.5rem',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between',
                  border: isSelected ? '1px solid var(--primary)' : 'var(--glass-border)',
                  boxShadow: isSelected ? '0 0 25px var(--primary-glow)' : 'var(--glass-shadow)',
                }}
              >
                <div>
                  {/* Card Header: Badges */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '0.85rem',
                    }}
                  >
                    <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
                      {comp.code && (
                        <span
                          style={{
                            fontFamily: 'var(--font-mono)',
                            fontSize: '0.75rem',
                            fontWeight: 700,
                            padding: '0.2rem 0.55rem',
                            borderRadius: 'var(--radius-sm)',
                            background: 'rgba(99, 102, 241, 0.15)',
                            color: 'var(--primary-light)',
                          }}
                        >
                          {comp.code}
                        </span>
                      )}
                      {isSelected && (
                        <span
                          style={{
                            fontSize: '0.7rem',
                            fontWeight: 700,
                            padding: '0.2rem 0.5rem',
                            borderRadius: '999px',
                            background: 'rgba(16, 185, 129, 0.15)',
                            color: 'var(--accent-emerald)',
                            border: '1px solid rgba(16, 185, 129, 0.3)',
                          }}
                        >
                          ĐANG CHỌN
                        </span>
                      )}
                    </div>

                    {isOwner ? (
                      <span
                        className="badge badge-published"
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '0.3rem',
                          background: 'rgba(245, 158, 11, 0.12)',
                          color: '#fbbf24',
                          border: '1px solid rgba(245, 158, 11, 0.3)',
                        }}
                      >
                        <Crown size={12} /> OWNER
                      </span>
                    ) : (
                      <span className="badge badge-draft">MEMBER</span>
                    )}
                  </div>

                  {/* Title & Slug */}
                  <h3 style={{ fontSize: '1.2rem', fontWeight: 800, color: 'white', marginBottom: '0.25rem' }}>
                    {comp.name}
                  </h3>
                  <div
                    style={{
                      fontSize: '0.8rem',
                      fontFamily: 'var(--font-mono)',
                      color: 'var(--accent-cyan)',
                      marginBottom: '0.75rem',
                    }}
                  >
                    slug: /{comp.slug}
                  </div>

                  {/* Description */}
                  <p
                    style={{
                      fontSize: '0.85rem',
                      color: 'var(--text-secondary)',
                      marginBottom: '1.25rem',
                      minHeight: '38px',
                      display: '-webkit-box',
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: 'vertical',
                      overflow: 'hidden',
                    }}
                  >
                    {comp.description || 'Chưa có mô tả thông tin doanh nghiệp.'}
                  </p>
                </div>

                {/* Card Actions */}
                <div
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '0.6rem',
                    paddingTop: '1rem',
                    borderTop: '1px solid var(--border-subtle)',
                  }}
                >
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <button
                      className={`btn btn-sm ${isSelected ? 'btn-secondary' : 'btn-primary'}`}
                      style={{ flex: 1, justifyContent: 'center' }}
                      onClick={() => onSelectCompany(comp)}
                    >
                      {isSelected ? 'Đang hoạt động' : 'Chọn Tenant Này'}
                    </button>

                    <button
                      className="btn btn-secondary btn-sm"
                      title="Danh sách nhân sự"
                      onClick={() => handleOpenMembers(comp)}
                    >
                      <Users size={14} /> Thành viên
                    </button>
                  </div>

                  {isOwner && (
                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                      {/* Step-up Ownership Transfer Button */}
                      <button
                        className="btn btn-secondary btn-sm"
                        style={{
                          flex: 1,
                          justifyContent: 'center',
                          color: '#fbbf24',
                          border: '1px solid rgba(245, 158, 11, 0.3)',
                          background: 'rgba(245, 158, 11, 0.05)',
                        }}
                        onClick={() => handleOpenTransfer(comp)}
                      >
                        <KeyRound size={13} /> Chuyển Quyền Sở Hữu (Step-up)
                      </button>

                      <button
                        className="btn btn-secondary btn-sm"
                        style={{ color: 'var(--accent-rose)' }}
                        title="Xóa công ty"
                        onClick={() => handleDeleteCompany(comp.id)}
                      >
                        <Trash2 size={13} />
                      </button>
                    </div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {/* Modal 1: Create Company */}
      {isCreateOpen && (
        <div className="modal-backdrop">
          <div className="modal-container" style={{ maxWidth: '520px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Building2 size={20} color="var(--primary-light)" />
                <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white' }}>
                  Khởi Tạo Công Ty / Tổ Chức Mới
                </h3>
              </div>
              <button
                onClick={() => setIsCreateOpen(false)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleCreateCompany}>
              <div className="modal-body">
                {createError && (
                  <div
                    style={{
                      padding: '0.75rem',
                      borderRadius: 'var(--radius-md)',
                      background: 'rgba(244, 63, 94, 0.12)',
                      border: '1px solid rgba(244, 63, 94, 0.3)',
                      color: '#fb7185',
                      fontSize: '0.825rem',
                      marginBottom: '1rem',
                    }}
                  >
                    {createError}
                  </div>
                )}

                <div className="input-group">
                  <label className="input-label">Tên công ty / Tổ chức *</label>
                  <input
                    type="text"
                    className="input-control"
                    placeholder="Ví dụ: Tập Đoàn NexaOps Toàn Cầu"
                    value={createName}
                    onChange={(e) => {
                      setCreateName(e.target.value)
                      if (!createSlug) {
                        setCreateSlug(
                          e.target.value
                            .toLowerCase()
                            .normalize('NFD')
                            .replace(/[\u0300-\u036f]/g, '')
                            .replace(/[^a-z0-9]/g, '-')
                            .replace(/-+/g, '-')
                            .replace(/^-|-$/g, '')
                        )
                      }
                    }}
                  />
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: '0.75rem' }}>
                  <div className="input-group">
                    <label className="input-label">Đường dẫn định danh (Slug)</label>
                    <input
                      type="text"
                      className="input-control"
                      placeholder="nexaops-global"
                      value={createSlug}
                      onChange={(e) => setCreateSlug(e.target.value)}
                    />
                  </div>

                  <div className="input-group">
                    <label className="input-label">Mã viết tắt (Code)</label>
                    <input
                      type="text"
                      className="input-control"
                      placeholder="NEXA-HQ"
                      value={createCode}
                      onChange={(e) => setCreateCode(e.target.value.toUpperCase())}
                    />
                  </div>
                </div>

                <div className="input-group">
                  <label className="input-label">Mô tả doanh nghiệp</label>
                  <textarea
                    className="input-control"
                    style={{ minHeight: '68px' }}
                    placeholder="Lĩnh vực hoạt động, trụ sở hoặc quy mô nhân sự..."
                    value={createDesc}
                    onChange={(e) => setCreateDesc(e.target.value)}
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsCreateOpen(false)}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={createLoading}>
                  {createLoading ? 'Đang tạo...' : 'Tạo Tổ Chức'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 2: View Members */}
      {viewingMembersCompany && (
        <div className="modal-backdrop">
          <div className="modal-container" style={{ maxWidth: '640px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Users size={20} color="var(--accent-cyan)" />
                <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white' }}>
                  Danh Sách Thành Viên: {viewingMembersCompany.name}
                </h3>
              </div>
              <button
                onClick={() => setViewingMembersCompany(null)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div className="modal-body" style={{ maxHeight: '60vh', overflowY: 'auto' }}>
              {membersLoading ? (
                <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
                  Đang tải danh sách thành viên...
                </div>
              ) : members.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)' }}>
                  Chưa có thành viên nào trong danh sách.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>
                  {members.map((m) => (
                    <div
                      key={m.id}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '0.75rem 1rem',
                        background: 'rgba(255, 255, 255, 0.02)',
                        border: '1px solid var(--border-subtle)',
                        borderRadius: 'var(--radius-md)',
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: 600, color: 'white', fontSize: '0.9rem' }}>
                          {m.user?.name || m.userName || m.email}
                        </div>
                        <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                          {m.user?.email || m.email}
                        </div>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <span
                          className={`badge ${
                            m.role.toUpperCase() === 'OWNER'
                              ? 'badge-published'
                              : m.role.toUpperCase() === 'ADMIN'
                              ? 'badge-approved'
                              : 'badge-draft'
                          }`}
                        >
                          {m.role}
                        </span>
                        <span
                          style={{
                            fontSize: '0.7rem',
                            color: m.status.toUpperCase() === 'ACTIVE' ? 'var(--accent-emerald)' : 'var(--text-muted)',
                          }}
                        >
                          {m.status}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setViewingMembersCompany(null)}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal 3: Step-up Ownership Transfer */}
      {transferCompany && (
        <div className="modal-backdrop">
          <div className="modal-container" style={{ maxWidth: '580px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <ShieldAlert size={22} color="#fbbf24" />
                <h3 style={{ fontSize: '1.1rem', fontWeight: 800, color: 'white' }}>
                  Xác Thực Bước 2 (Step-up Auth): Chuyển Quyền Sở Hữu
                </h3>
              </div>
              <button
                onClick={() => setTransferCompany(null)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleExecuteTransfer}>
              <div className="modal-body">
                {/* Critical Security Warning Banner */}
                <div
                  style={{
                    padding: '0.9rem 1rem',
                    borderRadius: 'var(--radius-md)',
                    background: 'rgba(245, 158, 11, 0.12)',
                    border: '1px solid rgba(245, 158, 11, 0.3)',
                    color: '#fbbf24',
                    fontSize: '0.825rem',
                    marginBottom: '1.25rem',
                    lineHeight: 1.5,
                  }}
                >
                  <strong>CẢNH BÁO QUAN TRỌNG:</strong> Bạn đang thực hiện chuyển giao quyền sở hữu tối cao
                  của công ty <strong>{transferCompany.name}</strong>. Sau khi hoàn tất, chủ sở hữu mới sẽ có toàn quyền
                  quản trị công ty và bạn sẽ được chuyển sang vai trò thành viên cấp thấp hơn.
                </div>

                {transferError && (
                  <div
                    style={{
                      padding: '0.75rem',
                      borderRadius: 'var(--radius-md)',
                      background: 'rgba(244, 63, 94, 0.12)',
                      border: '1px solid rgba(244, 63, 94, 0.3)',
                      color: '#fb7185',
                      fontSize: '0.825rem',
                      marginBottom: '1rem',
                    }}
                  >
                    {transferError}
                  </div>
                )}

                {transferSuccess && (
                  <div
                    style={{
                      padding: '0.75rem',
                      borderRadius: 'var(--radius-md)',
                      background: 'rgba(16, 185, 129, 0.12)',
                      border: '1px solid rgba(16, 185, 129, 0.3)',
                      color: '#34d399',
                      fontSize: '0.825rem',
                      marginBottom: '1rem',
                    }}
                  >
                    {transferSuccess}
                  </div>
                )}

                {/* Target Member Selection */}
                <div className="input-group">
                  <label className="input-label">Người tiếp nhận quyền sở hữu mới *</label>
                  {transferMembers.length === 0 ? (
                    <div style={{ color: 'var(--accent-rose)', fontSize: '0.825rem', padding: '0.5rem 0' }}>
                      Công ty chưa có thành viên Active nào khác để chuyển nhượng. Hãy mời thêm thành viên trước.
                    </div>
                  ) : (
                    <select
                      className="input-control"
                      value={transferTargetId}
                      onChange={(e) => setTransferTargetId(e.target.value)}
                    >
                      {transferMembers.map((m) => (
                        <option key={m.userId || m.id} value={m.userId || ''}>
                          {m.user?.name || m.userName || m.email} ({m.user?.email || m.email}) - {m.role}
                        </option>
                      ))}
                    </select>
                  )}
                </div>

                {/* Previous Owner New Role */}
                <div className="input-group">
                  <label className="input-label">Vai trò của bạn sau khi bàn giao</label>
                  <select
                    className="input-control"
                    value={transferRole}
                    onChange={(e) => setTransferRole(Number(e.target.value))}
                  >
                    <option value={1}>Quản Trị Viên (Admin)</option>
                    <option value={2}>Quản Lý Bộ Phận (Manager)</option>
                    <option value={3}>Thành Viên Thường (Member)</option>
                  </select>
                </div>

                {/* Step-up Password Field */}
                <div className="input-group" style={{ marginTop: '1.25rem' }}>
                  <label className="input-label" style={{ color: '#fbbf24' }}>
                    <span style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                      <KeyRound size={14} /> Mật khẩu tài khoản của bạn (Bắt buộc xác thực) *
                    </span>
                  </label>
                  <input
                    type="password"
                    className="input-control"
                    placeholder="Nhập mật khẩu hiện tại của bạn để ký duyệt..."
                    style={{ borderColor: 'rgba(245, 158, 11, 0.4)' }}
                    value={transferPassword}
                    onChange={(e) => setTransferPassword(e.target.value)}
                  />
                  <span style={{ fontSize: '0.725rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
                    Hệ thống .NET Backend sẽ xác thực mật khẩu trực tiếp qua cơ chế Step-up Authentication trước khi ghi nhận.
                  </span>
                </div>
              </div>

              <div className="modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setTransferCompany(null)}
                >
                  Hủy thao tác
                </button>
                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={transferLoading || transferMembers.length === 0}
                  style={{
                    background: 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)',
                    boxShadow: '0 4px 14px rgba(245, 158, 11, 0.3)',
                  }}
                >
                  {transferLoading ? 'Đang xác thực bảo mật...' : 'Xác Nhận Chuyển Giao Quyền Sở Hữu'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
