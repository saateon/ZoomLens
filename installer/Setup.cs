using System;using System.Diagnostics;using System.IO;using System.Reflection;using System.Threading;using System.Windows.Forms;using System.Runtime.InteropServices;

class HotForm : Form {
  [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint vk);
  [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
  public string Vbs; public bool Ok;
  public HotForm(string vbs){
    Vbs=vbs; ShowInTaskbar=false; WindowState=FormWindowState.Minimized; FormBorderStyle=FormBorderStyle.None; Opacity=0;
    Load+=delegate{ Visible=false; };
    CreateHandle(); Ok=RegisterHotKey(Handle,1,0x0003|0x4000,0x4D); // Ctrl+Alt+M (no repeat)
  }
  protected override void SetVisibleCore(bool v){ base.SetVisibleCore(false); }
  protected override void WndProc(ref Message m){
    if(m.Msg==0x0312){
      try{ EventWaitHandle ev; if(EventWaitHandle.TryOpenExisting("ZoomLensExit",out ev)){ ev.Set(); ev.Close(); } else { Process.Start(new ProcessStartInfo(Vbs){UseShellExecute=true}); } }catch{}
    }
    base.WndProc(ref m);
  }
  protected override void OnFormClosed(FormClosedEventArgs e){ UnregisterHotKey(Handle,1); base.OnFormClosed(e); }
}

class Lg{ public static void W(string s){ try{ File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZoomLens","host.log"),DateTime.Now.ToString("HH:mm:ss")+" "+s+Environment.NewLine); }catch{} } }
class Setup{
 static string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZoomLens");
 static MessageBoxOptions R=MessageBoxOptions.RtlReading|MessageBoxOptions.RightAlign;
 static string HostExe(){return Path.Combine(Dir,"ZoomLensHost.exe");}
 static void Lnk(string p,string target,string args,bool hot){
  dynamic sh=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
  dynamic s=sh.CreateShortcut(p);
  s.TargetPath=target; s.Arguments=args;
  s.IconLocation=Path.Combine(Dir,"ZoomLens.ico");
  s.WorkingDirectory=Dir; s.Description="ZoomLens"; s.Save();
 }
 static string[] Links(){return new[]{
  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"ZoomLens.lnk"),
  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"ZoomLens.lnk")};}
 static string StartupLnk(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"ZoomLensHotkey.lnk");}
 static void StopRunning(){
  try{var e=new EventWaitHandle(false,EventResetMode.AutoReset,"ZoomLensExit");e.Set();}catch{}
  foreach(var p in Process.GetProcessesByName("ZoomLensHost"))try{p.Kill();p.WaitForExit(2000);}catch{}
  Thread.Sleep(600);
 }
 static void StartHost(){
  if(File.Exists(HostExe()))Process.Start(new ProcessStartInfo(HostExe(),"--hotkey"){UseShellExecute=false,CreateNoWindow=true});
 }
 [STAThread] static void Main(string[] args){
  Application.EnableVisualStyles();
  Lg.W("main args="+args.Length);
  if(args.Length>0&&args[0]=="--hotkey"){
   bool created; var mx=new Mutex(true,"ZoomLensHostMutex",out created); Lg.W("mutex created="+created); if(!created)return;
   var f=new HotForm(Path.Combine(Dir,"ZoomLens.vbs")); 
   Lg.W("form ok="+f.Ok); if(!f.Ok){MessageBox.Show("کلید Ctrl+Alt+M در اختیار برنامه دیگری است و ZoomLens نتوانست آن را بگیرد.","ZoomLens",MessageBoxButtons.OK,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button1,R);return;}
   Lg.W("start ok="+f.Ok); AppDomain.CurrentDomain.UnhandledException+=delegate(object o,UnhandledExceptionEventArgs ea){Lg.W("EXC "+ea.ExceptionObject);}; Application.ThreadException+=delegate(object o,ThreadExceptionEventArgs ea){Lg.W("TEXC "+ea.Exception);}; Microsoft.Win32.SystemEvents.SessionEnding+=delegate{Lg.W("session ending");}; f.FormClosed+=delegate{Lg.W("form closed");}; Application.Run(f); Lg.W("run returned"); GC.KeepAlive(mx); return;
  }
  bool installed=File.Exists(Path.Combine(Dir,"ZoomLens.ps1"));
  if(installed){
   var r=MessageBox.Show("ZoomLens قبلاً نصب شده است.\n\nبله = نصب مجدد\nخیر = حذف (Uninstall)\nلغو = خروج","ZoomLens",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button1,R);
   if(r==DialogResult.Cancel)return;
   StopRunning();
   if(r==DialogResult.No){
    foreach(var l in Links())try{File.Delete(l);}catch{}
    try{File.Delete(StartupLnk());}catch{}
    try{Directory.Delete(Dir,true);}catch{}
    MessageBox.Show("ZoomLens حذف شد.","ZoomLens",MessageBoxButtons.OK,MessageBoxIcon.Information,MessageBoxDefaultButton.Button1,R);return;}
  }
  try{
   Directory.CreateDirectory(Dir);
   var a=Assembly.GetExecutingAssembly();
   foreach(var n in new[]{"ZoomLens.ps1","ZoomLens.vbs","ZoomLens.ico"})
    using(var s=a.GetManifestResourceStream(n))using(var f=File.Create(Path.Combine(Dir,n)))s.CopyTo(f);
   File.Copy(a.Location,HostExe(),true);
   string wscript=Path.Combine(Environment.SystemDirectory,"wscript.exe");
   string vbsArg="\""+Path.Combine(Dir,"ZoomLens.vbs")+"\"";
   foreach(var l in Links())Lnk(l,wscript,vbsArg,false);
   Lnk(StartupLnk(),HostExe(),"--hotkey",false);
   StartHost();
  }catch(Exception ex){MessageBox.Show("خطا در نصب:\n"+ex.Message,"ZoomLens",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
  var go=MessageBox.Show("نصب شد!\n\nکلید میانبر: Ctrl+Alt+M (باز/بسته)\nاین کلید از همین الان کار می‌کند و با هر بار روشن شدن ویندوز خودکار فعال می‌شود.\n\nالان ذره‌بین باز شود؟","ZoomLens",MessageBoxButtons.YesNo,MessageBoxIcon.Information,MessageBoxDefaultButton.Button1,R);
  if(go==DialogResult.Yes)Process.Start(new ProcessStartInfo(Path.Combine(Dir,"ZoomLens.vbs")){UseShellExecute=true});
 }}
