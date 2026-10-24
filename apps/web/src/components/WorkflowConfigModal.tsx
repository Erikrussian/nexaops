import React, { useState, useEffect } from 'react'
import type {
  FormDefinition,
  ApprovalStepType,
  CompanyMember,
} from '../types'
import { api } from '../services/api'
import {
  GitMerge,
  Plus,
  Trash2,
  ArrowUp,
  ArrowDown,
  CheckCircle,
  AlertTriangle,
  X,
} from 'lucide-react'

interface WorkflowConfigModalProps {
  form: FormDefinition
  companyId: string
  isOpen: boolean
  onClose: () => void
  onWorkflowSaved?: () => void
}

interface StepFormState {
  orderIndex: number
  name: string
  stepType: ApprovalStepType
  approverRole?: number | null
  specificApproverUserId?: string | null
  allowSelfApproval: boolean
  timeLimitHours?: number | null
}

export const WorkflowConfigModal: React.FC<WorkflowConfigModalProps> = ({
  form,
  companyId,
  isOpen,
  onClose,
  onWorkflowSaved,
}) => {
  const [workflowId, setWorkflowId] = useState<string | null>(null)
  const [name, setName] = useState(`Quy trình duyệt: ${form.title}`)
  const [description, setDescription] = useState('Phê duyệt tuần tự nhiều cấp độ.')
  const [isActive, setIsActive] = useState(true)
  const [steps, setSteps] = useState<StepFormState[]>([
    {
      orderIndex: 1,
      name: 'Trưởng phòng bộ phận thẩm định',
      stepType: 1, // DepartmentManager
      approverRole: null,
      specificApproverUserId: null,
      allowSelfApproval: false,
      timeLimitHours: 24,
    },
    {
      orderIndex: 2,
      name: 'Chủ doanh nghiệp phê duyệt cuối',
      stepType: 3, // CompanyOwner
      approverRole: null,
      specificApproverUserId: null,
      allowSelfApproval: false,
      timeLimitHours: 48,
    },
  ])

  const [members, setMembers] = useState<CompanyMember[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!isOpen) return

    let isMounted = true
    setIsLoading(true)
    setErrorMessage(null)
    setSuccessMessage(null)

    // Load existing workflow and company members
    Promise.all([
      api.getApprovalWorkflow(companyId, form.id),
      api.getCompanyMembers(companyId).catch(() => []),
    ])
      .then(([wf, memList]) => {
        if (!isMounted) return
        setMembers(memList)

        if (wf) {
          setWorkflowId(wf.id)
          setName(wf.name)
          setDescription(wf.description || '')
          setIsActive(wf.isActive)
          if (wf.steps && wf.steps.length > 0) {
            setSteps(
              wf.steps
                .sort((a, b) => a.orderIndex - b.orderIndex)
                .map((s) => ({
                  orderIndex: s.orderIndex,
                  name: s.name,
                  stepType: s.stepType,
                  approverRole: s.approverRole ?? null,
                  specificApproverUserId: s.specificApproverUserId ?? null,
                  allowSelfApproval: s.allowSelfApproval,
                  timeLimitHours: s.timeLimitHours ?? null,
                }))
            )
          }
        } else {
          setWorkflowId(null)
          setName(`Quy trình duyệt: ${form.title}`)
          setDescription('Quy trình thẩm định hồ sơ theo từng bước.')
          setIsActive(true)
        }
      })
      .catch((err) => {
        if (isMounted) {
          setErrorMessage(err.message || 'Không thể tải cấu hình quy trình')
        }
      })
      .finally(() => {
        if (isMounted) setIsLoading(false)
      })

    return () => {
      isMounted = false
    }
  }, [isOpen, companyId, form.id, form.title])

  if (!isOpen) return null

  const handleAddStep = () => {
    const nextOrder = steps.length + 1
    setSteps([
      ...steps,
      {
        orderIndex: nextOrder,
        name: `Bước ${nextOrder}: Phê duyệt bổ sung`,
        stepType: 0, // Role
        approverRole: 2, // Manager
        specificApproverUserId: null,
        allowSelfApproval: false,
        timeLimitHours: 24,
      },
    ])
  }

  const handleRemoveStep = (index: number) => {
    if (steps.length <= 1) {
      setErrorMessage('Quy trình phê duyệt cần ít nhất 1 bước.')
      return
    }
    const updated = steps
      .filter((_, idx) => idx !== index)
      .map((s, idx) => ({ ...s, orderIndex: idx + 1 }))
    setSteps(updated)
    setErrorMessage(null)
  }

  const handleMoveStep = (index: number, direction: 'up' | 'down') => {
    const targetIdx = direction === 'up' ? index - 1 : index + 1
    if (targetIdx < 0 || targetIdx >= steps.length) return
    const updated = [...steps]
    const temp = updated[index]
    updated[index] = updated[targetIdx]
    updated[targetIdx] = temp

    // Re-assign 1-based order
    setSteps(updated.map((s, idx) => ({ ...s, orderIndex: idx + 1 })))
  }

  const handleStepChange = (index: number, patch: Partial<StepFormState>) => {
    const updated = [...steps]
    updated[index] = { ...updated[index], ...patch }
    setSteps(updated)
  }

  const handleSave = async () => {
    if (!name.trim()) {
      setErrorMessage('Vui lòng nhập tên quy trình phê duyệt.')
      return
    }
    if (steps.length === 0) {
      setErrorMessage('Quy trình phải có ít nhất 1 bước phê duyệt.')
      return
    }
    for (const step of steps) {
      if (!step.name.trim()) {
        setErrorMessage(`Vui lòng nhập tên cho Bước ${step.orderIndex}.`)
        return
      }
      if (step.stepType === 2 && !step.specificApproverUserId) {
        setErrorMessage(`Vui lòng chọn nhân sự phê duyệt cụ thể cho Bước ${step.orderIndex}.`)
        return
      }
    }

    setIsSaving(true)
    setErrorMessage(null)
    setSuccessMessage(null)

    try {
      const saved = await api.saveApprovalWorkflow(companyId, form.id, {
        name,
        description,
        isActive,
        steps: steps.map((s) => ({
          orderIndex: s.orderIndex,
          name: s.name,
          stepType: s.stepType,
          approverRole: s.approverRole,
          specificApproverUserId: s.specificApproverUserId,
          allowSelfApproval: s.allowSelfApproval,
          timeLimitHours: s.timeLimitHours,
        })),
      })
      setWorkflowId(saved.id)
      setSuccessMessage('Đã lưu cấu hình quy trình phê duyệt thành công!')
      if (onWorkflowSaved) onWorkflowSaved()
      setTimeout(() => {
        onClose()
      }, 900)
    } catch (err: any) {
      setErrorMessage(err.message || 'Lỗi khi lưu quy trình phê duyệt')
    } finally {
      setIsSaving(false)
    }
  }

  const handleDelete = async () => {
    if (!window.confirm('Bạn có chắc chắn muốn xóa quy trình phê duyệt này không? Biểu mẫu sẽ quay lại chế độ duyệt 1 bước thông thường.')) {
      return
    }
    setIsDeleting(true)
    setErrorMessage(null)
    try {
      await api.deleteApprovalWorkflow(companyId, form.id)
      setWorkflowId(null)
      setSuccessMessage('Đã hủy cấu hình quy trình phê duyệt.')
      if (onWorkflowSaved) onWorkflowSaved()
      setTimeout(() => {
        onClose()
      }, 800)
    } catch (err: any) {
      setErrorMessage(err.message || 'Lỗi khi xóa quy trình')
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div className="modal-backdrop" style={{ zIndex: 1100 }}>
      <div
        className="modal-container animate-fade-in"
        style={{
          maxWidth: '820px',
          width: '94%',
          maxHeight: '92vh',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        {/* Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div
              style={{
                width: '38px',
                height: '38px',
                borderRadius: 'var(--radius-md)',
                background: 'linear-gradient(135deg, var(--accent-purple), var(--primary))',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'white',
              }}
            >
              <GitMerge size={20} />
            </div>
            <div>
              <h3 style={{ fontSize: '1.2rem', fontWeight: 800, color: 'white', margin: 0 }}>
                Cấu Hình Quy Trình Phê Duyệt Nhiều Cấp (Multi-step Workflow)
              </h3>
              <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)', marginTop: '0.2rem' }}>
                Biểu mẫu: <strong style={{ color: 'var(--primary-light)' }}>{form.title}</strong> ({form.code})
              </div>
            </div>
          </div>
          <button
            className="btn btn-secondary btn-sm"
            onClick={onClose}
            style={{ padding: '0.4rem' }}
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div className="modal-body" style={{ flex: 1, overflowY: 'auto', padding: '1.5rem' }}>
          {isLoading ? (
            <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-muted)' }}>
              Đang tải cấu hình quy trình phê duyệt...
            </div>
          ) : (
            <>
          {errorMessage && (
            <div
              style={{
                background: 'rgba(239, 68, 68, 0.15)',
                border: '1px solid rgba(239, 68, 68, 0.4)',
                padding: '0.75rem 1rem',
                borderRadius: 'var(--radius-md)',
                color: '#fca5a5',
                fontSize: '0.85rem',
                marginBottom: '1.25rem',
                display: 'flex',
                alignItems: 'center',
                gap: '0.5rem',
              }}
            >
              <AlertTriangle size={16} />
              {errorMessage}
            </div>
          )}

          {successMessage && (
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
              {successMessage}
            </div>
          )}

          {/* Workflow General Info */}
          <div
            style={{
              background: 'rgba(255, 255, 255, 0.02)',
              border: '1px solid var(--border-subtle)',
              borderRadius: 'var(--radius-md)',
              padding: '1.25rem',
              marginBottom: '1.5rem',
            }}
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '1rem',
              }}
            >
              <h4 style={{ fontSize: '0.95rem', fontWeight: 700, color: 'white', margin: 0 }}>
                Thông Tin Chung Quy Trình
              </h4>
              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                  fontSize: '0.85rem',
                  color: isActive ? 'var(--accent-emerald)' : 'var(--text-muted)',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                <input
                  type="checkbox"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  style={{ cursor: 'pointer' }}
                />
                {isActive ? 'Đang kích hoạt (Active)' : 'Tạm dừng quy trình'}
              </label>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '0.35rem' }}>
                  Tên quy trình phê duyệt *
                </label>
                <input
                  type="text"
                  className="input-field"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Ví dụ: Quy trình duyệt cấp thiết bị"
                  style={{ width: '100%' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '0.35rem' }}>
                  Mô tả quy trình
                </label>
                <input
                  type="text"
                  className="input-field"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Mô tả nguyên tắc và thẩm quyền duyệt..."
                  style={{ width: '100%' }}
                />
              </div>
            </div>
          </div>

          {/* Workflow Steps Builder */}
          <div>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '1rem',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <h4 style={{ fontSize: '1rem', fontWeight: 700, color: 'white', margin: 0 }}>
                  Các Cấp Độ Phê Duyệt Tuần Tự ({steps.length} bước)
                </h4>
              </div>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={handleAddStep}
                style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', color: 'var(--accent-cyan)' }}
              >
                <Plus size={14} /> Thêm Cấp Phê Duyệt
              </button>
            </div>

            {/* Steps Stack */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {steps.map((step, idx) => (
                <div
                  key={idx}
                  style={{
                    background: 'rgba(255, 255, 255, 0.03)',
                    border: '1px solid var(--border-subtle)',
                    borderRadius: 'var(--radius-md)',
                    padding: '1.25rem',
                    position: 'relative',
                  }}
                >
                  {/* Step Top Bar */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '0.85rem',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.65rem' }}>
                      <span
                        style={{
                          background: 'linear-gradient(135deg, var(--primary) 0%, var(--accent-cyan) 100%)',
                          color: 'white',
                          fontWeight: 800,
                          fontSize: '0.75rem',
                          padding: '0.2rem 0.6rem',
                          borderRadius: '12px',
                        }}
                      >
                        Cấp {step.orderIndex}
                      </span>
                      <input
                        type="text"
                        className="input-field"
                        value={step.name}
                        onChange={(e) => handleStepChange(idx, { name: e.target.value })}
                        placeholder="Tên cấp duyệt (ví dụ: Trưởng phòng phê duyệt)"
                        style={{
                          fontWeight: 700,
                          color: 'white',
                          fontSize: '0.9rem',
                          minWidth: '280px',
                        }}
                      />
                    </div>

                    {/* Step Actions (Move & Delete) */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                      <button
                        type="button"
                        onClick={() => handleMoveStep(idx, 'up')}
                        disabled={idx === 0}
                        className="btn btn-secondary btn-sm"
                        style={{ padding: '0.3rem 0.45rem', opacity: idx === 0 ? 0.3 : 1 }}
                        title="Di chuyển lên trước"
                      >
                        <ArrowUp size={13} />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleMoveStep(idx, 'down')}
                        disabled={idx === steps.length - 1}
                        className="btn btn-secondary btn-sm"
                        style={{ padding: '0.3rem 0.45rem', opacity: idx === steps.length - 1 ? 0.3 : 1 }}
                        title="Di chuyển xuống sau"
                      >
                        <ArrowDown size={13} />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleRemoveStep(idx)}
                        className="btn btn-secondary btn-sm"
                        style={{ padding: '0.3rem 0.45rem', color: 'var(--accent-rose)' }}
                        title="Xóa bước này"
                      >
                        <Trash2 size={13} />
                      </button>
                    </div>
                  </div>

                  {/* Step Settings Grid */}
                  <div
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                      gap: '0.85rem',
                      background: 'rgba(0, 0, 0, 0.15)',
                      padding: '0.85rem',
                      borderRadius: 'var(--radius-sm)',
                    }}
                  >
                    {/* Approver Type */}
                    <div>
                      <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.3rem' }}>
                        Thẩm quyền phê duyệt
                      </label>
                      <select
                        className="input-field"
                        value={step.stepType}
                        onChange={(e) =>
                          handleStepChange(idx, {
                            stepType: Number(e.target.value) as ApprovalStepType,
                          })
                        }
                        style={{ width: '100%', fontSize: '0.85rem' }}
                      >
                        <option value={1}>Trưởng phòng ban của biểu mẫu (Dept Manager)</option>
                        <option value={3}>Chủ sở hữu công ty (Company Owner)</option>
                        <option value={0}>Theo vai trò thành viên (Role)</option>
                        <option value={2}>Chỉ định nhân sự cụ thể (Specific User)</option>
                      </select>
                    </div>

                    {/* Role selector if Role */}
                    {step.stepType === 0 && (
                      <div>
                        <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.3rem' }}>
                          Vai trò được quyền duyệt
                        </label>
                        <select
                          className="input-field"
                          value={step.approverRole ?? 2}
                          onChange={(e) =>
                            handleStepChange(idx, { approverRole: Number(e.target.value) })
                          }
                          style={{ width: '100%', fontSize: '0.85rem' }}
                        >
                          <option value={0}>Owner (Chủ sở hữu)</option>
                          <option value={1}>Admin (Quản trị viên)</option>
                          <option value={2}>Manager (Quản lý)</option>
                          <option value={3}>Member (Thành viên)</option>
                        </select>
                      </div>
                    )}

                    {/* Specific user selector if SpecificUser */}
                    {step.stepType === 2 && (
                      <div>
                        <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.3rem' }}>
                          Chọn nhân sự duyệt cụ thể
                        </label>
                        <select
                          className="input-field"
                          value={step.specificApproverUserId || ''}
                          onChange={(e) =>
                            handleStepChange(idx, { specificApproverUserId: e.target.value || null })
                          }
                          style={{ width: '100%', fontSize: '0.85rem' }}
                        >
                          <option value="">-- Chọn thành viên --</option>
                          {members.map((m) => (
                            <option key={m.userId} value={m.userId}>
                              {m.user?.name || m.user?.email || m.userId} ({m.role})
                            </option>
                          ))}
                        </select>
                      </div>
                    )}

                    {/* Time limit hours */}
                    <div>
                      <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.3rem' }}>
                        Hạn xử lý (Giờ, tùy chọn)
                      </label>
                      <input
                        type="number"
                        className="input-field"
                        min={1}
                        max={720}
                        value={step.timeLimitHours || ''}
                        onChange={(e) =>
                          handleStepChange(idx, {
                            timeLimitHours: e.target.value ? Number(e.target.value) : null,
                          })
                        }
                        placeholder="Không giới hạn"
                        style={{ width: '100%', fontSize: '0.85rem' }}
                      />
                    </div>
                  </div>

                  {/* Anti-fraud Self Approval Policy Checkbox */}
                  <div style={{ marginTop: '0.75rem' }}>
                    <label
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.45rem',
                        fontSize: '0.8rem',
                        color: 'var(--text-secondary)',
                        cursor: 'pointer',
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={step.allowSelfApproval}
                        onChange={(e) =>
                          handleStepChange(idx, { allowSelfApproval: e.target.checked })
                        }
                        style={{ cursor: 'pointer' }}
                      />
                      <span>
                        Cho phép người nộp tự duyệt bước này nếu thỏa mãn quyền hạn (Mặc định: Tắt để chống xung đột lợi ích)
                      </span>
                    </label>
                  </div>
                </div>
              ))}
            </div>
          </div>
          </>
          )}
        </div>

        {/* Modal Footer */}
        <div
          className="modal-footer"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '1rem 1.5rem',
            borderTop: '1px solid var(--border-subtle)',
          }}
        >
          <div>
            {workflowId && (
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={handleDelete}
                disabled={isDeleting || isSaving}
                style={{ color: 'var(--accent-rose)' }}
              >
                <Trash2 size={14} /> {isDeleting ? 'Đang hủy...' : 'Hủy quy trình này'}
              </button>
            )}
          </div>

          <div style={{ display: 'flex', gap: '0.75rem' }}>
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={isSaving}>
              Đóng
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleSave}
              disabled={isSaving || isDeleting}
              style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}
            >
              <CheckCircle size={15} />
              {isSaving ? 'Đang lưu cấu hình...' : 'Lưu Quy Trình Phê Duyệt'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
