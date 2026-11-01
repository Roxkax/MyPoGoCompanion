import { describe, expect, it } from 'vitest'
import { applyEnvironment, readEnvironment } from './environment'

describe('readEnvironment', () => {
  it('production: no label, the plain title and icon', () => {
    expect(readEnvironment({ VITE_APP_ENV: 'production', VITE_APP_VERSION: 'abc1234' })).toEqual({
      kind: 'production',
      label: null,
      detail: null,
      title: 'MyPoGoCompanion',
      icon: '/icon.svg',
    })
  })

  it('QA: labelled, with the pull request and commit it shows', () => {
    expect(
      readEnvironment({ VITE_APP_ENV: 'qa', VITE_PR_NUMBER: '920', VITE_APP_VERSION: 'abc1234' }),
    ).toEqual({
      kind: 'qa',
      label: 'QA',
      detail: 'PR #920 · abc1234',
      title: 'QA · MyPoGoCompanion',
      icon: '/icon-qa.svg',
    })
  })

  it('QA without details: just the label', () => {
    expect(readEnvironment({ VITE_APP_ENV: ' QA ' })).toMatchObject({ kind: 'qa', detail: null })
  })

  it.each([undefined, '', 'local', 'staging'])(
    'anything else (%s) is a local copy, never mistaken for prod',
    (value) => {
      expect(readEnvironment({ VITE_APP_ENV: value })).toMatchObject({
        kind: 'local',
        label: 'Local',
        title: 'Local · MyPoGoCompanion',
        icon: '/icon-local.svg',
      })
    },
  )

  it('reads the build settings by default', () => {
    expect(readEnvironment().kind).toBe('local')
  })
})

describe('applyEnvironment', () => {
  it('sets the tab title, the tab icon and data-env on <html>', () => {
    document.head.innerHTML = '<link rel="icon" href="/icon.svg" />'

    applyEnvironment(readEnvironment({ VITE_APP_ENV: 'qa' }))

    expect(document.title).toBe('QA · MyPoGoCompanion')
    expect(document.documentElement.dataset.env).toBe('qa')
    expect(document.querySelector('link[rel="icon"]')?.getAttribute('href')).toBe('/icon-qa.svg')
  })

  it('works on a page without an icon link', () => {
    document.head.innerHTML = ''

    applyEnvironment(readEnvironment({ VITE_APP_ENV: 'production' }))

    expect(document.title).toBe('MyPoGoCompanion')
    expect(document.documentElement.dataset.env).toBe('production')
  })
})
