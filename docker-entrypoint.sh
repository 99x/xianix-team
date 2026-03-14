#!/bin/sh
# docker-entrypoint.sh
#
# Runs as root on container start. Fixes ownership on named volumes so the
# non-root 'agent' user (uid 1001) can write to them, then drops privileges
# and execs the real command.
#
# Root cause: Docker named volumes are initialised with root ownership.
# The Dockerfile's RUN chown only affects the image layer; the volume mount
# at runtime overlays it, so the chown has no effect on the live filesystem.

set -e

chown -R agent:agent /tmp/pr-review-cache /home/agent/.claude 2>/dev/null || true

# Claude stores its config at ~/.claude.json (outside the ~/.claude/ directory).
# If the file is missing but a backup exists inside the volume, restore it so
# Claude does not emit repeated "configuration file not found" warnings.
CLAUDE_CONFIG="/home/agent/.claude.json"
CLAUDE_BACKUP_DIR="/home/agent/.claude/backups"
if [ ! -f "$CLAUDE_CONFIG" ] && [ -d "$CLAUDE_BACKUP_DIR" ]; then
    LATEST_BACKUP=$(ls -t "$CLAUDE_BACKUP_DIR"/.claude.json.backup.* 2>/dev/null | head -n1)
    if [ -n "$LATEST_BACKUP" ]; then
        cp "$LATEST_BACKUP" "$CLAUDE_CONFIG"
        chown agent:agent "$CLAUDE_CONFIG"
    fi
fi

exec gosu agent "$@"
