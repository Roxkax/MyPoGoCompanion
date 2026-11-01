import { useAuth } from '../auth/authContext'
import { GoogleButton } from './GoogleButton'
import { Icon, type IconName } from './icons'

export type PersonalFeature = 'collection' | 'savedFilters' | 'ai'

const prompts: Record<PersonalFeature, { icon: IconName; title: string; text: string }> = {
  collection: {
    icon: 'collection',
    title: 'Your collection lives here',
    text: 'Sign in, then upload your Poke Genie export to see your Pokémon ranked for every league and raid, and to get "your counters" and "your team" across the app.',
  },
  savedFilters: {
    icon: 'filters',
    title: 'Keep your filters',
    text: 'Sign in to save the search strings you build and find them again on every device.',
  },
  ai: {
    icon: 'ask',
    title: 'Ask the AI assistant',
    text: 'Sign in to ask questions about Pokémon GO, answered from the app’s own data.',
  },
}

/** Shown in place of a personal screen (collection, saved filters, AI) when nobody is signed in (screen F1). */
export function SignedOutPrompt({ feature }: { feature: PersonalFeature }) {
  const { signInError } = useAuth()
  const prompt = prompts[feature]
  return (
    <section className="signed-out card" aria-labelledby={`signed-out-${feature}`}>
      <span className="signed-out-icon">
        <Icon name={prompt.icon} size={20} />
      </span>
      <h2 id={`signed-out-${feature}`}>{prompt.title}</h2>
      <p>{prompt.text}</p>
      <GoogleButton />
      {signInError && (
        <p className="sign-in-error" role="alert">
          {signInError}
        </p>
      )}
    </section>
  )
}
