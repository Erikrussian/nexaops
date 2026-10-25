import * as signalR from '@microsoft/signalr'
import { api } from './api'
import type { AppNotification } from '../types'

type NotificationListener = (notification: AppNotification) => void
type WorkflowListener = (payload: any) => void

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private notificationListeners: NotificationListener[] = []
  private workflowListeners: WorkflowListener[] = []
  private isConnecting = false

  async startConnection(): Promise<void> {
    const token = api.getToken()
    if (!token) return

    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return
    }

    if (this.isConnecting) return
    this.isConnecting = true

    try {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications', {
          accessTokenFactory: () => api.getToken() || '',
          skipNegotiation: false,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build()

      this.connection.on('ReceiveNotification', (notification: AppNotification) => {
        this.notificationListeners.forEach((listener) => listener(notification))
      })

      this.connection.on('WorkflowStepUpdated', (payload: any) => {
        this.workflowListeners.forEach((listener) => listener(payload))
      })

      await this.connection.start()
    } catch (err) {
      console.warn('SignalR connection failed (will retry or fallback to polling):', err)
    } finally {
      this.isConnecting = false
    }
  }

  async stopConnection(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop()
      } catch {
        // Ignore stop error
      }
      this.connection = null
    }
  }

  async joinCompany(companyId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinCompany', companyId)
      } catch (err) {
        console.warn('Failed to join company group via SignalR:', err)
      }
    }
  }

  onNotification(listener: NotificationListener): () => void {
    this.notificationListeners.push(listener)
    return () => {
      this.notificationListeners = this.notificationListeners.filter((l) => l !== listener)
    }
  }

  onWorkflowEvent(listener: WorkflowListener): () => void {
    this.workflowListeners.push(listener)
    return () => {
      this.workflowListeners = this.workflowListeners.filter((l) => l !== listener)
    }
  }
}

export const signalRService = new SignalRService()
