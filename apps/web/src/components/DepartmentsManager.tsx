import { useState, useEffect, useCallback } from 'react'
import type { Company, Department, DepartmentTreeNode, CompanyMember } from '../types'
import { api } from '../services/api'
import {
  GitBranch,
  Plus,
  Edit2,
  Trash2,
  ChevronRight,
  ChevronDown,
  User as UserIcon,
  ShieldAlert,
  Building,
  List,
  FolderTree,
  X,
  Layers,
  CheckCircle2,
} from 'lucide-react'

interface DepartmentsManagerProps {
  activeCompany: Company | null
}

const INITIAL_DEPARTMENTS_TREE: DepartmentTreeNode[] = [
  {
    id: 'dept_root_1',
    companyId: 'comp_01',
    name: 'Khối Công Nghệ & Kỹ Thuật (Engineering Division)',
    code: 'ENG-DIV',
    description: 'Phụ trách toàn bộ kiến trúc hạ tầng đám mây, microservices và bảo mật thông tin.',
    isActive: true,
    createdAt: '2026-09-01T08:00:00Z',
    updatedAt: '2026-09-15T08:00:00Z',
    children: [
      {
        id: 'dept_sub_1_1',
        companyId: 'comp_01',
        name: 'Đội Ngũ Kỹ Thuật Phần Mềm & Cloud (Core Services)',
        code: 'ENG-CORE',
        parentId: 'dept_root_1',
        description: 'Phát triển backend .NET 10, PostgreSQL JSONB và frontend React client.',
        isActive: true,
        createdAt: '2026-09-05T08:00:00Z',
        updatedAt: '2026-09-18T08:00:00Z',
        children: [],
      },
      {
        id: 'dept_sub_1_2',
        companyId: 'comp_01',
        name: 'Đội Ngũ DevOps & Bảo Mật Hệ Thống (SecOps)',
        code: 'ENG-OPS',
        parentId: 'dept_root_1',
        description: 'Vận hành Docker containers, CI/CD pipeline và giám sát hiệu năng.',
        isActive: true,
        createdAt: '2026-09-08T08:00:00Z',
        updatedAt: '2026-09-20T08:00:00Z',
        children: [],
      },
    ],
  },
  {
    id: 'dept_root_2',
    companyId: 'comp_01',
    name: 'Khối Vận Hành Sản Phẩm & Kinh Doanh (Product & Operations)',
    code: 'OPS-DIV',
    description: 'Quản lý lộ trình tính năng sản phẩm và tăng trưởng doanh thu.',
    isActive: true,
    createdAt: '2026-09-02T08:00:00Z',
    updatedAt: '2026-09-16T08:00:00Z',
    children: [
      {
        id: 'dept_sub_2_1',
        companyId: 'comp_01',
        name: 'Bộ Phận Chăm Sóc & Hỗ Trợ Khách Hàng (Support 24/7)',
        code: 'OPS-SUPP',
        parentId: 'dept_root_2',
        description: 'Tiếp nhận yêu cầu hỗ trợ và xử lý sự cố người dùng.',
        isActive: true,
        createdAt: '2026-09-10T08:00:00Z',
        updatedAt: '2026-09-22T08:00:00Z',
        children: [],
      },
    ],
  },
  {
    id: 'dept_root_3',
    companyId: 'comp_01',
    name: 'Khối Quản Trị Nhân Sự & Hành Chính (People & Culture)',
    code: 'HR-DIV',
    description: 'Chính sách đãi ngộ, tuyển dụng nhân tài và đào tạo nội bộ.',
    isActive: true,
    createdAt: '2026-09-03T08:00:00Z',
    updatedAt: '2026-09-12T08:00:00Z',
    children: [],
  },
]

export function DepartmentsManager({ activeCompany }: DepartmentsManagerProps) {
  const [departmentsTree, setDepartmentsTree] = useState<DepartmentTreeNode[]>(INITIAL_DEPARTMENTS_TREE)
  const [flatDepartments, setFlatDepartments] = useState<Department[]>([])
  const [members, setMembers] = useState<CompanyMember[]>([])
  const [isLoading, setIsLoading] = useState<boolean>(true)
  const [viewMode, setViewMode] = useState<'tree' | 'flat'>('tree')
  const [expandedNodes, setExpandedNodes] = useState<Record<string, boolean>>({
    dept_root_1: true,
    dept_root_2: true,
    dept_root_3: true,
  })

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingDepartment, setEditingDepartment] = useState<Department | null>(null)
  const [modalName, setModalName] = useState('')
  const [modalCode, setModalCode] = useState('')
  const [modalParentId, setModalParentId] = useState<string>('')
  const [modalManagerId, setModalManagerId] = useState<string>('')
  const [modalDesc, setModalDesc] = useState('')
  const [modalLoading, setModalLoading] = useState(false)
  const [modalError, setModalError] = useState<string | null>(null)
  const [notification, setNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

  const loadData = useCallback(async () => {
    if (!activeCompany?.id) {
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    try {
      const [treeData, flatData, membersData] = await Promise.allSettled([
        api.getDepartmentsTree(activeCompany.id),
        api.getDepartments(activeCompany.id),
        api.getCompanyMembers(activeCompany.id),
      ])

      if (treeData.status === 'fulfilled' && treeData.value && treeData.value.length > 0) {
        setDepartmentsTree(treeData.value)
        // Auto expand all root nodes
        const exp: Record<string, boolean> = {}
        treeData.value.forEach((node) => {
          exp[node.id] = true
        })
        setExpandedNodes(exp)
      }

      if (flatData.status === 'fulfilled' && flatData.value) {
        setFlatDepartments(flatData.value)
      }

      if (membersData.status === 'fulfilled' && membersData.value) {
        setMembers(membersData.value.filter((m) => m.status.toUpperCase() === 'ACTIVE'))
      }
    } catch (err: any) {
      console.warn('Error fetching departments data:', err.message)
    } finally {
      setIsLoading(false)
    }
  }, [activeCompany?.id])

  useEffect(() => {
    loadData()
  }, [loadData])

  const toggleExpand = (id: string) => {
    setExpandedNodes((prev) => ({ ...prev, [id]: !prev[id] }))
  }

  const expandAll = () => {
    const exp: Record<string, boolean> = {}
    const traverse = (nodes: DepartmentTreeNode[]) => {
      nodes.forEach((n) => {
        exp[n.id] = true
        if (n.children && n.children.length > 0) {
          traverse(n.children)
        }
      })
    }
    traverse(departmentsTree)
    setExpandedNodes(exp)
  }

  const collapseAll = () => {
    setExpandedNodes({})
  }

  const handleOpenCreate = (parentId?: string) => {
    setEditingDepartment(null)
    setModalName('')
    setModalCode('')
    setModalParentId(parentId || '')
    setModalManagerId('')
    setModalDesc('')
    setModalError(null)
    setIsModalOpen(true)
  }

  const handleOpenEdit = (dept: Department) => {
    setEditingDepartment(dept)
    setModalName(dept.name)
    setModalCode(dept.code || '')
    setModalParentId(dept.parentId || '')
    setModalManagerId(dept.managerId || '')
    setModalDesc(dept.description || '')
    setModalError(null)
    setIsModalOpen(true)
  }

  const handleSaveDepartment = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!modalName.trim()) {
      setModalError('Tên phòng ban không được để trống')
      return
    }

    setModalLoading(true)
    setModalError(null)

    const payload = {
      name: modalName.trim(),
      code: modalCode.trim() || undefined,
      parentId: modalParentId || undefined,
      managerId: modalManagerId || undefined,
      description: modalDesc.trim() || undefined,
    }

    try {
      if (editingDepartment) {
        // Update existing department
        if (activeCompany?.id) {
          await api.updateDepartment(editingDepartment.id, payload)
        }
        setNotification({ type: 'success', message: 'Cập nhật phòng ban thành công!' })
      } else {
        // Create new department
        if (activeCompany?.id) {
          await api.createDepartment(activeCompany.id, payload)
        }
        setNotification({ type: 'success', message: 'Tạo phòng ban mới thành công!' })
      }

      setIsModalOpen(false)
      loadData()
    } catch (err: any) {
      setModalError(err.message || 'Thao tác không thành công')
    } finally {
      setModalLoading(false)
    }
  }

  const handleDelete = async (id: string, name: string, hasChildren: boolean) => {
    if (hasChildren) {
      alert(`[QUY TẮC TOÀN VẸN DỮ LIỆU]\nKhông thể xóa phòng ban "${name}" vì đang có các phòng ban con trực thuộc.\nVui lòng di chuyển hoặc xóa các phòng ban con trước.`)
      return
    }

    if (!confirm(`Bạn có chắc chắn muốn xóa phòng ban "${name}"?`)) {
      return
    }

    try {
      if (activeCompany?.id) {
        await api.deleteDepartment(id)
      } else {
        // Local removal
        const removeRecursive = (nodes: DepartmentTreeNode[]): DepartmentTreeNode[] => {
          return nodes
            .filter((n) => n.id !== id)
            .map((n) => ({ ...n, children: removeRecursive(n.children) }))
        }
        setDepartmentsTree(removeRecursive(departmentsTree))
      }
      setNotification({ type: 'success', message: `Đã xóa phòng ban "${name}" thành công!` })
      loadData()
    } catch (err: any) {
      setNotification({ type: 'error', message: err.message || 'Lỗi khi xóa phòng ban' })
    }
  }

  // Count metrics
  let totalDepts = 0
  let totalRoots = 0
  let totalSub = 0
  let managersCount = 0

  const countTraversal = (nodes: DepartmentTreeNode[], isRoot = true) => {
    nodes.forEach((n) => {
      totalDepts++
      if (isRoot) totalRoots++
      else totalSub++
      if (n.managerId || n.manager) managersCount++
      if (n.children && n.children.length > 0) {
        countTraversal(n.children, false)
      }
    })
  }
  countTraversal(departmentsTree)

  // Recursive Tree Node Renderer
  const renderTreeNode = (node: DepartmentTreeNode, level = 0) => {
    const isExpanded = Boolean(expandedNodes[node.id])
    const hasChildren = Boolean(node.children && node.children.length > 0)
    const managerName = node.manager?.name || (members.find((m) => m.userId === node.managerId)?.user?.name)

    return (
      <div key={node.id} style={{ marginLeft: level > 0 ? '1.75rem' : 0, position: 'relative' }}>
        {/* Tree Line Connector */}
        {level > 0 && (
          <div
            style={{
              position: 'absolute',
              left: '-1.15rem',
              top: '1.25rem',
              width: '1rem',
              height: '1px',
              background: 'rgba(99, 102, 241, 0.35)',
            }}
          />
        )}

        <div
          className="field-builder-card"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.85rem 1.15rem',
            marginBottom: '0.65rem',
            background: level === 0 ? 'rgba(99, 102, 241, 0.05)' : 'rgba(255, 255, 255, 0.02)',
            borderLeft: level === 0 ? '3px solid var(--primary)' : '1px solid var(--border-subtle)',
          }}
        >
          {/* Node Info */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flex: 1 }}>
            {/* Expand / Collapse Button */}
            {hasChildren ? (
              <button
                type="button"
                onClick={() => toggleExpand(node.id)}
                style={{
                  background: 'rgba(255, 255, 255, 0.05)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: '4px',
                  width: '24px',
                  height: '24px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  cursor: 'pointer',
                  color: 'var(--primary-light)',
                  padding: 0,
                }}
              >
                {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
              </button>
            ) : (
              <div style={{ width: '24px', display: 'flex', justifyContent: 'center' }}>
                <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: 'var(--text-muted)' }} />
              </div>
            )}

            {/* Department Icon */}
            <div
              style={{
                width: '32px',
                height: '32px',
                borderRadius: 'var(--radius-sm)',
                background: level === 0 ? 'rgba(99, 102, 241, 0.15)' : 'rgba(6, 182, 212, 0.12)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: level === 0 ? 'var(--primary-light)' : 'var(--accent-cyan)',
              }}
            >
              {level === 0 ? <Building size={16} /> : <GitBranch size={16} />}
            </div>

            {/* Details */}
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <strong style={{ fontSize: '0.925rem', color: 'white' }}>{node.name}</strong>
                {node.code && (
                  <span
                    style={{
                      fontFamily: 'var(--font-mono)',
                      fontSize: '0.7rem',
                      fontWeight: 700,
                      padding: '0.1rem 0.45rem',
                      borderRadius: 'var(--radius-sm)',
                      background: 'rgba(255, 255, 255, 0.05)',
                      color: 'var(--text-secondary)',
                      border: '1px solid var(--border-subtle)',
                    }}
                  >
                    {node.code}
                  </span>
                )}
                {hasChildren && (
                  <span
                    style={{
                      fontSize: '0.7rem',
                      color: 'var(--primary-light)',
                      background: 'rgba(99, 102, 241, 0.12)',
                      padding: '0.1rem 0.45rem',
                      borderRadius: '999px',
                    }}
                  >
                    {node.children.length} phòng ban con
                  </span>
                )}
              </div>

              {node.description && (
                <div style={{ fontSize: '0.775rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
                  {node.description}
                </div>
              )}
            </div>
          </div>

          {/* Node Right: Manager & Actions */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            {/* Manager Tag */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8rem' }}>
              <UserIcon size={13} color={managerName ? 'var(--accent-emerald)' : 'var(--text-muted)'} />
              <span style={{ color: managerName ? 'var(--accent-emerald)' : 'var(--text-muted)' }}>
                {managerName ? `Trưởng phòng: ${managerName}` : 'Chưa gán trưởng phòng'}
              </span>
            </div>

            {/* Buttons */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                title="Thêm phòng ban con trực thuộc"
                onClick={() => handleOpenCreate(node.id)}
                style={{ padding: '0.25rem 0.5rem', fontSize: '0.75rem' }}
              >
                <Plus size={13} /> Con
              </button>

              <button
                type="button"
                className="btn btn-secondary btn-sm"
                title="Chỉnh sửa phòng ban"
                onClick={() => handleOpenEdit(node)}
                style={{ padding: '0.25rem 0.45rem' }}
              >
                <Edit2 size={13} />
              </button>

              <button
                type="button"
                className="btn btn-secondary btn-sm"
                title={hasChildren ? 'Chặn xóa: Đang có phòng ban con' : 'Xóa phòng ban'}
                onClick={() => handleDelete(node.id, node.name, hasChildren)}
                style={{
                  padding: '0.25rem 0.45rem',
                  color: hasChildren ? 'var(--text-muted)' : 'var(--accent-rose)',
                  opacity: hasChildren ? 0.4 : 1,
                  cursor: hasChildren ? 'not-allowed' : 'pointer',
                }}
              >
                <Trash2 size={13} />
              </button>
            </div>
          </div>
        </div>

        {/* Render Children Recursively */}
        {hasChildren && isExpanded && (
          <div style={{ borderLeft: '1px dashed rgba(99, 102, 241, 0.25)', marginLeft: '1rem', paddingLeft: '0.5rem' }}>
            {node.children.map((child) => renderTreeNode(child, level + 1))}
          </div>
        )}
      </div>
    )
  }

  return (
    <div className="container animate-fade-in" style={{ padding: '2.5rem 1.5rem 4rem' }}>
      {/* Top Banner */}
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
              background: 'rgba(6, 182, 212, 0.15)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--accent-cyan)',
            }}
          >
            <GitBranch size={22} />
          </div>
          <div>
            <h2 style={{ fontSize: '1.5rem', fontWeight: 800, color: 'white' }}>
              Cơ Cấu Tổ Chức & Phòng Ban (Tree Hierarchy)
            </h2>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem' }}>
              Phân cấp phòng ban trực quan, gán nhân sự quản lý và ràng buộc toàn vẹn dữ liệu cha/con
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          {/* View Toggle */}
          <div
            style={{
              display: 'flex',
              background: 'rgba(255, 255, 255, 0.04)',
              borderRadius: 'var(--radius-md)',
              padding: '0.2rem',
              border: '1px solid var(--border-subtle)',
            }}
          >
            <button
              type="button"
              onClick={() => setViewMode('tree')}
              className={`btn btn-sm ${viewMode === 'tree' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ border: 'none' }}
            >
              <FolderTree size={14} /> Dạng Cây
            </button>
            <button
              type="button"
              onClick={() => setViewMode('flat')}
              className={`btn btn-sm ${viewMode === 'flat' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ border: 'none' }}
            >
              <List size={14} /> Danh Sách
            </button>
          </div>

          <button className="btn btn-primary" onClick={() => handleOpenCreate()}>
            <Plus size={16} /> Tạo Phòng Ban Mới
          </button>
        </div>
      </div>

      {/* Notification Toast */}
      {notification && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.85rem 1.25rem',
            borderRadius: 'var(--radius-md)',
            marginBottom: '1.5rem',
            background: notification.type === 'success' ? 'rgba(16, 185, 129, 0.12)' : 'rgba(244, 63, 94, 0.12)',
            border: `1px solid ${notification.type === 'success' ? 'rgba(16, 185, 129, 0.3)' : 'rgba(244, 63, 94, 0.3)'}`,
            color: notification.type === 'success' ? '#34d399' : '#fb7185',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem' }}>
            {notification.type === 'success' ? <CheckCircle2 size={18} /> : <ShieldAlert size={18} />}
            <span>{notification.message}</span>
          </div>
          <button
            type="button"
            onClick={() => setNotification(null)}
            style={{ background: 'none', border: 'none', color: 'inherit', cursor: 'pointer' }}
          >
            <X size={16} />
          </button>
        </div>
      )}

      {/* KPI Strip */}
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
            Tổng Phòng Ban
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'white' }}>{totalDepts}</div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--primary-light)', textTransform: 'uppercase' }}>
            Khối Cấp Cao (Roots)
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--primary-light)' }}>
            {totalRoots}
          </div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--accent-cyan)', textTransform: 'uppercase' }}>
            Phòng Ban Con (Sub-nodes)
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-cyan)' }}>
            {totalSub}
          </div>
        </div>
        <div className="glass-card" style={{ padding: '1rem 1.25rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--accent-emerald)', textTransform: 'uppercase' }}>
            Đã Gán Trưởng Phòng
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: 800, color: 'var(--accent-emerald)' }}>
            {managersCount} / {totalDepts}
          </div>
        </div>
      </div>

      {/* Main View Card */}
      <div className="glass-card" style={{ padding: '1.75rem' }}>
        {/* Tree Toolbar */}
        {viewMode === 'tree' && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '1.25rem',
              paddingBottom: '0.75rem',
              borderBottom: '1px solid var(--border-subtle)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
              <Layers size={16} />
              <span>Cấu trúc phân cấp tổ chức doanh nghiệp</span>
            </div>

            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={expandAll}
                style={{ fontSize: '0.75rem' }}
              >
                Mở rộng tất cả
              </button>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={collapseAll}
                style={{ fontSize: '0.75rem' }}
              >
                Thu gọn tất cả
              </button>
            </div>
          </div>
        )}

        {/* Content */}
        {isLoading ? (
          <div style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-muted)' }}>
            Đang tải dữ liệu cơ cấu phòng ban...
          </div>
        ) : departmentsTree.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '3.5rem' }}>
            <GitBranch size={44} color="var(--text-muted)" style={{ margin: '0 auto 1rem' }} />
            <h3 style={{ fontSize: '1.2rem', fontWeight: 700, color: 'white' }}>
              Chưa có phòng ban nào trong tổ chức
            </h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.875rem', marginTop: '0.5rem', marginBottom: '1.5rem' }}>
              Tạo phòng ban đầu tiên để phân cấp chức năng và giao quyền phê duyệt hồ sơ.
            </p>
            <button className="btn btn-primary" onClick={() => handleOpenCreate()}>
              <Plus size={16} /> Tạo Phòng Ban Đầu Tiên
            </button>
          </div>
        ) : viewMode === 'tree' ? (
          /* Tree View */
          <div>{departmentsTree.map((node) => renderTreeNode(node, 0))}</div>
        ) : (
          /* Flat Table View */
          <div className="table-wrapper">
            <table className="custom-table">
              <thead>
                <tr>
                  <th>Tên Phòng Ban</th>
                  <th>Mã Code</th>
                  <th>Phòng Ban Cha</th>
                  <th>Trưởng Phòng</th>
                  <th>Mô Tả</th>
                  <th style={{ textAlign: 'right' }}>Thao Tác</th>
                </tr>
              </thead>
              <tbody>
                {(flatDepartments.length > 0
                  ? flatDepartments
                  : departmentsTree.flatMap((r) => [r, ...r.children])
                ).map((dept) => {
                  const parentDept = flatDepartments.find((d) => d.id === dept.parentId)
                  const mgr = members.find((m) => m.userId === dept.managerId)?.user

                  return (
                    <tr key={dept.id}>
                      <td style={{ fontWeight: 600, color: 'white' }}>{dept.name}</td>
                      <td>
                        {dept.code ? (
                          <span
                            style={{
                              fontFamily: 'var(--font-mono)',
                              fontSize: '0.75rem',
                              padding: '0.15rem 0.4rem',
                              borderRadius: '4px',
                              background: 'rgba(255, 255, 255, 0.05)',
                              color: 'var(--primary-light)',
                            }}
                          >
                            {dept.code}
                          </span>
                        ) : (
                          '—'
                        )}
                      </td>
                      <td>{parentDept?.name || (dept.parentId ? 'Phòng ban cha' : '— (Cấp cao nhất)')}</td>
                      <td>{mgr?.name || (dept.managerId ? 'Đã gán' : 'Chưa gán')}</td>
                      <td style={{ maxWidth: '240px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {dept.description || '—'}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', gap: '0.35rem' }}>
                          <button
                            className="btn btn-secondary btn-sm"
                            onClick={() => handleOpenEdit(dept)}
                            title="Sửa"
                          >
                            <Edit2 size={13} />
                          </button>
                          <button
                            className="btn btn-secondary btn-sm"
                            onClick={() => handleDelete(dept.id, dept.name, false)}
                            style={{ color: 'var(--accent-rose)' }}
                            title="Xóa"
                          >
                            <Trash2 size={13} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Create / Edit Department Modal */}
      {isModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-container" style={{ maxWidth: '540px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <GitBranch size={20} color="var(--accent-cyan)" />
                <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white' }}>
                  {editingDepartment ? 'Chỉnh Sửa Phòng Ban' : 'Tạo Phòng Ban Mới'}
                </h3>
              </div>
              <button
                onClick={() => setIsModalOpen(false)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSaveDepartment}>
              <div className="modal-body">
                {modalError && (
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
                    {modalError}
                  </div>
                )}

                <div className="input-group">
                  <label className="input-label">Tên phòng ban / Khối chức năng *</label>
                  <input
                    type="text"
                    className="input-control"
                    placeholder="Ví dụ: Phòng Kỹ Thuật Đám Mây (Cloud Architecture)"
                    value={modalName}
                    onChange={(e) => setModalName(e.target.value)}
                  />
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: '0.75rem' }}>
                  <div className="input-group">
                    <label className="input-label">Mã viết tắt (Code)</label>
                    <input
                      type="text"
                      className="input-control"
                      placeholder="ENG-CLOUD"
                      value={modalCode}
                      onChange={(e) => setModalCode(e.target.value.toUpperCase())}
                    />
                  </div>

                  <div className="input-group">
                    <label className="input-label">Trực thuộc (Phòng ban cha)</label>
                    <select
                      className="input-control"
                      value={modalParentId}
                      onChange={(e) => setModalParentId(e.target.value)}
                    >
                      <option value="">-- Cấp cao nhất (Root) --</option>
                      {(flatDepartments.length > 0
                        ? flatDepartments
                        : departmentsTree.flatMap((r) => [r, ...r.children])
                      )
                        .filter((d) => !editingDepartment || d.id !== editingDepartment.id)
                        .map((d) => (
                          <option key={d.id} value={d.id}>
                            {d.name}
                          </option>
                        ))}
                    </select>
                  </div>
                </div>

                {/* Manager Selector */}
                <div className="input-group">
                  <label className="input-label">Trưởng phòng phụ trách (Manager)</label>
                  <select
                    className="input-control"
                    value={modalManagerId}
                    onChange={(e) => setModalManagerId(e.target.value)}
                  >
                    <option value="">-- Chưa chỉ định trưởng phòng --</option>
                    {members.map((m) => (
                      <option key={m.userId || m.id} value={m.userId || ''}>
                        {m.user?.name || m.userName || m.email} ({m.role})
                      </option>
                    ))}
                  </select>
                  <span style={{ fontSize: '0.725rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
                    Chỉ thành viên hợp lệ (Active) trong công ty mới có thể được chỉ định làm Trưởng phòng.
                  </span>
                </div>

                <div className="input-group">
                  <label className="input-label">Mô tả chức năng & nhiệm vụ</label>
                  <textarea
                    className="input-control"
                    style={{ minHeight: '68px' }}
                    placeholder="Mô tả phạm vi trách nhiệm của bộ phận này..."
                    value={modalDesc}
                    onChange={(e) => setModalDesc(e.target.value)}
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsModalOpen(false)}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={modalLoading}>
                  {modalLoading ? 'Đang lưu...' : editingDepartment ? 'Cập Nhật Phòng Ban' : 'Tạo Phòng Ban'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
