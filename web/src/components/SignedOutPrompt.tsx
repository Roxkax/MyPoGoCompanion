import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/authContext'
import { GoogleButton } from './GoogleButton'
import { Icon, type IconName } from './icons'

export type PersonalFeature = 'collection' | 'savedFilters' | 'ai'

const prompts = {
  collection: {
    icon: 'collection',
    title: 'signedOut.collectionTitle',
    text: 'signedOut.collectionText',
  },
  savedFilters: {
    icon: 'filters',
    title: 'signedOut.savedFiltersTitle',
    text: 'signedOut.savedFiltersText',
  },
  ai: { icon: 'ask', title: 'signedOut.aiTitle', text: 'signedOut.aiText' },
} as const satisfies Record<PersonalFeature, { icon: IconName; title: string; text: string }>

/** Shown in place of a personal screen (collection, saved filters, AI) when nobody is signed in (screen F1). */
export function SignedOutPrompt({ feature }: { feature: PersonalFeature }) {
  const { signInError } = useAuth()
  const { t } = useTranslation()
  const prompt = prompts[feature]
  return (
    <section className="signed-out card" aria-labelledby={`signed-out-${feature}`}>
      <span className="signed-out-icon">
        <Icon name={prompt.icon} size={20} />
      </span>
      <h2 id={`signed-out-${feature}`}>{t(prompt.title)}</h2>
      <p>{t(prompt.text)}</p>
      <GoogleButton />
      {signInError && (
        <p className="sign-in-error" role="alert">
          {t(`signIn.errors.${signInError}`)}
        </p>
      )}
    </section>
  )
}
