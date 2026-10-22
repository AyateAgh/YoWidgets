namespace YoWidgets;

internal sealed class AttachmentPicker : Window
{
    sealed record Entry(string Path,bool Directory) {public string Name=>System.IO.Path.GetFileName(Path.TrimEnd('\\'));}
    readonly ListBox list=new() {SelectionMode=SelectionMode.Extended,BorderThickness=new Thickness(0)};
    readonly TextBox address=Ui.Input(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    readonly TextBlock status=Ui.Text("Select files, or one working folder",12,"Muted");
    string current="";
    public string[] SelectedFiles {get;private set;}=[];
    public string? SelectedFolder {get;private set;}
    public AttachmentPicker()
    {
        Title="Files and folders";Width=Math.Min(840,SystemParameters.WorkArea.Width*.86);Height=Math.Min(580,SystemParameters.WorkArea.Height*.82);MinWidth=520;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=Ui.Brush("Background");Foreground=Ui.Brush("Text");
        var layout=new DockPanel {Margin=new Thickness(20)};
        var bar=new DockPanel {Margin=new Thickness(0,0,0,14)};var up=WidgetVisuals.IconButton("chevron-up","Parent folder",()=>Navigate(Directory.GetParent(current)?.FullName??current));bar.Children.Add(up);bar.Children.Add(address);DockPanel.SetDock(bar,Dock.Top);layout.Children.Add(bar);
        var bottom=new DockPanel {Margin=new Thickness(0,14,0,0)};var add=Ui.Button("Add selected",Accept,true);DockPanel.SetDock(add,Dock.Right);bottom.Children.Add(add);var folder=Ui.Button("Use this folder",()=>{SelectedFolder=current;DialogResult=true;});DockPanel.SetDock(folder,Dock.Right);bottom.Children.Add(folder);bottom.Children.Add(status);DockPanel.SetDock(bottom,Dock.Bottom);layout.Children.Add(bottom);
        var main=new Grid();main.ColumnDefinitions.Add(new() {Width=new GridLength(150)});main.ColumnDefinitions.Add(new());var places=new StackPanel {Margin=new Thickness(0,0,12,0)};
        foreach(var (name,path) in new[]{("Desktop",Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),("Documents",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),("Downloads",System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")),("Home",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))})places.Children.Add(Ui.Button(name,()=>Navigate(path)));
        foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady))places.Children.Add(Ui.Button(drive.Name,()=>Navigate(drive.RootDirectory.FullName)));main.Children.Add(places);
        list.Background=Ui.Brush("Panel");list.Foreground=Ui.Brush("Text");Grid.SetColumn(list,1);main.Children.Add(list);layout.Children.Add(main);Content=layout;
        address.PreviewKeyDown+=(_,e)=> {if(e.Key==Key.Enter){e.Handled=true;Navigate(address.Text);}};
        list.MouseDoubleClick+=(_,_)=> {if(list.SelectedItem is ListBoxItem {Tag:Entry entry}){if(entry.Directory)Navigate(entry.Path);else Accept();}};
        list.SelectionChanged+=(_,_)=>status.Text=list.SelectedItems.Count==0?"Select files, or one working folder":$"{list.SelectedItems.Count} selected";
        Navigate(address.Text);
    }
    void Navigate(string path)
    {
        try {
            path=System.IO.Path.GetFullPath(path);var entries=Directory.EnumerateFileSystemEntries(path).Take(2001).Select(p=>new Entry(p,Directory.Exists(p))).OrderByDescending(p=>p.Directory).ThenBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
            current=path;address.Text=path;list.Items.Clear();foreach(var entry in entries.Take(2000)) {
                var row=new DockPanel {Margin=new Thickness(4,6,4,6)};var icon=WidgetVisuals.Icon(entry.Directory?"folder":"file",18,"Text");icon.Margin=new Thickness(0,0,10,0);row.Children.Add(icon);row.Children.Add(Ui.Text(entry.Name,13));
                list.Items.Add(new ListBoxItem {Content=row,Tag=entry,ToolTip=entry.Path,HorizontalContentAlignment=HorizontalAlignment.Stretch});
            }status.Text=entries.Length>2000?"Showing the first 2,000 entries. Enter a more specific path.":"Select files, or one working folder";
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException){status.Text="This folder is unavailable.";}
    }
    void Accept()
    {
        var entries=list.SelectedItems.Cast<ListBoxItem>().Select(i=>(Entry)i.Tag).ToArray();
        if(entries.Length==0)return;if(entries.Count(e=>e.Directory)>1){status.Text="Choose only one working folder.";return;}
        if(entries.Count(e=>!e.Directory)>AssistantAttachment.MaxFiles){status.Text="Attach up to 5 files.";return;}
        SelectedFolder=entries.FirstOrDefault(e=>e.Directory)?.Path;SelectedFiles=entries.Where(e=>!e.Directory).Select(e=>e.Path).ToArray();DialogResult=true;
    }
}
