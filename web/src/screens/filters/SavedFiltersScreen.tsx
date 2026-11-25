import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { api, dataOrThrow } from '../../api/client'
import { useAuth } from '../../auth/authContext'
import { LoadError } from '../../components/LoadError'
import { useFormats } from '../../i18n/languageContext'
import { CopyableSearch } from '../../ui/CopyableSearch'
import { Sheet } from '../../ui/Sheet'
import { FilterNameSheet } from './FilterNameSheet'
import { characterLimit } from './filterBuilder'
import {
  savedFilterAddress,
  savedFiltersKey,
  useSavedFilters,
  useSavedSearchParts,
  type SavedFilter,
} from './savedFilters'

type Action = 'rename' | 'duplicate' | 'delete' | 'menu' | 'copy'

export function SavedFiltersScreen() {
  const { state } = useAuth()
  return state.status === 'signedIn' ? (
    <SavedList key={state.user.id} userId={state.user.id} />
  ) : null
}

function SavedList({ userId }: { userId: string }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const client = useQueryClient()
  const query = useSavedFilters(userId)
  const [selected, setSelected] = useState<{ filter: SavedFilter; action: Action } | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState(false)
  const close = () => setSelected(null)

  function refresh() {
    close()
    void client.invalidateQueries({ queryKey: savedFiltersKey(userId) })
  }

  async function nameFilter(name: string) {
    if (!selected) return
    const params = { path: { id: selected.filter.id } }
    const body = { name }
    if (selected.action === 'rename') {
      dataOrThrow(await api.PUT('/saved-filters/{id}/name', { params, body }))
    } else {
      dataOrThrow(await api.POST('/saved-filters/{id}/duplicate', { params, body }))
    }
    refresh()
  }

  async function deleteFilter() {
    if (!selected || deleting) return
    setDeleting(true)
    setDeleteError(false)
    try {
      dataOrThrow(
        await api.DELETE('/saved-filters/{id}', { params: { path: { id: selected.filter.id } } }),
      )
      refresh()
    } catch {
      setDeleteError(true)
    } finally {
      setDeleting(false)
    }
  }

  return (
    <section className="saved-filters" aria-label={t('titles.savedFilters')}>
      <div className="saved-filter-heading">
        <h2>{t('titles.savedFilters')}</h2>
        <Link className="button" to="/filters/builder">
          {t('savedFilters.new')}
        </Link>
      </div>
      {query.isPending && <p role="status">{t('savedFilters.loading')}</p>}
      {query.isError && <LoadError error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.length === 0 && <p>{t('savedFilters.empty')}</p>}
      {!!query.data?.length && (
        <ul className="ui-card saved-filter-list">
          {query.data.map((filter) => (
            <li key={filter.id} className="saved-filter-row">
              <Link to={savedFilterAddress(filter.id)}>{filter.name}</Link>
              <code dir="ltr">{filter.searchString}</code>
              <time dateTime={filter.createdAt}>
                {t('savedFilters.savedOn', { date: formats.day(new Date(filter.createdAt)) })}
              </time>
              {filter.searchString.length <= characterLimit ? (
                <CopyableSearch
                  key={filter.searchString}
                  searchString={filter.searchString}
                  preview={false}
                />
              ) : (
                <button
                  type="button"
                  className="button"
                  onClick={() => setSelected({ filter, action: 'copy' })}
                >
                  {t('keepers.copy')}
                </button>
              )}
              <button
                type="button"
                className="icon-button"
                aria-haspopup="dialog"
                aria-label={t('savedFilters.more', { name: filter.name })}
                onClick={() => setSelected({ filter, action: 'menu' })}
              >
                ⋯
              </button>
            </li>
          ))}
        </ul>
      )}
      {selected?.action === 'menu' && (
        <Sheet id="saved-filter-menu" title={selected.filter.name} onClose={close}>
          <div className="saved-filter-menu">
            <Link className="button" to={savedFilterAddress(selected.filter.id)}>
              {t('savedFilters.open')}
            </Link>
            {(['rename', 'duplicate', 'delete'] as const).map((action) => (
              <button
                key={action}
                className="button"
                type="button"
                onClick={() => {
                  setDeleteError(false)
                  setSelected({ filter: selected.filter, action })
                }}
              >
                {t(`savedFilters.${action}`)}
              </button>
            ))}
          </div>
        </Sheet>
      )}
      {(selected?.action === 'rename' || selected?.action === 'duplicate') && (
        <FilterNameSheet
          title={t(`savedFilters.${selected.action}Title`)}
          search={selected.filter.searchString}
          initialName={
            selected.action === 'rename'
              ? selected.filter.name
              : t('savedFilters.copyName', { name: selected.filter.name }).slice(0, 100)
          }
          action={t(`savedFilters.${selected.action}`)}
          onClose={close}
          onSubmit={nameFilter}
        />
      )}
      {selected?.action === 'delete' && (
        <Sheet
          id="delete-saved-filter"
          title={t('savedFilters.deleteTitle')}
          onClose={() => {
            if (!deleting) close()
          }}
        >
          <p>{t('savedFilters.deleteQuestion', { name: selected.filter.name })}</p>
          {deleteError && <p role="alert">{t('savedFilters.actionError')}</p>}
          <div className="saved-filter-actions">
            <button type="button" className="button" disabled={deleting} onClick={close}>
              {t('savedFilters.cancel')}
            </button>
            <button
              type="button"
              className="button"
              disabled={deleting}
              onClick={() => void deleteFilter()}
            >
              {t('savedFilters.delete')}
            </button>
          </div>
        </Sheet>
      )}
      {selected?.action === 'copy' && (
        <SavedSearchParts search={selected.filter.searchString} onClose={close} />
      )}
    </section>
  )
}

function SavedSearchParts({ search, onClose }: { search: string; onClose: () => void }) {
  const { t } = useTranslation()
  const formats = useFormats()
  const query = useSavedSearchParts(search)
  return (
    <Sheet id="saved-filter-copy" title={t('keepers.copy')} onClose={onClose}>
      {query.isPending && <p role="status">{t('filterBuilder.status.Checking')}</p>}
      {query.isError && <LoadError error={query.error} onRetry={() => void query.refetch()} />}
      {query.data &&
        (query.data.parts.length ? (
          <div className="builder-copy">
            <p className="builder-note">{t('filterBuilder.pasteParts')}</p>
            {query.data.parts.map((part, index) => (
              <CopyableSearch
                key={index}
                searchString={part}
                range={{ number: t('filterBuilder.part', { number: formats.number(index + 1) }) }}
              />
            ))}
          </div>
        ) : (
          <p role="status">{t('savedFilters.copyError')}</p>
        ))}
    </Sheet>
  )
}
