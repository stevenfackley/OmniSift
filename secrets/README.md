# Secrets

This directory holds Docker file-based secrets used by `docker-compose.prod.yml`.

**`*.txt` files are gitignored — never commit secret values.**

## Required files

Create each file containing only the secret value (no trailing newline needed):

| File | Content |
|---|---|
| `postgres_password.txt` | PostgreSQL password for the `omnisift` superuser |
| `Anthropic__ApiKey.txt` | Anthropic API key (`sk-ant-...`) |
| `OpenAI__ApiKey.txt` | OpenAI API key (`sk-...`) |
| `Tavily__ApiKey.txt` | Tavily search API key (`tvly-...`) |
| `Jwt__Secret.txt` | JWT signing secret (min 32 chars — generate with `openssl rand -base64 48`) |
| `ConnectionStrings__DefaultConnection.txt` | Full Npgsql connection string (see below) |

### Connection string format

```
Host=db;Port=5432;Database=omnisift;Username=omnisift_app;Password=<postgres_password>
```

## Quick setup for local prod testing

```bash
mkdir -p secrets
echo "your_postgres_password"              > secrets/postgres_password.txt
echo "sk-ant-..."                          > secrets/Anthropic__ApiKey.txt
echo "sk-..."                              > secrets/OpenAI__ApiKey.txt
echo "tvly-..."                            > secrets/Tavily__ApiKey.txt
openssl rand -base64 48                    > secrets/Jwt__Secret.txt
echo "Host=db;Port=5432;Database=omnisift;Username=omnisift_app;Password=your_postgres_password" \
                                           > secrets/ConnectionStrings__DefaultConnection.txt
```

Then run:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## CI/CD

In your deployment pipeline, write these files from your secrets manager
(HashiCorp Vault, AWS Secrets Manager, Azure Key Vault, GCP Secret Manager, etc.)
before invoking `docker compose up`.

## How it works

The API reads secret files from `/run/secrets/` at startup via `AddKeyPerFile`.
Filenames use `__` as the section separator, which maps to .NET config keys:
- `Anthropic__ApiKey` → `Anthropic:ApiKey`
- `ConnectionStrings__DefaultConnection` → `ConnectionStrings:DefaultConnection`
