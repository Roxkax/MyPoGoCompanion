import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { pokedexSections } from '../../layout/nav'
import { TabScreen } from '../../layout/Screen'
import { useIsDesktop } from '../../layout/useMediaQuery'
import { toggleType } from '../../pokedex/filters'
import { Card } from '../../ui/Card'
import type { PokemonType } from '../../ui/pokemonTypes'
import { SegmentedSwitch } from '../../ui/SegmentedSwitch'
import { TypeBadge } from '../../ui/TypeBadge'
import { chartModes, chartTypes, readChart, writeChart } from './chart'
import { MatchupResults } from './MatchupResults'
import { MatchupTable } from './MatchupTable'

export function TypeChartScreen() {
  const { t } = useTranslation()
  const desktop = useIsDesktop()
  const [params, setParams] = useSearchParams()
  const address = params.toString()
  const [draft, setDraft] = useState(() => ({
    address,
    selection: readChart(params),
    pending: [] as string[],
  }))
  // Keep successive picks responsive while the router updates the address.
  if (draft.address !== address) {
    const written = draft.pending.indexOf(address)
    setDraft(
      written < 0
        ? { address, selection: readChart(params), pending: [] }
        : { ...draft, address, pending: draft.pending.slice(written + 1) },
    )
  }
  const { selection } = draft
  const { mode, defenders, attacker } = selection
  const selected = mode === 'attacking' ? (attacker ? [attacker] : []) : defenders

  function update(next: typeof selection) {
    const query = writeChart(next)
    const nextAddress = query.toString()
    if (nextAddress === writeChart(selection).toString()) return
    setDraft({ ...draft, selection: next, pending: [...draft.pending, nextAddress] })
    void setParams(query, { replace: true })
  }

  function choose(type: PokemonType) {
    update(
      mode === 'attacking'
        ? { ...selection, attacker: type }
        : { ...selection, defenders: toggleType(defenders, type) },
    )
  }

  return (
    <TabScreen
      title="pokedex"
      desktopTitle="typeChart"
      sections={{ label: 'pokedex', links: pokedexSections }}
    >
      <div className="type-chart-layout">
        <div className="type-chart-panel">
          <SegmentedSwitch
            label={t('chart.mode')}
            options={chartModes
              .filter((value) => !desktop || value !== 'full')
              .map((value) => ({ value, label: t(`chart.${value}`) }))}
            value={desktop && mode === 'full' ? 'defending' : mode}
            onChange={(value) => update({ ...selection, mode: value })}
          />
          {(mode !== 'full' || desktop) && (
            <>
              <Card className="type-chart-picker">
                <div className="type-chart-picker-head">
                  <span className="type-chart-chips">
                    {selected.map((type) => (
                      <TypeBadge key={type} type={type} />
                    ))}
                    {selected.length === 0 &&
                      t(mode === 'attacking' ? 'chart.pickAttack' : 'chart.pickDefense')}
                  </span>
                  {selected.length > 0 && (
                    <button
                      type="button"
                      className="type-chart-clear"
                      onClick={() =>
                        update(
                          mode === 'attacking'
                            ? { ...selection, attacker: null }
                            : { ...selection, defenders: [] },
                        )
                      }
                    >
                      {t('chart.clear')}
                    </button>
                  )}
                </div>
                <div
                  className="type-chart-choices"
                  role="group"
                  aria-label={t(mode === 'attacking' ? 'chart.pickAttack' : 'chart.pickDefense')}
                >
                  {chartTypes.map((type) => (
                    <button
                      key={type}
                      type="button"
                      aria-pressed={selected.includes(type)}
                      onClick={() => choose(type)}
                    >
                      <TypeBadge type={type} />
                    </button>
                  ))}
                </div>
              </Card>
              {selected.length > 0 &&
                (mode === 'attacking' && attacker ? (
                  <MatchupResults attacker={attacker} />
                ) : (
                  <MatchupResults defenders={defenders} />
                ))}
            </>
          )}
        </div>
        {(desktop || mode === 'full') && (
          <div className="type-chart-panel">
            {desktop && (
              <Link to="/pokedex/moves" className="type-chart-moves">
                {t('titles.moves')} ›
              </Link>
            )}
            <MatchupTable
              defenders={mode === 'attacking' ? [] : defenders}
              attacker={mode === 'attacking' ? attacker : null}
            />
          </div>
        )}
      </div>
    </TabScreen>
  )
}
