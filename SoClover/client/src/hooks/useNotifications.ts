import { useCallback } from 'react'
import { useNotificationStore } from '../core/store'
import { NotificationType, NotificationPosition } from '../core/store/notificationSlice'

export const useNotifications = () => {
  const addNotification = useNotificationStore((state) => state.addNotification)
  const removeNotification = useNotificationStore((state) => state.removeNotification)

  const notifySuccess = useCallback((
    message: string,
    options?: { position?: NotificationPosition; duration?: number }
  ) => {
    addNotification({
      message,
      type: 'success',
      position: options?.position || 'bottom-right',
      duration: options?.duration || 4000,
      dismissible: true,
    })
  }, [addNotification])

  const notifyError = useCallback((
    message: string,
    options?: { position?: NotificationPosition; duration?: number }
  ) => {
    addNotification({
      message,
      type: 'error',
      position: options?.position || 'bottom-right',
      duration: options?.duration || 4000,
      dismissible: true,
    })
  }, [addNotification])

  const notifyInfo = useCallback((
    message: string,
    options?: { position?: NotificationPosition; duration?: number }
  ) => {
    addNotification({
      message,
      type: 'info',
      position: options?.position || 'bottom-right',
      duration: options?.duration || 4000,
      dismissible: true,
    })
  }, [addNotification])

  const notifyWarning = useCallback((
    message: string,
    options?: { position?: NotificationPosition; duration?: number }
  ) => {
    addNotification({
      message,
      type: 'warning',
      position: options?.position || 'bottom-right',
      duration: options?.duration || 4000,
      dismissible: true,
    })
  }, [addNotification])

  const notifyTopCenter = useCallback((
    message: string,
    options?: { type?: NotificationType; duration?: number }
  ) => {
    addNotification({
      message,
      type: options?.type || 'info',
      position: 'top-center',
      duration: options?.duration || 10000,
      dismissible: true,
    })
  }, [addNotification])

  return {
    notifySuccess,
    notifyError,
    notifyInfo,
    notifyWarning,
    notifyTopCenter,
    dismiss: removeNotification,
    notifications: useNotificationStore((state) => state.notifications)
  }
}
