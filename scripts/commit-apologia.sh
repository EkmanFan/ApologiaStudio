#!/usr/bin/env bash

# ApologiaStudio safe commit + push helper.
#
# Usage:
#   bash commit-apologia.sh "add user-created agents"
#
# Optional commit type:
#   bash commit-apologia.sh "fix agent bubble display name" fix
#
# Result:
#   feat: add user-created agents
#   fix: fix agent bubble display name

PROJECT_DIR="${APOLOGIA_PROJECT_DIR:-$HOME/RiderProjects/ApologiaStudio}"
REASON="${1:-}"
COMMIT_TYPE="${2:-feat}"

print_error() {
    echo
    echo "ERROR: $1"
}

if [[ -z "$REASON" ]]; then
    echo "Usage:"
    echo "  bash $0 \"commit reason\" [type]"
    echo
    echo "Examples:"
    echo "  bash $0 \"add user-created agents\""
    echo "  bash $0 \"fix agent bubble display name\" fix"
    echo
    echo "Supported types: feat, fix, refactor, chore, test, docs, perf"
    exit 2
fi

case "$COMMIT_TYPE" in
    feat|fix|refactor|chore|test|docs|perf)
        ;;
    *)
        print_error "Unsupported commit type '$COMMIT_TYPE'."
        echo "Supported types: feat, fix, refactor, chore, test, docs, perf"
        exit 2
        ;;
esac

COMMIT_MESSAGE="${COMMIT_TYPE}: ${REASON}"

echo "ApologiaStudio safe commit helper"
echo "Project: $PROJECT_DIR"
echo "Commit:  $COMMIT_MESSAGE"
echo

# ---------------------------------------------------------------------------
# 1. Repository checks
# ---------------------------------------------------------------------------
echo "1/9 Checking repository..."

if ! cd "$PROJECT_DIR"; then
    print_error "Cannot enter project directory: $PROJECT_DIR"
    exit 1
fi

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    print_error "$PROJECT_DIR is not a Git repository."
    exit 1
fi

CURRENT_BRANCH="$(git branch --show-current 2>/dev/null)"

if [[ -z "$CURRENT_BRANCH" ]]; then
    print_error "Detached HEAD. Commit aborted."
    exit 1
fi

echo "Branch: $CURRENT_BRANCH"

if [[ -n "$(git diff --name-only --diff-filter=U)" ]]; then
    print_error "Unresolved Git conflicts exist."
    git diff --name-only --diff-filter=U
    exit 1
fi

if [[ -z "$(git status --porcelain)" ]]; then
    echo
    echo "Nothing to commit. Working tree is already clean."
    exit 0
fi

echo
git status --short
echo

# ---------------------------------------------------------------------------
# 2. Remote synchronization checks
# ---------------------------------------------------------------------------
echo "2/9 Checking remote state..."

if ! git remote get-url origin >/dev/null 2>&1; then
    print_error "Remote 'origin' is not configured."
    exit 1
fi

if ! git fetch --prune origin; then
    print_error "git fetch failed. Nothing was committed."
    exit 1
fi

UPSTREAM="$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null)"

if [[ -z "$UPSTREAM" ]]; then
    print_error "Current branch '$CURRENT_BRANCH' has no upstream branch."
    exit 1
fi

LOCAL_HEAD="$(git rev-parse HEAD)"
REMOTE_HEAD="$(git rev-parse "$UPSTREAM")"
MERGE_BASE="$(git merge-base HEAD "$UPSTREAM")"

if [[ "$LOCAL_HEAD" == "$REMOTE_HEAD" ]]; then
    echo "Local branch is up to date with $UPSTREAM."
elif [[ "$LOCAL_HEAD" == "$MERGE_BASE" ]]; then
    print_error "Local branch is behind $UPSTREAM. Pull/rebase before committing."
    exit 1
elif [[ "$REMOTE_HEAD" == "$MERGE_BASE" ]]; then
    echo "Local branch is already ahead of $UPSTREAM."
else
    print_error "Local branch and $UPSTREAM have diverged."
    exit 1
fi

echo

# ---------------------------------------------------------------------------
# 3. Stage and inspect
# ---------------------------------------------------------------------------
echo "3/9 Staging changes..."

if ! git add -A; then
    print_error "git add failed."
    exit 1
fi

if git diff --cached --quiet; then
    echo "Nothing remains staged after git add."
    exit 0
fi

echo
git diff --cached --stat
echo

# Refuse common local secret/config files if they somehow became staged.
STAGED_PATHS="$(git diff --cached --name-only)"

if printf '%s\n' "$STAGED_PATHS" | grep -Eq '(^|/)(\.env($|\.)|\.envrc$|appsettings\.(Local|Development\.local)\.json$|secrets?\.json$)'; then
    print_error "A potentially local/secret configuration file is staged:"
    printf '%s\n' "$STAGED_PATHS" | grep -E '(^|/)(\.env($|\.)|\.envrc$|appsettings\.(Local|Development\.local)\.json$|secrets?\.json$)'
    echo
    echo "Unstage/review it before committing."
    exit 1
fi

# ---------------------------------------------------------------------------
# 4. Whitespace / patch integrity
# ---------------------------------------------------------------------------
echo "4/9 Checking staged diff..."

if ! git diff --cached --check; then
    print_error "Whitespace errors were found in staged changes."
    echo "Nothing was committed or pushed."
    exit 1
fi

echo "Staged diff check passed."
echo

# ---------------------------------------------------------------------------
# 5. Build
# ---------------------------------------------------------------------------
echo "5/9 Building solution..."

if ! dotnet build; then
    print_error "dotnet build failed."
    echo "Nothing was committed or pushed."
    exit 1
fi

echo

# ---------------------------------------------------------------------------
# 6. PostgreSQL
# ---------------------------------------------------------------------------
echo "6/9 Ensuring PostgreSQL is available..."

if [[ ! -x "./scripts/db-up.sh" ]]; then
    print_error "./scripts/db-up.sh does not exist or is not executable."
    exit 1
fi

if ! ./scripts/db-up.sh; then
    print_error "PostgreSQL startup/readiness check failed."
    exit 1
fi

# direnv normally provides the test connection string. If this shell does not
# have it but .envrc exists and direnv is installed, load it only for this
# script process.
if [[ -z "${APOLOGIASTUDIO_TEST_DB_CONNECTION:-}" ]] && command -v direnv >/dev/null 2>&1 && [[ -f ".envrc" ]]; then
    eval "$(direnv export bash 2>/dev/null)"
fi

if [[ -z "${APOLOGIASTUDIO_TEST_DB_CONNECTION:-}" ]]; then
    print_error "APOLOGIASTUDIO_TEST_DB_CONNECTION is not configured."
    echo "Open a fresh Rider terminal in the project directory so direnv can load .envrc."
    exit 1
fi

echo

# ---------------------------------------------------------------------------
# 7. Full test suite
# ---------------------------------------------------------------------------
echo "7/9 Running full test suite..."

if ! dotnet test --no-restore; then
    print_error "dotnet test failed."
    echo "Nothing was committed or pushed."
    exit 1
fi

echo

# Re-check after build/tests in case tooling generated tracked changes.
if ! git diff --cached --check; then
    print_error "Staged diff became invalid after validation."
    exit 1
fi

UNSTAGED_TRACKED="$(git diff --name-only)"
UNTRACKED="$(git ls-files --others --exclude-standard)"

if [[ -n "$UNSTAGED_TRACKED" || -n "$UNTRACKED" ]]; then
    echo "Build/tests left additional working-tree changes."
    echo
    if [[ -n "$UNSTAGED_TRACKED" ]]; then
        echo "Unstaged tracked files:"
        printf '%s\n' "$UNSTAGED_TRACKED"
    fi
    if [[ -n "$UNTRACKED" ]]; then
        echo "Untracked files:"
        printf '%s\n' "$UNTRACKED"
    fi
    echo
    print_error "Commit aborted because the validated staged snapshot no longer represents the entire working tree."
    echo "Review the files, then run the commit helper again."
    exit 1
fi

# ---------------------------------------------------------------------------
# 8. Commit
# ---------------------------------------------------------------------------
echo "8/9 Creating commit..."

if ! git commit -m "$COMMIT_MESSAGE"; then
    print_error "git commit failed."
    exit 1
fi

echo

# ---------------------------------------------------------------------------
# 9. Push and final verification
# ---------------------------------------------------------------------------
echo "9/9 Pushing commit..."

if ! git push; then
    print_error "git push failed."
    echo "The commit exists locally but was NOT pushed."
    echo "After fixing the remote issue, run: git push"
    exit 1
fi

echo
echo "Final Git state:"
git status
echo
git log -2 --oneline

echo
echo "SUCCESS: build and tests passed; changes were committed and pushed."
echo "Commit: $COMMIT_MESSAGE"
