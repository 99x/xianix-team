# ── Stage 1: Build .NET 9 app ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY AgentTeam.Console/AgentTeam.Console.csproj AgentTeam.Console/
RUN dotnet restore AgentTeam.Console/AgentTeam.Console.csproj

COPY AgentTeam.Console/ AgentTeam.Console/
RUN dotnet publish AgentTeam.Console/AgentTeam.Console.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
# Base: .NET 9 runtime on Debian Bookworm slim
FROM mcr.microsoft.com/dotnet/runtime:9.0

ENV DEBIAN_FRONTEND=noninteractive

# ── Core system packages ───────────────────────────────────────────────────────
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    gnupg \
    git \
    gosu \
    python3 \
    util-linux \
    jq \
    bash \
  && rm -rf /var/lib/apt/lists/*

# ── Node.js 20 (claude CLI + npx for GitHub MCP server) ──────────────────────
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
  && apt-get install -y nodejs \
  && rm -rf /var/lib/apt/lists/*

# ── Claude Code CLI ───────────────────────────────────────────────────────────
RUN npm install -g @anthropic-ai/claude-code

# ── GitHub CLI (gh) — posts GitHub PR review comments ────────────────────────
RUN curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
      | dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg \
  && chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg \
  && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
      > /etc/apt/sources.list.d/github-cli.list \
  && apt-get update && apt-get install -y gh \
  && rm -rf /var/lib/apt/lists/*

# ── Azure CLI + azure-devops extension — posts ADO PR review threads ──────────
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash \
  && az extension add --name azure-devops --yes \
  && rm -rf /var/lib/apt/lists/*

# ── Non-root user ─────────────────────────────────────────────────────────────
# claude CLI refuses --dangerously-skip-permissions when running as root.
RUN groupadd --gid 1001 agent \
  && useradd --uid 1001 --gid agent --shell /bin/bash --create-home agent \
  && mkdir -p /tmp/pr-review-cache /home/agent/.claude \
  && chown -R agent:agent /tmp/pr-review-cache /home/agent/.claude

# ── Copy published .NET app ───────────────────────────────────────────────────
COPY --from=build /app/publish /app

# ── Copy scripts (run-pr-review.sh and any helpers) ───────────────────────────
COPY scripts/ /app/scripts/
RUN chmod +x /app/scripts/*.sh \
  && chown -R agent:agent /app

# ── Entrypoint shim ───────────────────────────────────────────────────────────
# Named volumes are mounted as root-owned by default. This shim runs as root,
# fixes ownership on the two volumes, then drops to the 'agent' user.
# Without this, 'agent' cannot write lock files or clone into /tmp/pr-review-cache.
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Tell RunPrReviewScriptActivity where to find scripts/run-pr-review.sh.
# The script itself clones the xianix-team plugin repo at runtime into
# XIANIX_CACHE_DIR (default: /tmp/pr-review-cache/xianix-team).
ENV XIANIX_REPO_ROOT=/app

WORKDIR /app

# All secrets (XIANS_SERVER_URL, XIANS_API_KEY, ANTHROPIC_API_KEY,
# GITHUB_TOKEN, AZURE_TOKEN, GIT_USER_NAME, GIT_USER_EMAIL, …) must be
# supplied at container start via environment variables — never baked in.
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["dotnet", "AgentTeam.Console.dll"]
