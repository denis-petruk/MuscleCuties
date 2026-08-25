#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using AndroidApp = Android.App.Application;
using NotificationReceiver = MuscleCuties.App.Services.Notifications.NotificationBroadcastReceiver;
#elif IOS || MACCATALYST
using Foundation;
using UserNotifications;
#endif

namespace MuscleCuties.App.Services.Notifications;

public sealed class LocalNotificationService : ILocalNotificationService
{
    public Task<bool> ShowAsync(int notificationId, string title, string body)
    {
#if ANDROID
        return ShowAndroidAsync(notificationId, title, body);
#elif IOS || MACCATALYST
        return ScheduleAppleAsync(notificationId, title, body, DateTime.Now.AddSeconds(1));
#else
        return Task.FromResult(false);
#endif
    }

    public Task<bool> ScheduleAsync(int notificationId, string title, string body, DateTime scheduledAt)
    {
#if ANDROID
        return ScheduleAndroidAsync(notificationId, title, body, scheduledAt);
#elif IOS || MACCATALYST
        return ScheduleAppleAsync(notificationId, title, body, scheduledAt);
#else
        return Task.FromResult(false);
#endif
    }

#if ANDROID
#pragma warning disable CA1416
    private const int NotificationPermissionRequestCode = 4601;

    private static Task<bool> ShowAndroidAsync(int notificationId, string title, string body)
    {
        if (!EnsureAndroidNotificationPermission())
            return Task.FromResult(false);

        NotificationReceiver.ShowNotification(AndroidApp.Context, title, body, notificationId);
        return Task.FromResult(true);
    }

    private static Task<bool> ScheduleAndroidAsync(int notificationId, string title, string body, DateTime scheduledAt)
    {
        if (scheduledAt <= DateTime.Now.AddSeconds(1))
            return ShowAndroidAsync(notificationId, title, body);

        if (!EnsureAndroidNotificationPermission())
            return Task.FromResult(false);

        var context = AndroidApp.Context;
        var intent = new Intent(context, typeof(NotificationReceiver));
        intent.PutExtra(NotificationReceiver.IdExtra, notificationId);
        intent.PutExtra(NotificationReceiver.TitleExtra, title);
        intent.PutExtra(NotificationReceiver.BodyExtra, body);

        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            notificationId,
            intent,
            BuildPendingIntentFlags());
        if (pendingIntent is null)
            return Task.FromResult(false);

        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        var triggerMillis = new DateTimeOffset(scheduledAt).ToUnixTimeMilliseconds();

        alarmManager?.Set(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
        return Task.FromResult(alarmManager is not null);
    }

    private static bool EnsureAndroidNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return true;

        var activity = Platform.CurrentActivity;
        if (activity is null)
            return false;

        if (activity.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted)
            return true;

        activity.RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationPermissionRequestCode);
        return false;
    }

    private static PendingIntentFlags BuildPendingIntentFlags()
    {
        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;

        return flags;
    }
#pragma warning restore CA1416
#elif IOS || MACCATALYST
    private static async Task<bool> ScheduleAppleAsync(int notificationId, string title, string body, DateTime scheduledAt)
    {
        var center = UNUserNotificationCenter.Current;
        var isAuthorized = await RequestAppleAuthorizationAsync(center);
        if (!isAuthorized)
            return false;

        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = body,
            Sound = UNNotificationSound.Default
        };
        var trigger = CreateAppleTrigger(scheduledAt);
        var request = UNNotificationRequest.FromIdentifier($"cycle-phase-{notificationId}", content, trigger);

        return await AddAppleNotificationRequestAsync(center, request);
    }

    private static Task<bool> RequestAppleAuthorizationAsync(UNUserNotificationCenter center)
    {
        var completion = new TaskCompletionSource<bool>();
        center.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound,
            (approved, error) => completion.TrySetResult(approved && error is null));
        return completion.Task;
    }

    private static UNNotificationTrigger CreateAppleTrigger(DateTime scheduledAt)
    {
        if (scheduledAt <= DateTime.Now.AddSeconds(1))
            return UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);

        var components = new NSDateComponents
        {
            Calendar = NSCalendar.CurrentCalendar,
            Year = scheduledAt.Year,
            Month = scheduledAt.Month,
            Day = scheduledAt.Day,
            Hour = scheduledAt.Hour,
            Minute = scheduledAt.Minute,
            Second = scheduledAt.Second
        };

        return UNCalendarNotificationTrigger.CreateTrigger(components, false);
    }

    private static Task<bool> AddAppleNotificationRequestAsync(
        UNUserNotificationCenter center,
        UNNotificationRequest request)
    {
        var completion = new TaskCompletionSource<bool>();
        center.AddNotificationRequest(request, error =>
        {
            if (error is null)
                completion.TrySetResult(true);
            else
                completion.TrySetException(new NSErrorException(error));
        });

        return completion.Task;
    }
#endif
}
