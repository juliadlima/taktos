#!/usr/bin/env bash
set -euo pipefail

if [[ ! "${TAKTOS_APP_SQL_PASSWORD}" =~ ^[A-Za-z0-9]{16,80}$ ]]; then
    echo "Use de 16 a 80 letras e números na senha SQL da aplicação." >&2
    exit 1
fi

export SQLCMDPASSWORD="$TAKTOS_SQL_PASSWORD"
sqlcmd=/opt/mssql-tools18/bin/sqlcmd
for script in /opt/taktos/banco/[0-9][0-9]-*.sql; do
    "$sqlcmd" -S sql -U sa -C -b -d master -i "$script"
done

"$sqlcmd" -S sql -U sa -C -b -d master -i /opt/taktos/infra/permissoes-sql.sql \
    -v "SenhaPortal=$TAKTOS_APP_SQL_PASSWORD"
