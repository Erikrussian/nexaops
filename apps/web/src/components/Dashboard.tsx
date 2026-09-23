import React, { useState } from 'react'
import {
  FileText,
  Send,
  CheckCircle,
  Users,
  Plus,
  ArrowUpRight,
  TrendingUp,
  ShieldAlert,
  Download,
  Filter,
  Eye,
} from 'lucide-react'
import { MetricCard } from './MetricCard'
import { StatusBadge } from './StatusBadge'
import type { FormSubmission } from '../types'

export const Dashboard: React.FC = () => {
  const [filterDays, setFilterDays] = useState<number>(7)
  const [submissions, setSubmissions] = useState<FormSubmission[]>([
    {
      id: 'sub-1',
      formDefinitionId: 'form-1',
      formTitle: 'Đơn xin nghỉ phép năm',
      formCode: 'LEAVE_ANNUAL',
      companyId: 'comp-1',
      submittedById: 'u-1',
      submittedByName: 'Nguyễn Văn An',
      data: { reason: 'Nghỉ du lịch gia đình', days: 3 },
      status: 0, // Pending
      createdAt: '2026-09-23T08:30:00Z',
      updatedAt: '2026-09-23T08:30:00Z',
    },
    {
      id: 'sub-2',
      formDefinitionId: 'form-2',
      formTitle: 'Yêu cầu cấp phát thiết bị',
      formCode: 'EQUIP_REQ',
      companyId: 'comp-1',
      submittedById: 'u-2',
      submittedByName: 'Trần Thị Mai',
      data: { device: 'MacBook Pro M3 Max 36GB', department: 'Engineering' },
      status: 1, // Approved
      reviewNotes: 'Đã phê duyệt bàn giao từ kho IT',
      reviewedByName: 'Cao Hoàng Linh (Owner)',
      reviewedAt: '2026-09-22T14:15:00Z',
      createdAt: '2026-09-22T09:00:00Z',
      updatedAt: '2026-09-22T14:15:00Z',
    },
    {
      id: 'sub-3',
      formDefinitionId: 'form-3',
      formTitle: 'Phiếu thanh toán công tác phí',
      formCode: 'EXPENSE_CLAIM',
      companyId: 'comp-1',
      submittedById: 'u-3',
      submittedByName: 'Lê Hoàng Nam',
      data: { amount: 4500000, description: 'Chi phí vé máy bay & khách sạn hội nghị' },
      status: 0, // Pending
      createdAt: '2026-09-21T16:45:00Z',
      updatedAt: '2026-09-21T16:45:00Z',
    },
    {
      id: 'sub-4',
      formDefinitionId: 'form-1',
      formTitle: 'Đơn xin nghỉ phép năm',
      formCode: 'LEAVE_ANNUAL',
      companyId: 'comp-1',
      submittedById: 'u-4',
      submittedByName: 'Phạm Minh Đức',
      data: { reason: 'Việc cá nhân đột xuất', days: 1 },
      status: 2, // Rejected
      reviewNotes: 'Trùng lịch release sản phẩm quan trọng',
      reviewedByName: 'Cao Hoàng Linh (Owner)',
      reviewedAt: '2026-09-20T10:00:00Z',
      createdAt: '2026-09-20T08:00:00Z',
      updatedAt: '2026-09-20T10:00:00Z',
    },
  ])

  const handleReview = (id: string, newStatus: 1 | 2) => {
    setSubmissions((prev) =>
      prev.map((s) =>
        s.id === id
          ? {
              ...s,
              status: newStatus,
              reviewNotes: newStatus === 1 ? 'Đã phê duyệt nhanh qua Dashboard' : 'Từ chối yêu cầu',
              reviewedByName: 'Admin User',
              reviewedAt: new Date().toISOString(),
            }
          : s
      )
    )
  }

  // Trend Chart mock heights
  const trendData = [
    { day: 'T4 (17/9)', count: 12, height: '45%' },
    { day: 'T5 (18/9)', count: 19, height: '70%' },
    { day: 'T6 (19/9)', count: 24, height: '90%' },
    { day: 'T7 (20/9)', count: 8, height: '30%' },
    { day: 'CN (21/9)', count: 5, height: '20%' },
    { day: 'T2 (22/9)', count: 22, height: '82%' },
    { day: 'T3 (23/9)', count: 27, height: '100%' },
  ]

  const topForms = [
    { title: 'Đơn xin nghỉ phép năm', code: 'LEAVE_ANNUAL', count: 68, percentage: 85 },
    { title: 'Yêu cầu cấp phát thiết bị', code: 'EQUIP_REQ', count: 42, percentage: 55 },
    { title: 'Thanh toán công tác phí', code: 'EXPENSE_CLAIM', count: 28, percentage: 38 },
    { title: 'Đánh giá thử việc nhân sự', code: 'PROBATION_EVAL', count: 16, percentage: 22 },
  ]

  return (
    <div className="container animate-fade-in" style={{ paddingBottom: '3rem' }}>
      {/* Header Banner */}
      <div
        style={{
          marginTop: '2rem',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-end',
          flexWrap: 'wrap',
          gap: '1rem',
        }}
      >
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.35rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--accent-cyan)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em' }}>
              Multi-Tenant Cloud Operations
            </span>
            <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: 'var(--accent-emerald)' }}></span>
          </div>
          <h1 style={{ fontSize: '2rem', fontWeight: 800, letterSpacing: '-0.03em', color: 'white' }}>
            Bảng Điều Hành Doanh Nghiệp
          </h1>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.95rem' }}>
            Theo dõi thời gian thực các biểu mẫu động, quy trình duyệt đơn và lịch sử kiểm toán của tổ chức.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem' }}>
          <div
            style={{
              display: 'flex',
              background: 'rgba(255, 255, 255, 0.05)',
              borderRadius: 'var(--radius-md)',
              padding: '0.2rem',
              border: '1px solid var(--border-subtle)',
            }}
          >
            <button
              onClick={() => setFilterDays(7)}
              style={{
                padding: '0.4rem 0.8rem',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                background: filterDays === 7 ? 'var(--primary)' : 'transparent',
                color: filterDays === 7 ? 'white' : 'var(--text-secondary)',
                fontWeight: 600,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
            >
              7 Ngày
            </button>
            <button
              onClick={() => setFilterDays(30)}
              style={{
                padding: '0.4rem 0.8rem',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                background: filterDays === 30 ? 'var(--primary)' : 'transparent',
                color: filterDays === 30 ? 'white' : 'var(--text-secondary)',
                fontWeight: 600,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
            >
              30 Ngày
            </button>
          </div>

          <button className="btn btn-primary">
            <Plus size={16} /> Tạo Form Động Mới
          </button>
        </div>
      </div>

      {/* Metrics Row */}
      <div className="dashboard-grid">
        <MetricCard
          title="Tổng Biểu Mẫu"
          value="14"
          trend="+3 form trong tháng"
          icon={<FileText size={22} />}
          accentColor="var(--accent-cyan)"
        />
        <MetricCard
          title="Tổng Đơn Đã Nộp"
          value="186"
          trend="+28.4% so với kỳ trước"
          icon={<Send size={22} />}
          accentColor="var(--primary-light)"
        />
        <MetricCard
          title="Tỷ Lệ Phê Duyệt"
          value="87.5%"
          trend="Thời gian xử lý: 2.4 giờ"
          icon={<CheckCircle size={22} />}
          accentColor="var(--accent-emerald)"
        />
        <MetricCard
          title="Nhân Sự Hoạt Động"
          value="48"
          trend="100% active member"
          icon={<Users size={22} />}
          accentColor="var(--accent-purple)"
        />
      </div>

      {/* Two Columns Section */}
      <div className="content-columns">
        {/* Left Column: Recent Submissions & Activity */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.75rem' }}>
          {/* Submissions Table Card */}
          <div className="glass-card" style={{ padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <div>
                <h3 style={{ fontSize: '1.15rem', fontWeight: 700, color: 'white' }}>Danh Sách Bài Nộp Chờ Duyệt</h3>
                <p style={{ fontSize: '0.825rem', color: 'var(--text-muted)' }}>Cập nhật theo thời gian thực từ các phòng ban</p>
              </div>
              <button className="btn btn-secondary btn-sm">
                <Filter size={14} /> Lọc dữ liệu
              </button>
            </div>

            <div className="table-wrapper">
              <table className="custom-table">
                <thead>
                  <tr>
                    <th>Tên Biểu Mẫu</th>
                    <th>Người Nộp</th>
                    <th>Nội Dung Tóm Tắt</th>
                    <th>Trạng Thái</th>
                    <th style={{ textAlign: 'right' }}>Thao Tác</th>
                  </tr>
                </thead>
                <tbody>
                  {submissions.map((sub) => (
                    <tr key={sub.id}>
                      <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>
                        <div>{sub.formTitle}</div>
                        <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>
                          {sub.formCode}
                        </div>
                      </td>
                      <td>
                        <div style={{ color: 'var(--text-primary)' }}>{sub.submittedByName}</div>
                        <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                          {new Date(sub.createdAt).toLocaleDateString('vi-VN')}
                        </div>
                      </td>
                      <td style={{ maxWidth: '220px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {Object.entries(sub.data)
                          .map(([k, v]) => `${k}: ${v}`)
                          .join(' | ')}
                      </td>
                      <td>
                        <StatusBadge type="submission" status={sub.status} />
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        {sub.status === 0 ? (
                          <div style={{ display: 'inline-flex', gap: '0.4rem' }}>
                            <button
                              onClick={() => handleReview(sub.id, 1)}
                              className="btn btn-sm"
                              style={{ background: 'rgba(16, 185, 129, 0.15)', color: '#34d399', border: '1px solid rgba(16, 185, 129, 0.3)' }}
                            >
                              Duyệt
                            </button>
                            <button
                              onClick={() => handleReview(sub.id, 2)}
                              className="btn btn-sm"
                              style={{ background: 'rgba(244, 63, 94, 0.15)', color: '#fb7185', border: '1px solid rgba(244, 63, 94, 0.3)' }}
                            >
                              Từ chối
                            </button>
                          </div>
                        ) : (
                          <button className="btn btn-secondary btn-sm">
                            <Eye size={13} /> Chi tiết
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Activity Trend Bar Chart */}
          <div className="glass-card" style={{ padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
              <div>
                <h3 style={{ fontSize: '1.15rem', fontWeight: 700, color: 'white' }}>Xu Hướng Nộp Đơn 7 Ngày Qua</h3>
                <p style={{ fontSize: '0.825rem', color: 'var(--text-muted)' }}>Tần suất tương tác trung bình 21 bài nộp / ngày</p>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', color: 'var(--accent-emerald)', fontSize: '0.85rem', fontWeight: 600 }}>
                <TrendingUp size={16} /> +18% tăng trưởng
              </div>
            </div>

            {/* Custom Bar Visualizer */}
            <div
              style={{
                display: 'flex',
                alignItems: 'flex-end',
                justifyContent: 'space-between',
                height: '160px',
                gap: '1rem',
                paddingTop: '1rem',
                borderBottom: '1px solid var(--border-subtle)',
                paddingBottom: '0.5rem',
              }}
            >
              {trendData.map((item, idx) => (
                <div
                  key={idx}
                  style={{
                    flex: 1,
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    height: '100%',
                    justifyContent: 'flex-end',
                    gap: '0.5rem',
                  }}
                >
                  <span style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-muted)' }}>{item.count}</span>
                  <div
                    style={{
                      width: '100%',
                      maxWidth: '42px',
                      height: item.height,
                      background: 'linear-gradient(180deg, var(--primary) 0%, rgba(99, 102, 241, 0.25) 100%)',
                      borderRadius: '4px 4px 0 0',
                      transition: 'height 0.4s ease',
                      boxShadow: '0 0 12px rgba(99, 102, 241, 0.2)',
                    }}
                  ></div>
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>{item.day}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Right Column: Top Forms & Quick Actions & System Info */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.75rem' }}>
          {/* Quick Actions Card */}
          <div className="glass-card" style={{ padding: '1.5rem' }}>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white', marginBottom: '1rem' }}>Thao Tác Nhanh</h3>

            <div className="quick-action-item">
              <div className="quick-action-icon">
                <Plus size={20} />
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: '0.9rem', color: 'white' }}>Tạo Biểu Mẫu Mới</div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Thiết kế schema động không cần code</div>
              </div>
              <ArrowUpRight size={16} color="var(--text-muted)" />
            </div>

            <div className="quick-action-item">
              <div className="quick-action-icon" style={{ color: 'var(--accent-purple)' }}>
                <ShieldAlert size={20} />
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: '0.9rem', color: 'white' }}>Xem Nhật Ký Kiểm Toán</div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Truy vết mọi thay đổi dữ liệu</div>
              </div>
              <ArrowUpRight size={16} color="var(--text-muted)" />
            </div>

            <div className="quick-action-item">
              <div className="quick-action-icon" style={{ color: 'var(--accent-emerald)' }}>
                <Download size={20} />
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: '0.9rem', color: 'white' }}>Xuất Dữ Liệu Báo Cáo</div>
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Tải file CSV/JSON phân tích</div>
              </div>
              <ArrowUpRight size={16} color="var(--text-muted)" />
            </div>
          </div>

          {/* Top Active Forms Widget */}
          <div className="glass-card" style={{ padding: '1.5rem' }}>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'white', marginBottom: '1rem' }}>Top Biểu Mẫu Tích Cực</h3>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {topForms.map((form, idx) => (
                <div key={idx}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', marginBottom: '0.35rem' }}>
                    <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{form.title}</span>
                    <span style={{ color: 'var(--primary-light)', fontWeight: 700 }}>{form.count} đơn</span>
                  </div>
                  <div
                    style={{
                      height: '6px',
                      width: '100%',
                      background: 'rgba(255, 255, 255, 0.06)',
                      borderRadius: '999px',
                      overflow: 'hidden',
                    }}
                  >
                    <div
                      style={{
                        height: '100%',
                        width: `${form.percentage}%`,
                        background: 'linear-gradient(90deg, var(--primary) 0%, var(--accent-cyan) 100%)',
                        borderRadius: '999px',
                      }}
                    ></div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* System Security Status */}
          <div
            className="glass-card"
            style={{
              padding: '1.25rem',
              background: 'linear-gradient(135deg, rgba(99, 102, 241, 0.08) 0%, rgba(16, 22, 34, 0.7) 100%)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
              <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: 'var(--accent-emerald)' }}></span>
              <span style={{ fontWeight: 700, fontSize: '0.85rem', color: 'white' }}>Nền Tảng .NET 10 Đang Hoạt Động</span>
            </div>
            <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', lineHeight: 1.4 }}>
              Hệ thống được bảo vệ bởi cơ chế Tenant Isolation, PostgreSQL JSONB Engine và đã vượt qua 100% các bài kiểm thử tự động.
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
