# Release: signed APK
Cutting a release, installing it, and the signing keystore. CI is in [Dev setup & build](dev-setup-build.md#ci), branch rules in [Source control](dev-setup-build.md#source-control).

Run Actions → Release → **Run workflow** on `main`, then approve the `release` environment. `release.yml`:
1. Works out the version from the highest `semver:*` label among the PRs merged since the last `vX.Y.Z` tag (`1.0.0` for the first). No PRs ends it green with nothing released. The last tag counts even when a force-push left its commit off main: PRs merged by that commit are already released and are skipped, and a PR whose merge commit GitHub no longer links is looked up in `.github/scripts/rewritten-commits.txt`.
2. Runs the dev pipeline, the QA pipeline, the Release E2E (an x64 Release APK starts cleanly on an emulator and the `@release` and `@backup` scenarios run on it, and, once there is a last release, it installs over that release's Release APK and keeps its settings) and the Upgrade E2E (the same upgrade check for the Debug build; both skip until the first release exists). See [E2E tests](e2e-tests.md#other-builds).
3. After approval, builds the arm64 Release APK signed with the release keystore, checks it (`check-apk.sh`, `check-backup.sh`) and creates the release `vX.Y.Z`: the APK, its `.sha256`, and notes listing the merged PRs.

A failed run creates no tag or release: fix the cause and run it again.

Settings outside the repo: the `semver:*` labels; the `release` environment (owner is the required reviewer, admins can't bypass, `main` only) with secrets `RELEASE_KEYSTORE_BASE64`, `RELEASE_KEYSTORE_PASSWORD` and `RELEASE_KEY_ALIAS`; the ruleset `Protect release tags`, so `v*` tags can't be deleted or moved.

## Install or update on the phone
See the README's [Install](../README.md#install). An update installs over the app and keeps its data.

**The same keystore must sign every future APK.** If it's lost, updates can't be installed over the existing app; the only way back is to uninstall, which wipes all of the app's data.

## Keystore handling
- PKCS12, RSA 4096, valid 10000 days, one password for the store and the key.
- Release cert SHA-256: `07:02:25:A2:56:98:2B:94:CE:AC:8A:2C:C5:5F:A0:5F:BB:80:36:0B:A2:92:58:AC:5D:CF:8A:92:33:3E:A6:46` (`keytool -list -v -keystore <file>`, uppercase and colon-separated). The same value goes in `CERT_SHA256` in `release.yml` and nowhere else; the first job of the release workflow (`Release cert pinned`) fails the release if it is not a real 32-byte digest, and `check-apk.sh` fails it if the APK is signed by any other cert.
- The keystore file lives outside the repo and is gitignored. CI gets it only through the `release` environment's secrets (base64 for the file) and deletes it after the build.
- The owner keeps one backup of the keystore file with its passwords and alias, in a private location that is not recorded in the repo.
- Local Release builds are signed with the debug key.
