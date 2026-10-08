export type UserRole = 'User' | 'Admin'

export interface User {
  id: string
  name: string
  email: string
  role: UserRole
  avatar?: string
  phone?: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface AuthResponse {
  accessToken: string
  message?: string
  user: User
}

export type MemberRole = 'Owner' | 'Admin' | 'Manager' | 'Member'
export type MemberStatus = 'Invited' | 'Active' | 'Inactive'

export interface Company {
  id: string
  name: string
  slug: string
  code?: string
  logo?: string
  description?: string
  ownerId: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateCompanyRequest {
  name: string
  slug?: string
  code?: string
  logo?: string
  description?: string
}

export interface TransferOwnershipRequest {
  newOwnerId: string
  password: string
  previousOwnerNewRole: number // 1: Admin, 2: Manager, 3: Member
}

export interface CompanyMember {
  id: string
  companyId: string
  userId?: string
  user?: User
  userName?: string
  userEmail?: string
  email: string
  role: string
  status: string
  departmentId?: string
  department?: Department
  inviteExpiresAt?: string
  invitedById?: string
  invitedBy?: User
  createdAt: string
  updatedAt: string
}

export interface InviteMemberRequest {
  email: string
  role?: number
  departmentId?: string
}

export interface InviteMemberResponse {
  message: string
  member: CompanyMember
  inviteLink: string
}

export interface AcceptInviteRequest {
  inviteToken: string
}

export interface AcceptInviteResponse {
  message: string
  company: Company
  role: string
}

export interface UpdateMemberRequest {
  role?: number
  status?: number
  departmentId?: string
}


export interface Department {
  id: string
  companyId: string
  name: string
  code?: string
  description?: string
  parentId?: string
  managerId?: string
  manager?: User
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface DepartmentTreeNode extends Department {
  children: DepartmentTreeNode[]
}

export interface CreateDepartmentRequest {
  name: string
  code?: string
  parentId?: string
  managerId?: string
  description?: string
}

export interface UpdateDepartmentRequest {
  name?: string
  code?: string
  parentId?: string
  managerId?: string
  description?: string
  isActive?: boolean
}

export type FormStatus = 0 | 1 | 2 // 0: Draft, 1: Published, 2: Archived
export type FormFieldType = 0 | 1 | 2 | 3 | 4 | 5 | 6 // Text, Number, Date, Select, Checkbox, Textarea, Email

export interface FormField {
  id: string
  name: string
  label: string
  type: FormFieldType
  required: boolean
  placeholder?: string
  defaultValue?: unknown
  options?: string[]
  min?: number
  max?: number
  pattern?: string
}

export interface FormDefinition {
  id: string
  companyId: string
  departmentId?: string
  departmentName?: string
  title: string
  code: string
  description?: string
  status: FormStatus
  version: number
  fields: FormField[]
  createdById: string
  createdByName?: string
  createdAt: string
  updatedAt: string
}

export type SubmissionStatus = 0 | 1 | 2 | 3 // 0: Pending, 1: Approved, 2: Rejected, 3: Draft

export interface FormSubmission {
  id: string
  formDefinitionId: string
  formTitle: string
  formCode: string
  companyId: string
  submittedById: string
  submittedByName?: string
  data: Record<string, unknown>
  status: SubmissionStatus
  reviewNotes?: string
  reviewedById?: string
  reviewedByName?: string
  reviewedAt?: string
  createdAt: string
  updatedAt: string
}

export interface DailySubmissionMetric {
  date: string
  count: number
}

export interface FormUsageMetric {
  formId: string
  title: string
  code: string
  submissionCount: number
}

export interface CompanyFormAnalytics {
  companyId: string
  totalForms: number
  draftForms: number
  publishedForms: number
  archivedForms: number
  totalSubmissions: number
  pendingSubmissions: number
  approvedSubmissions: number
  rejectedSubmissions: number
  approvalRatePercentage: number
  recentDailySubmissions: DailySubmissionMetric[]
  topActiveForms: FormUsageMetric[]
}

export interface AuditLog {
  id: string
  companyId: string
  actorId?: string
  actorName?: string
  actorEmail?: string
  action: string
  entityType: string
  entityId: string
  details: Record<string, unknown>
  ipAddress?: string
  userAgent?: string
  createdAt: string
}

export interface PagedResult<T> {
  items: T[]
  meta: {
    total: number
    page: number
    limit: number
    totalPages: number
  }
}
