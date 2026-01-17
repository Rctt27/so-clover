import { useEffect, useState } from 'react'
import { useGameStore } from '../../core/store'

export const Timer = () => {
  const phaseEndsAtUtc = useGameStore((state) => state.phaseEndsAtUtc)
  const [timeLeft, setTimeLeft] = useState<number | null>(null)
  const [isWarning, setIsWarning] = useState(false)

  useEffect(() => {
    if (!phaseEndsAtUtc) {
      setTimeLeft(null)
      setIsWarning(false)
      return
    }

    const deadline = new Date(phaseEndsAtUtc).getTime()

    const calculateTimeLeft = () => {
      const now = new Date().getTime()
      const diff = Math.max(0, Math.ceil((deadline - now) / 1000))
      setTimeLeft(diff)
      setIsWarning(diff <= 30)
    }

    calculateTimeLeft()
    const interval = setInterval(calculateTimeLeft, 1000)

    return () => clearInterval(interval)
  }, [phaseEndsAtUtc])

  if (timeLeft === null) return null

  const minutes = Math.floor(timeLeft / 60)
    .toString()
    .padStart(2, '0')
  const seconds = (timeLeft % 60).toString().padStart(2, '0')

  return (
    <span
      className={`text-sm font-bold font-mono transition-colors duration-300 ${
        isWarning ? 'text-red-500 animate-pulse' : 'text-gray-700'
      }`}
    >
      {minutes}:{seconds}
    </span>
  )
}
