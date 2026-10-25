#!/bin/bash
# Pushes the ci-output folder (logs, test results, screenshots) as a single commit to the given branch,
# replacing what was there, so a run's output can be read with git.
set -u
branch=$1
mkdir -p ci-output
[ -d TestResults ] && cp -r TestResults ci-output/ 2>/dev/null
echo "${GITHUB_SHA:-local} · run ${GITHUB_RUN_ID:-?}" > ci-output/RUN.txt
cd ci-output
git init -q -b "$branch"
git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git add -A
git commit -q -m "CI output for ${GITHUB_SHA:-local}"
git push -q -f "https://x-access-token:${GITHUB_TOKEN}@github.com/${GITHUB_REPOSITORY}.git" "$branch"
