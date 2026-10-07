import { useState, useEffect, useMemo } from 'react'
import type { FormSubmission, FormDefinition, SubmissionStatus, Company } from '../types'
import { api } from '../services/api'
import { StatusBadge } from './StatusBadge'
import {
  Inbox,
  Search,
  CheckCircle,
  XCircle,
  Clock,
  FileSpreadsheet,
  MessageSquare,
  Eye,
  RefreshCw,
  Code2,
  Check,
  X,
  FileCheck,
} from 'lucide-react'

// Initial realistic pre-seeded submissions for instant showcase & test workflows
const INITIAL_SUBMISSIONS: FormSubmission[] = [
  {
    id: 'sub_01',
    formDefinitionId: 'f_01',
    formTitle: 'Đơn Đề Nghị Cấp Thiết Bị & Bản Quyền Phần Mềm',
    formCode: 'IT_ASSET_REQ',
    companyId: 'comp_01',
    submittedById: 'usr_03',
    submittedByName: 'Đặng Tuấn Anh',
    data: {
      employeeName: 'Đặng Tuấn Anh',
      corporateEmail: 'anh.dt@nexaops.com',
      hardwareType: 'MacBook Pro 16" M3 Max 64GB',
      estimatedBudget: 3500,
      businessJustification: 'Cần nâng cấp để biên dịch microservices C# và chạy test suite tích hợp cục bộ nhanh hơn.',
      termsAgreed: true,
    },
    status: 0, // Pending
    createdAt: '2026-10-06T09:30:00Z',
    updatedAt: '2026-10-06T09:30:00Z',
  },
  {
    id: 'sub_02',
    formDefinitionId: 'f_02',
    formTitle: 'Đơn Đăng Ký Nghỉ Phép & Work-From-Home',
    formCode: 'HR_LEAVE_WFH',
    companyId: 'comp_01',
    submittedById: 'usr_04',
    submittedByName: 'Hoàng Mai Phương',
    data: {
      applicantName: 'Hoàng Mai Phương',
      leaveType: 'Work-From-Home (Linh hoạt)',
      startDate: '2026-10-12',
      endDate: '2026-10-14',
      handoverPerson: 'Nguyễn Văn Minh (Tech Lead)',
      emergencyPhone: '0912345678',
    },
    status: 1, // Approved
    reviewNotes: 'Đồng ý phê duyệt WFH 3 ngày theo chính sách hybrid của công ty.',
    reviewedById: 'usr_02',
    reviewedByName: 'Lê HR Lead',
    reviewedAt: '2026-10-06T14:20:00Z',
    createdAt: '2026-10-06T11:15:00Z',
    updatedAt: '2026-10-06T14:20:00Z',
  },
  {
    id: 'sub_03',
    formDefinitionId: 'f_01',
    formTitle: 'Đơn Đề Nghị Cấp Thiết Bị & Bản Quyền Phần Mềm',
    formCode: 'IT_ASSET_REQ',
    companyId: 'comp_01',
    submittedById: 'usr_05',
    submittedByName: 'Vũ Quốc Bảo',
    data: {
      employeeName: 'Vũ Quốc Bảo',
      corporateEmail: 'bao.vq@nexaops.com',
      hardwareType: 'AWS Credit & Cloud Sandbox',
      estimatedBudget: 8500,
      businessJustification: 'Xin cấp credit thử nghiệm huấn luyện mô hình dự đoán tải hệ thống.',
      termsAgreed: true,
    },
    status: 2, // Rejected
    reviewNotes: 'Ngân sách vượt hạn mức quý 4 ($5,000 max). Vui lòng tái cấu trúc chi phí.',
    reviewedById: 'usr_01',
    reviewedByName: 'Trần Kỹ Sư Trưởng',
    reviewedAt: '2026-10-05T16:45:00Z',
    createdAt: '2026-10-05T10:00:00Z',
    updatedAt: '2026-10-05T16:45:00Z',
  },
  {
    id: 'sub_04',
    formDefinitionId: 'f_03',
    formTitle: 'Báo Cáo Sự Cố Hạ Tầng & Bảo Mật (Incident Post-Mortem)',
    formCode: 'OPS_INCIDENT_RPT',
    companyId: 'comp_01',
    submittedById: 'usr_01',
    submittedByName: 'Trần Kỹ Sư Trưởng',
    data: {
      incidentId: 'INC-2026-10-02',
      severity: 'P1 - Critical',
      downtimeMinutes: 18,
      rootCause: 'Connection Pool cạn kiệt do spike đột biến từ worker process nền.',
      actionItems: 'Tăng max_connections PostgreSQL lên 300 và bổ sung connection pooler PgBouncer.',
    },
    status: 0, // Pending
    createdAt: '2026-10-04T15:20:00Z',
    updatedAt: '2026-10-04T15:20:00Z',
  },
]

interface FormSubmissionsInboxProps {
  activeCompany?: Company | null
  forms?: FormDefinition[]
  preselectedFormId?: string | null
  onClearFormFilter?: () => void
}

export function FormSubmissionsInbox({
  activeCompany,
  forms = [],
  preselectedFormId,
  onClearFormFilter,
}: FormSubmissionsInboxProps) {
  const [submissions, setSubmissions] = useState<FormSubmission[]>(INITIAL_SUBMISSIONS)
  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState<'ALL' | SubmissionStatus>('ALL')
  const [selectedFormFilter, setSelectedFormFilter] = useState<string>(preselectedFormId || 'ALL')
  const [isLoading, setIsLoading] = useState(false)
  const [activeReviewSubmission, setActiveReviewSubmission] = useState<FormSubmission | null>(null)

  // Review action modal state
  const [reviewNote, setReviewNote] = useState('')
  const [isSubmittingReview, setIsSubmittingReview] = useState(false)
  const [showJsonInspector, setShowJsonInspector] = useState(false)
  const [actionSuccessMsg, setActionSuccessMsg] = useState<string | null>(null)

  // Sync preselectedFormId when changed from parent
  useEffect(() => {
    if (preselectedFormId) {
      setSelectedFormFilter(preselectedFormId)
    }
  }, [preselectedFormId])

  // Fetch real submissions when activeCompany changes
  const fetchSubmissions = async () => {
    if (!activeCompany?.id) return
    setIsLoading(true)
    try {
      const data = await api.getCompanySubmissions(activeCompany.id)
      if (data && data.length > 0) {
        setSubmissions(data)
      }
    } catch (err: any) {
      console.warn('Could not fetch backend submissions, retaining seeded data:', err.message)
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    fetchSubmissions()
  }, [activeCompany?.id])

  // Filter submissions
  const filteredSubmissions = useMemo(() => {
    return submissions.filter((sub) => {
      // Status filter
      if (statusFilter !== 'ALL' && sub.status !== statusFilter) {
        return false
      }

      // Form filter
      if (selectedFormFilter !== 'ALL' && sub.formDefinitionId !== selectedFormFilter) {
        return false
      }

      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase()
        const matchTitle = sub.formTitle?.toLowerCase().includes(query)
        const matchCode = sub.formCode?.toLowerCase().includes(query)
        const matchSubmitter = sub.submittedByName?.toLowerCase().includes(query)
        const matchNotes = sub.reviewNotes?.toLowerCase().includes(query)
        return matchTitle || matchCode || matchSubmitter || matchNotes
      }

      return true
    })
  }, [submissions, statusFilter, selectedFormFilter, searchQuery])

  // KPI Metrics calculation
  const metrics = useMemo(() => {
    const total = submissions.length
    const pending = submissions.filter((s) => s.status === 0).length
    const approved = submissions.filter((s) => s.status === 1).length
    const rejected = submissions.filter((s) => s.status === 2).length
    const approvalRate = total > 0 ? Math.round((approved / (approved + rejected || 1)) * 100) : 100

    return { total, pending, approved, rejected, approvalRate }
  }, [submissions])

  // Open review modal
  const handleOpenReview = (submission: FormSubmission) => {
    setActiveReviewSubmission(submission)
    setReviewNote(submission.reviewNotes || '')
    setShowJsonInspector(false)
    setActionSuccessMsg(null)
  }

  // Handle Review action (Approve or Reject)
  const handleProcessReview = async (newStatus: SubmissionStatus) => {
    if (!activeReviewSubmission) return
    setIsSubmittingReview(true)
    setActionSuccessMsg(null)

    try {
      if (activeCompany?.id) {
        await api.reviewSubmission(
          activeCompany.id,
          activeReviewSubmission.id,
          newStatus,
          reviewNote.trim() || undefined
        )
      }

      // Update local state seamlessly
      const updated: FormSubmission = {
        ...activeReviewSubmission,
        status: newStatus,
        reviewNotes: reviewNote.trim() || undefined,
        reviewedByName: 'Quản Trị Viên (Bạn)',
        reviewedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      }

      setSubmissions((prev) => prev.map((s) => (s.id === updated.id ? updated : s)))
      setActiveReviewSubmission(updated)
      setActionSuccessMsg(
        newStatus === 1
          ? 'Bài nộp đã được phê duyệt thành công.'
          : newStatus === 2
          ? 'Đã từ chối bài nộp với lý do được ghi nhận.'
          : 'Đã chuyển trạng thái bài nộp về Chờ xử lý.'
      )

      setTimeout(() => {
        setActionSuccessMsg(null)
      }, 3500)
    } catch (err: any) {
      console.error('Review failed:', err)
      // Optimistic local update fallback
      const updated: FormSubmission = {
        ...activeReviewSubmission,
        status: newStatus,
        reviewNotes: reviewNote.trim() || undefined,
        reviewedByName: 'Quản Trị Viên (Bạn)',
        reviewedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      }
      setSubmissions((prev) => prev.map((s) => (s.id === updated.id ? updated : s)))
      setActiveReviewSubmission(updated)
    } finally {
      setIsSubmittingReview(false)
    }
  }

  // Helper to format date
  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '-'
    const d = new Date(dateStr)
    return d.toLocaleString('vi-VN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    })
  }

  // Find matching form definition for field labels
  const activeFormDef = useMemo(() => {
    if (!activeReviewSubmission) return null
    return forms.find((f) => f.id === activeReviewSubmission.formDefinitionId) || null
  }, [activeReviewSubmission, forms])

  return (
    <div className="submissions-inbox-container animate-fade-in">
      {/* KPI Summary Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: '1rem',
          marginBottom: '1.75rem',
        }}
      >
        <div className="glass-card" style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Tổng Bài Nộp</span>
            <Inbox size={18} color="var(--primary-light)" />
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'white' }}>{metrics.total}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
            Tất cả phản hồi nhận được
          </div>
        </div>

        <div
          className="glass-card"
          style={{
            padding: '1.25rem',
            border: metrics.pending > 0 ? '1px solid rgba(245, 158, 11, 0.4)' : undefined,
            boxShadow: metrics.pending > 0 ? '0 0 20px rgba(245, 158, 11, 0.1)' : undefined,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Chờ Phê Duyệt</span>
            <Clock size={18} color="var(--accent-amber)" />
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-amber)' }}>
            {metrics.pending}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
            Cần thẩm định & phản hồi
          </div>
        </div>

        <div className="glass-card" style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Đã Phê Duyệt</span>
            <CheckCircle size={18} color="var(--accent-emerald)" />
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-emerald)' }}>
            {metrics.approved}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
            Chấp thuận hợp lệ
          </div>
        </div>

        <div className="glass-card" style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Đã Từ Chối</span>
            <XCircle size={18} color="var(--accent-rose)" />
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-rose)' }}>
            {metrics.rejected}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
            Yêu cầu làm rõ hoặc hủy
          </div>
        </div>

        <div className="glass-card" style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Tỷ Lệ Chấp Thuận</span>
            <FileCheck size={18} color="var(--accent-cyan)" />
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-cyan)' }}>
            {metrics.approvalRate}%
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
            Hiệu suất duyệt hồ sơ
          </div>
        </div>
      </div>

      {/* Filter & Toolbar */}
      <div
        className="glass-card"
        style={{
          padding: '1.25rem',
          marginBottom: '1.5rem',
          display: 'flex',
          flexWrap: 'wrap',
          gap: '1rem',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        {/* Search */}
        <div style={{ position: 'relative', flex: '1 1 240px', minWidth: '220px' }}>
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
            className="input-field"
            placeholder="Tìm theo người nộp, tên biểu mẫu hoặc mã..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{ paddingLeft: '2.5rem', width: '100%' }}
          />
        </div>

        {/* Form Selector Dropdown */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', minWidth: '220px', flex: '1 1 200px' }}>
          <FileSpreadsheet size={16} color="var(--text-muted)" />
          <select
            className="input-field"
            value={selectedFormFilter}
            onChange={(e) => setSelectedFormFilter(e.target.value)}
            style={{ flex: 1, padding: '0.55rem 0.85rem' }}
          >
            <option value="ALL">Tất cả mẫu biểu mẫu ({forms.length})</option>
            {forms.map((f) => (
              <option key={f.id} value={f.id}>
                {f.title} ({f.code})
              </option>
            ))}
          </select>
          {selectedFormFilter !== 'ALL' && (
            <button
              className="btn btn-secondary btn-sm"
              title="Xóa lọc theo mẫu này"
              onClick={() => {
                setSelectedFormFilter('ALL')
                if (onClearFormFilter) onClearFormFilter()
              }}
              style={{ padding: '0.55rem' }}
            >
              <X size={14} />
            </button>
          )}
        </div>

        {/* Status Filter Buttons */}
        <div style={{ display: 'flex', gap: '0.35rem', flexWrap: 'wrap', alignItems: 'center' }}>
          <button
            className={`btn btn-sm ${statusFilter === 'ALL' ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter('ALL')}
          >
            Tất cả ({submissions.length})
          </button>
          <button
            className={`btn btn-sm ${statusFilter === 0 ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter(0)}
            style={{
              borderColor: statusFilter === 0 ? undefined : 'rgba(245, 158, 11, 0.3)',
              color: statusFilter === 0 ? undefined : 'var(--accent-amber)',
            }}
          >
            <Clock size={13} /> Chờ duyệt ({metrics.pending})
          </button>
          <button
            className={`btn btn-sm ${statusFilter === 1 ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter(1)}
            style={{
              borderColor: statusFilter === 1 ? undefined : 'rgba(16, 185, 129, 0.3)',
              color: statusFilter === 1 ? undefined : 'var(--accent-emerald)',
            }}
          >
            <CheckCircle size={13} /> Đã duyệt ({metrics.approved})
          </button>
          <button
            className={`btn btn-sm ${statusFilter === 2 ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter(2)}
            style={{
              borderColor: statusFilter === 2 ? undefined : 'rgba(244, 63, 94, 0.3)',
              color: statusFilter === 2 ? undefined : 'var(--accent-rose)',
            }}
          >
            <XCircle size={13} /> Từ chối ({metrics.rejected})
          </button>
          <button
            className="btn btn-secondary btn-sm"
            onClick={fetchSubmissions}
            disabled={isLoading}
            title="Làm mới dữ liệu từ máy chủ"
            style={{ padding: '0.55rem' }}
          >
            <RefreshCw size={14} className={isLoading ? 'spin' : ''} />
          </button>
        </div>
      </div>

      {/* Submissions List / Table */}
      {filteredSubmissions.length === 0 ? (
        <div className="glass-card" style={{ padding: '3.5rem 1.5rem', textAlign: 'center' }}>
          <Inbox size={44} color="var(--text-muted)" style={{ margin: '0 auto 1rem' }} />
          <h4 style={{ color: 'white', fontWeight: 700, marginBottom: '0.5rem' }}>
            Không tìm thấy bài nộp nào
          </h4>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', maxWidth: '440px', margin: '0 auto' }}>
            {searchQuery || statusFilter !== 'ALL' || selectedFormFilter !== 'ALL'
              ? 'Thử điều chỉnh bộ lọc tìm kiếm hoặc trạng thái phê duyệt để tìm kết quả phù hợp.'
              : 'Chưa có thành viên nào gửi phản hồi cho biểu mẫu trong tổ chức.'}
          </p>
        </div>
      ) : (
        <div className="glass-card" style={{ overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Người nộp đơn</th>
                  <th>Biểu mẫu liên quan</th>
                  <th>Thời gian gửi</th>
                  <th>Trạng thái</th>
                  <th>Thông tin thẩm định</th>
                  <th style={{ textAlign: 'right' }}>Hành động</th>
                </tr>
              </thead>
              <tbody>
                {filteredSubmissions.map((sub) => (
                  <tr key={sub.id} className="table-row-hover">
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                          <div
                            style={{
                              width: '34px',
                              height: '34px',
                              borderRadius: '50%',
                              background: 'linear-gradient(135deg, var(--primary) 0%, var(--accent-cyan) 100%)',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              color: 'white',
                              fontWeight: 700,
                              fontSize: '0.8rem',
                              flexShrink: 0,
                            }}
                          >
                            {(sub.submittedByName || 'N')[0]?.toUpperCase()}
                          </div>
                          <div>
                            <div style={{ fontWeight: 600, color: 'white', fontSize: '0.875rem' }}>
                              {sub.submittedByName || 'Nhân sự nội bộ'}
                            </div>
                            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                              {(sub.data as any)?.corporateEmail || sub.submittedById || 'ID: ' + sub.id.slice(0, 8)}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td>
                        <div>
                          <div style={{ fontWeight: 600, color: 'var(--text-primary)', fontSize: '0.875rem' }}>
                            {sub.formTitle}
                          </div>
                          <div
                            style={{
                              fontFamily: 'var(--font-mono)',
                              fontSize: '0.72rem',
                              color: 'var(--primary-light)',
                              display: 'inline-block',
                              marginTop: '0.2rem',
                            }}
                          >
                            {sub.formCode}
                          </div>
                        </div>
                      </td>
                      <td>
                        <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                          {formatDate(sub.createdAt)}
                        </div>
                      </td>
                      <td>
                        <StatusBadge type="submission" status={sub.status} />
                      </td>
                      <td>
                        {sub.status === 0 ? (
                          <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)', fontStyle: 'italic' }}>
                            Chưa thẩm định
                          </span>
                        ) : (
                          <div>
                            <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                              Bởi: <strong style={{ color: 'white' }}>{sub.reviewedByName || 'Quản lý'}</strong>
                            </div>
                            {sub.reviewNotes && (
                              <div
                                style={{
                                  fontSize: '0.75rem',
                                  color: 'var(--text-muted)',
                                  maxWidth: '220px',
                                  whiteSpace: 'nowrap',
                                  overflow: 'hidden',
                                  textOverflow: 'ellipsis',
                                }}
                                title={sub.reviewNotes}
                              >
                                "{sub.reviewNotes}"
                              </div>
                            )}
                          </div>
                        )}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleOpenReview(sub)}
                          style={{
                            background:
                              sub.status === 0 ? 'rgba(99, 102, 241, 0.15)' : 'rgba(255, 255, 255, 0.05)',
                            borderColor:
                              sub.status === 0 ? 'rgba(99, 102, 241, 0.4)' : 'var(--border-subtle)',
                            color: sub.status === 0 ? 'white' : 'var(--text-secondary)',
                          }}
                        >
                          <Eye size={14} />
                          {sub.status === 0 ? 'Thẩm định' : 'Chi tiết'}
                        </button>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Review & Detail Modal */}
      {activeReviewSubmission && (
        <div className="modal-backdrop" style={{ zIndex: 1050 }}>
          <div
            className="modal-container animate-fade-in"
            style={{ maxWidth: '780px', width: '92%', maxHeight: '90vh', display: 'flex', flexDirection: 'column' }}
          >
            {/* Modal Header */}
            <div className="modal-header">
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.65rem', marginBottom: '0.25rem' }}>
                  <h3 style={{ fontSize: '1.2rem', fontWeight: 800, color: 'white' }}>
                    Thẩm Định & Duyệt Bài Nộp
                  </h3>
                  <StatusBadge type="submission" status={activeReviewSubmission.status} />
                </div>
                <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                  Biểu mẫu: <strong style={{ color: 'var(--primary-light)' }}>{activeReviewSubmission.formTitle}</strong> (
                  {activeReviewSubmission.formCode})
                </div>
              </div>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => setActiveReviewSubmission(null)}
                style={{ padding: '0.4rem' }}
              >
                <X size={18} />
              </button>
            </div>

            {/* Modal Body */}
            <div className="modal-body" style={{ flex: 1, overflowY: 'auto', padding: '1.5rem' }}>
              {actionSuccessMsg && (
                <div
                  style={{
                    background: 'rgba(16, 185, 129, 0.15)',
                    border: '1px solid rgba(16, 185, 129, 0.4)',
                    padding: '0.75rem 1rem',
                    borderRadius: 'var(--radius-md)',
                    color: '#6ee7b7',
                    fontSize: '0.85rem',
                    marginBottom: '1.25rem',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                  }}
                >
                  <CheckCircle size={16} />
                  {actionSuccessMsg}
                </div>
              )}

              {/* Submission Meta Grid */}
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                  gap: '0.85rem',
                  background: 'rgba(255, 255, 255, 0.02)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-md)',
                  padding: '1rem',
                  marginBottom: '1.5rem',
                }}
              >
                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Người gửi yêu cầu</div>
                  <div style={{ fontWeight: 600, color: 'white', marginTop: '0.15rem' }}>
                    {activeReviewSubmission.submittedByName || 'Nhân sự nội bộ'}
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Thời gian gửi</div>
                  <div style={{ color: 'var(--text-primary)', marginTop: '0.15rem', fontSize: '0.85rem' }}>
                    {formatDate(activeReviewSubmission.createdAt)}
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Mã số bài nộp</div>
                  <div
                    style={{
                      fontFamily: 'var(--font-mono)',
                      fontSize: '0.75rem',
                      color: 'var(--primary-light)',
                      marginTop: '0.15rem',
                    }}
                  >
                    {activeReviewSubmission.id}
                  </div>
                </div>
                {activeReviewSubmission.reviewedAt && (
                  <div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Thời gian thẩm định</div>
                    <div style={{ color: 'var(--text-primary)', marginTop: '0.15rem', fontSize: '0.85rem' }}>
                      {formatDate(activeReviewSubmission.reviewedAt)} bởi{' '}
                      <strong>{activeReviewSubmission.reviewedByName}</strong>
                    </div>
                  </div>
                )}
              </div>

              {/* Dynamic Field Values */}
              <div style={{ marginBottom: '1.5rem' }}>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    marginBottom: '0.85rem',
                  }}
                >
                  <h4 style={{ fontSize: '0.95rem', fontWeight: 700, color: 'white' }}>
                    Nội Dung Dữ Liệu Bài Nộp (PostgreSQL JSONB)
                  </h4>
                  <button
                    className="btn btn-secondary btn-sm"
                    onClick={() => setShowJsonInspector(!showJsonInspector)}
                    style={{ fontSize: '0.75rem', padding: '0.35rem 0.65rem' }}
                  >
                    <Code2 size={13} /> {showJsonInspector ? 'Xem Dạng Bảng' : 'Xem JSON Thô'}
                  </button>
                </div>

                {showJsonInspector ? (
                  <pre className="code-preview-box" style={{ maxHeight: '280px', overflowY: 'auto' }}>
                    {JSON.stringify(activeReviewSubmission.data, null, 2)}
                  </pre>
                ) : (
                  <div
                    style={{
                      border: '1px solid var(--border-subtle)',
                      borderRadius: 'var(--radius-md)',
                      overflow: 'hidden',
                      background: 'rgba(255, 255, 255, 0.01)',
                    }}
                  >
                    <table className="data-table" style={{ margin: 0 }}>
                      <thead>
                        <tr>
                          <th style={{ width: '38%' }}>Trường thông tin</th>
                          <th>Giá trị phản hồi</th>
                        </tr>
                      </thead>
                      <tbody>
                        {Object.entries(activeReviewSubmission.data || {}).map(([key, val]) => {
                          const matchedField = activeFormDef?.fields?.find((f) => f.name === key)
                          const label = matchedField?.label || key

                          let displayVal: React.ReactNode = String(val)
                          if (val === true) {
                            displayVal = (
                              <span style={{ color: 'var(--accent-emerald)', display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                                <Check size={14} /> Đồng ý / Cam kết
                              </span>
                            )
                          } else if (val === false) {
                            displayVal = <span style={{ color: 'var(--accent-rose)' }}>Không đồng ý</span>
                          } else if (typeof val === 'number') {
                            displayVal = <strong style={{ color: 'var(--accent-cyan)' }}>{val.toLocaleString()}</strong>
                          } else if (matchedField?.type === 6 || (typeof val === 'string' && val.includes('@'))) {
                            displayVal = <a href={`mailto:${val}`} style={{ color: 'var(--primary-light)' }}>{String(val)}</a>
                          }

                          return (
                            <tr key={key}>
                              <td>
                                <div style={{ fontWeight: 600, color: 'white', fontSize: '0.85rem' }}>{label}</div>
                                <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>
                                  {key}
                                </div>
                              </td>
                              <td style={{ fontSize: '0.875rem', color: 'var(--text-primary)', wordBreak: 'break-word' }}>
                                {displayVal}
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* Review Decision & Notes Section */}
              <div
                style={{
                  borderTop: '1px solid var(--border-subtle)',
                  paddingTop: '1.25rem',
                  marginTop: '1.25rem',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
                  <MessageSquare size={16} color="var(--primary-light)" />
                  <h4 style={{ fontSize: '0.95rem', fontWeight: 700, color: 'white' }}>
                    Quyết Định Thẩm Định Của Quản Lý
                  </h4>
                </div>

                <div style={{ marginBottom: '1rem' }}>
                  <label
                    style={{
                      display: 'block',
                      fontSize: '0.8rem',
                      color: 'var(--text-secondary)',
                      marginBottom: '0.4rem',
                    }}
                  >
                    Ý kiến phê duyệt / Lý do từ chối (Ghi chú phản hồi cho người nộp)
                  </label>
                  <textarea
                    className="input-field"
                    rows={3}
                    placeholder="Nhập ghi chú phản hồi (ví dụ: Đồng ý cấp thiết bị, hoặc Vui lòng chỉnh sửa ngân sách...)"
                    value={reviewNote}
                    onChange={(e) => setReviewNote(e.target.value)}
                    style={{ width: '100%', resize: 'vertical' }}
                  />
                </div>

                {/* Action Buttons */}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', justifyContent: 'flex-end' }}>
                  {activeReviewSubmission.status !== 0 && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => handleProcessReview(0)}
                      disabled={isSubmittingReview}
                      title="Chuyển lại về trạng thái chờ xử lý"
                    >
                      <Clock size={14} /> Chuyển về Chờ Duyệt
                    </button>
                  )}

                  <button
                    className="btn btn-sm"
                    onClick={() => handleProcessReview(2)}
                    disabled={isSubmittingReview}
                    style={{
                      background: 'rgba(244, 63, 94, 0.15)',
                      border: '1px solid rgba(244, 63, 94, 0.4)',
                      color: '#f87171',
                    }}
                  >
                    <XCircle size={15} /> Từ Chối Bài Nộp
                  </button>

                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => handleProcessReview(1)}
                    disabled={isSubmittingReview}
                    style={{
                      background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                      boxShadow: '0 4px 14px rgba(16, 185, 129, 0.35)',
                    }}
                  >
                    <CheckCircle size={15} /> Phê Duyệt Bài Nộp
                  </button>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setActiveReviewSubmission(null)}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
