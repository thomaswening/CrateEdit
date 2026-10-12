// Spike: how do replace techniques behave on this machine's file system?
using System.Diagnostics;

var sample = args[0];
var dir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "crateedit-write-spike");
Directory.CreateDirectory(dir);
Console.WriteLine($"Folder: {dir}  (drive format: {new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dir))!).DriveFormat})");

string Fresh(string name)
{
    var p = Path.Combine(dir, name);
    if (File.Exists(p)) File.Delete(p);
    File.Copy(sample, p);
    File.SetCreationTimeUtc(p, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    return p;
}

string Times(string p) => $"created {File.GetCreationTimeUtc(p):yyyy-MM-dd HH:mm}, modified {File.GetLastWriteTimeUtc(p):yyyy-MM-dd HH:mm}";
var newContent = File.ReadAllBytes(sample); newContent[^1] ^= 0xFF; // same size, one byte differs

void Report(string technique, Action<string, string> replace)
{
    var target = Fresh("target.mp3");
    var temp = Path.Combine(dir, "target.mp3.crateedit-tmp");
    File.WriteAllBytes(temp, newContent);
    File.SetCreationTimeUtc(temp, DateTime.UtcNow);
    var sw = Stopwatch.StartNew();
    try { replace(temp, target); Console.WriteLine($"{technique,-34} ok in {sw.ElapsedMilliseconds,4} ms; {Times(target)}; content new: {File.ReadAllBytes(target)[^1] == newContent[^1]}"); }
    catch (Exception e) { Console.WriteLine($"{technique,-34} FAILED: {e.GetType().Name}: {e.Message.Split('\n')[0]}"); }
    foreach (var f in Directory.GetFiles(dir).Where(f => !f.EndsWith("target.mp3"))) File.Delete(f);

    // Same, while another program holds the file open for reading (like a player).
    target = Fresh("target.mp3");
    File.WriteAllBytes(temp, newContent);
    foreach (var (label, share) in new[] { ("reader FileShare.Read", FileShare.Read), ("reader FileShare.ReadWrite|Delete", FileShare.ReadWrite | FileShare.Delete) })
    {
        if (!File.Exists(temp)) File.WriteAllBytes(temp, newContent);
        using var reader = new FileStream(target, FileMode.Open, FileAccess.Read, share);
        try { replace(temp, target); Console.WriteLine($"   while {label,-30}: ok"); }
        catch (Exception e) { Console.WriteLine($"   while {label,-30}: FAILED {e.GetType().Name}"); }
    }
    foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
}

Report("File.Move(overwrite: true)", (t, d) => File.Move(t, d, overwrite: true));
Report("File.Replace(no backup)", (t, d) => File.Replace(t, d, null));
Report("File.Replace(with backup)", (t, d) => File.Replace(t, d, d + ".crateedit-bak"));

// In-place: overwrite only the first 64 KB (the tag region), file stays the same object.
{
    var target = Fresh("target.mp3");
    var sw = Stopwatch.StartNew();
    using (var fs = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
    {
        fs.Write(newContent, 0, 65536);
        fs.Flush(flushToDisk: true);
    }
    Console.WriteLine($"{"In-place tag overwrite (64 KB)",-34} ok in {sw.ElapsedMilliseconds,4} ms; {Times(target)}");
    using var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
    try { using var w = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); Console.WriteLine("   while reader FileShare.Read           : ok"); }
    catch (Exception e) { Console.WriteLine($"   while reader FileShare.Read           : FAILED {e.GetType().Name}"); }
    reader.Dispose();
    using var reader2 = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    try { using var w = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); Console.WriteLine("   while reader FileShare.ReadWrite      : ok"); }
    catch (Exception e) { Console.WriteLine($"   while reader FileShare.ReadWrite      : FAILED {e.GetType().Name}"); }
}

// Cost of writing a complete new file (temp + flush) for this sample size.
{
    var sw = Stopwatch.StartNew();
    var temp = Path.Combine(dir, "full.tmp");
    using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write)) { fs.Write(newContent); fs.Flush(flushToDisk: true); }
    Console.WriteLine($"{"Full file write + flush",-34} {sw.ElapsedMilliseconds,4} ms for {newContent.Length / 1_000_000.0:F1} MB");
    File.Delete(temp);
}
foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
