import { useState } from 'react'
import type { FormDefinition } from '../types'
import { Send, CheckCircle2, AlertCircle, X, FileText } from 'lucide-react'

interface FormSubmissionModalProps {
  form: FormDefinition | null
  isOpen: boolean
  onClose: () => void
  onSubmitSuccess?: (submission: any) => void
}

export function FormSubmissionModal({ form, isOpen, onClose, onSubmitSuccess }: FormSubmissionModalProps) {
  const [formData, setFormData] = useState<Record<string, any>>({})
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [submittedResult, setSubmittedResult] = useState<any | null>(null)

  if (!isOpen || !form) return null

  const handleFieldChange = (key: string, value: any) => {
    setFormData((prev) => ({ ...prev, [key]: value }))
    if (errors[key]) {
      setErrors((prev) => {
        const next = { ...prev }
        delete next[key]
        return next
      })
    }
  }

  const validate = (): boolean => {
    const errs: Record<string, string> = {}

    for (const field of form.fields) {
      const val = formData[field.name]

      if (field.required) {
        if (val === undefined || val === null || val === '' || (field.type === 4 && val !== true)) {
          errs[field.name] = `Trường "${field.label}" là bắt buộc`
          continue
        }
      }

      if (val !== undefined && val !== null && val !== '') {
        // Email check
        if (field.type === 6) {
          const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
          if (!emailRegex.test(String(val))) {
            errs[field.name] = 'Địa chỉ email không đúng định dạng'
          }
        }

        // Number min/max check
        if (field.type === 1) {
          const num = Number(val)
          if (isNaN(num)) {
            errs[field.name] = 'Giá trị phải là một chữ số'
          } else {
            if (field.min !== undefined && num < field.min) {
              errs[field.name] = `Giá trị tối thiểu cho phép là ${field.min}`
            }
            if (field.max !== undefined && num > field.max) {
              errs[field.name] = `Giá trị tối đa cho phép là ${field.max}`
            }
          }
        }
      }
    }

    setErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!validate()) return

    setIsSubmitting(true)

    // Simulate API submission delay
    setTimeout(() => {
      const receipt = {
        id: `sub_${Date.now()}`,
        formDefinitionId: form.id,
        formTitle: form.title,
        formCode: form.code,
        submittedByName: 'Trần Kỹ Sư (Kỹ thuật)',
        data: formData,
        status: 0, // Pending
        createdAt: new Date().toISOString(),
      }

      setSubmittedResult(receipt)
      setIsSubmitting(false)
      if (onSubmitSuccess) {
        onSubmitSuccess(receipt)
      }
    }, 600)
  }

  const handleReset = () => {
    setSubmittedResult(null)
    setFormData({})
    setErrors({})
    onClose()
  }

  return (
    <div className="modal-backdrop">
      <div className="modal-container" style={{ maxWidth: '640px' }}>
        {/* Modal Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div
              style={{
                width: '36px',
                height: '36px',
                borderRadius: 'var(--radius-md)',
                background: 'rgba(6, 182, 212, 0.15)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'var(--accent-cyan)',
              }}
            >
              <FileText size={18} />
            </div>
            <div>
              <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white' }}>
                Nộp Dữ Liệu: {form.title}
              </h3>
              <span className="badge badge-published" style={{ marginTop: '0.2rem' }}>
                {form.code}
              </span>
            </div>
          </div>
          <button
            onClick={handleReset}
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

        {/* Modal Body */}
        <div className="modal-body">
          {submittedResult ? (
            <div style={{ textAlign: 'center', padding: '1.5rem 0' }}>
              <div
                style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  background: 'rgba(16, 185, 129, 0.15)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'var(--accent-emerald)',
                  marginBottom: '1rem',
                }}
              >
                <CheckCircle2 size={36} />
              </div>
              <h4 style={{ fontSize: '1.3rem', fontWeight: 800, color: 'white', marginBottom: '0.5rem' }}>
                Đã nộp biểu mẫu thành công!
              </h4>
              <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', marginBottom: '1.5rem' }}>
                Hồ sơ đã được lưu vào hệ thống dưới mã số <code>{submittedResult.id}</code> và đang chuyển cho Quản lý phê duyệt.
              </p>

              <div
                style={{
                  background: '#06080e',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-md)',
                  padding: '1rem',
                  textAlign: 'left',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.4rem', textTransform: 'uppercase' }}>
                  Dữ liệu JSONB đã ghi nhận:
                </div>
                <pre
                  style={{
                    fontFamily: 'var(--font-mono)',
                    fontSize: '0.75rem',
                    color: '#a5b4fc',
                    whiteSpace: 'pre-wrap',
                  }}
                >
                  {JSON.stringify(submittedResult.data, null, 2)}
                </pre>
              </div>

              <button className="btn btn-primary" style={{ marginTop: '1.5rem', width: '100%' }} onClick={handleReset}>
                Hoàn tất & Đóng
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              {form.description && (
                <div
                  style={{
                    background: 'rgba(255, 255, 255, 0.02)',
                    border: '1px solid var(--border-subtle)',
                    borderRadius: 'var(--radius-md)',
                    padding: '0.85rem',
                    marginBottom: '1.25rem',
                    fontSize: '0.85rem',
                    color: 'var(--text-secondary)',
                  }}
                >
                  {form.description}
                </div>
              )}

              {form.fields.map((field) => (
                <div key={field.id} className="input-group">
                  <label className="input-label">
                    <span>
                      {field.label}{' '}
                      {field.required && <span style={{ color: 'var(--accent-rose)' }}>*</span>}
                    </span>
                  </label>

                  {/* Text Field */}
                  {field.type === 0 && (
                    <input
                      type="text"
                      className="input-control"
                      placeholder={field.placeholder}
                      value={formData[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                    />
                  )}

                  {/* Email Field */}
                  {field.type === 6 && (
                    <input
                      type="email"
                      className="input-control"
                      placeholder={field.placeholder || 'example@company.com'}
                      value={formData[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                    />
                  )}

                  {/* Number Field */}
                  {field.type === 1 && (
                    <input
                      type="number"
                      min={field.min}
                      max={field.max}
                      className="input-control"
                      placeholder={field.placeholder}
                      value={formData[field.name] ?? ''}
                      onChange={(e) =>
                        handleFieldChange(
                          field.name,
                          e.target.value === '' ? '' : Number(e.target.value)
                        )
                      }
                    />
                  )}

                  {/* Date Field */}
                  {field.type === 2 && (
                    <input
                      type="date"
                      className="input-control"
                      value={formData[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                    />
                  )}

                  {/* Select Field */}
                  {field.type === 3 && (
                    <select
                      className="input-control"
                      value={formData[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                    >
                      <option value="">-- Vui lòng chọn một giá trị --</option>
                      {(field.options || []).map((opt) => (
                        <option key={opt} value={opt}>
                          {opt}
                        </option>
                      ))}
                    </select>
                  )}

                  {/* Checkbox Field */}
                  {field.type === 4 && (
                    <label className="switch-wrapper" style={{ marginTop: '0.4rem' }}>
                      <input
                        type="checkbox"
                        className="switch-checkbox"
                        checked={Boolean(formData[field.name])}
                        onChange={(e) => handleFieldChange(field.name, e.target.checked)}
                      />
                      <span>Đồng ý và xác nhận thông tin này</span>
                    </label>
                  )}

                  {/* Textarea Field */}
                  {field.type === 5 && (
                    <textarea
                      className="input-control"
                      placeholder={field.placeholder}
                      value={formData[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                    />
                  )}

                  {/* Field Error Message */}
                  {errors[field.name] && (
                    <span
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.3rem',
                        fontSize: '0.75rem',
                        color: 'var(--accent-rose)',
                        marginTop: '0.25rem',
                      }}
                    >
                      <AlertCircle size={13} /> {errors[field.name]}
                    </span>
                  )}
                </div>
              ))}

              <div className="modal-footer" style={{ margin: '1.5rem -1.75rem -1.75rem', background: 'transparent' }}>
                <button type="button" className="btn btn-secondary" onClick={handleReset}>
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
                  <Send size={15} /> {isSubmitting ? 'Đang gửi...' : 'Gửi Đơn / Nộp Dữ Liệu'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  )
}
