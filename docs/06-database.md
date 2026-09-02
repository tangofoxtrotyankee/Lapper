# Database Plan

## Cloud DB
Neon PostgreSQL.

## ORM
Drizzle ORM.

## Rules
- metadata/configuration only
- no screenshots
- no OCR text
- no captured UI text
- no prompt/response bodies
- all tenant-owned data protected by RLS

## Tables

### users
- id uuid pk
- auth_subject text unique
- email_hash text nullable
- status text
- created_at timestamptz
- deleted_at timestamptz nullable

### organisations
- id uuid pk
- name text
- plan text
- billing_status text
- data_region text
- created_at timestamptz

### memberships
- organisation_id uuid fk
- user_id uuid fk
- role text
- created_at timestamptz
- composite unique organisation_id,user_id

### devices
- id uuid pk
- user_id uuid fk
- installation_id text unique
- platform text
- app_version text
- last_seen_at timestamptz
- revoked_at timestamptz nullable

### user_settings
- user_id uuid pk/fk
- settings jsonb
- updated_at timestamptz

### laps
- id uuid pk
- organisation_id uuid fk
- user_id uuid nullable fk
- name text
- description text nullable
- configuration jsonb
- version int
- is_enabled boolean
- created_at timestamptz
- updated_at timestamptz

### subscriptions
- organisation_id uuid pk/fk
- stripe_customer_id text nullable
- stripe_subscription_id text nullable
- plan text
- status text
- current_period_end timestamptz nullable

### entitlements
- organisation_id uuid pk/fk
- monthly_requests int
- monthly_image_requests int
- deep_requests int
- laps_limit int
- members_limit int
- voice_seconds int

### usage_daily
- organisation_id uuid
- user_id uuid
- usage_date date
- text_requests int
- image_requests int
- input_tokens bigint
- output_tokens bigint
- voice_seconds int
- estimated_cost_micros bigint
- primary key organisation_id,user_id,usage_date

### ai_runs
- request_id uuid pk
- organisation_id uuid
- user_id uuid
- model text
- route text
- prompt_version text
- input_tokens int
- output_tokens int
- image_used boolean
- latency_ms int
- success boolean
- error_code text nullable
- created_at timestamptz

### feedback
- id uuid pk
- request_id uuid nullable
- organisation_id uuid
- user_id uuid
- rating smallint nullable
- category text nullable
- comment text nullable
- content_attachment_opt_in boolean default false
- created_at timestamptz

### audit_events
For security/account/admin actions only. No screen content.

## Roles
- lapper_migrator
- lapper_runtime
- lapper_readonly

Runtime role does not own tables, is not superuser and cannot bypass RLS.

## RLS request context
At the start of DB transaction set:
- `app.user_id`
- `app.org_id`

Use transaction-local settings only.

## No vector database
Do not add embeddings/history search during MVP.
