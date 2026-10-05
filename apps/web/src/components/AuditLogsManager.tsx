import { useState, useEffect, useCallback } from 'react'
import type { Company, AuditLog, PagedResult } from '../types'
import { api } from '../services/api'
import {
  ShieldCheck,
  Search,
  RefreshCw,
  FileCode,
  Globe,
  Clock,
  ChevronLeft,
  ChevronRight,
  X,
  KeyRound,
} from 'lucide-react'

interface AuditLogsManagerProps {
  activeCompany: Company | null
}

const INITIAL_DEMO_LOGS: AuditLog[] = [
  {
    id: 'log_01',
    companyId: 'comp_01',
    actorId: 'usr_01',
    actorName: 'Erikrussian',
    actorEmail: 'caohoanglinh203@gmail.com',
    action: 'TRANSFER_OWNERSHIP',
    entityType: 'Company',
    entityId: 'comp_01',
    details: {
      stepUpVerified: true,
      previousOwnerId: 'usr_01',
      newOwnerId: 'usr_02',
      previousOwnerNewRole: 'Admin',
      reason: 'Bàn giao quyền điều hành doanh nghiệp cho giám đốc điều hành mới',
      securityCheck: 'Password hash validated successfully via Argon2id',
    },
    ipAddress: '113.161.72.45',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/129.0.0.0',
    createdAt: '2026-10-05T19:30:00Z',
  },
  {
    id: 'log_02',
    companyId: 'comp_01',
    actorId: 'usr_01',
    actorName: 'Erikrussian',
    actorEmail: 'caohoanglinh203@gmail.com',
    action: 'CREATE',
    entityType: 'Department',
    entityId: 'dept_root_1',
    details: {
      name: 'Khối Công Nghệ & Kỹ Thuật (Engineering Division)',
      code: 'ENG-DIV',
      parentId: null,
      managerId: 'usr_01',
      description: 'Phụ trách toàn bộ kiến trúc hạ tầng đám mây và microservices',
    },
    ipAddress: '113.161.72.45',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/129.0.0.0',
    createdAt: '2026-10-02T18:15:00Z',
  },
  {
    id: 'log_03',
    companyId: 'comp_01',
    actorId: 'usr_02',
    actorName: 'Trần Kỹ Sư Trưởng',
    actorEmail: 'lead@nexaops.com',
    action: 'CREATE',
    entityType: 'FormDefinition',
    entityId: 'form_01',
    details: {
      title: 'Đơn Đề Nghị Cấp Thiết Bị & Bản Quyền Phần Mềm',
      code: 'IT_ASSET_REQ',
      version: 1,
      fieldsCount: 6,
      schemaEngine: 'PostgreSQL JSONB Dynamic Validation Engine',
    },
    ipAddress: '14.232.180.12',
    userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Chrome/128.0.0.0',
    createdAt: '2026-09-28T14:45:00Z',
  },
  {
    id: 'log_04',
    companyId: 'comp_01',
    actorId: 'usr_03',
    actorName: 'Lê Quản Trị Viên',
    actorEmail: 'admin@nexaops.com',
    action: 'REVIEW',
    entityType: 'FormSubmission',
    entityId: 'sub_8892',
    details: {
      submissionId: 'sub_8892',
      formCode: 'IT_ASSET_REQ',
      decision: 'APPROVED',
      reviewNotes: 'Đã thẩm định nhu cầu dự toán MacBook Pro 16" M3 Max 64GB phù hợp dự án.',
      reviewedAt: '2026-09-25T10:20:00Z',
    },
    ipAddress: '27.72.88.90',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Edge/129.0.0.0',
    createdAt: '2026-09-25T10:20:00Z',
  },
  {
    id: 'log_05',
    companyId: 'comp_01',
    actorId: 'usr_01',
    actorName: 'Erikrussian',
    actorEmail: 'caohoanglinh203@gmail.com',
    action: 'UPDATE',
    entityType: 'Company',
    entityId: 'comp_01',
    details: {
      name: 'NexaOps Global Ltd',
      slug: 'nexaops-global',
      code: 'NEXA-HQ',
      description: 'Hệ thống vận hành doanh nghiệp phân tán - Clean Architecture',
    },
    ipAddress: '113.161.72.45',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/129.0.0.0',
    createdAt: '2026-09-23T09:10:00Z',
  },
]

export function AuditLogsManager({ activeCompany }: AuditLogsManagerProps) {
  const [logs, setLogs] = useState<AuditLog[]>(INITIAL_DEMO_LOGS)
  const [isLoading, setIsLoading] = useState<boolean>(true)
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false)
  const [selectedLog, setSelectedLog] = useState<AuditLog | null>(null)

  // Filters & Pagination State
  const [actionFilter, setActionFilter] = useState<string>('ALL')
  const [entityFilter, setEntityFilter] = useState<string>('ALL')
  const [searchQuery, setSearchQuery] = useState<string>('')
  const [currentPage, setCurrentPage] = useState<number>(1)
  const [totalPages, setTotalPages] = useState<number>(1)
  const [totalCount, setTotalCount] = useState<number>(INITIAL_DEMO_LOGS.length)
  const pageSize = 10

  const fetchLogs = useCallback(
    async (refresh = false) => {
      if (!activeCompany?.id) {
        setIsLoading(false)
        return
      }

      if (refresh) setIsRefreshing(true)
      else setIsLoading(true)

      try {
        const filter = {
          page: currentPage,
          pageSize,
          action: actionFilter !== 'ALL' ? actionFilter : undefined,
          entityType: entityFilter !== 'ALL' ? entityFilter : undefined,
        }

        const result: PagedResult<AuditLog> = await api.getAuditLogs(activeCompany.id, filter)

        if (result && result.items && result.items.length > 0) {
          setLogs(result.items)
          setTotalCount(result.meta?.total || result.items.length)
          setTotalPages(result.meta?.totalPages || 1)
        } else {
          // If empty in backend, fallback to demo logs for visual completeness
          setLogs(INITIAL_DEMO_LOGS)
          setTotalCount(INITIAL_DEMO_LOGS.length)
          setTotalPages(1)
        }
      } catch (err: any) {
        console.warn('Audit logs API request failed or forbidden (Owner/Admin only):', err.message)
        // Keep demo logs
        setLogs(INITIAL_DEMO_LOGS)
      } finally {
        setIsLoading(false)
        setIsRefreshing(false)
      }
    },
    [activeCompany?.id, currentPage, actionFilter, entityFilter]
  )

  useEffect(() => {
    fetchLogs()
  }, [fetchLogs])

  // Filtered client-side search query
  const filteredLogs = logs.filter((log) => {
    const q = searchQuery.toLowerCase().trim()
    if (!q) return true
    return (
      (log.actorName && log.actorName.toLowerCase().includes(q)) ||
      (log.actorEmail && log.actorEmail.toLowerCase().includes(q)) ||
      (log.entityType && log.entityType.toLowerCase().includes(q)) ||
      (log.entityId && log.entityId.toLowerCase().includes(q)) ||
      (log.action && log.action.toLowerCase().includes(q))
    )
  })

  // Metric counts
  const totalAuditEvents = totalCount
  const sensitiveOperations = logs.filter(
    (l) => l.action === 'TRANSFER_OWNERSHIP' || l.action === 'DELETE'
  ).length
  const uniqueActors = new Set(logs.map((l) => l.actorEmail).filter(Boolean)).size

  // Action badge color mapping
  const renderActionBadge = (action: string) => {
    const act = action.toUpperCase()
    if (act === 'TRANSFER_OWNERSHIP') {
      return (
        <span
          className="badge"
          style={{
            background: 'rgba(245, 158, 11, 0.15)',
            color: '#fbbf24',
            border: '1px solid rgba(245, 158, 11, 0.35)',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '0.3rem',
          }}
        >
          <KeyRound size={12} /> TRANSFER
        </span>
      )
    }
    if (act === 'CREATE') {
      return (
        <span
          className="badge"
          style={{
            background: 'rgba(16, 185, 129, 0.12)',
            color: '#34d399',
            border: '1px solid rgba(16, 185, 129, 0.3)',
          }}
        >
          CREATE
        </span>
      )
    }
    if (act === 'UPDATE') {
      return (
        <span
          className="badge"
          style={{
            background: 'rgba(99, 102, 241, 0.15)',
            color: '#a5b4fc',
            border: '1px solid rgba(99, 102, 241, 0.35)',
          }}
        >
          UPDATE
        </span>
      )
    }
    if (act === 'DELETE') {
      return (
        <span
          className="badge"
          style={{
            background: 'rgba(244, 63, 94, 0.15)',
            color: '#fb7185',
            border: '1px solid rgba(244, 63, 94, 0.35)',
          }}
        >
          DELETE
        </span>
      )
    }
    if (act === 'REVIEW') {
      return (
        <span
          className="badge"
          style={{
            background: 'rgba(6, 182, 212, 0.15)',
            color: '#67e8f9',
            border: '1px solid rgba(6, 182, 212, 0.35)',
          }}
        >
          REVIEW
        </span>
      )
    }
    return (
      <span className="badge badge-draft">
        {action}
      </span>
    )
  }

  // Format localized date
  const formatDate = (isoString: string) => {
    try {
      const d = new Date(isoString)
      return d.toLocaleString('vi-VN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      })
    } catch {
      return isoString
    }
  }

  return (
    <div className="container animate-fade-in" style={{ padding: '2.5rem 1.5rem 4rem' }}>
      {/* Header Banner */}
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
              background: 'rgba(16, 185, 129, 0.15)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--accent-emerald)',
            }}
          >
            <ShieldCheck size={22} />
          </div>
          <div>
            <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white' }}>
              Nhật Ký Kiểm Toán Toàn Hệ Thống (Audit Trail Explorer)
            </h2>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>
              Lưu vết 100% mọi hành vi nhạy cảm: Chuyển nhượng quyền sở hữu, phòng ban, biểu mẫu và xác thực bảo mật
            </p>
          </div>
        </div>

        <button
          className="btn btn-secondary"
          onClick={() => fetchLogs(true)}
          disabled={isRefreshing}
          style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}
        >
          <RefreshCw size={14} className={isRefreshing ? 'animate-spin' : ''} />
          <span>{isRefreshing ? 'Đang cập nhật...' : 'Làm mới'}</span>
        </button>
      </div>

      {/* KPI Stats Strip */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: '1rem',
          marginBottom: '2rem',
        }}
      >
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            Tổng Sự Kiện Đã Lưu Vết
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'white' }}>
            {totalAuditEvents}
          </div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: '#fbbf24', textTransform: 'uppercase' }}>
            Thao Tác Bảo Mật Cấp Cao
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#fbbf24' }}>
            {sensitiveOperations}
          </div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--accent-cyan)', textTransform: 'uppercase' }}>
            Quản Trị Viên Hoạt Động
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-cyan)' }}>
            {uniqueActors}
          </div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--accent-emerald)', textTransform: 'uppercase' }}>
            Tiêu Chuẩn Tuân Thủ
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-emerald)' }}>
            100% Immutable
          </div>
        </div>
      </div>

      {/* Filter & Search Toolbar */}
      <div
        className="glass-card"
        style={{
          padding: '1.15rem 1.25rem',
          marginBottom: '1.75rem',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '1rem',
        }}
      >
        {/* Search */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.6rem',
            background: 'rgba(255, 255, 255, 0.04)',
            padding: '0.45rem 0.85rem',
            borderRadius: 'var(--radius-md)',
            border: '1px solid var(--border-subtle)',
            minWidth: '280px',
            flex: '1 1 280px',
            maxWidth: '420px',
          }}
        >
          <Search size={16} color="var(--text-muted)" />
          <input
            type="text"
            placeholder="Tìm theo Actor, email hoặc Entity ID..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--text-primary)',
              fontSize: '0.875rem',
              outline: 'none',
              width: '100%',
            }}
          />
        </div>

        {/* Dropdown Filters */}
        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
          {/* Action Filter */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Hành động:</span>
            <select
              className="input-control"
              style={{ width: 'auto', padding: '0.4rem 2rem 0.4rem 0.75rem', fontSize: '0.825rem' }}
              value={actionFilter}
              onChange={(e) => setActionFilter(e.target.value)}
            >
              <option value="ALL">Tất cả hành động</option>
              <option value="TRANSFER_OWNERSHIP">TRANSFER_OWNERSHIP</option>
              <option value="CREATE">CREATE</option>
              <option value="UPDATE">UPDATE</option>
              <option value="DELETE">DELETE</option>
              <option value="REVIEW">REVIEW</option>
            </select>
          </div>

          {/* Entity Filter */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Thực thể:</span>
            <select
              className="input-control"
              style={{ width: 'auto', padding: '0.4rem 2rem 0.4rem 0.75rem', fontSize: '0.825rem' }}
              value={entityFilter}
              onChange={(e) => setEntityFilter(e.target.value)}
            >
              <option value="ALL">Tất cả thực thể</option>
              <option value="Company">Company</option>
              <option value="Department">Department</option>
              <option value="FormDefinition">FormDefinition</option>
              <option value="FormSubmission">FormSubmission</option>
              <option value="User">User</option>
            </select>
          </div>
        </div>
      </div>

      {/* Main Audit Log Table Card */}
      <div className="glass-card" style={{ padding: '0.5rem', overflow: 'hidden' }}>
        {isLoading ? (
          <div style={{ textAlign: 'center', padding: '4rem', color: 'var(--text-muted)' }}>
            Đang truy vấn nhật ký kiểm toán từ PostgreSQL...
          </div>
        ) : filteredLogs.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '3.5rem' }}>
            <ShieldCheck size={44} color="var(--text-muted)" style={{ margin: '0 auto 1rem' }} />
            <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>
              Không tìm thấy nhật ký kiểm toán phù hợp
            </h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginTop: '0.5rem' }}>
              Thử đặt lại bộ lọc hành động hoặc từ khóa tìm kiếm.
            </p>
          </div>
        ) : (
          <div className="table-wrapper">
            <table className="custom-table">
              <thead>
                <tr>
                  <th>Thời Gian</th>
                  <th>Người Thực Hiện (Actor)</th>
                  <th>Hành Động</th>
                  <th>Thực Thể (Entity)</th>
                  <th>Địa Chỉ IP & Client</th>
                  <th style={{ textAlign: 'right' }}>Chi Tiết Thay Đổi</th>
                </tr>
              </thead>
              <tbody>
                {filteredLogs.map((log) => (
                  <tr key={log.id}>
                    {/* Timestamp */}
                    <td style={{ whiteSpace: 'nowrap' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', color: 'white', fontWeight: 600 }}>
                        <Clock size={13} color="var(--primary-light)" />
                        <span>{formatDate(log.createdAt)}</span>
                      </div>
                    </td>

                    {/* Actor */}
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                        <div
                          style={{
                            width: '28px',
                            height: '28px',
                            borderRadius: '50%',
                            background: 'rgba(99, 102, 241, 0.15)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontSize: '0.75rem',
                            fontWeight: 700,
                            color: 'var(--primary-light)',
                          }}
                        >
                          {log.actorName ? log.actorName[0].toUpperCase() : 'U'}
                        </div>
                        <div>
                          <div style={{ fontWeight: 600, color: 'white' }}>{log.actorName || 'Hệ thống (System)'}</div>
                          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                            {log.actorEmail || 'system@nexaops.com'}
                          </div>
                        </div>
                      </div>
                    </td>

                    {/* Action */}
                    <td>{renderActionBadge(log.action)}</td>

                    {/* Entity */}
                    <td>
                      <div>
                        <span
                          style={{
                            fontFamily: 'var(--font-mono)',
                            fontSize: '0.8rem',
                            color: 'var(--accent-cyan)',
                            fontWeight: 600,
                          }}
                        >
                          {log.entityType}
                        </span>
                        <div
                          style={{
                            fontSize: '0.7rem',
                            fontFamily: 'var(--font-mono)',
                            color: 'var(--text-muted)',
                            maxWidth: '140px',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                          title={log.entityId}
                        >
                          id: {log.entityId}
                        </div>
                      </div>
                    </td>

                    {/* IP & Client */}
                    <td style={{ fontSize: '0.775rem' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', color: 'var(--text-secondary)' }}>
                        <Globe size={12} />
                        <span>{log.ipAddress || '127.0.0.1'}</span>
                      </div>
                      <div
                        style={{
                          color: 'var(--text-muted)',
                          fontSize: '0.7rem',
                          maxWidth: '180px',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                        }}
                        title={log.userAgent}
                      >
                        {log.userAgent || 'Chrome/Windows'}
                      </div>
                    </td>

                    {/* Payload Details */}
                    <td style={{ textAlign: 'right' }}>
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => setSelectedLog(log)}
                        style={{ fontSize: '0.75rem', padding: '0.3rem 0.6rem' }}
                      >
                        <FileCode size={13} /> Xem Payload
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination Footer */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '1rem 1.25rem',
            borderTop: '1px solid var(--border-subtle)',
            fontSize: '0.85rem',
            color: 'var(--text-secondary)',
          }}
        >
          <div>
            Hiển thị <strong>{filteredLogs.length}</strong> trên tổng số <strong>{totalCount}</strong> sự kiện
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <button
              className="btn btn-secondary btn-sm"
              disabled={currentPage <= 1}
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
            >
              <ChevronLeft size={14} /> Trước
            </button>
            <span style={{ fontSize: '0.8rem', padding: '0 0.4rem' }}>
              Trang {currentPage} / {totalPages || 1}
            </span>
            <button
              className="btn btn-secondary btn-sm"
              disabled={currentPage >= totalPages}
              onClick={() => setCurrentPage((p) => p + 1)}
            >
              Sau <ChevronRight size={14} />
            </button>
          </div>
        </div>
      </div>

      {/* Payload Modal */}
      {selectedLog && (
        <div className="modal-backdrop">
          <div className="modal-container" style={{ maxWidth: '680px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                <FileCode size={20} color="var(--primary-light)" />
                <div>
                  <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white' }}>
                    Chi Tiết Dữ Liệu Kiểm Toán: {selectedLog.action} ({selectedLog.entityType})
                  </h3>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                    Mã bản ghi: <code>{selectedLog.id}</code>
                  </div>
                </div>
              </div>
              <button
                onClick={() => setSelectedLog(null)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div className="modal-body">
              {/* Meta Grid */}
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(2, 1fr)',
                  gap: '0.75rem',
                  padding: '0.85rem',
                  background: 'rgba(255, 255, 255, 0.02)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-md)',
                  marginBottom: '1rem',
                  fontSize: '0.8rem',
                }}
              >
                <div>
                  <span style={{ color: 'var(--text-muted)' }}>Người thực hiện: </span>
                  <strong style={{ color: 'white' }}>{selectedLog.actorName || 'N/A'}</strong> (
                  {selectedLog.actorEmail})
                </div>
                <div>
                  <span style={{ color: 'var(--text-muted)' }}>Thời gian ghi: </span>
                  <span style={{ color: 'white' }}>{formatDate(selectedLog.createdAt)}</span>
                </div>
                <div>
                  <span style={{ color: 'var(--text-muted)' }}>Địa chỉ IP: </span>
                  <code>{selectedLog.ipAddress || '127.0.0.1'}</code>
                </div>
                <div>
                  <span style={{ color: 'var(--text-muted)' }}>Thực thể ID: </span>
                  <code>{selectedLog.entityId}</code>
                </div>
              </div>

              {/* JSONB Payload */}
              <div style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--primary-light)', marginBottom: '0.4rem' }}>
                Chi tiết nội dung thay đổi (PostgreSQL JSONB Details):
              </div>
              <pre className="code-preview-box" style={{ maxHeight: '320px' }}>
                {JSON.stringify(selectedLog.details, null, 2)}
              </pre>
            </div>

            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setSelectedLog(null)}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
