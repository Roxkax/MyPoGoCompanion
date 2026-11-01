import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { Card } from './Card'
import { Chip } from './Chip'
import { Pill } from './Pill'
import { isPokemonType, pokemonTypes, typeName, versionKinds } from './pokemonTypes'
import { SegmentedLinks } from './SegmentedLinks'
import { SegmentedSwitch } from './SegmentedSwitch'
import { SpriteBox } from './SpriteBox'
import { TypeBadge } from './TypeBadge'
import { VersionMarker } from './VersionMarker'

describe('Card', () => {
  it('is a padded box by default', () => {
    render(<Card>Kyogre</Card>)

    const card = screen.getByText('Kyogre')
    expect(card.tagName).toBe('DIV')
    expect(card).toHaveClass('ui-card', 'ui-card-padded')
  })

  it('can be a section without padding, with its own class and label', () => {
    render(
      <Card as="section" padded={false} className="rows" aria-label="Rankings">
        rows
      </Card>,
    )

    const card = screen.getByRole('region', { name: 'Rankings' })
    expect(card).toHaveClass('ui-card', 'rows')
    expect(card).not.toHaveClass('ui-card-padded')
  })
})

describe('Chip', () => {
  it('a toggle: pressed or not, and clickable', async () => {
    const click = vi.fn()
    render(
      <>
        <Chip selected onClick={click}>
          Shiny
        </Chip>
        <Chip>Released</Chip>
      </>,
    )

    expect(screen.getByRole('button', { name: 'Shiny' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Released' })).toHaveAttribute(
      'aria-pressed',
      'false',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Shiny' }))
    expect(click).toHaveBeenCalledOnce()
  })

  it('a set filter with a × that removes it', async () => {
    const remove = vi.fn()
    render(
      <>
        <Chip onRemove={remove}>Dragon</Chip>
        <Chip onRemove={remove} removeLabel="Clear the type filter">
          Fire
        </Chip>
      </>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Remove Dragon' }))
    expect(remove).toHaveBeenCalledOnce()
    expect(screen.getByRole('button', { name: 'Clear the type filter' })).toBeInTheDocument()
  })
})

describe('Pill', () => {
  it('has a tone and an optional dot', () => {
    const { container } = render(
      <>
        <Pill tone="live" dot>
          Live now
        </Pill>
        <Pill>Ends Sat</Pill>
      </>,
    )

    expect(screen.getByText('Live now')).toHaveAttribute('data-tone', 'live')
    expect(screen.getByText('Ends Sat')).toHaveAttribute('data-tone', 'soft')
    expect(container.querySelectorAll('.ui-pill-dot')).toHaveLength(1)
  })
})

describe('types and versions', () => {
  it('knows the 18 types, in order', () => {
    expect(pokemonTypes).toHaveLength(18)
    expect(pokemonTypes[0]).toBe('normal')
    expect(isPokemonType('dragon')).toBe(true)
    expect(isPokemonType('shadow')).toBe(false)
    expect(typeName('electric')).toBe('Electric')
  })

  it('a type badge shows the type in its colour', () => {
    render(<TypeBadge type="ground" size="large" />)

    const badge = screen.getByText('Ground')
    expect(badge).toHaveAttribute('data-type', 'ground')
    expect(badge).toHaveAttribute('data-size', 'large')
  })

  it.each([
    ['gmax', 'G-Max'],
    ['shadow', 'Shadow'],
    ['hisuian', 'Hisuian'],
  ] as const)('version marker %s says %s', (kind, label) => {
    render(<VersionMarker kind={kind} />)

    expect(screen.getByText(label)).toHaveAttribute('data-kind', kind)
  })

  it('a version marker can carry its own words (Mega X)', () => {
    render(<VersionMarker kind="mega" label="Mega X" />)

    expect(screen.getByText('Mega X')).toBeInTheDocument()
    expect(versionKinds).toContain('primal')
  })
})

describe('SpriteBox', () => {
  it('shows the picture in a square box', () => {
    render(<SpriteBox src="/sprites/445.png" alt="Garchomp" size={48} />)

    const img = screen.getByRole('img', { name: 'Garchomp' })
    expect(img).toHaveAttribute('src', '/sprites/445.png')
    expect(img.parentElement).toHaveStyle({ width: '48px', height: '48px' })
  })

  it('a placeholder of the same size when there is no picture or it fails to load', () => {
    const { rerender } = render(<SpriteBox src="/sprites/broken.png" alt="Gible" />)

    fireEvent.error(screen.getByRole('img', { name: 'Gible' }))
    expect(screen.getByRole('img', { name: 'Gible' })).toHaveClass('ui-sprite-empty')

    rerender(<SpriteBox src="/sprites/443.png" alt="Gible" />)
    expect(screen.getByRole('img', { name: 'Gible' }).tagName).toBe('IMG')

    rerender(<SpriteBox alt="" />)
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })
})

function Kinds({ onChange }: { onChange?: (value: string) => void }) {
  const [value, setValue] = useState<'fast' | 'charged' | 'all'>('charged')
  return (
    <SegmentedSwitch
      label="Move kind"
      value={value}
      onChange={(next) => {
        setValue(next)
        onChange?.(next)
      }}
      options={[
        { value: 'fast', label: 'Fast', count: 80 },
        { value: 'charged', label: 'Charged', count: 235 },
        { value: 'all', label: 'All' },
      ]}
    />
  )
}

describe('SegmentedSwitch', () => {
  it('a radio group with the selected option checked; counts after the labels', () => {
    render(<Kinds />)

    expect(screen.getByRole('radiogroup', { name: 'Move kind' })).toBeInTheDocument()
    const charged = screen.getByRole('radio', { name: 'Charged235' })
    expect(charged).toHaveAttribute('aria-checked', 'true')
    expect(charged).toHaveAttribute('tabindex', '0')
    expect(screen.getByRole('radio', { name: 'Fast80' })).toHaveAttribute('tabindex', '-1')
  })

  it('click selects; the arrow keys move the selection and wrap around', async () => {
    const change = vi.fn()
    render(<Kinds onChange={change} />)

    await userEvent.click(screen.getByRole('radio', { name: 'Fast80' }))
    expect(change).toHaveBeenLastCalledWith('fast')

    await userEvent.keyboard('{ArrowLeft}')
    expect(screen.getByRole('radio', { name: 'All' })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('radio', { name: 'All' })).toHaveFocus()

    await userEvent.keyboard('{ArrowRight}')
    expect(change).toHaveBeenLastCalledWith('fast')
    await userEvent.keyboard('{ArrowDown}{ArrowUp}{ArrowDown}')
    expect(change).toHaveBeenLastCalledWith('charged')

    change.mockClear()
    await userEvent.keyboard('a')
    expect(change).not.toHaveBeenCalled()
  })
})

describe('SegmentedLinks', () => {
  it('links, the current section marked', () => {
    render(
      <MemoryRouter initialEntries={['/battle/tiers/ultra-league']}>
        <SegmentedLinks
          label="Battle sections"
          links={[
            { to: '/battle/raids', label: 'Raids', end: true },
            { to: '/battle/tiers', label: 'Tier lists' },
          ]}
        />
      </MemoryRouter>,
    )

    expect(screen.getByRole('navigation', { name: 'Battle sections' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tier lists' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Raids' })).not.toHaveAttribute('aria-current')
  })
})
