# Status — auth polish and app-wide consistency pass

## Changed files

- `src/MuscleCuties.UI/Pages/Auth/LoginPage.xaml` — shared `PageHeader` with no back action; form scroll area; account-switch footer anchored to the bottom safe area; standard Apple button; Show/Hide password control.
- `src/MuscleCuties.UI/Pages/Auth/RegisterPage.xaml` — shared `PageHeader` with the visible back arrow bound to `GoBackCommand`; visible password rules; Show/Hide controls; quiet Privacy Policy link.
- `src/MuscleCuties.UI/Pages/Auth/OtpVerificationPage.xaml`, `PasswordRecoveryPage.xaml` — shared `PageHeader` composition.
- `src/MuscleCuties.UI/Pages/Auth/{LoginPage.xaml.cs,RegisterPage.xaml.cs}` — password reveal handlers.
- `src/MuscleCuties.UI/ViewModels/Auth/{LoginViewModel.cs,RegisterViewModel.cs}` — VoiceOver announcement of auth errors.
- `src/MuscleCuties.UI/Services/Localization/{UiTextCatalog.NutritionProfileAuth.cs,RussianUiText.NutritionProfileAuth.cs,UiTextCatalog.RuntimeText.cs}` — EN/RU password, consent and Apple copy; sentence-case labels.
- `src/MuscleCuties.UI/Resources/Styles/{Colors.xaml,Styles.xaml}`, `Resources/Images/icon_apple.svg` — platform-owned `ButtonApple` treatment.
- `src/MuscleCuties.UI/Services/Auth/PlatformAccountSignInAdapter.cs` — actionable Apple failure copy.
- `tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs` — auth layout/header contract and the Apple button exception.
- `ROADMAP.md`, `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` — current status reconciled; historical gate rows marked superseded where V2 replaced the legacy path.
- `docs/design/{AUDIT_2026-11-02.md,SOFT_MINIMAL_PROPOSAL.md,SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md,REFERENCES.md}` and `docs/design/references/*` — restored from Git, marked as historical records; all presentation-spec links resolve.
- `src/MuscleCuties.UI/MuscleCuties.UI.csproj` — TestFlight build 6.
- Commits: `e411958f`, `0204bd2f`, `143bc38c`, `23b73055`, `b5078cdd`, `4f5343cb`.

## Test commands and output

```
dotnet test tests/MuscleCuties.Core.Tests --nologo -v:q
Passed! - Failed: 0, Passed: 1637, Skipped: 0, Total: 1637

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

Presentation-spec design links: `broken_design_links= []`.

Earlier in this branch: Registration.Tests 70/70; Server.Tests 125/125; Apple integration 2/2.

## Not verified

- The full QA journey could not run: registration reached the local API and returned `500` because the local email-challenge HMAC keys are not configured. This is a local environment issue, not a UI regression. The design journey therefore did not reach Today, Cycle, Train, Nutrition or You.
- Login, Register and Password Recovery were visually checked on the simulator. OTP was not visually reached; its header change is covered by the build and source test.
- Dark appearance, VoiceOver announcements, and the largest accessibility text sizes were not exercised on a device.
- App-wide copy-size and button-height proposals below were not applied; they require a separate visual pass across every tab.
- Build 6 was not uploaded: no `MuscleCutiesTestFlight` Keychain item exists on this machine.

## Next step / blocker

1. Store the app-specific password with `xcrun altool --store-password-in-keychain-item MuscleCutiesTestFlight -u deniska.petruk@icloud.com -p '<app-specific-password>'`, then upload `src/MuscleCuties.UI/bin/Release/net10.0-ios/ios-arm64/MuscleCuties.UI.ipa` (1.0 build 6).
2. Configure the local API email-challenge HMAC keys to complete the full design journey and verify every tab.
3. Package L / Gate 95 remains owner-gated; HIIT, fasting UI, exercise media and Android remain deliberately deferred.
