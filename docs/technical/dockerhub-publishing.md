# Docker Hub Publishing Guide

The GitHub Actions workflow at `.github/workflows/docker-publish.yml` automatically builds and pushes the `xianix-pr-review-agent` image to Docker Hub whenever a version tag is pushed to the repository.

---

## One-time Setup

### 1. Create a Docker Hub repository

1. Log in to [hub.docker.com](https://hub.docker.com)
2. Click **Create repository**
3. Name it `xianix-pr-review-agent` and set visibility to **Public** or **Private**

### 2. Create a Docker Hub access token

1. Go to **Account Settings → Security → Access Tokens**
2. Click **Generate new token**
3. Give it a descriptive name (e.g. `github-actions-xianix-team`)
4. Set permissions to **Read & Write**
5. Copy the token — it is only shown once

### 3. Add secrets to the GitHub repository

Go to your GitHub repository → **Settings → Secrets and variables → Actions → New repository secret** and add both:

| Secret name | Value |
|---|---|
| `DOCKERHUB_USERNAME` | Your Docker Hub username |
| `DOCKERHUB_TOKEN` | The access token created above |

---

## Publishing a Release

Every push of a `v*.*.*` tag triggers the workflow. No manual steps are needed beyond tagging.

```bash
# Commit and push your changes first
git add .
git commit -m "release: v1.2.0"
git push

# Create and push the version tag
git tag v1.2.0
git push origin v1.2.0
```

The workflow then:

1. Checks out the code at that tag
2. Builds the Docker image using the multi-stage `Dockerfile`
3. Pushes three tags to Docker Hub:

| Published tag | Meaning |
|---|---|
| `you/xianix-pr-review-agent:1.2.0` | Exact, immutable version |
| `you/xianix-pr-review-agent:1` | Floating major-version pointer |
| `you/xianix-pr-review-agent:latest` | Always the newest release |

---

## Monitoring the Build

1. Open the **Actions** tab in your GitHub repository
2. Click the **Publish Docker Image** workflow run
3. Expand the **Build and push** step to watch the layer-by-layer output

> **Build time:** The first build takes ~10–15 minutes (Node.js + Claude CLI + Azure CLI layers). Subsequent tag builds are much faster because Docker layer cache is stored in Docker Hub under the `buildcache` tag and reused automatically.

---

## Pulling the Published Image

Once the workflow completes, anyone with access to the repository can pull the image:

```bash
# Latest release
docker pull you/xianix-pr-review-agent:latest

# Specific version
docker pull you/xianix-pr-review-agent:1.2.0
```

Run it with your environment variables:

```bash
docker run --rm \
  --env-file AgentTeam.Console/.env \
  -v pr-review-cache:/tmp/pr-review-cache \
  -v claude-home:/root/.claude \
  you/xianix-pr-review-agent:latest
```

Or reference the published image in `docker-compose.yml` instead of building locally:

```yaml
services:
  pr-review-agent:
    image: you/xianix-pr-review-agent:latest   # pull from Docker Hub
    # build: .                                  # comment out the local build
```

---

## Tag Versioning Convention

Follow [Semantic Versioning](https://semver.org):

| Tag | When to use |
|---|---|
| `v1.0.0` | First stable release |
| `v1.0.1` | Bug fix, no new features |
| `v1.1.0` | New feature, backward-compatible |
| `v2.0.0` | Breaking change |

Only tags matching `v*.*.*` trigger the publish workflow. Regular branch pushes and PRs do not publish to Docker Hub.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Workflow not triggered | Tag doesn't match `v*.*.*` | Use format `v1.0.0`, not `1.0.0` or `release-1` |
| `unauthorized: incorrect username or password` | Secret misconfigured | Re-enter `DOCKERHUB_TOKEN` — confirm it has Read & Write scope |
| Build fails on `az extension add` | Transient network error during image build | Re-run the failed job from the Actions tab |
| `denied: requested access to the resource is denied` | Docker Hub repo is private and token lacks access | Check the token's repository permissions on Docker Hub |
| `buildcache` tag not found on first run | Expected — cache is empty on the very first build | Ignore; subsequent builds will be faster |
