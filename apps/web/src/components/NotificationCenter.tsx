import React, { useState, useEffect, useRef } from 'react'
import { api } from '../services/api'
import { signalRService } from '../services/signalr'
import type { AppNotification } from '../types'
import {
  Bell,
  CheckCheck,
  CheckCircle2,
  XCircle,
  Clock,
  FileSpreadsheet,
  AlertCircle,
  RefreshCw,
  ExternalLink,
  Check,
  Trash2,
} from 'lucide-react'

interface NotificationCenterProps {
  companyId?: string
  onNavigateToSubmissions?: () => void
}

export const NotificationCenter: React.FC<NotificationCenterProps> = ({
  companyId,
  onNavigateToSubmissions,
}) => {
  const [isOpen, setIsOpen] = useState(false)
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [activeFilter, setActiveFilter] = useState<'all' | 'unread'>('all')
  const [isLoading, setIsLoading] = useState(false)
  const [isMarkingAll, setIsMarkingAll] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

  const fetchUnreadCount = async () => {
    if (!companyId) return
    try {
      const res = await api.getUnreadNotificationCount(companyId)
      setUnreadCount(res.unreadCount || 0)
    } catch {
      // Ignore background poll errors
    }
  }

  const fetchNotifications = async () => {
    if (!companyId) return
    setIsLoading(true)
    try {
      const res = await api.getNotifications(
        companyId,
        activeFilter === 'unread' ? false : undefined,
        1,
        25
      )
      setNotifications(res.items || [])
      setUnreadCount(res.unreadCount || 0)
    } catch (err) {
      console.warn('Failed to fetch notifications:', err)
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    fetchUnreadCount()
    const interval = setInterval(fetchUnreadCount, 30000)
    return () => clearInterval(interval)
  }, [companyId])

  // SignalR real-time notification receiver
  useEffect(() => {
    signalRService.startConnection()
    if (companyId) {
      signalRService.joinCompany(companyId)
    }

    const unsubscribe = signalRService.onNotification((newNotif) => {
      setNotifications((prev) => [newNotif, ...prev])
      setUnreadCount((c) => c + 1)
    })

    return () => {
      unsubscribe()
    }
  }, [companyId])

  useEffect(() => {
    if (isOpen && companyId) {
      fetchNotifications()
    }
  }, [isOpen, activeFilter, companyId])

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false)
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [isOpen])

  const handleMarkAsRead = async (id: string, e?: React.MouseEvent) => {
    e?.stopPropagation()
    try {
      await api.markNotificationAsRead(id)
      setNotifications((prev) =>
        prev.map((n) => (n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n))
      )
      setUnreadCount((c) => Math.max(0, c - 1))
    } catch (err) {
      console.error('Failed to mark notification as read:', err)
    }
  }

  const handleMarkAllAsRead = async () => {
    if (!companyId || isMarkingAll) return
    setIsMarkingAll(true)
    try {
      await api.markAllNotificationsAsRead(companyId)
      setNotifications((prev) =>
        prev.map((n) => ({ ...n, isRead: true, readAt: new Date().toISOString() }))
      )
      setUnreadCount(0)
    } catch (err) {
      console.error('Failed to mark all notifications as read:', err)
    } finally {
      setIsMarkingAll(false)
    }
  }

  const handleDeleteNotification = async (id: string, e?: React.MouseEvent) => {
    e?.stopPropagation()
    try {
      await api.deleteNotification(id)
      const target = notifications.find((n) => n.id === id)
      setNotifications((prev) => prev.filter((n) => n.id !== id))
      if (target && !target.isRead) {
        setUnreadCount((c) => Math.max(0, c - 1))
      }
    } catch (err) {
      console.error('Failed to delete notification:', err)
    }
  }

  const handleClearAll = async () => {
    if (!companyId || notifications.length === 0) return
    try {
      await api.clearAllNotifications(companyId)
      setNotifications([])
      setUnreadCount(0)
    } catch (err) {
      console.error('Failed to clear notifications:', err)
    }
  }

  const handleNotificationClick = (notification: AppNotification) => {
    if (!notification.isRead) {
      handleMarkAsRead(notification.id)
    }
    if (onNavigateToSubmissions) {
      onNavigateToSubmissions()
      setIsOpen(false)
    }
  }

  const getNotificationIcon = (type: number) => {
    switch (type) {
      case 0: // FormSubmitted
        return <FileSpreadsheet size={16} color="var(--primary-light)" />
      case 1: // StepWaitingApproval
        return <Clock size={16} color="#fbbf24" />
      case 2: // StepApproved
        return <CheckCircle2 size={16} color="var(--accent-emerald)" />
      case 3: // StepRejected
        return <XCircle size={16} color="var(--accent-rose)" />
      case 4: // FormCompleted
        return <CheckCircle2 size={16} color="var(--accent-emerald)" />
      case 5: // FormRejected
        return <XCircle size={16} color="var(--accent-rose)" />
      default:
        return <AlertCircle size={16} color="var(--accent-cyan)" />
    }
  }

  const formatRelativeTime = (isoString: string) => {
    try {
      const date = new Date(isoString)
      const now = new Date()
      const diffMs = now.getTime() - date.getTime()
      const diffMin = Math.floor(diffMs / 60000)
      if (diffMin < 1) return 'Vừa xong'
      if (diffMin < 60) return `${diffMin} phút trước`
      const diffHours = Math.floor(diffMin / 60)
      if (diffHours < 24) return `${diffHours} giờ trước`
      const diffDays = Math.floor(diffHours / 24)
      if (diffDays < 7) return `${diffDays} ngày trước`
      return date.toLocaleDateString('vi-VN')
    } catch {
      return isoString
    }
  }

  return (
    <div style={{ position: 'relative' }} ref={dropdownRef}>
      {/* Bell Trigger Button */}
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        style={{
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: '38px',
          height: '38px',
          borderRadius: 'var(--radius-md)',
          background: isOpen ? 'rgba(99, 102, 241, 0.2)' : 'rgba(255, 255, 255, 0.04)',
          border: '1px solid',
          borderColor: isOpen ? 'var(--primary)' : 'var(--border-subtle)',
          cursor: 'pointer',
          color: isOpen ? 'var(--primary-light)' : 'var(--text-secondary)',
          transition: 'all 0.2s ease',
        }}
        title="Trung tâm thông báo"
      >
        <Bell size={18} />
        {unreadCount > 0 && (
          <span
            style={{
              position: 'absolute',
              top: '-4px',
              right: '-4px',
              minWidth: '18px',
              height: '18px',
              padding: '0 4px',
              borderRadius: '9px',
              background: 'linear-gradient(135deg, #ef4444 0%, #f43f5e 100%)',
              color: 'white',
              fontSize: '0.68rem',
              fontWeight: 800,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 0 8px rgba(244, 63, 94, 0.6)',
              animation: 'pulse 2s infinite',
            }}
          >
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {/* Notification Dropdown Panel */}
      {isOpen && (
        <div
          style={{
            position: 'absolute',
            top: 'calc(100% + 10px)',
            right: 0,
            width: '380px',
            maxWidth: '92vw',
            maxHeight: '520px',
            background: 'var(--bg-secondary)',
            border: '1px solid var(--border-hover)',
            borderRadius: 'var(--radius-lg)',
            boxShadow: '0 16px 40px rgba(0, 0, 0, 0.7), 0 0 20px rgba(99, 102, 241, 0.1)',
            zIndex: 300,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          }}
        >
          {/* Header */}
          <div
            style={{
              padding: '0.9rem 1rem',
              borderBottom: '1px solid var(--border-subtle)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              background: 'rgba(255, 255, 255, 0.02)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Bell size={17} color="var(--primary-light)" />
              <span style={{ fontWeight: 700, color: 'white', fontSize: '0.92rem' }}>
                Thông Báo
              </span>
              {unreadCount > 0 && (
                <span
                  style={{
                    background: 'rgba(99, 102, 241, 0.2)',
                    color: 'var(--primary-light)',
                    fontSize: '0.72rem',
                    fontWeight: 700,
                    padding: '0.1rem 0.45rem',
                    borderRadius: '10px',
                    border: '1px solid rgba(99, 102, 241, 0.4)',
                  }}
                >
                  {unreadCount} mới
                </span>
              )}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              {unreadCount > 0 && (
                <button
                  type="button"
                  onClick={handleMarkAllAsRead}
                  disabled={isMarkingAll}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.25rem',
                    background: 'none',
                    border: 'none',
                    color: 'var(--accent-cyan)',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    cursor: 'pointer',
                    padding: '0.2rem 0.4rem',
                    borderRadius: 'var(--radius-sm)',
                  }}
                  title="Đánh dấu tất cả đã đọc"
                >
                  <CheckCheck size={14} />
                  {isMarkingAll ? 'Đang lưu...' : 'Đã đọc hết'}
                </button>
              )}
              {notifications.length > 0 && (
                <button
                  type="button"
                  onClick={handleClearAll}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.2rem',
                    background: 'none',
                    border: 'none',
                    color: 'var(--text-muted)',
                    fontSize: '0.75rem',
                    cursor: 'pointer',
                    padding: '0.2rem 0.35rem',
                    borderRadius: 'var(--radius-sm)',
                  }}
                  title="Xóa toàn bộ thông báo"
                  onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--accent-rose)')}
                  onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--text-muted)')}
                >
                  <Trash2 size={13} />
                  Xóa hết
                </button>
              )}
              <button
                type="button"
                onClick={fetchNotifications}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  cursor: 'pointer',
                  padding: '0.2rem',
                }}
                title="Làm mới thông báo"
              >
                <RefreshCw size={13} className={isLoading ? 'spin' : ''} />
              </button>
            </div>
          </div>

          {/* Filter Bar */}
          <div
            style={{
              display: 'flex',
              padding: '0.5rem 1rem',
              gap: '0.5rem',
              borderBottom: '1px solid var(--border-subtle)',
              background: 'rgba(0, 0, 0, 0.1)',
            }}
          >
            <button
              type="button"
              onClick={() => setActiveFilter('all')}
              style={{
                background: activeFilter === 'all' ? 'var(--primary)' : 'transparent',
                color: activeFilter === 'all' ? 'white' : 'var(--text-secondary)',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                padding: '0.25rem 0.6rem',
                fontSize: '0.75rem',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Tất cả
            </button>
            <button
              type="button"
              onClick={() => setActiveFilter('unread')}
              style={{
                background: activeFilter === 'unread' ? 'var(--primary)' : 'transparent',
                color: activeFilter === 'unread' ? 'white' : 'var(--text-secondary)',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                padding: '0.25rem 0.6rem',
                fontSize: '0.75rem',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Chưa đọc {unreadCount > 0 ? `(${unreadCount})` : ''}
            </button>
          </div>

          {/* Notifications List */}
          <div
            style={{
              flex: 1,
              overflowY: 'auto',
              maxHeight: '360px',
            }}
          >
            {isLoading && notifications.length === 0 ? (
              <div style={{ padding: '2rem', textAlign: 'center', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                Đang tải thông báo...
              </div>
            ) : notifications.length === 0 ? (
              <div
                style={{
                  padding: '3rem 1.5rem',
                  textAlign: 'center',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  gap: '0.75rem',
                }}
              >
                <div
                  style={{
                    width: '44px',
                    height: '44px',
                    borderRadius: '50%',
                    background: 'rgba(255, 255, 255, 0.04)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: 'var(--text-muted)',
                  }}
                >
                  <Bell size={20} />
                </div>
                <div style={{ color: 'var(--text-secondary)', fontSize: '0.85rem', fontWeight: 600 }}>
                  Không có thông báo nào
                </div>
                <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem', maxWidth: '240px' }}>
                  {activeFilter === 'unread'
                    ? 'Bạn đã đọc hết tất cả thông báo.'
                    : 'Các thông báo về phê duyệt hồ sơ và cập nhật hệ thống sẽ hiển thị tại đây.'}
                </div>
              </div>
            ) : (
              notifications.map((n) => (
                <div
                  key={n.id}
                  onClick={() => handleNotificationClick(n)}
                  style={{
                    padding: '0.85rem 1rem',
                    borderBottom: '1px solid var(--border-subtle)',
                    background: n.isRead ? 'transparent' : 'rgba(99, 102, 241, 0.06)',
                    cursor: 'pointer',
                    display: 'flex',
                    gap: '0.75rem',
                    alignItems: 'flex-start',
                    transition: 'background 0.15s ease',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = n.isRead
                      ? 'rgba(255, 255, 255, 0.03)'
                      : 'rgba(99, 102, 241, 0.12)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = n.isRead
                      ? 'transparent'
                      : 'rgba(99, 102, 241, 0.06)'
                  }}
                >
                  {/* Icon */}
                  <div
                    style={{
                      width: '32px',
                      height: '32px',
                      borderRadius: 'var(--radius-sm)',
                      background: 'rgba(255, 255, 255, 0.05)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      flexShrink: 0,
                      marginTop: '0.1rem',
                    }}
                  >
                    {getNotificationIcon(n.type)}
                  </div>

                  {/* Body */}
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: '0.4rem',
                        marginBottom: '0.2rem',
                      }}
                    >
                      <span
                        style={{
                          fontWeight: n.isRead ? 600 : 700,
                          color: n.isRead ? 'var(--text-primary)' : 'white',
                          fontSize: '0.84rem',
                          lineHeight: 1.3,
                        }}
                      >
                        {n.title}
                      </span>
                      {!n.isRead && (
                        <span
                          style={{
                            width: '8px',
                            height: '8px',
                            borderRadius: '50%',
                            background: 'var(--primary-light)',
                            flexShrink: 0,
                          }}
                        />
                      )}
                    </div>

                    <p
                      style={{
                        fontSize: '0.78rem',
                        color: 'var(--text-secondary)',
                        margin: '0 0 0.35rem 0',
                        lineHeight: 1.4,
                        wordBreak: 'break-word',
                      }}
                    >
                      {n.message}
                    </p>

                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        fontSize: '0.7rem',
                        color: 'var(--text-muted)',
                      }}
                    >
                      <span>{formatRelativeTime(n.createdAt)}</span>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        {!n.isRead && (
                          <button
                            type="button"
                            onClick={(e) => handleMarkAsRead(n.id, e)}
                            style={{
                              background: 'none',
                              border: 'none',
                              color: 'var(--primary-light)',
                              fontSize: '0.7rem',
                              fontWeight: 600,
                              cursor: 'pointer',
                              display: 'flex',
                              alignItems: 'center',
                              gap: '0.2rem',
                              padding: 0,
                            }}
                          >
                            <Check size={11} /> Đã đọc
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={(e) => handleDeleteNotification(n.id, e)}
                          title="Xóa thông báo này"
                          style={{
                            background: 'none',
                            border: 'none',
                            color: 'var(--text-muted)',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            padding: 0,
                          }}
                          onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--accent-rose)')}
                          onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--text-muted)')}
                        >
                          <Trash2 size={12} />
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>

          {/* Footer */}
          {notifications.length > 0 && onNavigateToSubmissions && (
            <div
              style={{
                padding: '0.65rem 1rem',
                borderTop: '1px solid var(--border-subtle)',
                background: 'rgba(255, 255, 255, 0.02)',
                textAlign: 'center',
              }}
            >
              <button
                type="button"
                onClick={() => {
                  onNavigateToSubmissions()
                  setIsOpen(false)
                }}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--primary-light)',
                  fontSize: '0.78rem',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '0.35rem',
                }}
              >
                Xem Hộp thư phê duyệt hồ sơ <ExternalLink size={12} />
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
