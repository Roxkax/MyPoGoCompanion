import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Schemas } from '../../api/client'
import { LanguageStore } from '../../i18n/language'
import { renderApp } from '../../testing/app'
import { MemoryStorage } from '../../testing/auth'

type Part = Schemas['KeeperFilterPart']

const parts: Part[] = [
  {
    kind: 'GreatLeague',
    part: 1,
    variant: '',
    fromDex: 1,
    toDex: 511,
    searchString: '1-511&cp-1500',
  },
  {
    kind: 'GreatLeague',
    part: 2,
    variant: '',
    fromDex: 512,
    toDex: 1025,
    searchString: '512-1025&cp-1500',
  },
  {
    kind: 'MaxBattle',
    part: 1,
    variant: '',
    fromDex: null,
    toDex: null,
    searchString: 'maxmove1-&3-4attack',
  },
  {
    kind: 'MaxBattle',
    part: 1,
    variant: 'attack-15',
    fromDex: null,
    toDex: null,
    searchString: 'maxmove1-&4attack',
  },
  {
    kind: 'Mega',
    part: 1,
    variant: '',
    fromDex: null,
    toDex: null,
    searchString: 'mega0-,382-383',
  },
  {
    kind: 'Mega',
    part: 1,
    variant: 'with-pre-evolutions',
    fromDex: null,
    toDex: null,
    searchString: 'mega0-,382-383,4-5',
  },
]

let answer: () => Response

beforeEach(() => {
  answer = () => Response.json({ filters: parts })
  vi.stubGlobal('fetch', async (input: RequestInfo | URL) =>
    new Request(input).url.endsWith('/keeper-filters') ? answer() : new Promise(() => {}),
  )
})
afterEach(() => vi.unstubAllGlobals())

describe('Keepers screen', () => {
  it('copies each requested part and gives feedback on that button', async () => {
    const user = userEvent.setup()
    renderApp('/filters', { layout: 'desktop' })
    const great = within(await screen.findByRole('region', { name: 'Great League' }))

    await user.click(great.getByRole('button', { name: /^Copy Part 2/ }))
    expect(await navigator.clipboard.readText()).toBe('512-1025&cp-1500')
    expect(great.getByRole('button', { name: /^Copied Part 2/ })).toBeInTheDocument()

    await user.click(great.getByRole('button', { name: /^Copy Part 1/ }))
    expect(await navigator.clipboard.readText()).toBe('1-511&cp-1500')
  })

  it('switches variants and exposes a manual copy field if clipboard access fails', async () => {
    const user = userEvent.setup()
    const clipboard = vi
      .spyOn(navigator.clipboard, 'writeText')
      .mockRejectedValueOnce(new Error('denied'))
    renderApp('/filters', { layout: 'phone' })
    const max = within(await screen.findByRole('region', { name: 'Max Battle attackers' }))
    await user.click(max.getByRole('radio', { name: '15 only' }))
    await user.click(max.getByRole('button', { name: 'Copy' }))
    expect(
      await max.findByRole('textbox', { name: 'Copy this search string manually' }),
    ).toHaveValue('maxmove1-&4attack')
    clipboard.mockRestore()

    const mega = within(screen.getByRole('region', { name: 'Megas' }))
    expect(mega.getByRole('radio', { name: 'Yes' })).toBeChecked()
    await user.click(mega.getByRole('radio', { name: 'No' }))
    await user.click(mega.getByRole('button', { name: 'Copy' }))
    expect(await navigator.clipboard.readText()).toBe('mega0-,382-383')
  })

  it('does not offer choices absent from the published data and localizes labels', async () => {
    answer = () => Response.json({ filters: [parts[2]] })
    const languages = new LanguageStore(new MemoryStorage(), ['pt-PT'])
    renderApp('/filters', { languages })
    const max = within(await screen.findByRole('region', { name: 'Atacantes de Batalhas Max' }))
    expect(max.getByRole('button', { name: 'Copiar' })).toBeInTheDocument()
    expect(max.queryByRole('radiogroup')).not.toBeInTheDocument()
  })

  it('copies the English transfer search with every keeper tag and exclusion in Portuguese too', async () => {
    const user = userEvent.setup()
    const languages = new LanguageStore(new MemoryStorage(), ['pt-PT'])
    renderApp('/filters', { languages })
    const transfer = within(await screen.findByRole('region', { name: 'Seguro para transferir' }))

    expect(transfer.getByText(/Verifica os resultados da pesquisa/)).toBeVisible()
    await user.click(transfer.getByRole('button', { name: 'Copiar' }))
    expect(await navigator.clipboard.readText()).toBe(
      '!#GL&!#UL&!#Raid&!#Max&!#Mega&!shiny&!lucky&!legendary&!mythical&!favorite&!4*&!costume',
    )
    expect(transfer.getByRole('button', { name: 'Copiado' })).toBeVisible()
  })

  it('retries failed loading and has an empty state before publication', async () => {
    const user = userEvent.setup()
    answer = () => Response.json({ title: 'Try again later' }, { status: 503 })
    renderApp('/filters')
    expect(screen.getByRole('status')).toHaveTextContent('Loading keeper filters')
    const retry = await screen.findByRole('button', { name: 'Try again' })
    answer = () => Response.json({ filters: [] })
    await user.click(retry)
    expect(
      await screen.findByRole('heading', { name: 'Keeper filters are being prepared' }),
    ).toBeVisible()
    expect(screen.queryByRole('region', { name: 'Safe to transfer' })).not.toBeInTheDocument()
  })
})
