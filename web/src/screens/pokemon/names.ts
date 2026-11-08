import type { TFunction } from 'i18next'
import type { Language } from '../../i18n/languages'
import type { FamilyMember, PageForm, PageMove, PageVersion, PokemonPage } from '../../pokemon/page'

// Names on the Pokémon page in the app's language: the game's Portuguese text when there is one, else English.

export function speciesNameOf(page: PokemonPage, language: Language): string {
  return (language === 'pt-PT' ? page.namePt : null) ?? page.nameEn
}

export function formName(form: PageForm, page: PokemonPage, language: Language): string {
  if (form.isDefault) return speciesNameOf(page, language)
  return (language === 'pt-PT' ? form.namePt : null) ?? form.nameEn
}

export function memberName(member: FamilyMember, language: Language): string {
  return (language === 'pt-PT' ? member.namePt : null) ?? member.nameEn
}

export function moveName(move: PageMove | undefined, id: string, language: Language): string {
  if (!move) return id
  return (language === 'pt-PT' ? move.namePt : null) ?? move.nameEn
}

/** The switcher's text: the species' name for the main form, "Shadow", "Mega", "Shadow Hisuian"… */
export function versionLabel(
  version: PageVersion,
  page: PokemonPage,
  language: Language,
  t: TFunction,
): string {
  const form = version.form
  const label = form.isDefault
    ? null
    : ((language === 'pt-PT' ? form.labelPt : null) ?? form.labelEn ?? form.nameEn)
  if (!version.shadow) return label ?? speciesNameOf(page, language)
  return label ? `${t('versions.shadow')} ${label}` : t('versions.shadow')
}
