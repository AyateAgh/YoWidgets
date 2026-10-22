param(
 [ValidateSet('state','media','calendar_list','calendar_add','calendar_complete','focus','status')][string]$Action='state',
 [string]$ArgumentsJson='{}',
 [string]$DataRoot=(Join-Path $env:LOCALAPPDATA 'YoWidgets')
)
$ErrorActionPreference='Stop'
# PowerShell 7 client; no keys, code evaluation or network listener.
$null=$ArgumentsJson | ConvertFrom-Json
$canonical=[IO.Path]::GetFullPath($DataRoot).ToUpperInvariant()
$pipeName='YoWidgets.WidgetBus.'+[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).Substring(0,20)
$pipe=[IO.Pipes.NamedPipeClientStream]::new('.',$pipeName,[IO.Pipes.PipeDirection]::InOut,[IO.Pipes.PipeOptions]::Asynchronous)
$deadline=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(15))
try{
 $pipe.ConnectAsync($deadline.Token).GetAwaiter().GetResult()
 $reader=[IO.StreamReader]::new($pipe,[Text.Encoding]::UTF8,$false,1024,$true)
 $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true)
 try{
  $writer.AutoFlush=$true
  $request=@{action=$Action;args=($ArgumentsJson | ConvertFrom-Json)} | ConvertTo-Json -Depth 12 -Compress
  if($request.Length -gt 16384){throw 'Widget request is too large.'}
  $writer.WriteLine($request)
  $reply=$reader.ReadLineAsync($deadline.Token).GetAwaiter().GetResult()
  if(-not $reply){throw 'No widget response.'}
  $reply
 }finally{$writer.Dispose();$reader.Dispose()}
}finally{$deadline.Dispose();$pipe.Dispose()}
