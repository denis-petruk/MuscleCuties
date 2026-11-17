# Status — Apple sign-in fix and TestFlight candidate

## Changed files

- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/AppleIdentityTokenValidator.cs` — isolated Apple identity-token contract with explicit failure outcomes and verified email extraction.
- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/PostgresAccountAuthenticationUseCase.cs` — first Apple sign-in now creates an account; an existing verified-email account is linked instead of duplicated.
- `services/MuscleCuties.Server/ApiApplication.cs` — injects the authentication logger for rejection reasons.
- `src/MuscleCuties.Infrastructure/Account/SignIn/CloudAccountSignInStore.cs` — a successful Apple sign-in routes to the dashboard.
- `src/MuscleCuties.UI/Pages/Auth/LoginPage.xaml`, `LoginPage.xaml.cs`, `ViewModels/Auth/LoginViewModel.cs` — button reads “Log in with Apple” and the Apple sheet starts automatically on launch and after logout.
- `src/MuscleCuties.UI/Services/Localization/{UiTextCatalog.NutritionProfileAuth.cs,RussianUiText.NutritionProfileAuth.cs}` — EN/RU login-with-Apple copy.
- `tests/MuscleCuties.Registration.Tests/AppleIdentityTokenValidatorTests.cs` — seven token-contract tests.
- `tests/MuscleCuties.Registration.Integration.Tests/AppleSignInPostgresTests.cs` — account creation and email-link tests against disposable Postgres.
- `src/MuscleCuties.UI/MuscleCuties.UI.csproj` — TestFlight build 5.
- Commits: `e411958f` (Apple sign-in), `0204bd2f` (workout session fixes and FDC setup doc).

## Test commands and output

```
dotnet test tests/MuscleCuties.Registration.Tests --nologo -v:q
Passed! - Failed: 0, Passed: 70, Skipped: 0, Total: 70

dotnet test tests/MuscleCuties.Server.Tests --nologo -v:q
Passed! - Failed: 0, Passed: 125, Skipped: 0, Total: 125

dotnet test tests/MuscleCuties.Registration.Integration.Tests --filter AppleSignInPostgresTests
Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2

dotnet build src/MuscleCuties.UI -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
Build succeeded. 0 Warning(s) 0 Error(s)

MC_IOS_VERSION=1.0 MC_IOS_BUILD=5 scripts/archive-ios-testflight.sh
bundle_id=com.musclecuties.app version=1.0 build=5
content_audit=PASS
healthkit_profile=True healthkit_app=True signin_profile=True signin_app=True
beta_reports_profile=True get_task_allow_profile=False get_task_allow_app=False
IPA cleanliness: PASS
```

Beta API deployment: server alias `beta` at version 16, worker at version 3, both serving the candidate `CodeSha256`; `/health/live` and `/health/ready` return 200.

## Not verified

- Real Apple authorization on a physical device was not completed. The simulator’s Apple ID could not finish the system sheet (“Could Not Connect”), so the device-side sheet-to-token step remains unverified.
- The new account creation/link path was exercised with synthetic Apple-signed tokens against Postgres, not with a live Apple identity token.
- The IPA was not uploaded: no App Store Connect API key (`.p8` + key id + issuer id) and no app-specific password are present on this machine. The only key found, `HN4A2D448R`, is the Sign in with Apple server key, not an App Store Connect key.

## Next step / blocker

Provide either an App Store Connect API key path with its key id and issuer id, or an app-specific password for the Apple ID, to upload `src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/MuscleCuties.UI.ipa` (1.0 build 5). After upload, install the TestFlight build and complete one Apple sign-in on a device to verify the live token path.
