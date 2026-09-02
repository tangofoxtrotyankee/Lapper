-- Lapper initial metadata schema skeleton.
-- Claude Code should convert this into the chosen Drizzle migration format during Phase 5.

create extension if not exists pgcrypto;

create table organisations (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  plan text not null default 'alpha',
  billing_status text not null default 'none',
  data_region text not null default 'eu',
  created_at timestamptz not null default now()
);

create table users (
  id uuid primary key default gen_random_uuid(),
  auth_subject text not null unique,
  email_hash text,
  status text not null default 'active',
  created_at timestamptz not null default now(),
  deleted_at timestamptz
);

create table memberships (
  organisation_id uuid not null references organisations(id) on delete cascade,
  user_id uuid not null references users(id) on delete cascade,
  role text not null,
  created_at timestamptz not null default now(),
  primary key (organisation_id, user_id)
);

create table laps (
  id uuid primary key default gen_random_uuid(),
  organisation_id uuid not null references organisations(id) on delete cascade,
  user_id uuid references users(id) on delete cascade,
  name text not null,
  description text,
  configuration jsonb not null default '{}'::jsonb,
  version integer not null default 1,
  is_enabled boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table organisations enable row level security;
alter table memberships enable row level security;
alter table laps enable row level security;

-- Policies intentionally completed in Phase 5 after runtime transaction-context implementation.
