using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BootCampCharge;

/// <summary>
/// SMC 深度诊断 v3 — 带文件日志，出错也不会丢结果。
/// </summary>
internal static class SmcDiag
{
    static string LogPath = Path.Combine(AppContext.BaseDirectory, "diag-log.txt");
    static StreamWriter? _log;

    static void Log(string msg)
    {
        Console.WriteLine(msg);
        try
        {
            _log ??= new StreamWriter(LogPath, true) { AutoFlush = true };
            _log.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Log("═══════════════════════════════════════════");
            Log("   SMC 深度诊断 v3 — 充电恢复条件测试");
            Log("═══════════════════════════════════════════");
            Log("");

            // 检查驱动
            bool driverOk;
            try { driverOk = InpOut.DriverOpen(); }
            catch (Exception ex) { Log($"[FAIL] 加载驱动异常: {ex.Message}"); driverOk = false; }

            if (!driverOk)
            {
                Log("[FAIL] InpOut 驱动未加载！请以管理员身份运行。");
                Console.WriteLine("按回车退出...");
                Console.ReadLine();
                return;
            }
            Log("[OK] InpOut 驱动已加载");

            // 打开 SMC
            Smc smc = Smc.Open();
            Log("[OK] SMC 已打开");
            Log("");

            // 当前状态
            Log("═══ 当前状态 ═══");
            PrintStatus(smc);
            Log("");

            var (pct, ac, charging) = ReadWindowsPower();
            if (!ac)
            {
                Log("⚠ 充电器未连接！请插上充电器后重试。");
                Console.WriteLine("按回车退出...");
                Console.ReadLine();
                return;
            }

            // ── 实验 A：BCLM=80→0 能否恢复充电？ ──
            Log("═══════════════════════════════════════════");
            Log("实验 A：BCLM=80→0 能否恢复充电？");
            Log("═══════════════════════════════════════════");
            Log("");

            Log("步骤 A1: 设 BCLM=80, CH0B=0（确保限制状态）");
            WriteSmc(smc, "CH0B", 0);
            Thread.Sleep(300);
            WriteSmc(smc, "BCLM", 80);
            Thread.Sleep(2000);
            PrintStatus(smc);
            Log("");

            Log("步骤 A2: 改 BCLM=0");
            WriteSmc(smc, "BCLM", 0);
            Thread.Sleep(2000);
            PrintStatus(smc);
            Log("");

            Log("步骤 A3: 等 15 秒观察充电状态...");
            bool aRecharge = ObserveCharging(15);
            Log($"★ 实验 A 结果: BCLM=0 后充电 = {aRecharge}");
            Log($"  → {(aRecharge ? "BCLM=0 可以恢复充电" : "BCLM=0 不能恢复充电")}");
            Log("");

            // ── 实验 B：BCLM=100 能否恢复充电？ ──
            Log("═══════════════════════════════════════════");
            Log("实验 B：BCLM=80→100 能否恢复充电？");
            Log("（可能 BCLM=0=不充电，BCLM=100=充到100%）");
            Log("═══════════════════════════════════════════");
            Log("");

            Log("步骤 B1: 先设 BCLM=80 确保限制");
            WriteSmc(smc, "BCLM", 80);
            Thread.Sleep(1000);
            PrintStatus(smc);
            Log("");

            Log("步骤 B2: 改 BCLM=100");
            WriteSmc(smc, "BCLM", 100);
            Thread.Sleep(2000);
            PrintStatus(smc);
            Log("");

            Log("步骤 B3: 等 15 秒观察...");
            bool bRecharge = ObserveCharging(15);
            Log($"★ 实验 B 结果: BCLM=100 后充电 = {bRecharge}");
            Log("");

            // ── 实验 C：BCLM=0 + 拔插充电器 ──
            Log("═══════════════════════════════════════════");
            Log("实验 C：BCLM=0 + 物理拔插充电器");
            Log("═══════════════════════════════════════════");
            Log("");

            Log("步骤 C1: 设 BCLM=80 确保限制");
            WriteSmc(smc, "BCLM", 80);
            Thread.Sleep(1000);
            PrintStatus(smc);
            Log("");

            Log("步骤 C2: 改 BCLM=0");
            WriteSmc(smc, "BCLM", 0);
            Thread.Sleep(1000);
            Log("");

            Log(">>> 请现在拔掉充电器！等3秒再插回！<<<");
            Console.WriteLine("");
            Console.WriteLine(">>> 请拔掉充电器，等3秒后插回，然后按回车 <<<");
            Console.ReadLine();
            Thread.Sleep(2000);

            Log("步骤 C3: 拔插后状态：");
            PrintStatus(smc);
            Log("");

            Log("步骤 C4: 等 15 秒观察...");
            bool cRecharge = ObserveCharging(15);
            Log($"★ 实验 C 结果: 拔插+BCLM=0 后充电 = {cRecharge}");
            Log("");

            // ── 汇总 ──
            Log("═══════════════════════════════════════════");
            Log("                    汇总");
            Log("═══════════════════════════════════════════");
            Log($"  BCLM=80→0     恢复充电: {aRecharge}");
            Log($"  BCLM=80→100   恢复充电: {bRecharge}");
            Log($"  拔插+BCLM=0   恢复充电: {cRecharge}");
            Log("");
            Log("结果已保存到 diag-log.txt");
            Log("");

            // 恢复
            Log("恢复 BCLM=80...");
            WriteSmc(smc, "BCLM", 80);
            Thread.Sleep(500);
            WriteSmc(smc, "CH0B", 0);
            PrintStatus(smc);

            Console.WriteLine("");
            Console.WriteLine("诊断完成！结果已保存到 diag-log.txt");
            Console.WriteLine("请把 diag-log.txt 的内容告诉我，或者直接告诉我汇总结果。");
            Console.WriteLine("按回车退出...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Log($"[致命错误] {ex}");
            Console.WriteLine($"[致命错误] {ex.Message}");
            Console.WriteLine("按回车退出...");
            Console.ReadLine();
        }
    }

    static bool ObserveCharging(int seconds)
    {
        bool lastCharging = false;
        for (int i = seconds; i > 0; i--)
        {
            Thread.Sleep(1000);
            var (p, _, c) = ReadWindowsPower();
            lastCharging = c;
            Console.Write($"\r  {i,2}s: 电量={p}% 充电={c}    ");
            Log($"  {i,2}s: 电量={p}% 充电={c}");
        }
        Console.WriteLine("");
        return lastCharging;
    }

    static void PrintStatus(Smc smc)
    {
        int bclm = ReadSmcByte(smc, "BCLM");
        int ch0b = ReadSmcByte(smc, "CH0B");
        var (pct, ac, charging) = ReadWindowsPower();
        double? temp = ReadTemp(smc);

        Log($"  ┌─────────────────────────────────┐");
        Log($"  │ 电量:{pct,3}%  充电:{charging}  BCLM:{bclm,3}  CH0B:{ch0b}");
        Log($"  │ 充电器:{(ac ? "已连接" : "未连接")}  温度:{(temp.HasValue ? temp.Value.ToString("0.0") : "--")}°C");
        Log($"  └─────────────────────────────────┘");
    }

    static int ReadSmcByte(Smc smc, string key)
    {
        try { if (smc.KeyExists(key)) return smc.ReadKey(key, 1)[0]; } catch { }
        return -1;
    }

    static void WriteSmc(Smc smc, string key, int val)
    {
        try
        {
            if (smc.KeyExists(key))
            {
                smc.WriteKey(key, new byte[] { (byte)val });
                Log($"  [写入] {key}={val}");
            }
        }
        catch (Exception ex) { Log($"  [错误] {key}={val}: {ex.Message}"); }
    }

    static double? ReadTemp(Smc smc)
    {
        try { return smc.ReadNumber("TC0P"); } catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, Reserved1;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS s);

    static (int pct, bool ac, bool charging) ReadWindowsPower()
    {
        var sps = new SYSTEM_POWER_STATUS();
        GetSystemPowerStatus(ref sps);
        return (sps.BatteryLifePercent, sps.ACLineStatus == 1, sps.BatteryFlag == 8);
    }
}
