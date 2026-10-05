import type {
  AuthResponse,
  Company,
  CompanyFormAnalytics,
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
} from '../types'

const BASE_URL = '' // Uses Vite proxy config

class ApiClient {
  private token: string | null = null
  private unauthorizedListeners: Array<() => void> = []

  constructor() {
    this.token = localStorage.getItem('nexaops_token')
  }

  setToken(token: string) {
    this.token = token
    localStorage.setItem('nexaops_token', token)
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

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const headers = new Headers(options.headers || {})
    headers.set('Content-Type', 'application/json')

    if (this.token) {
      headers.set('Authorization', `Bearer ${this.token}`)
    }

    try {
      const response = await fetch(`${BASE_URL}${endpoint}`, {
        ...options,
        headers,
      })

      if (!response.ok) {
        if (response.status === 401) {
          this.unauthorizedListeners.forEach((l) => l())
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

      return await response.json()
    } catch (err: any) {
      console.warn(`API request to ${endpoint} failed:`, err.message)
      throw err
    }
  }

  // Auth APIs
  async register(name: string, email: string, password: string): Promise<AuthResponse> {
    const res = await this.request<AuthResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ name, email, password }),
    })
    this.setToken(res.accessToken)
    return res
  }

  async login(email: string, password: string): Promise<AuthResponse> {
    const res = await this.request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    this.setToken(res.accessToken)
    return res
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
