import { describe, expect, it } from 'vitest'
import { formatsFor } from './format'

// Tuesday 27 October 2026, 18:30 on this device's clock (the design's sample moment).
const tuesday = new Date(2026, 9, 27, 18, 30)

describe('English: the British style the design uses', () => {
  const en = formatsFor('en')

  it('Today’s header', () => {
    expect(en.longDay(tuesday, false)).toBe('Tuesday 27 October')
    expect(en.longDay(tuesday, true)).toBe('Tuesday 27 October 2026')
  })

  it('days and times', () => {
    expect(en.day(tuesday)).toBe('Tue 27 Oct')
    expect(en.dayTime(tuesday)).toBe('Tue 27 Oct, 18:30')
    expect(en.time(tuesday)).toBe('18:30')
    expect(en.time(new Date(2026, 9, 27, 9, 5))).toBe('09:05')
  })

  it('numbers and shares', () => {
    expect(en.number(1234.5)).toBe('1,234.5')
    expect(en.number(0.5, { minimumFractionDigits: 2 })).toBe('0.50')
    expect(en.percent(0.97)).toBe('97%')
    expect(en.percent(0.975, 1)).toBe('97.5%')
  })
})

describe('Português (Portugal)', () => {
  const pt = formatsFor('pt-PT')

  it('Today’s header starts with a capital, months stay small', () => {
    expect(pt.longDay(tuesday, false)).toBe('Terça-feira, 27 de outubro')
    expect(pt.longDay(tuesday, true)).toBe('Terça-feira, 27 de outubro de 2026')
  })

  it('days and times: day before month, 24-hour clock', () => {
    expect(pt.day(tuesday)).toMatch(/^ter\S*, 27\/10$/)
    expect(pt.dayTime(tuesday)).toMatch(/^ter\S*, 27\/10, 18:30$/)
    expect(pt.time(tuesday)).toBe('18:30')
  })

  it('numbers: decimal comma, thousands grouped with a space from 10 000', () => {
    expect(pt.number(1234.5)).toBe('1234,5')
    expect(pt.number(12345.5).replace(/\s/g, ' ')).toBe('12 345,5')
    expect(pt.percent(0.975, 1).replace(/\s/g, ' ')).toMatch(/^97,5 ?%$/)
  })
})
