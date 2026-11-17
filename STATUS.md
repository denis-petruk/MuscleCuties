# Status — Apple sign-in and auth design fixes

## Changed files

- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/AppleIdentityTokenValidator.cs` — isolated Apple identity-token contract with explicit failure outcomes and verified email extraction.
- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/PostgresAccountAuthenticationUseCase.cs` — first Apple sign-in creates an account; an existing verified-email account is linked instead of duplicated.
- `services/MuscleCuties.Server/ApiApplication.cs` — injects the authentication logger for rejection reasons.
- `src/MuscleCuties.Infrastructure/Account/SignIn/CloudAccountSignInStore.cs` — a successful Apple sign-in routes to the dashboard.
- `src/MuscleCuties.UI/Pages/Auth/LoginPage.xaml`, `LoginPage.xaml.cs`, `ViewModels/Auth/LoginViewModel.cs` — standard Sign in with Apple control plus automatic Apple sheet on launch and after logout.
- `src/MuscleCuties.UI/Pages/Auth/RegisterPage.xaml`, `RegisterPage.xaml.cs` — visible password requirements, Show/Hide controls, and a quiet Privacy Policy link inside the consent block.
- `src/MuscleCuties.UI/Resources/Styles/{Colors.xaml,Styles.xaml}`, `Resources/Images/icon_apple.svg` — platform-owned `ButtonApple` treatment with the Apple mark.
- `src/MuscleCuties.UI/Services/Auth/PlatformAccountSignInAdapter.cs` — actionable Apple failure copy.
- `src/MuscleCuties.UI/Services/Localization/{UiTextCatalog.NutritionProfileAuth.cs,RussianUiText.NutritionProfileAuth.cs,UiTextCatalog.RuntimeText.cs}` — EN/RU password, consent and Apple copy; errors announced to VoiceOver.
- `tests/MuscleCuties.Registration.Tests/AppleIdentityTokenValidatorTests.cs` — seven token-contract tests.
- `tests/MuscleCuties.Registration.Integration.Tests/AppleSignInPostgresTests.cs` — account creation and email-link tests against disposable Postgres.
- `tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs` — permits the platform Apple button alongside the two brand roles.
- `src/MuscleCuties.UI/MuscleCuties.UI.csproj` — TestFlight build 6.
- Commits: `e411958f` (Apple sign-in), `0204bd2f` (workout fixes and FDC setup), `143bc38c` (auth design fixes).

## Test commands and output

```
dotnet test tests/MuscleCuties.Core.Tests --nologo -v:q
Passed! - Failed: 0, Passed: 1636, Skipped: 0, Total: 1636

dotnet build src/MuscleCuties.UI -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
Build succeeded. 0 Warning(s) 0 Error(s)

MC_IOS_VERSION=1.0 MC_IOS_BUILD=6 scripts/archive-ios-testflight.sh
Build succeeded.
bundle_id=com.musclecuties.app version=1.0 build=6
content_audit=PASS
healthkit_profile=True healthkit_app=True signin_profile=True signin_app=True
beta_reports_profile=True get_task_allow_profile=False get_task_allow_app=False
IPA cleanliness: PASS
```

Earlier in this branch: Registration.Tests 70/70; Server.Tests 125/125; Apple integration 2/2.

Beta API deployment: server alias `beta` at version 16, worker at version 3, both serving the candidate `CodeSha256`; `/health/live` and `/health/ready` return 200.

Visual check: Login and Register were rendered on the iPhone 16 Pro simulator. The Apple row now shows the Apple mark and “Sign in with Apple”; the registration rules are visible; Show switches to Hide. Light appearance only.

## Not verified

- Real Apple authorization on a physical device was not completed. The simulator’s Apple ID cannot finish the system sheet (“Could Not Connect”), so the sheet-to-live-token step remains unverified.
- The new account creation/link path was exercised with synthetic Apple-signed tokens against Postgres, not with a live Apple identity token.
- VoiceOver announcements, dark appearance, and the largest accessibility text sizes were not exercised on a device.
- Build 6 was not uploaded: no App Store Connect API key and no `MuscleCutiesTestFlight` Keychain item are present on this machine.

## Next step / blocker

Store an app-specific password in Keychain with
`xcrun altool --store-password-in-keychain-item MuscleCutiesTestFlight -u deniska.petruk@icloud.com -p '<app-specific-password>'`,
then upload `src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/MuscleCuties.UI.ipa` (1.0 build 6). After upload, install the TestFlight build and complete one Apple sign-in on a device.
