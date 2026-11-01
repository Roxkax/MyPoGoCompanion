import { api, ApiError, dataOrThrow, type Schemas } from '../api/client'

// The sign-in and account calls, on the client generated from the API's OpenAPI document.

export { ApiError }

export type Role = Schemas['EffectiveRole']

/** The signed-in user. */
export type Me = Schemas['MeResponse']

/** A sign-in or refresh: a 15-minute access token, the next refresh token and the user. */
export type SessionResponse = Schemas['SessionResponse']

const bearer = (accessToken: string) => ({ Authorization: `Bearer ${accessToken}` })

export async function fetchGoogleClientId(): Promise<string> {
  return dataOrThrow(await api.GET('/auth/google/client')).clientId
}

/** `language`: the one the device is showing; a new account starts with it (an existing one keeps its own). */
export async function signInWithGoogle(
  code: string,
  codeVerifier: string,
  redirectUri: string,
  language?: string,
): Promise<SessionResponse> {
  return dataOrThrow(
    await api.POST('/auth/google', { body: { code, codeVerifier, redirectUri, language } }),
  )
}

export async function refreshSession(refreshToken: string): Promise<SessionResponse> {
  return dataOrThrow(await api.POST('/auth/refresh', { body: { refreshToken } }))
}

export async function signOut(refreshToken: string): Promise<void> {
  dataOrThrow(await api.POST('/auth/sign-out', { body: { refreshToken } }))
}

/** Saves the interface language with the account (en or pt-PT). */
export async function saveLanguage(accessToken: string, language: string): Promise<void> {
  dataOrThrow(await api.PUT('/me/language', { headers: bearer(accessToken), body: { language } }))
}

export async function signOutEverywhere(accessToken: string): Promise<void> {
  dataOrThrow(await api.POST('/auth/sign-out-everywhere', { headers: bearer(accessToken) }))
}
