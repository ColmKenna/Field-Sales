#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
demo_volume="fieldsales-demo-sqlserver-data"

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <start|seed|status|reset>" >&2
    exit 2
fi

case "$1" in
    start)
        exec dotnet run --project "$repo_root/src/FieldSales.AppHost" --launch-profile demo
        ;;
    seed|status)
        # Check the demo container is running before invoking the fixed local endpoint.
        running="$(docker ps --filter "volume=$demo_volume" --format '{{.ID}}')"
        if [[ -z "$running" ]]; then
            echo "Start the demo environment first: $0 start" >&2
            exit 1
        fi
        exec dotnet run --project "$repo_root/tools/FieldSales.DemoData" -- "$1"
        ;;
    reset)
        # Never stop an active application or force-remove its storage. Docker also
        # refuses removal if a container acquires the volume after this check.
        running="$(docker ps --filter "volume=$demo_volume" --format '{{.ID}}')"
        if [[ -n "$running" ]]; then
            echo "Stop the demo AppHost (Ctrl+C) before resetting its data." >&2
            exit 1
        fi
        stopped="$(docker ps -a --filter "volume=$demo_volume" --format '{{.ID}}')"
        while IFS= read -r container; do
            if [[ -n "$container" ]]; then docker rm "$container" >/dev/null; fi
        done <<< "$stopped"
        volumes="$(docker volume ls --format '{{.Name}}')"
        if ! printf '%s\n' "$volumes" | grep -Fx "$demo_volume" >/dev/null; then
            echo "Demo data is already removed."
            exit 0
        fi
        docker volume rm "$demo_volume" >/dev/null
        echo "Removed demo databases, sample data, and demo sessions. Start the demo profile to recreate empty databases."
        ;;
    *)
        echo "Usage: $0 <start|seed|status|reset>" >&2
        exit 2
        ;;
esac
