import { useState } from 'react'
import type { FormField, FormFieldType, FormStatus } from '../types'
import { Plus, Trash2, ArrowUp, ArrowDown, Eye, Code, CheckCircle, X, Sparkles } from 'lucide-react'

interface FormBuilderModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (form: {
    title: string
    code: string
    description?: string
    fields: FormField[]
    status: FormStatus
  }) => void
}

const FIELD_TYPE_LABELS: Record<FormFieldType, string> = {
  0: 'Văn bản ngắn (Text)',
  1: 'Số nguyên / thực (Number)',
  2: 'Ngày tháng (Date)',
  3: 'Danh sách lựa chọn (Select)',
  4: 'Hộp kiểm (Checkbox)',
  5: 'Đoạn văn bản dài (Textarea)',
  6: 'Thư điện tử (Email)',
}

export function FormBuilderModal({ isOpen, onClose, onSave }: FormBuilderModalProps) {
  const [title, setTitle] = useState('')
  const [code, setCode] = useState('')
  const [description, setDescription] = useState('')
  const [activePreviewTab, setActivePreviewTab] = useState<'preview' | 'json'>('preview')

  const [fields, setFields] = useState<FormField[]>([
    {
      id: 'f_name',
      name: 'fullName',
      label: 'Họ và tên nhân sự',
      type: 0,
      required: true,
      placeholder: 'Nguyễn Văn A',
    },
    {
      id: 'f_email',
      name: 'email',
      label: 'Email doanh nghiệp',
      type: 6,
      required: true,
      placeholder: 'user@nexaops.com',
    },
    {
      id: 'f_dept',
      name: 'department',
      label: 'Phòng ban công tác',
      type: 3,
      required: true,
      options: ['Kỹ thuật & DevOps', 'Sản phẩm (Product)', 'Kinh doanh & Sales', 'Nhân sự & HC'],
    },
  ])

  // Live test preview state
  const [previewValues, setPreviewValues] = useState<Record<string, any>>({})

  if (!isOpen) return null

  const handleAddField = () => {
    const newIndex = fields.length + 1
    const newField: FormField = {
      id: `f_${Date.now()}`,
      name: `field_${newIndex}`,
      label: `Trường dữ liệu ${newIndex}`,
      type: 0,
      required: false,
      placeholder: 'Nhập thông tin...',
    }
    setFields([...fields, newField])
  }

  const handleRemoveField = (id: string) => {
    setFields(fields.filter((f) => f.id !== id))
  }

  const handleMoveField = (index: number, direction: 'up' | 'down') => {
    const targetIndex = direction === 'up' ? index - 1 : index + 1
    if (targetIndex < 0 || targetIndex >= fields.length) return
    const reordered = [...fields]
    const temp = reordered[index]
    reordered[index] = reordered[targetIndex]
    reordered[targetIndex] = temp
    setFields(reordered)
  }

  const handleUpdateField = (id: string, updates: Partial<FormField>) => {
    setFields(
      fields.map((f) => {
        if (f.id !== id) return f
        const updated = { ...f, ...updates }
        // Auto update name slug if label changes and name matches old label slug
        if (updates.label && (!f.name || f.name.startsWith('field_'))) {
          updated.name = updates.label
            .toLowerCase()
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/[^a-z0-9]/g, '_')
            .replace(/_+/g, '_')
            .replace(/^_|_$/g, '')
        }
        return updated
      })
    )
  }

  const handleTitleChange = (val: string) => {
    setTitle(val)
    if (!code) {
      setCode(
        val
          .toUpperCase()
          .normalize('NFD')
          .replace(/[\u0300-\u036f]/g, '')
          .replace(/[^A-Z0-9]/g, '_')
          .slice(0, 16)
      )
    }
  }

  const handleSubmit = (status: FormStatus) => {
    if (!title.trim() || !code.trim()) {
      alert('Vui lòng nhập Tiêu đề và Mã code biểu mẫu')
      return
    }
    if (fields.length === 0) {
      alert('Vui lòng thêm ít nhất một trường dữ liệu')
      return
    }

    onSave({
      title: title.trim(),
      code: code.trim().toUpperCase(),
      description: description.trim() || undefined,
      fields,
      status,
    })
    onClose()
  }

  return (
    <div className="modal-backdrop">
      <div className="modal-container" style={{ maxWidth: '1100px', height: '88vh' }}>
        {/* Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div
              style={{
                width: '36px',
                height: '36px',
                borderRadius: 'var(--radius-md)',
                background: 'linear-gradient(135deg, var(--primary) 0%, var(--accent-purple) 100%)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'white',
              }}
            >
              <Sparkles size={18} />
            </div>
            <div>
              <h3 style={{ fontSize: '1.15rem', fontWeight: 700, color: 'white' }}>
                Trình Thiết Kế Biểu Mẫu Động (Visual Form Builder)
              </h3>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                Định nghĩa schema linh hoạt lưu trữ PostgreSQL JSONB với động cơ Dynamic Validation Engine
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--text-muted)',
              cursor: 'pointer',
              padding: '0.4rem',
            }}
          >
            <X size={20} />
          </button>
        </div>

        {/* Body Split */}
        <div className="modal-body" style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: '1.5rem', padding: '1.5rem' }}>
          {/* Left: Builder Editor */}
          <div style={{ overflowY: 'auto', paddingRight: '0.5rem' }}>
            <h4 style={{ fontSize: '0.9rem', fontWeight: 700, color: 'var(--primary-light)', marginBottom: '1rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              1. Thông Tin Biểu Mẫu
            </h4>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '0.75rem' }}>
              <div className="input-group">
                <label className="input-label">Tiêu đề biểu mẫu *</label>
                <input
                  type="text"
                  className="input-control"
                  placeholder="Ví dụ: Đơn Đăng Ký Cấp Thiết Bị Mới"
                  value={title}
                  onChange={(e) => handleTitleChange(e.target.value)}
                />
              </div>

              <div className="input-group">
                <label className="input-label">Mã Code *</label>
                <input
                  type="text"
                  className="input-control"
                  placeholder="HR_DEVICE_01"
                  value={code}
                  onChange={(e) => setCode(e.target.value.toUpperCase())}
                />
              </div>
            </div>

            <div className="input-group">
              <label className="input-label">Mô tả mục đích sử dụng</label>
              <textarea
                className="input-control"
                style={{ minHeight: '60px' }}
                placeholder="Mô tả tóm tắt quy trình tiếp nhận và xử lý biểu mẫu này..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>

            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', margin: '1.5rem 0 0.75rem' }}>
              <h4 style={{ fontSize: '0.9rem', fontWeight: 700, color: 'var(--accent-cyan)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                2. Danh Sách Trường Dữ Liệu ({fields.length})
              </h4>
              <button className="btn btn-secondary btn-sm" onClick={handleAddField}>
                <Plus size={14} /> Thêm trường
              </button>
            </div>

            {/* Field Items */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {fields.map((field, idx) => (
                <div key={field.id} className="field-builder-card">
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      <span
                        style={{
                          fontSize: '0.75rem',
                          fontFamily: 'var(--font-mono)',
                          background: 'rgba(99, 102, 241, 0.15)',
                          color: 'var(--primary-light)',
                          padding: '0.15rem 0.45rem',
                          borderRadius: '4px',
                        }}
                      >
                        #{idx + 1}
                      </span>
                      <strong style={{ fontSize: '0.85rem', color: 'white' }}>
                        {field.label || 'Chưa đặt tên'}
                      </strong>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                      <button
                        className="btn btn-secondary btn-sm"
                        disabled={idx === 0}
                        onClick={() => handleMoveField(idx, 'up')}
                        style={{ padding: '0.2rem 0.4rem', opacity: idx === 0 ? 0.3 : 1 }}
                      >
                        <ArrowUp size={13} />
                      </button>
                      <button
                        className="btn btn-secondary btn-sm"
                        disabled={idx === fields.length - 1}
                        onClick={() => handleMoveField(idx, 'down')}
                        style={{ padding: '0.2rem 0.4rem', opacity: idx === fields.length - 1 ? 0.3 : 1 }}
                      >
                        <ArrowDown size={13} />
                      </button>
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => handleRemoveField(field.id)}
                        style={{ padding: '0.2rem 0.4rem', color: 'var(--accent-rose)' }}
                      >
                        <Trash2 size={13} />
                      </button>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr', gap: '0.5rem', marginBottom: '0.5rem' }}>
                    <div>
                      <input
                        type="text"
                        className="input-control"
                        placeholder="Nhãn hiển thị (Label)"
                        value={field.label}
                        onChange={(e) => handleUpdateField(field.id, { label: e.target.value })}
                      />
                    </div>
                    <div>
                      <select
                        className="input-control"
                        value={field.type}
                        onChange={(e) =>
                          handleUpdateField(field.id, { type: Number(e.target.value) as FormFieldType })
                        }
                      >
                        {Object.entries(FIELD_TYPE_LABELS).map(([k, v]) => (
                          <option key={k} value={k}>
                            {v}
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', marginBottom: '0.5rem' }}>
                    <input
                      type="text"
                      className="input-control"
                      placeholder="Mã định danh (Key name)"
                      value={field.name}
                      onChange={(e) => handleUpdateField(field.id, { name: e.target.value })}
                    />
                    <input
                      type="text"
                      className="input-control"
                      placeholder="Văn bản gợi ý (Placeholder)"
                      value={field.placeholder || ''}
                      onChange={(e) => handleUpdateField(field.id, { placeholder: e.target.value })}
                    />
                  </div>

                  {/* Options for Select */}
                  {field.type === 3 && (
                    <div style={{ marginBottom: '0.5rem' }}>
                      <input
                        type="text"
                        className="input-control"
                        placeholder="Các lựa chọn (phân cách bằng dấu phẩy): Lựa chọn A, Lựa chọn B, Lựa chọn C"
                        value={(field.options || []).join(', ')}
                        onChange={(e) =>
                          handleUpdateField(field.id, {
                            options: e.target.value
                              .split(',')
                              .map((o) => o.trim())
                              .filter(Boolean),
                          })
                        }
                      />
                    </div>
                  )}

                  {/* Number min/max */}
                  {field.type === 1 && (
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', marginBottom: '0.5rem' }}>
                      <input
                        type="number"
                        className="input-control"
                        placeholder="Giá trị nhỏ nhất (Min)"
                        value={field.min ?? ''}
                        onChange={(e) =>
                          handleUpdateField(field.id, {
                            min: e.target.value === '' ? undefined : Number(e.target.value),
                          })
                        }
                      />
                      <input
                        type="number"
                        className="input-control"
                        placeholder="Giá trị lớn nhất (Max)"
                        value={field.max ?? ''}
                        onChange={(e) =>
                          handleUpdateField(field.id, {
                            max: e.target.value === '' ? undefined : Number(e.target.value),
                          })
                        }
                      />
                    </div>
                  )}

                  {/* Required Switch */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-start' }}>
                    <label className="switch-wrapper">
                      <input
                        type="checkbox"
                        className="switch-checkbox"
                        checked={field.required}
                        onChange={(e) => handleUpdateField(field.id, { required: e.target.checked })}
                      />
                      <span>Bắt buộc nhập dữ liệu (Required)</span>
                    </label>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Right: Live Preview & JSONB Schema Preview */}
          <div
            style={{
              background: 'rgba(0, 0, 0, 0.25)',
              border: '1px solid var(--border-subtle)',
              borderRadius: 'var(--radius-md)',
              display: 'flex',
              flexDirection: 'column',
              overflow: 'hidden',
            }}
          >
            {/* Tab switch */}
            <div
              style={{
                display: 'flex',
                borderBottom: '1px solid var(--border-subtle)',
                background: 'rgba(255, 255, 255, 0.02)',
              }}
            >
              <button
                onClick={() => setActivePreviewTab('preview')}
                style={{
                  flex: 1,
                  padding: '0.75rem',
                  background: 'none',
                  border: 'none',
                  borderBottom: activePreviewTab === 'preview' ? '2px solid var(--primary)' : 'none',
                  color: activePreviewTab === 'preview' ? 'white' : 'var(--text-secondary)',
                  fontWeight: 600,
                  fontSize: '0.85rem',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '0.4rem',
                  cursor: 'pointer',
                }}
              >
                <Eye size={15} /> Xem Trước Giao Diện Thực
              </button>
              <button
                onClick={() => setActivePreviewTab('json')}
                style={{
                  flex: 1,
                  padding: '0.75rem',
                  background: 'none',
                  border: 'none',
                  borderBottom: activePreviewTab === 'json' ? '2px solid var(--primary)' : 'none',
                  color: activePreviewTab === 'json' ? 'white' : 'var(--text-secondary)',
                  fontWeight: 600,
                  fontSize: '0.85rem',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '0.4rem',
                  cursor: 'pointer',
                }}
              >
                <Code size={15} /> Schema PostgreSQL JSONB
              </button>
            </div>

            {/* Tab Content */}
            <div style={{ flex: 1, overflowY: 'auto', padding: '1.25rem' }}>
              {activePreviewTab === 'preview' ? (
                <div>
                  <div
                    style={{
                      borderBottom: '1px solid var(--border-subtle)',
                      paddingBottom: '1rem',
                      marginBottom: '1.25rem',
                    }}
                  >
                    <span className="badge badge-published" style={{ marginBottom: '0.5rem' }}>
                      {code || 'CODE_PLACEHOLDER'}
                    </span>
                    <h3 style={{ fontSize: '1.2rem', fontWeight: 800, color: 'white', marginTop: '0.25rem' }}>
                      {title || 'Tiêu đề biểu mẫu chưa đặt'}
                    </h3>
                    {description && (
                      <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginTop: '0.25rem' }}>
                        {description}
                      </p>
                    )}
                  </div>

                  {/* Render fields */}
                  {fields.map((f) => (
                    <div key={f.id} className="input-group">
                      <label className="input-label">
                        <span>
                          {f.label}{' '}
                          {f.required && <span style={{ color: 'var(--accent-rose)' }}>*</span>}
                        </span>
                        <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>
                          key: {f.name}
                        </span>
                      </label>

                      {f.type === 0 && (
                        <input
                          type="text"
                          className="input-control"
                          placeholder={f.placeholder}
                          value={previewValues[f.name] || ''}
                          onChange={(e) =>
                            setPreviewValues({ ...previewValues, [f.name]: e.target.value })
                          }
                        />
                      )}

                      {f.type === 6 && (
                        <input
                          type="email"
                          className="input-control"
                          placeholder={f.placeholder || 'name@domain.com'}
                          value={previewValues[f.name] || ''}
                          onChange={(e) =>
                            setPreviewValues({ ...previewValues, [f.name]: e.target.value })
                          }
                        />
                      )}

                      {f.type === 1 && (
                        <input
                          type="number"
                          min={f.min}
                          max={f.max}
                          className="input-control"
                          placeholder={f.placeholder || '0'}
                          value={previewValues[f.name] || ''}
                          onChange={(e) =>
                            setPreviewValues({ ...previewValues, [f.name]: Number(e.target.value) })
                          }
                        />
                      )}

                      {f.type === 2 && (
                        <input
                          type="date"
                          className="input-control"
                          value={previewValues[f.name] || ''}
                          onChange={(e) =>
                            setPreviewValues({ ...previewValues, [f.name]: e.target.value })
                          }
                        />
                      )}

                      {f.type === 3 && (
                        <select
                          className="input-control"
                          value={previewValues[f.name] || ''}
                          onChange={(e) =>
                            setPreviewValues({ ...previewValues, [f.name]: e.target.value })
                          }
                        >
                          <option value="">-- Vui lòng chọn một tùy chọn --</option>
                          {(f.options || []).map((opt) => (
                            <option key={opt} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                      )}

                      {f.type === 4 && (
                        <label className="switch-wrapper" style={{ marginTop: '0.35rem' }}>
                          <input
                            type="checkbox"
                            className="switch-checkbox"
                            checked={Boolean(previewValues[f.name])}
                            onChange={(e) =>
                              setPreviewValues({ ...previewValues, [f.name]: e.target.checked })
                            }
                          />
                          <span>Xác nhận lựa chọn này</span>
                        </label>
                      )}

                      {f.type === 5 && (
                        <textarea
                          className="input-control"
                          placeholder={f.placeholder}
                          value={previewValues[f.name] || ''}
                          onChange={(e) =>
                            setPreviewValues({ ...previewValues, [f.name]: e.target.value })
                          }
                        />
                      )}
                    </div>
                  ))}

                  <div
                    style={{
                      marginTop: '1.5rem',
                      padding: '1rem',
                      borderRadius: 'var(--radius-md)',
                      background: 'rgba(99, 102, 241, 0.05)',
                      border: '1px dashed var(--primary-glow)',
                    }}
                  >
                    <div style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--primary-light)', marginBottom: '0.4rem' }}>
                      Payload dữ liệu JSONB đang nhập thử:
                    </div>
                    <pre
                      style={{
                        fontFamily: 'var(--font-mono)',
                        fontSize: '0.75rem',
                        color: 'var(--accent-cyan)',
                        whiteSpace: 'pre-wrap',
                      }}
                    >
                      {JSON.stringify(previewValues, null, 2)}
                    </pre>
                  </div>
                </div>
              ) : (
                <div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '0.75rem' }}>
                    Cấu trúc JSON Schema sẽ được lưu thẳng vào cột <code>fields (JSONB)</code> của bảng{' '}
                    <code>form_definitions</code> trên PostgreSQL:
                  </p>
                  <pre className="code-preview-box">
                    {JSON.stringify(
                      {
                        title,
                        code,
                        description,
                        version: 1,
                        fieldsCount: fields.length,
                        fields,
                      },
                      null,
                      2
                    )}
                  </pre>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onClose}>
            Hủy bỏ
          </button>
          <button className="btn btn-secondary" onClick={() => handleSubmit(0)}>
            Lưu bản nháp (Draft)
          </button>
          <button className="btn btn-primary" onClick={() => handleSubmit(1)}>
            <CheckCircle size={16} /> Xuất bản ngay (Publish)
          </button>
        </div>
      </div>
    </div>
  )
}
