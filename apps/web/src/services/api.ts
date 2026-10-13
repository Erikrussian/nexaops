import type {
  AuthResponse,
  Company,
  CompanyFormAnalytics,
  FormDetailAnalytics,
  CompanyMember,
  CreateCompanyRequest,
  TransferOwnershipRequest,
  Department,
  DepartmentTreeNode,
  CreateDepartmentRequest,
  UpdateDepartmentRequest,
  FormDefinition,
  FormSubmission,
  AuditLog,
  PagedResult,
  User,
  InviteMemberRequest,
  InviteMemberResponse,
  AcceptInviteRequest,
  AcceptInviteResponse,
  UpdateMemberRequest,
  UserSession,
} from '../types'

const BASE_URL = '' // Uses Vite proxy config

class ApiClient {
  private token: string | null = null
  private isRefreshing: boolean = false
  private refreshQueue: Array<(token: string | null, error?: Error) => void> = []
  private unauthorizedListeners: Array<() => void> = []

  constructor() {
    this.token = localStorage.getItem('nexaops_token')
  }

  setToken(token: string | null) {
    this.token = token
    if (token) {
      localStorage.setItem('nexaops_token', token)
    } else {
      localStorage.removeItem('nexaops_token')
    }
  }

  clearToken() {
    this.token = null
    localStorage.removeItem('nexaops_token')
  }

  getToken(): string | null {
    return this.token
  }

  onUnauthorized(listener: () => void) {
    this.unauthorizedListeners.push(listener)
  }

  removeUnauthorized(listener: () => void) {
    this.unauthorizedListeners = this.unauthorizedListeners.filter((l) => l !== listener)
  }

  private async request<T>(endpoint: string, options: RequestInit = {}, isRetry: boolean = false): Promise<T> {
    const headers = new Headers(options.headers || {})
    headers.set('Content-Type', 'application/json')

    if (this.token) {
      headers.set('Authorization', `Bearer ${this.token}`)
    }

    try {
      const response = await fetch(`${BASE_URL}${endpoint}`, {
        ...options,
        headers,
        credentials: 'include', // Ensures HttpOnly cookies (refreshToken) are attached
      })

      if (!response.ok) {
        // Handle 401 Unauthorized
        if (response.status === 401) {
          const isAuthEndpoint =
            endpoint.startsWith('/auth/login') ||
            endpoint.startsWith('/auth/register') ||
            endpoint.startsWith('/auth/refresh')

          // If not an auth endpoint and we haven't retried yet, perform transparent refresh
          if (!isAuthEndpoint && !isRetry) {
            return await this.handleUnauthorizedWithRefresh<T>(endpoint, options)
          }

          if (isAuthEndpoint && endpoint.startsWith('/auth/refresh')) {
            this.clearToken()
            this.unauthorizedListeners.forEach((l) => l())
          }
        }

        let errorData: any
        try {
          errorData = await response.json()
        } catch {
          errorData = { detail: response.statusText }
        }

        throw new Error(
          errorData.detail ||
          errorData.message ||
          errorData.title ||
          (errorData.errors ? Object.values(errorData.errors).flat().join(', ') : null) ||
          `Request failed with status ${response.status}`
        )
      }

      if (response.status === 204) {
        return undefined as T
      }

      return await response.json()
    } catch (err: any) {
      console.warn(`API request to ${endpoint} failed:`, err.message)
      throw err
    }
  }

  private async handleUnauthorizedWithRefresh<T>(endpoint: string, options: RequestInit): Promise<T> {
    if (this.isRefreshing) {
      return new Promise<T>((resolve, reject) => {
        this.refreshQueue.push((newToken, err) => {
          if (err || !newToken) {
            reject(err || new Error('Phiên đăng nhập đã hết hạn'))
          } else {
            resolve(this.request<T>(endpoint, options, true))
          }
        })
      })
    }

    this.isRefreshing = true

    try {
      const refreshRes = await this.refreshToken()
      const newToken = refreshRes.accessToken

      const queue = [...this.refreshQueue]
      this.refreshQueue = []
      queue.forEach((callback) => callback(newToken))

      return await this.request<T>(endpoint, options, true)
    } catch (refreshErr: any) {
      const queue = [...this.refreshQueue]
      this.refreshQueue = []
      queue.forEach((callback) => callback(null, refreshErr))

      this.clearToken()
      this.unauthorizedListeners.forEach((l) => l())
      throw refreshErr
    } finally {
      this.isRefreshing = false
    }
  }

  // Auth APIs
  async register(name: string, email: string, password: string): Promise<AuthResponse> {
    const res = await this.request<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ name, email, password }),
      credentials: 'include',
    })
    this.setToken(res.accessToken)
    return res
  }

  async login(email: string, password: string): Promise<AuthResponse> {
    const res = await this.request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
      credentials: 'include',
    })
    this.setToken(res.accessToken)
    return res
  }

  async refreshToken(): Promise<AuthResponse> {
    const res = await this.request<AuthResponse>('/auth/refresh', {
      method: 'POST',
      credentials: 'include',
    })
    this.setToken(res.accessToken)
    return res
  }

  async logout(): Promise<void> {
    try {
      await this.request<void>('/auth/logout', {
        method: 'POST',
        credentials: 'include',
      })
    } catch {
      // Ignore network errors during logout
    } finally {
      this.clearToken()
      this.unauthorizedListeners.forEach((l) => l())
    }
  }

  async logoutAll(): Promise<void> {
    try {
      await this.request<void>('/auth/logout-all', {
        method: 'POST',
        credentials: 'include',
      })
    } finally {
      this.clearToken()
      this.unauthorizedListeners.forEach((l) => l())
    }
  }

  async getSessions(): Promise<UserSession[]> {
    return this.request<UserSession[]>('/auth/sessions')
  }

  async revokeSession(sessionId: string): Promise<void> {
    await this.request<void>(`/auth/sessions/${sessionId}`, {
      method: 'DELETE',
    })
  }

  async getMe(): Promise<User> {
    return this.request<User>('/auth/me')
  }

  // Companies APIs
  async getMyCompanies(): Promise<Company[]> {
    return this.request<Company[]>('/companies')
  }

  async getCompanyById(id: string): Promise<Company> {
    return this.request<Company>(`/companies/${id}`)
  }

  async createCompany(data: CreateCompanyRequest): Promise<Company> {
    return this.request<Company>('/companies', {
      method: 'POST',
      body: JSON.stringify(data),
    })
  }

  async getCompanyMembers(companyId: string): Promise<CompanyMember[]> {
    return this.request<CompanyMember[]>(`/companies/${companyId}/members`)
  }

  async inviteMember(companyId: string, data: InviteMemberRequest): Promise<InviteMemberResponse> {
    return this.request<InviteMemberResponse>(`/companies/${companyId}/members/invite`, {
      method: 'POST',
      body: JSON.stringify(data),
    })
  }

  async acceptInvite(data: AcceptInviteRequest): Promise<AcceptInviteResponse> {
    return this.request<AcceptInviteResponse>('/company-members/accept-invite', {
      method: 'POST',
      body: JSON.stringify(data),
    })
  }

  async updateMember(memberId: string, data: UpdateMemberRequest): Promise<CompanyMember> {
    return this.request<CompanyMember>(`/company-members/${memberId}`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    })
  }

  async removeMember(memberId: string): Promise<{ message: string }> {
    return this.request<{ message: string }>(`/company-members/${memberId}`, {
      method: 'DELETE',
    })
  }

  async transferOwnership(companyId: string, data: TransferOwnershipRequest): Promise<Company> {
    return this.request<Company>(`/companies/${companyId}/transfer-ownership`, {
      method: 'POST',
      body: JSON.stringify(data),
    })
  }

  async deleteCompany(companyId: string): Promise<void> {
    return this.request<void>(`/companies/${companyId}`, {
      method: 'DELETE',
    })
  }

  // Departments APIs
  async getDepartments(companyId: string): Promise<Department[]> {
    return this.request<Department[]>(`/companies/${companyId}/departments`)
  }

  async getDepartmentsTree(companyId: string): Promise<DepartmentTreeNode[]> {
    return this.request<DepartmentTreeNode[]>(`/companies/${companyId}/departments?tree=true`)
  }

  async createDepartment(companyId: string, data: CreateDepartmentRequest): Promise<Department> {
    return this.request<Department>(`/companies/${companyId}/departments`, {
      method: 'POST',
      body: JSON.stringify(data),
    })
  }

  async updateDepartment(departmentId: string, data: UpdateDepartmentRequest): Promise<Department> {
    return this.request<Department>(`/departments/${departmentId}`, {
      method: 'PATCH',
      body: JSON.stringify(data),
    })
  }

  async deleteDepartment(departmentId: string): Promise<void> {
    return this.request<void>(`/departments/${departmentId}`, {
      method: 'DELETE',
    })
  }

  // Forms APIs
  async getForms(companyId: string): Promise<FormDefinition[]> {
    return this.request<FormDefinition[]>(`/companies/${companyId}/forms`)
  }

  async getFormById(companyId: string, formId: string): Promise<FormDefinition> {
    return this.request<FormDefinition>(`/companies/${companyId}/forms/${formId}`)
  }

  async createForm(
    companyId: string,
    dto: {
      title: string
      code: string
      description?: string
      departmentId?: string
      fields: import('../types').FormField[]
    }
  ): Promise<FormDefinition> {
    return this.request<FormDefinition>(`/companies/${companyId}/forms`, {
      method: 'POST',
      body: JSON.stringify(dto),
    })
  }

  async changeFormStatus(
    companyId: string,
    formId: string,
    status: import('../types').FormStatus
  ): Promise<FormDefinition> {
    return this.request<FormDefinition>(`/companies/${companyId}/forms/${formId}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    })
  }

  async deleteForm(companyId: string, formId: string): Promise<void> {
    return this.request<void>(`/companies/${companyId}/forms/${formId}`, {
      method: 'DELETE',
    })
  }

  // Submissions APIs
  async submitForm(
    companyId: string,
    formId: string,
    data: Record<string, unknown>
  ): Promise<FormSubmission> {
    return this.request<FormSubmission>(`/companies/${companyId}/forms/${formId}/submissions`, {
      method: 'POST',
      body: JSON.stringify({ data }),
    })
  }
  async getCompanySubmissions(companyId: string): Promise<FormSubmission[]> {
    return this.request<FormSubmission[]>(`/companies/${companyId}/submissions`)
  }

  async reviewSubmission(companyId: string, submissionId: string, status: number, notes?: string): Promise<FormSubmission> {
    return this.request<FormSubmission>(`/companies/${companyId}/submissions/${submissionId}/review`, {
      method: 'POST',
      body: JSON.stringify({ status, notes }),
    })
  }

  // Analytics APIs
  async getCompanyAnalytics(companyId: string, days = 30): Promise<CompanyFormAnalytics> {
    return this.request<CompanyFormAnalytics>(`/companies/${companyId}/forms/analytics/overview?days=${days}`)
  }

  async getFormAnalytics(companyId: string, formId: string): Promise<FormDetailAnalytics> {
    return this.request<FormDetailAnalytics>(`/companies/${companyId}/forms/${formId}/analytics`)
  }

  // Audit Logs APIs
  async getAuditLogs(
    companyId: string,
    filter?: {
      action?: string
      entityType?: string
      actorId?: string
      fromDate?: string
      toDate?: string
      page?: number
      pageSize?: number
    }
  ): Promise<PagedResult<AuditLog>> {
    const params = new URLSearchParams()
    if (filter?.page) params.set('page', filter.page.toString())
    if (filter?.pageSize) params.set('pageSize', filter.pageSize.toString())
    if (filter?.action) params.set('action', filter.action)
    if (filter?.entityType) params.set('entityType', filter.entityType)
    if (filter?.actorId) params.set('actorId', filter.actorId)
    if (filter?.fromDate) params.set('fromDate', filter.fromDate)
    if (filter?.toDate) params.set('toDate', filter.toDate)
    const qs = params.toString() ? `?${params.toString()}` : ''
    return this.request<PagedResult<AuditLog>>(`/companies/${companyId}/audit-logs${qs}`)
  }
}

export const api = new ApiClient()
