# Environments and Deployment

## Environments
Maintain fully separate:
- local
- staging
- production

Do not share production database credentials, OpenAI keys, Auth0 applications or Stripe secrets with staging.

## Backend environment variables
Example names only:

```text
NODE_ENV=
PORT=
DATABASE_URL=
DATABASE_DIRECT_URL=
AUTH0_ISSUER_BASE_URL=
AUTH0_AUDIENCE=
OPENAI_API_KEY=
OPENAI_BASE_URL=
OPENAI_MODEL_CHEAP=gpt-5.6-luna
OPENAI_MODEL_DEFAULT=gpt-5.6-terra
OPENAI_MODEL_DEEP=gpt-5.6-sol
SENTRY_DSN=
STRIPE_SECRET_KEY=
STRIPE_WEBHOOK_SECRET=
```

Do not put secrets in checked-in `.env` files.

## Desktop public configuration
Permitted:
- API base URL
- Auth0 domain
- Auth0 native client ID
- Auth0 audience
- app release channel

Never embed:
- OpenAI key
- database URL
- Stripe secret
- Auth0 client secret
- signing private key

## Railway
Deploy backend with:
- EU region
- health check `/health/ready`
- minimum one instance
- restart on failure
- environment-specific service variables

Add external uptime monitoring because deployment health checks are not equivalent to continuous production monitoring.

## Neon
Use:
- pooled connection for runtime
- direct connection for migrations
- separate staging/prod projects or branches with deliberate access policy

## Database migrations
CI/CD should not allow random application instances to perform migrations at startup.

Preferred production flow:
1. build/test
2. backup/check migration safety
3. run migration with migrator credential
4. deploy API
5. smoke test readiness

## Desktop releases
- build x64 MSIX
- sign package
- produce checksums
- retain release provenance/SBOM
- staged release channel during beta
- never auto-run downloaded executables outside signed update path
