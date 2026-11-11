using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.UI.Infrastructure.Network;
using MuscleCuties.UI.Services;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.Services.Notifications;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Security;
using MuscleCuties.UseCases.Features.Cycle.Overview;
#if IOS || MACCATALYST
using UIKit;
#endif

namespace MuscleCuties.UI.Composition;

public static class PlatformCompositionExtensions
{
    // Matches AuthInputValidator's minimum length and character-class requirements.
    private const string NewPasswordRules =
        "required: upper; required: lower; required: digit; required: [-().&@?'#,/+_*]; minlength: 8;";

    public static MauiAppBuilder ConfigurePlatformInputHandlers(this MauiAppBuilder builder)
    {
        return builder.ConfigureMauiHandlers(handlers =>
        {
#if ANDROID
            EntryHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            {
                ClearAndroidInputChrome(handler.PlatformView);
                ConfigureAndroidAutofill(handler.PlatformView, handler.VirtualView.AutomationId);
            });
            PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                ClearAndroidInputChrome(handler.PlatformView));
            DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                ClearAndroidInputChrome(handler.PlatformView));
            TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                ClearAndroidInputChrome(handler.PlatformView));
#elif IOS || MACCATALYST
            // RM00/CR01: Layout crash protection for native Button.LayoutButton
            // Safeguards against NullReferenceException in Button.LayoutButton -> CrossPlatformArrange
            // when buttons are unmapped or pages are popped while layout arrange is running.
            ButtonHandler.Mapper.AppendToMapping("MuscleCutiesButtonCornerRadiusAndLayoutProtection", (handler, _) =>
            {
                if (handler.PlatformView?.Layer is null || handler.VirtualView is null)
                    return;

                handler.PlatformView.Layer.CornerRadius = (nfloat)handler.VirtualView.CornerRadius;
                handler.PlatformView.Layer.MasksToBounds = handler.VirtualView.CornerRadius > 0;
            });
            EntryHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            {
                handler.PlatformView.BorderStyle = UITextBorderStyle.None;
                handler.PlatformView.BackgroundColor = UIColor.Clear;

                var textContentType = GetAuthFieldKind(handler.VirtualView.AutomationId) switch
                {
                    AuthFieldKind.LoginIdentifier or AuthFieldKind.AccountUsername => UITextContentType.Username,
                    AuthFieldKind.EmailAddress => UITextContentType.EmailAddress,
                    AuthFieldKind.CurrentPassword => UITextContentType.Password,
                    AuthFieldKind.NewPassword => UITextContentType.NewPassword,
                    AuthFieldKind.OneTimeCode => UITextContentType.OneTimeCode,
                    _ => null
                };

                if (textContentType is not null)
                    handler.PlatformView.TextContentType = textContentType;

                if (handler.VirtualView.AutomationId is "RegisterPassword")
                    handler.PlatformView.PasswordRules = UITextInputPasswordRules.Create(NewPasswordRules);
            });
            PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            {
                handler.PlatformView.BorderStyle = UITextBorderStyle.None;
                handler.PlatformView.BackgroundColor = UIColor.Clear;
            });
            DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                handler.PlatformView.BackgroundColor = UIColor.Clear);
            TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                handler.PlatformView.BackgroundColor = UIColor.Clear);
#endif
        });
    }

    public static IServiceCollection RegisterPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationContextService, NavigationContextService>();
        services.AddSingleton<ITokenStorage, SecureStorageService>();
        services.AddSingleton(AccountSessionGate.Shared);
        services.AddSingleton<ILocalNotificationService, LocalNotificationService>();

#if IOS || MACCATALYST
        services.AddSingleton<AppleSignInService>();
        services.AddSingleton<IAppleSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
        services.AddSingleton<IPlatformSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
#endif

#if IOS
        services.AddSingleton<Platforms.iOS.AppleHealthDataProvider>();
#if DEBUG
        if (MuscleCuties.UI.Services.Health.SyntheticHealthDataProvider.IsRequested)
        {
            // Simulator QA only: synthetic fixture instead of HealthKit (see SyntheticHealthDataProvider).
            services.AddSingleton<MuscleCuties.UI.Services.Health.SyntheticHealthDataProvider>();
            services.AddSingleton<IHealthDataProvider>(sp => sp.GetRequiredService<MuscleCuties.UI.Services.Health.SyntheticHealthDataProvider>());
            services.AddSingleton<IHealthAuthorizationProvider>(sp => sp.GetRequiredService<MuscleCuties.UI.Services.Health.SyntheticHealthDataProvider>());
        }
        else
#endif
        {
            services.AddSingleton<IHealthDataProvider>(sp => sp.GetRequiredService<Platforms.iOS.AppleHealthDataProvider>());
            services.AddSingleton<IHealthAuthorizationProvider>(sp => sp.GetRequiredService<Platforms.iOS.AppleHealthDataProvider>());
        }
        services.AddSingleton<IHealthSyncService, HealthSyncService>();
#else
        services.AddSingleton<IHealthSyncService, DisabledHealthSyncService>();
#endif
        services.AddScoped<ICyclePhaseNotificationService>(sp =>
            new CyclePhaseNotificationService(
                sp.GetRequiredService<ICycleOverviewUseCase>(),
                sp.GetRequiredService<ICyclePhaseProjectionUseCase>(),
                sp.GetRequiredService<ILocalNotificationService>()));
        services.AddScoped<IFeedbackEmailService, FeedbackEmailService>();

        return services;
    }

    public static MauiAppBuilder ConfigureAppLogging(this MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder;
    }

    // Enforces TLS 1.3 and SPKI certificate pinning for USDA FoodData Central requests with cookies disabled.
    internal static HttpMessageHandler CreateFdcHttpHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions =
            {
                EnabledSslProtocols = SslProtocols.Tls13,
                RemoteCertificateValidationCallback = FdcCertificatePinningPolicy.Validate
            },
            UseCookies = false
        };
    }

    private enum AuthFieldKind
    {
        None,
        LoginIdentifier,
        EmailAddress,
        AccountUsername,
        CurrentPassword,
        NewPassword,
        OneTimeCode
    }

    private static AuthFieldKind GetAuthFieldKind(string? automationId)
    {
        return automationId switch
        {
            "LoginEmail" => AuthFieldKind.LoginIdentifier,
            "RegisterEmail" => AuthFieldKind.EmailAddress,
            "RegisterUsername" => AuthFieldKind.AccountUsername,
            "OtpEntry" => AuthFieldKind.OneTimeCode,
            "LoginPassword" or "ProfileCurrentPassword" => AuthFieldKind.CurrentPassword,
            "RegisterPassword" or "RegisterConfirmPassword" or "ProfileNewPassword" or "ProfileConfirmPassword" =>
                AuthFieldKind.NewPassword,
            _ => AuthFieldKind.None
        };
    }

#if ANDROID
    private static void ClearAndroidInputChrome(Android.Views.View view)
    {
        view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        view.SetBackgroundColor(Android.Graphics.Color.Transparent);
        view.SetPadding(0, 0, 0, 0);
    }

    private static void ConfigureAndroidAutofill(Android.Widget.EditText editText, string? automationId)
    {
        var hint = GetAuthFieldKind(automationId) switch
        {
            AuthFieldKind.LoginIdentifier or AuthFieldKind.EmailAddress => Android.Views.View.AutofillHintEmailAddress,
            AuthFieldKind.CurrentPassword => Android.Views.View.AutofillHintPassword,
            AuthFieldKind.NewPassword => "newPassword",
            _ => null
        };

        if (hint is null) return;

        editText.ImportantForAutofill = Android.Views.ImportantForAutofill.Yes;
        editText.SetAutofillHints(hint);
    }
#endif
}
