---
name: post-review
description: Post the current PR review as GitHub comments. Requires a PR number. Usage: /post-review [pr-number]
argument-hint: [pr-number]
---

Post the PR review findings as GitHub review comments on PR #$ARGUMENTS.

## Steps

1. **Verify PR exists**
   ```bash
   gh pr view $ARGUMENTS --json number,title,state,headRefName
   ```

2. **Format the review for GitHub**
   - Convert the review report into GitHub's review comment format
   - Map file-level findings to inline comments with file path and line number
   - Prepare a summary comment with the overall verdict and report

3. **Confirm before posting**
   Show the user what will be posted and ask for confirmation:
   - Overall verdict (APPROVE / REQUEST_CHANGES / COMMENT)
   - Number of inline comments
   - Summary preview

4. **Post the review**
   ```bash
   # Post inline comments and overall review
   gh pr review $ARGUMENTS \
     --body "[summary]" \
     --[approve|request-changes|comment]
   ```

   For inline comments on specific lines:
   ```bash
   gh api repos/{owner}/{repo}/pulls/$ARGUMENTS/reviews \
     --method POST \
     --field body="[summary]" \
     --field event="[APPROVE|REQUEST_CHANGES|COMMENT]" \
     --field comments="[inline comments JSON]"
   ```

5. **Confirm success**
   Output the link to the posted review on GitHub.

> **Note:** Requires `gh` CLI authenticated with repo write access. Run `gh auth status` to check.
