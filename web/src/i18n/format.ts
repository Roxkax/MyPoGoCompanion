import { locales, type Language } from './languages'

// Dates and numbers per language (Intl, built into the browser): English in the British style, Portuguese in
// Portugal's. Plurals are i18next's (`key_one` / `key_other`, from Intl.PluralRules).

function upperFirst(text: string, locale: string): string {
  return text.charAt(0).toLocaleUpperCase(locale) + text.slice(1)
}

export interface Formats {
  /** 1,234.5 · 1234,5 (Portuguese groups thousands from 10 000: 12 345,5). */
  number: (value: number, options?: Intl.NumberFormatOptions) => string
  /** A share from 0 to 1: 97% · 97 %. */
  percent: (share: number, fractionDigits?: number) => string
  /** "Tue 27 Oct" · "ter., 27/10". */
  day: (date: Date) => string
  /** "Tue 27 Oct, 18:30" · "ter., 27/10, 18:30". */
  dayTime: (date: Date) => string
  /** "18:30" in both. */
  time: (date: Date) => string
  /** Today's header (A1): "Tuesday 27 October [2026]" · "Terça-feira, 27 de outubro [de 2026]". */
  longDay: (date: Date, withYear: boolean) => string
}

/** The formats for one language. Times are shown as the clock on the device shows them (its time zone). */
export function formatsFor(language: Language): Formats {
  const locale = locales[language]
  const numbers = new Intl.NumberFormat(locale)
  const short: Intl.DateTimeFormatOptions =
    language === 'en'
      ? { weekday: 'short', day: 'numeric', month: 'short' }
      : { weekday: 'short', day: '2-digit', month: '2-digit' }
  const day = new Intl.DateTimeFormat(locale, short)
  const time = new Intl.DateTimeFormat(locale, {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })

  function longDay(date: Date, withYear: boolean): string {
    const options: Intl.DateTimeFormatOptions = {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      ...(withYear ? { year: 'numeric' } : {}),
    }
    if (language === 'en') {
      // Put together from parts: browsers differ on the comma after the weekday ("Tuesday, 27 October").
      const parts = new Intl.DateTimeFormat(locale, options).formatToParts(date)
      const part = (type: Intl.DateTimeFormatPartTypes) =>
        parts.find((p) => p.type === type)?.value ?? ''
      const text = `${part('weekday')} ${part('day')} ${part('month')}`
      return withYear ? `${text} ${part('year')}` : text
    }
    return upperFirst(new Intl.DateTimeFormat(locale, options).format(date), locale)
  }

  return {
    number: (value, options) =>
      options ? new Intl.NumberFormat(locale, options).format(value) : numbers.format(value),
    percent: (share, fractionDigits = 0) =>
      new Intl.NumberFormat(locale, {
        style: 'percent',
        maximumFractionDigits: fractionDigits,
      }).format(share),
    day: (date) => day.format(date),
    dayTime: (date) => `${day.format(date)}, ${time.format(date)}`,
    time: (date) => time.format(date),
    longDay,
  }
}
