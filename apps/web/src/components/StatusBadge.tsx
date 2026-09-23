import React from 'react'
import { Clock, CheckCircle2, XCircle, FileEdit, Globe } from 'lucide-react'

interface StatusBadgeProps {
  type: 'form' | 'submission'
  status: number // 0, 1, 2, 3
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({ type, status }) => {
  if (type === 'form') {
    switch (status) {
      case 1:
        return (
          <span className="badge badge-published">
            <Globe size={12} /> Published
          </span>
        )
      case 2:
        return (
          <span className="badge badge-draft">
            <FileEdit size={12} /> Archived
          </span>
        )
      case 0:
      default:
        return (
          <span className="badge badge-draft">
            <FileEdit size={12} /> Draft
          </span>
        )
    }
  }

  // Submission Status
  switch (status) {
    case 1:
      return (
        <span className="badge badge-approved">
          <CheckCircle2 size={12} /> Approved
        </span>
      )
    case 2:
      return (
        <span className="badge badge-rejected">
          <XCircle size={12} /> Rejected
        </span>
      )
    case 0:
    default:
      return (
        <span className="badge badge-pending">
          <Clock size={12} /> Pending
        </span>
      )
  }
}
