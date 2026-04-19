import { StateCreator } from 'zustand'
import { generateId } from '../generateId'

export type NotificationType = 'success' | 'error' | 'info' | 'warning'

export type NotificationPosition = 'bottom-right' | 'top-center'

export interface Notification {
  id: string
  message: string
  type: NotificationType
  position: NotificationPosition
  duration?: number
  dismissible?: boolean
  createdAt: number
}

export interface NotificationSlice {
  notifications: Notification[]
  /** 
   * @deprecated Use useNotifications hook instead of direct store access
   */
  addNotification: (notification: Omit<Notification, 'id' | 'createdAt'>) => void
  removeNotification: (id: string) => void
  clearAll: () => void
}

export const createNotificationSlice: StateCreator<NotificationSlice, [["zustand/devtools", never]]> = (set) => ({
  notifications: [],

  addNotification: (notification) => set((state) => {
    const newNotifications = [
      ...state.notifications,
      {
        ...notification,
        id: generateId(),
        createdAt: Date.now(),
      }
    ]

    // Keep only the last 5 notifications
    if (newNotifications.length > 5) {
      newNotifications.shift()
    }

    return { notifications: newNotifications }
  }, false, 'NotificationStore/addNotification'),

  removeNotification: (id) => set((state) => ({
    notifications: state.notifications.filter(n => n.id !== id)
  }), false, 'NotificationStore/removeNotification'),

  clearAll: () => set({ notifications: [] }, false, 'NotificationStore/clearAll'),
})
