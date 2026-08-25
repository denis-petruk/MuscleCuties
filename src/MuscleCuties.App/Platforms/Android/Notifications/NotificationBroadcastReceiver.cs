using Android.App;
using Android.Content;
using Android.OS;

namespace MuscleCuties.App.Services.Notifications;

#pragma warning disable CA1416, CA1422

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class NotificationBroadcastReceiver : BroadcastReceiver
{
    public const string IdExtra = "notification_id";
    public const string TitleExtra = "notification_title";
    public const string BodyExtra = "notification_body";

    private const string ChannelId = "cycle_phase_updates";
    private const string ChannelName = "Cycle phase updates";
    private const int FallbackNotificationId = 740_000;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;

        var notificationId = intent.GetIntExtra(IdExtra, FallbackNotificationId);
        var title = intent.GetStringExtra(TitleExtra) ?? "Cycle phase updated";
        var body = intent.GetStringExtra(BodyExtra) ?? "Your dashboard and plans are updated.";

        ShowNotification(context, title, body, notificationId);
    }

    public static void ShowNotification(Context context, string title, string body, int notificationId)
    {
        var notificationManager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (notificationManager is null)
            return;

        EnsureChannel(notificationManager);

        var builder = BuildNotification(context, title, body, notificationId);
        notificationManager.Notify(notificationId, builder.Build());
    }

    private static Notification.Builder BuildNotification(
        Context context,
        string title,
        string body,
        int notificationId)
    {
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(context, ChannelId)
            : new Notification.Builder(context);

        builder
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true)
            .SetStyle(new Notification.BigTextStyle().BigText(body));

        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
        if (launchIntent is not null)
        {
            launchIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(
                context,
                notificationId,
                launchIntent,
                BuildPendingIntentFlags());
            if (pendingIntent is not null)
                builder.SetContentIntent(pendingIntent);
        }

        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            builder.SetPriority((int)NotificationPriority.Default);

        return builder;
    }

    private static void EnsureChannel(NotificationManager notificationManager)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        using var channel = new NotificationChannel(
            ChannelId,
            ChannelName,
            NotificationImportance.Default)
        {
            Description = "Cycle phase change reminders"
        };
        notificationManager.CreateNotificationChannel(channel);
    }

    private static PendingIntentFlags BuildPendingIntentFlags()
    {
        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;

        return flags;
    }
}

#pragma warning restore CA1416, CA1422
