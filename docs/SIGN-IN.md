# Sign-in (Google) and roles

How signing in works, how to set it up for QA, prod and your computer, and the security review (E01-F02).

## How it works

1. **Continue with Google** sends the browser to Google with the app's client id, a random `state` and a PKCE challenge: the SHA-256 of a secret the browser keeps (authorization code + PKCE).
2. Google sends the browser back to the web app's `/auth/callback` with a one-time code. The web app checks the `state`, then passes the code, the PKCE secret and the language the device is showing to the API (`POST /auth/google`). A new account starts with that language; an existing account keeps its own, and the device switches to it (E01-F04).
3. The API swaps the code with Google using the client secret, which only the API knows. It checks that the answer is a real, verified Google account for our client, creates or updates the `users` row (name, email, picture) and throws Google's own tokens away.
4. The API answers with **its own tokens**:
   - an **access token**: signed, 15 minutes, kept only in the page's memory and sent with each API call;
   - a **refresh token**: random, 30 days, kept in the browser's localStorage. The database keeps only its SHA-256 hash.
5. When the access token runs out, the web app swaps the refresh token for a new pair (`POST /auth/refresh`). Each swap replaces the refresh token. If an old one is ever presented again, it was copied, so that whole sign-in ends and the person signs in again on that device.
6. **Sign out** ends this device's sign-in. **Sign out everywhere** ends every sign-in of the account.

**Roles:**
- The **super admin** is the account whose Google-confirmed email is `Auth__SuperAdminEmail`: your account. It comes from configuration, so no database change or QA reset can remove it.
- **Admins** are stored per user (`users.role`). Only the super admin can make someone admin or remove it (`GET /admin/users`, `PUT /admin/users/{id}/role`), and every change is kept in `role_changes`.
- The API reads the role from the database on every admin request, so a removal applies at once.
- The "Users & roles" screen comes with the admin panel (E10-F01, issue #924). Until then you are the only admin.

## Setting it up (about 20 minutes, once)

### 1. Google Cloud: the OAuth client (free)

1. Open [console.cloud.google.com](https://console.cloud.google.com), signed in with **your own Google account** (the super admin).
2. Top bar → project picker → **New project** → name `mypogocompanion` → **Create**, then select it.
3. Left menu → **Google Auth Platform** (or search "OAuth consent screen") → **Get started**:
   - **App name** `MyPoGoCompanion`, **User support email** your email. Don't upload a logo: a logo makes Google review the app.
   - **Audience**: **External**.
   - **Contact information**: your email → **Create**.
4. **Audience** page → **Publish app** → confirm. The app asks only for name, email and picture, which Google doesn't review. While the app is in "Testing", only the test users you list there can sign in.
5. **Clients** → **Create client**:
   - **Application type**: Web application. **Name**: `MyPoGoCompanion web`.
   - **Authorized JavaScript origins**: leave empty.
   - **Authorized redirect URIs**: add all of these (the web addresses are in Azure → Static Web App `swa-mypogocompanion` → **Overview** → URL, and its **Environments** → `qa`):
     - `https://<prod web address>/auth/callback`
     - `https://<qa web address>/auth/callback`
     - `http://localhost:5173/auth/callback`
     - `http://localhost:4173/auth/callback`
   - **Create**. Copy the **Client ID** and the **Client secret** (shown once; you can add a new secret later if lost).

### 2. A signing key per environment

Make two different random keys, one for `qa` and one for `prod`. In Azure Cloud Shell or any terminal:

```bash
openssl rand -base64 32
```

(PowerShell: `[Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }))`.)

### 3. GitHub: environments `qa` and `prod`

Settings → **Environments** → `qa`, then the same for `prod`:

| Kind | Name | Value |
|---|---|---|
| Variable | `GOOGLE_CLIENT_ID` | the Client ID (same for both) |
| Variable | `SUPER_ADMIN_EMAIL` | your Google email (same for both) |
| Secret | `GOOGLE_CLIENT_SECRET` | the Client secret (same for both) |
| Secret | `AUTH_SIGNING_KEY` | that environment's key from step 2 |

The next deploy passes them to the API: for QA, a push to a PR with the `qa` label; for prod, the next merge (or **Prod deploy → Run workflow**). Until all three of client id, client secret and signing key are set, the deploy prints a warning, the API answers "Sign-in isn't set up on this server yet" and everything else works.

### 4. Check it

- Open the web app → profile button (top right) → **Continue with Google** → choose your account → you're back on the page, signed in. The account menu shows **Super admin**.
- `https://<api address>/auth/google/client` answers your client id (503 means a setting is missing).

### Signing in on your computer

The API reads the same settings from .NET user secrets, which stay outside the repository:

```bash
cd api/MyPoGoCompanion.Api
dotnet user-secrets set "Auth:Google:ClientId" "<client id>"
dotnet user-secrets set "Auth:Google:ClientSecret" "<client secret>"
dotnet user-secrets set "Auth:SigningKey" "$(openssl rand -base64 32)"
dotnet user-secrets set "Auth:SuperAdminEmail" "<your Google email>"
```

Then run the API and the web dev server as usual (`README.md`) and sign in at `http://localhost:5173`.

## Security review (2026-11-01)

| Topic | What we do | What's left / accepted |
|---|---|---|
| **Token storage** | Access token in memory only (15 min). Refresh token in localStorage (owner's decision: the web app and the API are on different sites, so a cookie scripts can't read would be a third-party cookie that Safari and Firefox block). Rotated on every use, and reuse ends the sign-in. Only its SHA-256 is stored server-side. Strict **Content Security Policy** (scripts only from the site, connections only to the site and the API, no framing), so an injected script has little room to run or to send a token anywhere. | A script-injection bug could still read the refresh token. Move it to a cookie scripts can't read when a custom domain puts the app and the API on one site. |
| **CSRF** | Not applicable: the API never reads cookies; every call carries its token in the `Authorization` header or the body, which another site can't add. The OAuth `state` (random, checked on return) stops a forged Google callback; PKCE stops a stolen code being used. | — |
| **CORS** | Only the environment's web address (`Cors__AllowedOrigins`); no credentials mode. The OAuth return address must be an allowed web address plus `/auth/callback`, checked by the API before it asks Google. | — |
| **Rate limits** | `/auth/*`: 20 requests a minute per client address (a sign-in or refresh needs one or two); `429` with `Retry-After`. The client address comes from Container Apps' ingress (`X-Forwarded-For`, one hop; only the ingress can reach the container). | Per-user limits for the AI come with E11-F01. |
| **Google's answer** | The code is swapped server-side with the client secret. The ID token comes straight from Google over TLS, so its issuer, audience (our client), expiry and **verified email** are checked; the signature check is left to TLS (OpenID Connect Core 3.1.3.7). | — |
| **Roles** | The super admin comes from configuration; admin requests read the role from the database, not the token; only the super admin changes roles; the super admin can't be changed through the API; every change is kept. | The super admin's email must be a Google account that you control. |
| **Logs** | Tokens and secrets are never logged. Google's error codes are logged (cut to 300 characters) to explain failed sign-ins. Reuse of a refresh token is logged with the user and sign-in ids. | — |
| **Data kept** | Google name, email and picture (as the sign-in screen says), plus refresh-token hashes (deleted after they expire, at the user's next sign-in or refresh). | Delete account / download data is required before any public launch (E24-F05). |

**Known trade-off:** if a refresh request reaches the API but the answer is lost (the network drops at that moment), the browser retries with the replaced token and that sign-in ends, so the person signs in again. This is rare, and it's the price of detecting copied tokens.
