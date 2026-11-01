import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { readEnvironment } from '../environment'
import { EnvironmentBanner } from './EnvironmentBanner'

describe('EnvironmentBanner', () => {
  it('shows nothing in production', () => {
    const { container } = render(
      <EnvironmentBanner environment={readEnvironment({ VITE_APP_ENV: 'production' })} />,
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('on QA: says it is a test version and which pull request it shows', () => {
    render(
      <EnvironmentBanner
        environment={readEnvironment({
          VITE_APP_ENV: 'qa',
          VITE_PR_NUMBER: '920',
          VITE_APP_VERSION: 'abc1234',
        })}
      />,
    )

    const banner = screen.getByRole('note', { name: 'QA environment' })
    expect(banner).toHaveAttribute('data-env', 'qa')
    expect(banner).toHaveTextContent('QA')
    expect(banner).toHaveTextContent('Test version, not the real app')
    expect(banner).toHaveTextContent('PR #920 · abc1234')
  })

  it('keeps the label and the PR · commit whole; only the sentence may be shortened to fit one line', () => {
    const { container } = render(
      <EnvironmentBanner
        environment={readEnvironment({
          VITE_APP_ENV: 'qa',
          VITE_PR_NUMBER: '929',
          VITE_APP_VERSION: 'abc1234',
        })}
      />,
    )

    const parts = [...container.querySelectorAll('.env-banner > *')].map((part) => part.className)
    expect(parts).toEqual(['env-banner-label', 'env-banner-text', 'env-banner-detail num'])
  })

  it('locally: says it runs on this computer', () => {
    render(<EnvironmentBanner environment={readEnvironment({ VITE_APP_ENV: 'local' })} />)

    const banner = screen.getByRole('note', { name: 'Local environment' })
    expect(banner).toHaveTextContent('Running on this computer')
    expect(banner).not.toHaveTextContent('PR #')
  })
})
