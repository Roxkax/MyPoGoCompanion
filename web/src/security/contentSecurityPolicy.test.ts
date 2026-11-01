import { describe, expect, it } from 'vitest'
import { contentSecurityPolicy, withContentSecurityPolicy } from './contentSecurityPolicy'

describe('Content Security Policy', () => {
  it('lets the page talk only to itself and the API, and run only its own scripts', () => {
    const policy = contentSecurityPolicy('https://api-prod.example.azurecontainerapps.io/')

    expect(policy).toContain("script-src 'self';")
    expect(policy).toContain("connect-src 'self' https://api-prod.example.azurecontainerapps.io;")
    expect(policy).toContain("img-src 'self' data: https://lh3.googleusercontent.com;")
    expect(policy).toContain("frame-ancestors 'none'")
    expect(policy).not.toContain('unsafe')
  })

  it('without an API address, connects to its own origin only', () => {
    expect(contentSecurityPolicy('')).toContain("connect-src 'self';")
    expect(contentSecurityPolicy(undefined)).toContain("connect-src 'self';")
  })

  it('adds the policy to the Static Web Apps headers, keeping the others', () => {
    const config = JSON.parse(
      withContentSecurityPolicy(
        JSON.stringify({
          navigationFallback: { rewrite: '/index.html' },
          globalHeaders: { 'X-Content-Type-Options': 'nosniff' },
        }),
        'https://api.example',
      ),
    )

    expect(config.navigationFallback).toEqual({ rewrite: '/index.html' })
    expect(config.globalHeaders['X-Content-Type-Options']).toBe('nosniff')
    expect(config.globalHeaders['X-Frame-Options']).toBe('DENY')
    expect(config.globalHeaders['Content-Security-Policy']).toContain('https://api.example')
  })

  it('works when the configuration has no headers yet', () => {
    const config = JSON.parse(withContentSecurityPolicy('{}', undefined))

    expect(Object.keys(config.globalHeaders)).toEqual([
      'Content-Security-Policy',
      'X-Frame-Options',
    ])
  })
})
