import { useMemo } from 'react'
import { useFormats } from '../../i18n/languageContext'
import { multiplier } from './chart'

export function useMultiplier() {
  const formats = useFormats()
  const labels = useMemo(
    () =>
      new Map(
        [-4, -3, -2, -1, 0, 1, 2].map((power) => [
          power,
          `×${formats.number(multiplier(power), { maximumFractionDigits: 2 })}`,
        ]),
      ),
    [formats],
  )
  return (power: number) => labels.get(power)!
}
