#!/usr/bin/env bash
# Run from the Pulumi project: bash ddns/export-config.sh DESTINATION_DIRECTORY
set -euo pipefail
umask 077

destination=$1
mkdir -p "$destination"
chmod 700 "$destination"

pulumi stack output DnsUpdaterConfig --json |
    jq '. + {clientSecretFile: "/etc/azure-dns-ddns/client-secret"}' > "$destination/config.json"
pulumi stack output DnsUpdaterClientSecret --show-secrets --json |
    jq -r '.' > "$destination/client-secret"
chmod 600 "$destination/config.json" "$destination/client-secret"
