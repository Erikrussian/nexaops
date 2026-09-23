import React from 'react'

interface MetricCardProps {
  title: string
  value: string | number
  trend?: string
  icon: React.ReactNode
  accentColor?: string
}

export const MetricCard: React.FC<MetricCardProps> = ({
  title,
  value,
  trend,
  icon,
  accentColor = 'var(--primary)',
}) => {
  return (
    <div className="glass-card metric-card">
      <div className="metric-header">
        <span className="metric-title">{title}</span>
        <div className="metric-icon-box" style={{ color: accentColor }}>
          {icon}
        </div>
      </div>
      <div className="metric-value">{value}</div>
      {trend && <div className="metric-trend">{trend}</div>}
    </div>
  )
}
