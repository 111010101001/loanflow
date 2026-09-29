#!/usr/bin/env bash
# Adds an EF Core migration for the ledger database.
#
#   ./scripts/add-migration.sh InitialLedger
#
# The migration lands in src/LoanFlow.Infrastructure/Ledger/Migrations. The Worker applies
# pending migrations when it starts in Development, so there's no "database update" step.
# Read the generated Up()/Down() methods before committing: that's where you see what
# your Fluent API configuration actually does to the schema.
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <MigrationName>   e.g. $0 InitialLedger" >&2
  exit 1
fi

cd "$(dirname "$0")/.."

# Installs the dotnet-ef version pinned in .config/dotnet-tools.json (no global install needed).
dotnet tool restore

dotnet ef migrations add "$1" \
  --project src/LoanFlow.Infrastructure \
  --startup-project src/LoanFlow.Worker \
  --output-dir Ledger/Migrations
