/** "Tuesday 27 October" (phone) or "Tuesday 27 October 2026" (desktop), as in screen A1. */
export function todayLabel(now: Date, withYear: boolean): string {
  const parts = new Intl.DateTimeFormat('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).formatToParts(now)
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((p) => p.type === type)?.value ?? ''
  const date = `${part('weekday')} ${part('day')} ${part('month')}`
  return withYear ? `${date} ${part('year')}` : date
}
