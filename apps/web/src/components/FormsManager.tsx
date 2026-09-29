import { useState, useEffect } from 'react'
import type { FormDefinition, FormStatus } from '../types'
import { api } from '../services/api'
import { StatusBadge } from './StatusBadge'
import { FormBuilderModal } from './FormBuilderModal'
import { FormSubmissionModal } from './FormSubmissionModal'
import {
  Plus,
  Search,
  FileSpreadsheet,
  Layers,
  Send,
  Code2,
  Trash2,
  CheckCircle,
  Archive,
  RefreshCw,
} from 'lucide-react'

// Pre-seeded initial forms showcasing diverse field types
const INITIAL_FORMS: FormDefinition[] = [
  {
    id: 'f_01',
    companyId: 'comp_01',
    departmentName: 'Kỹ thuật & Công nghệ',
    title: 'Đơn Đề Nghị Cấp Thiết Bị & Bản Quyền Phần Mềm',
    code: 'IT_ASSET_REQ',
    description: 'Quy trình tiếp nhận đề xuất cấp Laptop M3 Max, màn hình 4K và license JetBrains/AWS cho kỹ sư công nghệ.',
    status: 1, // Published
    version: 2,
    fields: [
      { id: 'f1', name: 'employeeName', label: 'Họ tên kỹ sư đề xuất', type: 0, required: true, placeholder: 'Nguyễn Văn A' },
      { id: 'f2', name: 'corporateEmail', label: 'Email công vụ (@nexaops.com)', type: 6, required: true, placeholder: 'eng@nexaops.com' },
      { id: 'f3', name: 'hardwareType', label: 'Loại trang thiết bị', type: 3, required: true, options: ['MacBook Pro 16" M3 Max 64GB', 'ThinkPad P1 Gen 6', 'Màn hình Dell UltraSharp 32" 4K', 'AWS Credit & Cloud Sandbox'] },
      { id: 'f4', name: 'estimatedBudget', label: 'Dự toán kinh phí ($ USD)', type: 1, required: true, min: 100, max: 10000, placeholder: '3500' },
      { id: 'f5', name: 'businessJustification', label: 'Lý do & Mục đích công việc', type: 5, required: true, placeholder: 'Phục vụ triển khai tải và benchmark microservices...' },
      { id: 'f6', name: 'termsAgreed', label: 'Cam kết bảo mật tài sản công ty', type: 4, required: true },
    ],
    createdById: 'usr_01',
    createdByName: 'Trần Kỹ Sư Trưởng',
    createdAt: '2026-09-20T08:30:00Z',
    updatedAt: '2026-09-22T14:15:00Z',
  },
  {
    id: 'f_02',
    companyId: 'comp_01',
    departmentName: 'Nhân sự & Hành chính',
    title: 'Đơn Đăng Ký Nghỉ Phép & Work-From-Home',
    code: 'HR_LEAVE_WFH',
    description: 'Đăng ký ngày nghỉ phép năm (Annual Leave) hoặc làm việc từ xa kết hợp tuần linh hoạt.',
    status: 1, // Published
    version: 1,
    fields: [
      { id: 'f1', name: 'applicantName', label: 'Họ và tên nhân sự', type: 0, required: true },
      { id: 'f2', name: 'leaveType', label: 'Hình thức nghỉ / WFH', type: 3, required: true, options: ['Nghỉ phép thường niên', 'Work-From-Home (Linh hoạt)', 'Nghỉ ốm / Khám bệnh', 'Nghỉ việc riêng'] },
      { id: 'f3', name: 'startDate', label: 'Ngày bắt đầu', type: 2, required: true },
      { id: 'f4', name: 'endDate', label: 'Ngày kết thúc', type: 2, required: true },
      { id: 'f5', name: 'handoverPerson', label: 'Nhân sự nhận bàn giao công việc', type: 0, required: true, placeholder: 'Họ tên đồng nghiệp' },
      { id: 'f6', name: 'emergencyPhone', label: 'Số điện thoại khẩn cấp', type: 0, required: false, placeholder: '0901234567' },
    ],
    createdById: 'usr_02',
    createdByName: 'Lê HR Lead',
    createdAt: '2026-09-15T09:00:00Z',
    updatedAt: '2026-09-18T10:00:00Z',
  },
  {
    id: 'f_03',
    companyId: 'comp_01',
    departmentName: 'Vận hành Sản phẩm',
    title: 'Báo Cáo Sự Cố Hạ Tầng & Bảo Mật (Incident Post-Mortem)',
    code: 'OPS_INCIDENT_RPT',
    description: 'Mẫu phân tích nguyên nhân gốc rễ (RCA) sau sự cố hạ tầng sản xuất và giải pháp khắc phục.',
    status: 0, // Draft
    version: 1,
    fields: [
      { id: 'f1', name: 'incidentId', label: 'Mã số sự cố (Jira/PagerDuty)', type: 0, required: true, placeholder: 'INC-2026-09' },
      { id: 'f2', name: 'severity', label: 'Mức độ nghiêm trọng', type: 3, required: true, options: ['P0 - Blocker', 'P1 - Critical', 'P2 - Major', 'P3 - Minor'] },
      { id: 'f3', name: 'downtimeMinutes', label: 'Thời gian gián đoạn (phút)', type: 1, required: true, min: 0, max: 1440 },
      { id: 'f4', name: 'rootCause', label: 'Nguyên nhân gốc rễ kỹ thuật', type: 5, required: true },
      { id: 'f5', name: 'actionItems', label: 'Các đầu việc phòng ngừa tái diễn', type: 5, required: true },
    ],
    createdById: 'usr_01',
    createdByName: 'Trần Kỹ Sư Trưởng',
    createdAt: '2026-09-22T11:00:00Z',
    updatedAt: '2026-09-23T08:00:00Z',
  },
]

interface FormsManagerProps {
  activeCompany?: import('../types').Company | null
}

export function FormsManager({ activeCompany }: FormsManagerProps) {
  const [forms, setForms] = useState<FormDefinition[]>(INITIAL_FORMS)
  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState<'ALL' | FormStatus>('ALL')

  // Modals state
  const [isBuilderOpen, setIsBuilderOpen] = useState(false)
  const [testingForm, setTestingForm] = useState<FormDefinition | null>(null)
  const [schemaViewingForm, setSchemaViewingForm] = useState<FormDefinition | null>(null)

  // Fetch real forms when activeCompany changes
  useEffect(() => {
    if (!activeCompany?.id) return
    let isMounted = true

    api
      .getForms(activeCompany.id)
      .then((data) => {
        if (isMounted && data && data.length > 0) {
          setForms(data)
        }
      })
      .catch((err) => {
        console.warn('Could not fetch backend forms, retaining local templates:', err.message)
      })

    return () => {
      isMounted = false
    }
  }, [activeCompany?.id])

  // Filtering
  const filteredForms = forms.filter((f) => {
    const matchesSearch =
      f.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      f.code.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (f.departmentName && f.departmentName.toLowerCase().includes(searchQuery.toLowerCase()))

    const matchesStatus = statusFilter === 'ALL' || f.status === statusFilter

    return matchesSearch && matchesStatus
  })

  // Handlers
  const handleSaveNewForm = async (newFormData: {
    title: string
    code: string
    description?: string
    fields: any[]
    status: FormStatus
  }) => {
    const targetCompanyId = activeCompany?.id || 'comp_01'

    try {
      if (activeCompany?.id) {
        const created = await api.createForm(activeCompany.id, {
          title: newFormData.title,
          code: newFormData.code,
          description: newFormData.description,
          fields: newFormData.fields,
        })
        setForms([created, ...forms])
        return
      }
    } catch (err: any) {
      console.warn('Backend save form failed, saving locally:', err.message)
    }

    const newForm: FormDefinition = {
      id: `f_${Date.now()}`,
      companyId: targetCompanyId,
      title: newFormData.title,
      code: newFormData.code,
      description: newFormData.description,
      status: newFormData.status,
      version: 1,
      fields: newFormData.fields,
      departmentName: 'Kỹ thuật & Công nghệ',
      createdById: 'usr_current',
      createdByName: 'Admin NexaOps',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }

    setForms([newForm, ...forms])
  }

  const handleToggleStatus = async (formId: string) => {
    const form = forms.find((f) => f.id === formId)
    if (!form) return

    let nextStatus: FormStatus = 1
    if (form.status === 1) nextStatus = 2
    else if (form.status === 2) nextStatus = 1

    if (activeCompany?.id) {
      try {
        await api.changeFormStatus(activeCompany.id, formId, nextStatus)
      } catch (err: any) {
        console.warn('Backend status update failed, updating locally:', err.message)
      }
    }

    setForms(
      forms.map((f) => {
        if (f.id !== formId) return f
        return { ...f, status: nextStatus, updatedAt: new Date().toISOString() }
      })
    )
  }

  const handleDeleteForm = async (formId: string) => {
    if (!confirm('Bạn có chắc chắn muốn xóa biểu mẫu này khỏi hệ thống?')) {
      return
    }

    if (activeCompany?.id) {
      try {
        await api.deleteForm(activeCompany.id, formId)
      } catch (err: any) {
        console.warn('Backend delete form failed, deleting locally:', err.message)
      }
    }

    setForms(forms.filter((f) => f.id !== formId))
  }

  // Summary counts
  const totalCount = forms.length
  const publishedCount = forms.filter((f) => f.status === 1).length
  const draftCount = forms.filter((f) => f.status === 0).length

  return (
    <div className="container animate-fade-in" style={{ padding: '2.5rem 1.5rem 4rem' }}>
      {/* Top Banner / Actions */}
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
        <div>
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
              <FileSpreadsheet size={22} />
            </div>
            <div>
              <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white' }}>
                Quản Trị Biểu Mẫu Động (Dynamic Form Studio)
              </h2>
              <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>
                Thiết kế, quản lý vòng đời và kiểm thử nộp hồ sơ trực quan trên PostgreSQL JSONB
              </p>
            </div>
          </div>
        </div>

        <button className="btn btn-primary" onClick={() => setIsBuilderOpen(true)}>
          <Plus size={16} /> Thiết Kế Biểu Mẫu Mới
        </button>
      </div>

      {/* Summary KPI Strip */}
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
            Tổng Biểu Mẫu
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'white' }}>{totalCount}</div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--accent-emerald)', textTransform: 'uppercase' }}>
            Đang Xuất Bản (Active)
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-emerald)' }}>
            {publishedCount}
          </div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', textTransform: 'uppercase' }}>
            Bản Nháp (Draft)
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#94a3b8' }}>{draftCount}</div>
        </div>
      </div>

      {/* Search & Filter Toolbar */}
      <div
        className="glass-card"
        style={{
          padding: '1rem 1.25rem',
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
          }}
        >
          <Search size={16} color="var(--text-muted)" />
          <input
            type="text"
            placeholder="Tìm theo tiêu đề, mã code hoặc phòng ban..."
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

        {/* Filter Pills */}
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            className={`btn btn-sm ${statusFilter === 'ALL' ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter('ALL')}
          >
            Tất cả ({forms.length})
          </button>
          <button
            className={`btn btn-sm ${statusFilter === 1 ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter(1)}
          >
            Đã xuất bản ({publishedCount})
          </button>
          <button
            className={`btn btn-sm ${statusFilter === 0 ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter(0)}
          >
            Bản nháp ({draftCount})
          </button>
          <button
            className={`btn btn-sm ${statusFilter === 2 ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setStatusFilter(2)}
          >
            Lưu trữ ({forms.filter((f) => f.status === 2).length})
          </button>
        </div>
      </div>

      {/* Forms Grid */}
      {filteredForms.length === 0 ? (
        <div className="glass-card" style={{ padding: '3.5rem', textAlign: 'center' }}>
          <FileSpreadsheet size={48} color="var(--text-muted)" style={{ margin: '0 auto 1rem' }} />
          <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>
            Không tìm thấy biểu mẫu phù hợp
          </h3>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginTop: '0.5rem' }}>
            Thử thay đổi từ khóa tìm kiếm hoặc bấm nút "Thiết Kế Biểu Mẫu Mới" ở trên.
          </p>
        </div>
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(380px, 1fr))',
            gap: '1.5rem',
          }}
        >
          {filteredForms.map((form) => (
            <div
              key={form.id}
              className="glass-card"
              style={{
                padding: '1.5rem',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
              }}
            >
              <div>
                {/* Card Top: Code & Status */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    marginBottom: '0.85rem',
                  }}
                >
                  <span
                    style={{
                      fontFamily: 'var(--font-mono)',
                      fontSize: '0.75rem',
                      fontWeight: 700,
                      padding: '0.2rem 0.55rem',
                      borderRadius: 'var(--radius-sm)',
                      background: 'rgba(99, 102, 241, 0.15)',
                      color: 'var(--primary-light)',
                      border: '1px solid rgba(99, 102, 241, 0.3)',
                    }}
                  >
                    {form.code}
                  </span>
                  <StatusBadge status={form.status} type="form" />
                </div>

                {/* Form Title */}
                <h3
                  style={{
                    fontSize: '1.15rem',
                    fontWeight: 700,
                    color: 'white',
                    marginBottom: '0.5rem',
                    lineHeight: 1.4,
                  }}
                >
                  {form.title}
                </h3>

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
                  {form.description || 'Chưa có mô tả chi tiết.'}
                </p>

                {/* Metadata Pills */}
                <div
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    gap: '0.75rem',
                    fontSize: '0.75rem',
                    color: 'var(--text-muted)',
                    paddingTop: '0.75rem',
                    borderTop: '1px solid var(--border-subtle)',
                    marginBottom: '1.25rem',
                  }}
                >
                  <span style={{ display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
                    <Layers size={13} /> {form.fields.length} trường schema
                  </span>
                  <span>Phiên bản: v{form.version}</span>
                  {form.departmentName && (
                    <span style={{ color: 'var(--accent-cyan)' }}>
                      • {form.departmentName}
                    </span>
                  )}
                </div>
              </div>

              {/* Action Buttons */}
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '0.5rem',
                  paddingTop: '1rem',
                  borderTop: '1px solid var(--border-subtle)',
                }}
              >
                <div style={{ display: 'flex', gap: '0.4rem' }}>
                  {/* Test fill submission */}
                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => setTestingForm(form)}
                    title="Nộp thử nghiệm hoặc kiểm thử validation"
                  >
                    <Send size={13} /> Nộp Thử
                  </button>

                  {/* View JSONB schema */}
                  <button
                    className="btn btn-secondary btn-sm"
                    onClick={() => setSchemaViewingForm(form)}
                    title="Xem cấu trúc PostgreSQL JSONB"
                  >
                    <Code2 size={13} /> Schema
                  </button>
                </div>

                <div style={{ display: 'flex', gap: '0.35rem' }}>
                  {/* Status Toggle */}
                  <button
                    className="btn btn-secondary btn-sm"
                    onClick={() => handleToggleStatus(form.id)}
                    title={
                      form.status === 1
                        ? 'Chuyển sang Lưu trữ (Archive)'
                        : form.status === 0
                        ? 'Xuất bản (Publish)'
                        : 'Mở lại (Publish)'
                    }
                  >
                    {form.status === 1 ? (
                      <Archive size={13} />
                    ) : form.status === 0 ? (
                      <CheckCircle size={13} color="var(--accent-emerald)" />
                    ) : (
                      <RefreshCw size={13} />
                    )}
                  </button>

                  {/* Delete */}
                  <button
                    className="btn btn-secondary btn-sm"
                    onClick={() => handleDeleteForm(form.id)}
                    style={{ color: 'var(--accent-rose)' }}
                    title="Xóa biểu mẫu"
                  >
                    <Trash2 size={13} />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modal 1: Form Builder */}
      <FormBuilderModal
        isOpen={isBuilderOpen}
        onClose={() => setIsBuilderOpen(false)}
        onSave={handleSaveNewForm}
      />

      {/* Modal 2: Live Form Submission Test */}
      <FormSubmissionModal
        form={testingForm}
        isOpen={Boolean(testingForm)}
        onClose={() => setTestingForm(null)}
      />

      {/* Modal 3: View JSONB Schema */}
      {schemaViewingForm && (
        <div className="modal-backdrop">
          <div className="modal-container" style={{ maxWidth: '640px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Code2 size={20} color="var(--accent-cyan)" />
                <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white' }}>
                  Cấu Trúc PostgreSQL JSONB: {schemaViewingForm.code}
                </h3>
              </div>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => setSchemaViewingForm(null)}
              >
                Đóng
              </button>
            </div>
            <div className="modal-body">
              <pre className="code-preview-box">
                {JSON.stringify(
                  {
                    formCode: schemaViewingForm.code,
                    version: schemaViewingForm.version,
                    fieldsCount: schemaViewingForm.fields.length,
                    fields: schemaViewingForm.fields,
                  },
                  null,
                  2
                )}
              </pre>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
