import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useCurrentLanguage, useFormats } from '../../i18n/languageContext'
import type { Pokedex } from '../../pokedex/data'
import { parseQuery } from '../../pokedex/search'
import { alternativeLabel } from './labels'

/**
 * How the search read what was typed (decision 2026-11-08): one chip per group of words, alternatives joined by
 * "or"; a word that matches nothing is crossed out, so it's clear why nothing came back.
 */
export function QueryChips({ query, pokedex }: { query: string; pokedex: Pokedex }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const language = useCurrentLanguage()
  const groups = useMemo(() => parseQuery(query, pokedex).groups, [query, pokedex])
  if (!groups.length) return null

  const moveName = (id: string) => {
    const move = pokedex.movesById.get(id)
    return (language === 'pt-PT' ? move?.namePt : null) ?? move?.nameEn ?? id
  }

  return (
    <div className="pokedex-understood">
      <span className="pokedex-understood-label">{t('pokedex.understood')}</span>
      <ul>
        {groups.map((group, index) => (
          <li key={index} className="pokedex-term">
            {group.alternatives.map((alternative, at) => (
              <span key={at}>
                {at > 0 && <span className="pokedex-term-or">{t('pokedex.or')}</span>}
                <span data-unknown={alternative.term.kind === 'unknown' || undefined}>
                  {alternativeLabel(alternative, t, formats, moveName)}
                </span>
              </span>
            ))}
          </li>
        ))}
      </ul>
    </div>
  )
}
