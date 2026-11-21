$created=$false; $mtx=New-Object Threading.Mutex($true,"Local\ZoomLensMutex",[ref]$created); $ev=New-Object Threading.EventWaitHandle($false,"AutoReset","Local\ZoomLensExit")
if(-not $created){ $ev.Set() | Out-Null; exit }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W{
[DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")]public static extern short GetAsyncKeyState(int k);
[DllImport("user32.dll")]public static extern int GetWindowLong(IntPtr h,int i);
[DllImport("user32.dll")]public static extern int SetWindowLong(IntPtr h,int i,int v);
[DllImport("user32.dll")]public static extern bool SetWindowDisplayAffinity(IntPtr h,uint a);
}
"@
[W]::SetProcessDPIAware() | Out-Null
$size=300; $zoom=3.0
$f=New-Object Windows.Forms.Form
$f.FormBorderStyle='None'; $f.StartPosition='Manual'; $f.ShowInTaskbar=$false; $f.TopMost=$true
$f.Size="$size,$size"; $f.BackColor='Black'
$gp=New-Object Drawing.Drawing2D.GraphicsPath; $gp.AddEllipse(0,0,$size-1,$size-1)
$f.Region=New-Object Drawing.Region($gp)
$pb=New-Object Windows.Forms.PictureBox; $pb.Dock='Fill'; $f.Controls.Add($pb)
$f.Add_Shown({
  $h=$f.Handle
  # click-through + no-activate + tool window; hide lens from screen capture
  [W]::SetWindowLong($h,-20,([W]::GetWindowLong($h,-20) -bor 0x80020 -bor 0x8000000 -bor 0x80)) | Out-Null
  [W]::SetWindowDisplayAffinity($h,0x11) | Out-Null
})
$bmp=$null; $visible=$true; $lastZ=0
$t=New-Object Windows.Forms.Timer; $t.Interval=16
$t.Add_Tick({
  if($ev.WaitOne(0)){ $f.Close(); return }
  $ctrl=([W]::GetAsyncKeyState(0x11) -band 0x8000) -ne 0
  $alt=([W]::GetAsyncKeyState(0x12) -band 0x8000) -ne 0
  if($ctrl -and $alt){
    if([W]::GetAsyncKeyState(0x51) -band 1){ $f.Close(); return }          # Q
    if([W]::GetAsyncKeyState(0x5A) -band 1){ $script:visible=-not $script:visible; if($script:visible){$f.Show()}else{$f.Hide()} } # Z
    if([W]::GetAsyncKeyState(0x26) -band 1){ $script:zoom=[Math]::Min(16,$script:zoom+0.5) } # Up
    if([W]::GetAsyncKeyState(0x28) -band 1){ $script:zoom=[Math]::Max(1.5,$script:zoom-0.5) } # Down
  }
  if(-not $script:visible){return}
  $p=[Windows.Forms.Cursor]::Position
  $src=[int]($size/$zoom)
  $f.Location=New-Object Drawing.Point(($p.X-$size/2),($p.Y-$size/2))
  $b=New-Object Drawing.Bitmap($src,$src)
  $g=[Drawing.Graphics]::FromImage($b)
  $g.CopyFromScreen($p.X-$src/2,$p.Y-$src/2,0,0,(New-Object Drawing.Size($src,$src)))
  $g.Dispose()
  $big=New-Object Drawing.Bitmap($size,$size); $g2=[Drawing.Graphics]::FromImage($big)
  $g2.InterpolationMode='NearestNeighbor'; $g2.PixelOffsetMode='Half'
  $g2.DrawImage($b,0,0,$size,$size)
  $pen=New-Object Drawing.Pen([Drawing.Color]::DeepSkyBlue,4); $g2.DrawEllipse($pen,2,2,$size-5,$size-5)
  $g2.Dispose(); $b.Dispose()
  $old=$pb.Image; $pb.Image=$big; if($old){$old.Dispose()}
})
$ni=New-Object Windows.Forms.NotifyIcon; $ni.Icon=New-Object Drawing.Icon((Join-Path $PSScriptRoot "ZoomLens.ico")); $ni.Text="ZoomLens"; $ni.Visible=$true
$m=New-Object Windows.Forms.ContextMenuStrip
$toggle={ $script:visible=-not $script:visible; if($script:visible){$f.Show()}else{$f.Hide()} }
$i1=$m.Items.Add("نمایش / مخفی کردن"); $i1.Add_Click($toggle)
$i2=$m.Items.Add("زوم بیشتر (+)"); $i2.Add_Click({ $script:zoom=[Math]::Min(16,$script:zoom+0.5) })
$i3=$m.Items.Add("زوم کمتر (-)"); $i3.Add_Click({ $script:zoom=[Math]::Max(1.5,$script:zoom-0.5) })
[void]$m.Items.Add("-")
$i4=$m.Items.Add("خروج"); $i4.Add_Click({ $f.Close() })
$ni.ContextMenuStrip=$m; $ni.Add_MouseClick({ if($_.Button -eq "Left"){ & $toggle } })
$f.Add_FormClosed({ $ni.Visible=$false; $ni.Dispose() })
$t.Start()
[Windows.Forms.Application]::Run($f)
