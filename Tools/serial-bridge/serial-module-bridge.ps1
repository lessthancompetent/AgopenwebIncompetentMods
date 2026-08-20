# Serial <-> UDP bridge for AgOpenGPS-style USB modules (the AgIO role).
#
# A stock AOG module on USB streams raw PGN frames (0x80 0x81 src pgn len .. crc)
# over serial at 38400. AgOpenWeb speaks UDP only: it listens on 9999 and sends
# to modules on 8888. This bridge makes a USB module indistinguishable from a
# UDP one: serial frames go to 127.0.0.1:9999, and everything the app sends to
# port 8888 (it broadcasts, and also sends to loopback:8888) goes down the wire.
#
#   powershell -File serial-module-bridge.ps1 -Port COM12 [-Baud 38400]
param(
  [Parameter(Mandatory=$true)][string]$Port,
  [int]$Baud = 38400
)

$sp = New-Object System.IO.Ports.SerialPort $Port,$Baud,'None',8,'One'
$sp.DtrEnable = $false          # do not reset the board on open
$sp.Open()

$rx = New-Object System.Net.Sockets.UdpClient
$rx.Client.SetSocketOption([Net.Sockets.SocketOptionLevel]::Socket, [Net.Sockets.SocketOptionName]::ReuseAddress, $true)
$rx.Client.Bind((New-Object System.Net.IPEndPoint([Net.IPAddress]::Any, 8888)))
$rx.Client.ReceiveTimeout = 1

$tx = New-Object System.Net.Sockets.UdpClient
$app = New-Object System.Net.IPEndPoint([Net.IPAddress]::Loopback, 9999)

Write-Host "bridging $Port @ $Baud  <->  udp :8888 (from app) / 127.0.0.1:9999 (to app)"
$buf = New-Object System.Collections.Generic.List[byte]
$seen = @{}

while ($true) {
  # serial -> app: accumulate, extract whole PGN frames
  $n = $sp.BytesToRead
  if ($n -gt 0) {
    $tmp = New-Object byte[] $n
    [void]$sp.Read($tmp, 0, $n)
    $buf.AddRange($tmp)
    while ($buf.Count -ge 6) {
      if ($buf[0] -ne 0x80 -or $buf[1] -ne 0x81) { $buf.RemoveAt(0); continue }
      $len = $buf[4]; $total = 5 + $len + 1
      if ($buf.Count -lt $total) { break }
      $frame = $buf.GetRange(0, $total).ToArray()
      $buf.RemoveRange(0, $total)
      [void]$tx.Send($frame, $frame.Length, $app)
      $pgn = $frame[3]
      if (-not $seen.ContainsKey($pgn)) { $seen[$pgn] = $true; Write-Host ("module speaks PGN {0} (0x{0:X2}), {1} bytes" -f $pgn, $frame.Length) }
    }
  }
  # app -> serial: forward whatever lands on 8888
  try {
    $from = New-Object System.Net.IPEndPoint([Net.IPAddress]::Any, 0)
    $data = $rx.Receive([ref]$from)
    if ($data.Length -gt 0) { $sp.Write($data, 0, $data.Length) }
  } catch {}   # timeout: nothing waiting
  Start-Sleep -Milliseconds 5
}
