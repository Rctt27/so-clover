import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useNotifications } from '../../hooks/useNotifications'
import type { Notification } from '../../core/store/notificationSlice'

const NotificationItem = ({ notification }: { notification: Notification }) => {
  const { dismiss } = useNotifications()
  const { t } = useTranslation('common')

  useEffect(() => {
    if (notification.duration) {
      const timer = setTimeout(() => {
        dismiss(notification.id)
      }, notification.duration)
      return () => clearTimeout(timer)
    }
  }, [notification.duration, notification.id, dismiss])

  const getColors = () => {
    switch (notification.type) {
      case 'success': return 'bg-green-500 text-white'
      case 'error': return 'bg-red-500 text-white'
      case 'warning': return 'bg-orange-500 text-white'
      case 'info': return 'bg-blue-500 text-white'
    }
  }

  const getAnimation = () => {
    if (notification.position === 'top-center') {
      return {
        initial: { opacity: 0, y: -20, scale: 0.95 },
        animate: { opacity: 1, y: 0, scale: 1 },
        exit: { opacity: 0, scale: 0.95, transition: { duration: 0.2 } },
      }
    }
    return {
      initial: { opacity: 0, x: 50, scale: 0.9 },
      animate: { opacity: 1, x: 0, scale: 1 },
      exit: { opacity: 0, x: 20, scale: 0.9, transition: { duration: 0.2 } },
    }
  }

  return (
    <motion.div
      layout
      {...getAnimation()}
      transition={{ 
        type: 'spring', 
        stiffness: 400, 
        damping: 30,
        layout: { duration: 0.3 }
      }}
      className={`
        pointer-events-auto
        ${getColors()}
        px-6 py-4 rounded-xl shadow-xl
        max-w-md w-full
        flex items-center gap-3
        border border-white/10
      `}
      role="alert"
    >
      <div
        className="flex-1 font-medium"
        dangerouslySetInnerHTML={{ __html: notification.message }}
      />

      {notification.dismissible && (
        <button
          onClick={() => dismiss(notification.id)}
          className="
            opacity-80 hover:opacity-100 transition-opacity
            flex-shrink-0
          "
          aria-label={t('notification.close')}
        >
          <X size={20} />
        </button>
      )}
    </motion.div>
  )
}

export const NotificationContainer = () => {
  const { notifications } = useNotifications()

  // Séparer les notifications par position pour éviter les chevauchements
  const bottomRightNotifications = notifications?.filter(n => n.position === 'bottom-right') || []
  const topCenterNotifications = notifications?.filter(n => n.position === 'top-center') || []

  return (
    <>
      {/* Bottom-right stack: Latest on bottom, older pushed UP */}
      <div className="fixed bottom-6 right-6 z-[1000] flex flex-col-reverse gap-3 pointer-events-none w-full max-w-[400px]">
        <AnimatePresence mode="popLayout" initial={false}>
          {bottomRightNotifications.map((notification) => (
            <NotificationItem key={notification.id} notification={notification} />
          ))}
        </AnimatePresence>
      </div>

      {/* Top-center stack: Latest on top, older pushed DOWN */}
      <div className="fixed top-6 left-1/2 -translate-x-1/2 z-[1000] flex flex-col gap-3 pointer-events-none w-full max-w-[400px]">
        <AnimatePresence mode="popLayout" initial={false}>
          {topCenterNotifications.map((notification) => (
            <NotificationItem key={notification.id} notification={notification} />
          ))}
        </AnimatePresence>
      </div>
    </>
  )
}
