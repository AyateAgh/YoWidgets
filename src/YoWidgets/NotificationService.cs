using Windows.UI.Notifications.Management;
using Windows.UI.Notifications;
namespace YoWidgets;

internal sealed class NotificationService
{
    public bool HasIdentity {get;}
    public NotificationService(){try{_=Windows.ApplicationModel.Package.Current.Id;HasIdentity=true;}catch{}Status=HasIdentity?"Connect to see your Windows notifications.":"Windows notification access requires a packaged installation. This portable build can open notification settings.";}
    public record Entry(string App, string Title, string Body, DateTimeOffset Time);
    public IReadOnlyList<Entry> Entries { get; private set; } = Array.Empty<Entry>(); public string Status { get; private set; } = "Windows identity and permission required"; public event Action? Changed; bool suspended; int generation; public void Enable()
    {
        suspended = false;
        generation++;
    }
    public async Task RefreshAsync(bool request)
    {
        if (suspended)
            return;
        if(!HasIdentity){Entries=Array.Empty<Entry>();Status="Windows notification access requires a packaged installation. This portable build can open notification settings.";Changed?.Invoke();return;}
        var v = ++generation;
        try
        {
            var listener = UserNotificationListener.Current;
            var access = request ? await listener.RequestAccessAsync() : listener.GetAccessStatus();
            if (v != generation || suspended)
                return;
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                Status = "Allow notification access in Windows";
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
            Status = entries.Count == 0 ? "No new notifications" : "Windows notifications";
            listener.NotificationChanged -= OnChanged;
            listener.NotificationChanged += OnChanged;
            Changed?.Invoke();
        }
        catch (Exception e) { Status = "Install the package identity to enable access"; App.Log("notifications", e); Changed?.Invoke(); }
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
