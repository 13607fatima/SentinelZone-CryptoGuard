# Push the standalone repository to GitHub

The teammate owns a separate repository named **SentinelZone-CryptoGuard**. `USERNAME` below is a placeholder for that owner's account/organization, not an inferred account.

## Create an empty repository

Create an EMPTY repository named `SentinelZone-CryptoGuard`. Do not create a README, .gitignore or LICENSE in GitHub's UI; this handoff already contains its source/documentation and the project license remains undecided. Review publication terms with the owner. Enable private vulnerability reporting if desired; no security mailbox is supplied.

Extract the GitHub-ready ZIP. Enter `SentinelZone-CryptoGuard/` directly: `src/`, `tests/`, `global.json` and README must be immediate children. Do not copy the original bundle's binaries into this directory.

## First push

These Git/Python commands work from the repository root with Python available as `python` (use `python3` if that is your executable):

```bash
git init
git branch -M main
python scripts/check-repository.py
python -m unittest discover -s tests/Repository -v
python scripts/secret-scan.py
```

Run the relevant native .NET suites and schema tests in [docs/TESTING.md](docs/TESTING.md). If a required environment is unavailable, record UNVERIFIED; source-only checks are not a substitute for builds.

Then stage and review the exact bytes to be committed:

```bash
git add .
python scripts/secret-scan.py --staged
git status
git diff --cached --stat
git diff --cached --check -- . ':(exclude)licenses/**'
git diff --cached
git commit -m "Initial SentinelZone CryptoGuard release"
git remote add origin https://github.com/USERNAME/SentinelZone-CryptoGuard.git
git remote -v
git push -u origin main
```

Stop if any validation fails. If you change a file after staging, stage it again and rerun the staged scan. Ignore rules do not remove already tracked files. The cosmetic whitespace check excludes preserved upstream `licenses/` material because its original whitespace must remain intact; secret scanning still includes it. Authenticate using your normal Git credential helper/SSH configuration; never put a token in the remote URL or commit it.

## Verify GitHub Actions

Open Actions for the pushed commit and inspect Linux, Windows and parity job results, logs and artifacts. With an authenticated GitHub CLI, use `gh run list --branch main` and `gh run view RUN_ID --log-failed` (replace RUN_ID). Record the successful run URL and exact commit in a new validation report before changing HOSTED CI from UNVERIFIED. A workflow file or local syntax check is not a hosted run.

No push or Actions run has been performed by this preparation task. The owner URL remains a placeholder; no workflow badge was fabricated.

## Existing-repository update

Do not reinitialize, overwrite history, force-push or repoint an unrelated remote. Work from a clone of the intended component repository, confirm `git remote -v`, and start from a clean tree:

```bash
git switch main
git pull --ff-only
git switch -c prepare-cryptoguard-0.22.2-rc.1
```

Copy the prepared repository contents into that checkout after reviewing differences. Preserve its `.git` directory and unrelated owner changes. Run the validation/staging/review commands above, then:

```bash
git commit -m "Prepare standalone CryptoGuard evaluation handoff"
git push -u origin prepare-cryptoguard-0.22.2-rc.1
```

Open a pull request, review real CI results, and merge through the owner's normal workflow. The bundled staged scan does not audit old history: review existing history for secrets before changing visibility. If the target tag already exists, inspect it; do not delete/recreate it to fit this package.

## Create the v0.22.2-rc.1 prerelease

After reviewing main and the actual CI evidence:

```bash
git switch main
git pull --ff-only
git tag -a v0.22.2-rc.1 -m "CryptoGuard 0.22.2-rc.1 unsigned evaluation"
git push origin v0.22.2-rc.1
```

Inspect the tag-triggered workflow, including its `release` environment/job and artifact validation. An annotated tag is not a code signature. With an authenticated GitHub CLI:

```bash
gh release create v0.22.2-rc.1 --verify-tag --prerelease --title "CryptoGuard v0.22.2-rc.1 — UNSIGNED EVALUATION" --notes-file docs/GITHUB-RELEASE-NOTES.md
```

Alternatively, use Releases → Draft a new release, choose `v0.22.2-rc.1`, paste [the release notes](docs/GITHUB-RELEASE-NOTES.md), select **Set as a pre-release**, and leave **Set as latest release** unchecked. Upload the files listed in [GITHUB_RELEASE_ASSETS.md](GITHUB_RELEASE_ASSETS.md); review the final file names and checksums before publishing.

The notes must visibly retain **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**, all unverified gates and the distinction between original prebuilt assets and the prepared source tag. The CLI upload command and exact original-bundle asset map are in [GITHUB_RELEASE_ASSETS.md](GITHUB_RELEASE_ASSETS.md).
