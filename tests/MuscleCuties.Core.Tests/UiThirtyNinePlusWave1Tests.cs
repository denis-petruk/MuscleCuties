using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class UiThirtyNinePlusWave1Tests
{
    [Fact]
    public void AppStartupPage_allows_reset_on_any_startup_error_and_announces_status()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Startup", "AppStartupPage.xaml");
        var cs = UiSourceFixture.ReadUi("Pages", "Startup", "AppStartupPage.xaml.cs");

        Assert.Contains("Style=\"{StaticResource StateBusyPage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.HeadingLevel=\"Level1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"StartupErrorFooter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Dashboard_Startup_LoadingStatusA11y", xaml, StringComparison.Ordinal);
        Assert.Contains("ResetButton.IsVisible = true;", cs, StringComparison.Ordinal);
        Assert.Contains("_isActionInProgress", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginPage_validates_blank_fields_announces_status_and_locks_inputs_when_busy()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Auth", "LoginPage.xaml");
        var cs = UiSourceFixture.ReadUi("Pages", "Auth", "LoginPage.xaml.cs");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Auth", "LoginViewModel.cs");

        Assert.Contains("Placeholder=\"{loc:LocalizedText Auth_EmailOrUsernamePlaceholder}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticProperties.HeadingLevel=\"Level3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CappedTextSize.IsCompactControl=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.IsInAccessibleTree=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ClearButtonVisibility=\"WhileEditing\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OnPasswordChanged", vm, StringComparison.Ordinal);
        Assert.Contains("PrepareForPasswordUpdated", vm, StringComparison.Ordinal);
        Assert.Contains("OnDisappearing", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterPage_shows_username_rules_reports_field_errors_and_clears_on_edit()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Auth", "RegisterPage.xaml");
        var cs = UiSourceFixture.ReadUi("Pages", "Auth", "RegisterPage.xaml.cs");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Auth", "RegisterViewModel.cs");

        Assert.Contains("Auth_Register_UsernameRequirements", xaml, StringComparison.Ordinal);
        Assert.Contains("IsSpellCheckEnabled=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticProperties.HeadingLevel=\"Level3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CappedTextSize.IsCompactControl=\"True\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Email verification is unavailable. Please try again later.", vm, StringComparison.Ordinal);
        Assert.Contains("OnEmailChanged", vm, StringComparison.Ordinal);
        Assert.Contains("OnUsernameChanged", vm, StringComparison.Ordinal);
        Assert.Contains("OnPasswordChanged", vm, StringComparison.Ordinal);
        Assert.Contains("OnDisappearing", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void OtpVerificationPage_keeps_context_visible_at_accessibility_sizes_and_stacks_actions()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Auth", "OtpVerificationPage.xaml");
        var cs = UiSourceFixture.ReadUi("Pages", "Auth", "OtpVerificationPage.xaml.cs");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Auth", "OtpVerificationViewModel.cs");

        Assert.Contains("ShowBack=\"True\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Subtitle=\"{loc:LocalizedText Auth_Otp_WeSentA6DigitConfirmation}", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticProperties.HeadingLevel=\"Level3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Auth_Otp_CheckSpamHint", xaml, StringComparison.Ordinal);
        Assert.Contains("IsRunning=\"{Binding IsBusy}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Ui_Auth_Error_IncorrectVerificationCode", vm, StringComparison.Ordinal);
        Assert.Contains("OtpEntry.Focus()", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordRecoveryPage_shows_step_progress_password_toggles_and_clean_reset_handoff()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Auth", "PasswordRecoveryPage.xaml");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Auth", "PasswordRecoveryViewModel.cs");
        var mauiProgram = UiSourceFixture.ReadUi("MauiProgram.cs");

        Assert.Contains("PageHeader.EyebrowContent", xaml, StringComparison.Ordinal);
        Assert.Contains("StepProgressText", xaml, StringComparison.Ordinal);
        Assert.Contains("Auth_ShowPassword", xaml, StringComparison.Ordinal);
        Assert.Contains("Auth_Otp_CheckSpamHint", xaml, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticProperties.HeadingLevel=\"Level3\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OnEmailOrUsernameChanged", vm, StringComparison.Ordinal);
        Assert.Contains("OnNewPasswordChanged", vm, StringComparison.Ordinal);
        Assert.Contains("PrepareForPasswordUpdated", mauiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileSetupPage_shows_height_error_in_imperial_and_uses_shared_choice_cells()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Onboarding", "ProfileSetupPage.xaml");
        var cs = UiSourceFixture.ReadUi("Pages", "Onboarding", "ProfileSetupPage.xaml.cs");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Profile", "ProfileSetupViewModel.cs");

        Assert.DoesNotContain("ChoiceSelectedSurfaceLight", xaml, StringComparison.Ordinal);
        Assert.Contains("ChoiceCellCompact", xaml, StringComparison.Ordinal);
        Assert.Contains("ScreenReaderStatusAnnouncementBehavior", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,Auto,*,Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ProfileSetupScroll\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OnSelectedHeightCmChanged", vm, StringComparison.Ordinal);
        Assert.Contains("OnWellnessAcknowledgedChanged", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("_selectedInches = 6;", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OK\"", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void QuizPage_preserves_voiceover_selected_state_distinguishes_multiselect_and_adds_phase_help()
    {
        var xaml = UiSourceFixture.ReadUi("Pages", "Onboarding", "QuizPage.xaml");
        var vm = UiSourceFixture.ReadUi("ViewModels", "Quiz", "QuizViewModel.cs");

        Assert.DoesNotContain("SemanticProperties.Hint=\"{loc:LocalizedText Common_NotSelected}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionCheckbox", xaml, StringComparison.Ordinal);
        Assert.Contains("CurrentPhasePairHelperText", xaml, StringComparison.Ordinal);
        Assert.Contains("IsFirstQuestion && !IsBusy", vm, StringComparison.Ordinal);
        Assert.Contains("AccessibilityDecoration.HideAtAccessibilitySize=\"True\"", xaml, StringComparison.Ordinal);
    }
}
