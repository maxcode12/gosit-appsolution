# Production runbook

What exists in Azure today and how to change it. Written 2026-10-26.

## Where things live

Subscription **Zeezo Tech** (`782a1f48-4d72-45b7-881e-b35da2f4d3ef`), shared with other projects.

| What | Name | Notes |
|---|---|---|
| Public website | `swa-gosit-web` (rg `rg-gosit-web`) | Static Web Apps, free tier. `www.gosits.com` |
| Family, caregiver and staff web app | `app-gosit-web-kuu4l` (rg `rg-gosit-prod`) | App Service on the same plan, runs the Next.js container. One server answers both `app.gosits.com` (families and caregivers) and `admin.gosits.com` (staff); `src/proxy.ts` keeps the two sides on their own screens by Host header. |
| API | `app-gosit-api-kuu4l` (rg `rg-gosit-prod`, Canada Central) | App Service, Linux B1, runs the container, always on |
| App Service plan | `asp-gosit-prod` | Has room for the web app too |
| Database | `psql-gosit-kuu4l-p` | PostgreSQL 16, B1ms, 32 GB, **private network only** (no public address) |
| Network | `vnet-gosit-prod` | `snet-app` (API) and `snet-pg` (database) |
| Secrets | Key Vault `kv-gosit-kuu4l` | The API reads them through Key Vault references |
| Images | Registry `acrgositkuu4l` | |
| Files | Storage `stgositkuu4l` | `certification-documents`, `interview-recordings` |
| Identity | `id-gosit-api` | Pulls images and reads Key Vault. No passwords involved. |

## Why App Service and not Container Apps

The subscription is capped at two Container Apps environments in total and other projects use both. Moving to Container Apps later needs a quota increase.

## Deploy a new API version

```bash
TAG=v3-$(date +%Y%m%d%H%M)
az acr build -r acrgositkuu4l -t gosit-api:$TAG -f src/backend/Dockerfile src/backend
az webapp config container set -g rg-gosit-prod -n app-gosit-api-kuu4l \
  --container-image-name acrgositkuu4l.azurecr.io/gosit-api:$TAG
az webapp restart -g rg-gosit-prod -n app-gosit-api-kuu4l
```

Database changes apply by themselves on start (`Database__MigrateOnStartup=true`). The database has no public address, so there is no way to run SQL from a laptop. The health check `/health/ready` includes the database.

## Deploy the web app (app. and admin.)

```bash
TAG=w-$(date +%Y%m%d%H%M)
az acr build -r acrgositkuu4l -t gosit-web:$TAG -f src/web/gosit-web/Dockerfile   --build-arg NEXT_PUBLIC_API_BASE_URL=https://api.gosits.com   --build-arg NEXT_PUBLIC_SITE_URL=https://app.gosits.com src/web/gosit-web
az webapp config container set -g rg-gosit-prod -n app-gosit-web-kuu4l   --container-image-name acrgositkuu4l.azurecr.io/gosit-web:$TAG
az webapp restart -g rg-gosit-prod -n app-gosit-web-kuu4l
```

`NEXT_PUBLIC_*` values are baked in at build time, so changing one means a rebuild. The Windows CLI prints a harmless unicode crash while streaming the build log; check `az acr task list-runs -r acrgositkuu4l --top 1` for the real result. Settings on the app: `API_BASE_URL`, `APP_HOSTNAME`, `ADMIN_HOSTNAME`, `MARKETING_ORIGIN`.

Binding a domain needs, at the registrar, a CNAME (`app` or `admin` to `app-gosit-web-kuu4l.azurewebsites.net`) and a TXT (`asuid.app` or `asuid.admin`, value = the app's `customDomainVerificationId`, the same value as for the API). Then `az webapp config hostname add`, `az webapp config ssl create`, `az webapp config ssl bind`, as for `api.gosits.com`. Until a hostname is bound, App Service answers 404 "Site Not Found" for it.

## Deploy the public website

```bash
node scripts/build-marketing-site.mjs          # builds .marketing-build/out
SWA_CLI_DEPLOYMENT_TOKEN=$(az staticwebapp secrets list -n swa-gosit-web -g rg-gosit-web --query properties.apiKey -o tsv) \
  npx @azure/static-web-apps-cli deploy ./.marketing-build/out --env production
```

Run it from the repository root. The upload tool refuses to run from inside the folder it uploads. The contact form posts to `https://api.gosits.com` unless `MARKETING_API_URL` is set when building.

## Switches (App Service settings on the API)

- `Registration__Open` — `false` keeps sign-up closed. `true` opens it to everyone.
- `Registration__InvitedEmails` — addresses allowed to sign up while it is closed, separated by commas or spaces.
- `Cors__PublicFormOrigins` — websites allowed to post the contact form.
- `Certn__ApiKey` — turns the Certn connection on. Blank means every check waits for staff. Add it as a Key Vault secret and reference it, like the others.
- `Certn__BaseUrl` — `https://api.ca.certn.co` for production; Certn gives a different address for its testing environment.
- `Certn__WebhookSecret` — signs the messages Certn sends. Without it they are all refused.
- `Enquiries__NotifyEmail` — who is emailed when a message arrives. Blank means nobody, and it only works once outgoing email is configured.

## Secrets in Key Vault

`pg-admin-password`, `postgres-connection-string`, `jwt-signing-key`, `storage-connection-string`, `stripe-secret-key` (test mode), `livekit-api-key`, `livekit-api-secret`, `livekit-server-url`.

## Certn (background checks)

Built from Certn's public API reference and **not yet run against Certn**: it needs a test key. Identity and criminal record checks are ordered through Certn; the vulnerable sector check is deliberately left to staff until Certn confirms it exists in their current API. Certn emails the caregiver directly. Its notifications go to `https://api.gosits.com/api/v1/webhooks/certn`. Results appear beside the case in the admin verification queue as evidence; staff still approve or reject. First test: order one case in Certn's sandbox, watch the notification arrive, and compare the request shapes in `CertnBackgroundCheckProvider.cs` with what Certn accepts.

## Not done yet

- Certn: a test key, the signing secret, and registering the notification address in Certn's portal.
- The first administrator account (`Administration__Bootstrap__Email` / `__Password`).
- Stripe webhook endpoint for production and its signing secret.
- Outgoing email (`Email__SmtpUsername`, `Email__SmtpPassword`, `Email__FromEmail`).
- Azure Maps key (address distance search degrades without it).
- Family and caregiver web app at `app.gosits.com`, staff at `admin.gosits.com`.
- Monitoring and alerts (a Log Analytics workspace exists; nothing is sending to it yet).
- Patch the NuGet packages flagged with known vulnerabilities (`System.Security.Cryptography.Pkcs`).
