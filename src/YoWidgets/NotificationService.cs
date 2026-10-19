using Windows.UI.Notifications.Management;
using Windows.UI.Notifications;
namespace YoWidgets;

internal sealed class NotificationService
{
    public record Entry(string App, string Title, string Body, DateTimeOffset Time);
    public IReadOnlyList<Entry> Entries { get; private set; } = Array.Empty<Entry>(); public string Status { get; private set; } = "Нужны identity и разрешение Windows"; public event Action? Changed; bool suspended; int generation; public void Enable()
    {
        suspended = false;
        generation++;
    }
    public async Task RefreshAsync(bool request)
    {
        if (suspended)
            return;
        var v = ++generation;
        try
        {
            var listener = UserNotificationListener.Current;
            var access = request ? await listener.RequestAccessAsync() : listener.GetAccessStatus();
            if (v != generation || suspended)
                return;
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                Status = "Разрешите доступ к уведомлениям в Windows";
                Entries = Array.Empty<Entry>();
                Changed?.Invoke();
                return;
            }
            var notes = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            if (v != generation || suspended)
                return;
            var entries = new List<Entry>();
            foreach (var n in notes.OrderByDescending(n => n.CreationTime).Take(5))
            {
                try
                {
                    var texts = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements();
                    if (texts == null)
                        continue;
                    entries.Add(new(n.AppInfo.DisplayInfo.DisplayName, texts.FirstOrDefault()?.Text ?? "", string.Join(" ", texts.Skip(1).Select(t => t.Text)), n.CreationTime));
                }
                catch { }
            }
            Entries = entries;
            Status = entries.Count == 0 ? "Новых уведомлений нет" : "Уведомления Windows";
            listener.NotificationChanged -= OnChanged;
            listener.NotificationChanged += OnChanged;
            Changed?.Invoke();
        }
        catch (Exception e) { Status = "Для доступа установите пакет identity в настройках"; App.Log("notifications", e); Changed?.Invoke(); }
    }
    void OnChanged(UserNotificationListener s, UserNotificationChangedEventArgs e)
    {
        if (suspended)
            return;
        var version = generation;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!suspended && version == generation) _ = RefreshAsync(false); });
    }
    public void Suspend()
    {
        suspended = true;
        generation++;
        try
        {
            UserNotificationListener.Current.NotificationChanged -= OnChanged;
        }
        catch { }
        Entries = Array.Empty<Entry>();
    }
}
