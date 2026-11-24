import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { $api } from '../../api/client'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import {
  moveName,
  noFilters as noMoveFilters,
  writeFilters as writeMoveFilters,
} from '../../moves/view'
import { noFilters, writeFilters } from '../../pokedex/filters'
import type { PokemonType } from '../../ui/pokemonTypes'
import { SpriteBox } from '../../ui/SpriteBox'
import { pictureSources } from '../../ui/sprites'

const staleTime = 60 * 60 * 1000

export function RelatedPokemon({ types }: { types: readonly PokemonType[] }) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const query = $api.useQuery('get', '/pokedex', undefined, { staleTime })
  const matches =
    query.data?.species.flatMap((species) => {
      const form = species.forms.find(
        (form) => form.released && types.every((type) => form.types.includes(type)),
      )
      return form ? [{ species, form }] : []
    }) ?? []
  const examples = matches.slice(0, 3)
  const names = examples.map(
    ({ species }) => (language === 'pt-PT' ? species.namePt : null) ?? species.nameEn,
  )
  const to = `/pokedex?${writeFilters({ ...noFilters, types: [...types] })}`

  return (
    <RelatedLink to={to} label={t('chart.relatedPokemon')} names={names} count={matches.length}>
      {examples.length > 0 && (
        <span className="type-chart-sprites">
          {examples.map(({ species, form }) => (
            <SpriteBox key={species.dex} src={pictureSources(form)} alt="" size={30} />
          ))}
        </span>
      )}
    </RelatedLink>
  )
}

export function RelatedMoves({ type }: { type: PokemonType }) {
  const { t } = useTranslation()
  const language = useCurrentLanguage()
  const query = $api.useQuery('get', '/moves', undefined, { staleTime })
  const matches = query.data?.moves.filter((move) => move.type === type) ?? []
  const kind =
    matches.length > 0 && matches.every((move) => move.kind === 'Fast') ? 'fast' : 'charged'
  const to = `/pokedex/moves?${writeMoveFilters({ ...noMoveFilters, type, kind })}`
  const names = matches.slice(0, 3).map((move) => moveName(move, language))

  return (
    <RelatedLink to={to} label={t('chart.relatedMoves')} names={names} count={matches.length} />
  )
}

function RelatedLink({
  to,
  label,
  names,
  count,
  children,
}: {
  to: string
  label: string
  names: string[]
  count: number
  children?: ReactNode
}) {
  const { t } = useTranslation()
  const formats = useFormats()
  const remaining = count - names.length
  return (
    <Link to={to} className="type-chart-related" aria-label={label}>
      {children}
      <span>
        {label}
        {names.length > 0 && (
          <small>
            {remaining > 0
              ? t('chart.andMore', {
                  names: names.join(', '),
                  count: remaining,
                  more: formats.number(remaining),
                })
              : names.join(', ')}
          </small>
        )}
      </span>
      <span aria-hidden>›</span>
    </Link>
  )
}
