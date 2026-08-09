using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BootCampCharge;

internal static class Ch0BTest
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BootCampCharge CH0B 可用性测试 ===");
        Console.WriteLine();

        // Init driver
        if (!InpOut.DriverOpen())
        {
            Console.WriteLine("[FAIL] InpOut 驱动未加载！请以管理员身份运行。");
            Console.ReadLine();
            return;
        }
        Console.WriteLine("[OK] InpOut 驱动已加载");

        // Open SMC
        Smc smc;
        try
        {
            smc = Smc.Open();
            Console.WriteLine("[OK] SMC 已打开");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] 无法打开 SMC: {ex.Message}");
            Console.ReadLine();
            return;
        }

        // ── Step 0: 读取初始状态 ──
        int bclm0 = ReadBclm(smc);
        int ch0b0 = ReadCh0B(smc);
        var (pct0, ac0, charging0) = ReadWindowsPower();
        Console.WriteLine($"初始状态: BCLM={bclm0}, CH0B={ch0b0}, 电量={pct0}%, AC={(ac0 ? "连接" : "断开")}, Windows充电状态={(charging0 ? "充电中" : "未充电")}");
        Console.WriteLine();

        if (!ac0)
        {
            Console.WriteLine("[SKIP] 充电器未连接，无法测试充电控制。请插上充电器后重试。");
            Console.ReadLine();
            return;
        }

        // ── 实验 1: BCLM=0 时 CH0B=2 是否阻止充电 ──
        Console.WriteLine("━━━ 实验 1: BCLM=0 + CH0B=2 ━━━");
        Console.WriteLine("写入 BCLM=0...");
        WriteBclm(smc, 0);
        Thread.Sleep(1000);

        Console.WriteLine("写入 CH0B=2 (禁止充电)...");
        WriteCh0B(smc, 2);
        Thread.Sleep(1000);

        // 验证写入是否成功
        int ch0b_after_write = ReadCh0B(smc);
        Console.WriteLine($"  写入后读 CH0B = {ch0b_after_write} (期望 2)");
        Console.WriteLine($"  写入成功: {(ch0b_after_write == 2 ? "YES" : "NO - CH0B 写入被忽略")}");

        Console.WriteLine("等待 10 秒观察充电状态变化...");
        for (int i = 10; i > 0; i--)
        {
            Thread.Sleep(1000);
            var (pct, ac, charging) = ReadWindowsPower();
            Console.Write($"\r  {i}秒: 电量={pct}%, Windows={(charging ? "充电中" : "未充电")}, CH0B={ReadCh0B(smc)}    ");
        }
        Console.WriteLine();

        var (pct1, ac1, charging1) = ReadWindowsPower();
        Console.WriteLine($"结果: 电量={pct1}%, Windows充电状态={(charging1 ? "充电中" : "未充电")}");
        Console.WriteLine($"  → CH0B=2 生效: {(charging1 ? "NO - 仍在充电" : "YES - 充电已停止")}");
        Console.WriteLine();

        // ── 恢复 ──
        Console.WriteLine("━━━ 恢复 ━━━");
        Console.WriteLine("写入 CH0B=0 (恢复充电)...");
        WriteCh0B(smc, 0);
        Thread.Sleep(500);
        Console.WriteLine($"恢复 BCLM={bclm0}...");
        WriteBclm(smc, bclm0 > 0 ? bclm0 : 80);
        Thread.Sleep(1000);

        var (pct_final, ac_final, charging_final) = ReadWindowsPower();
        Console.WriteLine($"恢复后: BCLM={ReadBclm(smc)}, CH0B={ReadCh0B(smc)}, 电量={pct_final}%, 充电={charging_final}");
        Console.WriteLine();

        // ── 结论 ──
        Console.WriteLine("━━━ 结论 ━━━");
        bool ch0bWriteOk = (ch0b_after_write == 2);
        bool ch0bEffectOk = !charging1; // CH0B=2 后 Windows 报"未充电"

        Console.WriteLine($"CH0B 写入成功: {(ch0bWriteOk ? "YES" : "NO")}");
        Console.WriteLine($"CH0B 充电控制生效: {(ch0bEffectOk ? "YES" : "NO")}");

        if (ch0bWriteOk && ch0bEffectOk)
        {
            Console.WriteLine();
            Console.WriteLine("★ CH0B 在 Boot Camp 下可用！完整模式：放电/航行/过热/校准全部可用。");
        }
        else if (ch0bWriteOk && !ch0bEffectOk)
        {
            Console.WriteLine();
            Console.WriteLine("★ CH0B 能写入但不生效。降级模式：只有 BCLM 充电限制 + TopUp 可用。");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("★ CH0B 写入被忽略。降级模式：只有 BCLM 充电限制 + TopUp 可用。");
        }

        Console.WriteLine();
        Console.WriteLine("按回车退出...");
        Console.ReadLine();
    }

    // ── SMC helpers ──
    static int ReadBclm(Smc smc)
    {
        try
        {
            if (smc.KeyExists("BCLM")) return smc.ReadKey("BCLM", 1)[0];
        } catch {}
        return -1;
    }

    static int ReadCh0B(Smc smc)
    {
        try
        {
            if (smc.KeyExists("CH0B")) return smc.ReadKey("CH0B", 1)[0];
        } catch {}
        return -1;
    }

    static void WriteBclm(Smc smc, int val)
    {
        try { if (smc.KeyExists("BCLM")) smc.WriteKey("BCLM", new byte[]{(byte)val}); } catch {}
    }

    static void WriteCh0B(Smc smc, int val)
    {
        try
        {
            if (smc.KeyExists("CH0B")) smc.WriteKey("CH0B", new byte[]{(byte)val});
            if (smc.KeyExists("CH0C")) smc.WriteKey("CH0C", new byte[]{(byte)val});
        } catch {}
    }

    // ── Windows power status ──
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
        // BatteryFlag: 8 = charging, 1 = high, 2 = low, 4 = critical, 128 = no battery
    }
}
