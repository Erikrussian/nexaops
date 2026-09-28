import type {
  AuthResponse,
  Company,
  CompanyFormAnalytics,
  Department,
  FormDefinition,
  FormSubmission,
  AuditLog,
  PagedResult,
} from '../types'

const BASE_URL = '' // Uses Vite proxy config

class ApiClient {
  private token: string | null = null

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
        let errorData: any
        try {
          errorData = await response.json()
        } catch {
          errorData = { detail: response.statusText }
        }
        throw new Error(errorData.detail || errorData.message || `Request failed with status ${response.status}`)
      }

      return await response.json()
    } catch (err: any) {
      console.warn(`API request to ${endpoint} failed:`, err.message)
      throw err
    }
  }

  // Auth APIs
  async login(email: string, password: string): Promise<AuthResponse> {
    const res = await this.request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    this.setToken(res.accessToken)
    return res
  }

  // Companies APIs
  async getMyCompanies(): Promise<Company[]> {
    return this.request<Company[]>('/companies')
  }

  async getCompanyById(id: string): Promise<Company> {
    return this.request<Company>(`/companies/${id}`)
  }

  // Departments APIs
  async getDepartments(companyId: string): Promise<Department[]> {
    return this.request<Department[]>(`/companies/${companyId}/departments`)
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
  async getAuditLogs(companyId: string, page = 1, pageSize = 10): Promise<PagedResult<AuditLog>> {
    return this.request<PagedResult<AuditLog>>(`/companies/${companyId}/audit-logs?page=${page}&pageSize=${pageSize}`)
  }
}

export const api = new ApiClient()
