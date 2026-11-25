import { useQuery } from '@tanstack/react-query'
import { api, dataOrThrow, type Schemas } from '../../api/client'

export type SavedFilter = Schemas['SavedFilterResponse']

export const savedFiltersKey = (userId: string) => ['saved-filters', userId] as const

export function useSavedFilters(userId: string) {
  return useQuery({
    queryKey: savedFiltersKey(userId),
    queryFn: async ({ signal }) => dataOrThrow(await api.GET('/saved-filters', { signal })),
    gcTime: 0,
    retry: false,
  })
}

export function useSavedFilter(userId: string, id: string) {
  return useQuery({
    queryKey: [...savedFiltersKey(userId), id],
    queryFn: async ({ signal }) =>
      dataOrThrow(await api.GET('/saved-filters/{id}', { params: { path: { id } }, signal })),
    gcTime: 0,
    retry: false,
  })
}

export const savedFilterAddress = (id: string) => `/filters/builder?saved=${encodeURIComponent(id)}`

const draftKey = 'mpgc.filterSignInDraft'

export function rememberFilterDraft(value: string) {
  try {
    sessionStorage.setItem(draftKey, value)
  } catch {
    // The builder still works when the browser has disabled storage.
  }
}

export function readFilterDraft() {
  try {
    return sessionStorage.getItem(draftKey) ?? ''
  } catch {
    return ''
  }
}

export function clearFilterDraft() {
  try {
    sessionStorage.removeItem(draftKey)
  } catch {
    // Storage can be disabled.
  }
}

export function useSavedSearchParts(search: string) {
  return useQuery({
    queryKey: ['saved-search-parts', search],
    queryFn: async ({ signal }) =>
      dataOrThrow(await api.POST('/filter-search/check', { body: { expression: search }, signal })),
    gcTime: 0,
    retry: false,
  })
}
