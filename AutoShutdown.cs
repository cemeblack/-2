// AutoShutdown — выключает компьютер в заданное время,
// а за час до этого показывает таймер в правом нижнем углу экрана.
//
// Код намеренно написан на C# 5, чтобы его мог собрать компилятор
// csc.exe, который уже есть в любой Windows 10/11 (см. install.bat).

using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Settings
{
    // ===== Настройки (после изменения заново запустите install.bat) =====

    // Время выключения: 21:00
    public const int ShutdownHour = 21;
    public const int ShutdownMinute = 0;

    // За сколько минут до выключения показывать таймер
    public const int WarningMinutes = 60;

    // Если компьютер спал/был выключен в момент выключения и проснулся
    // не позже, чем через столько минут, — выключить сразу.
    // Если позже — ждать следующего дня.
    public const int LateToleranceMinutes = 5;
}

static class Program
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        // Не даём запустить вторую копию программы.
        bool isFirstInstance;
        using (var mutex = new Mutex(true, "AutoShutdown_SingleInstance", out isFirstInstance))
        {
            if (!isFirstInstance)
                return;

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ShutdownScheduler());
        }
    }
}

// Работает в фоне без окна; раз в секунду проверяет время.
class ShutdownScheduler : ApplicationContext
{
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    CountdownForm form;
    DateTime target;

    public ShutdownScheduler()
    {
        target = NextShutdownTime(DateTime.Now);
        timer.Interval = 1000;
        timer.Tick += delegate { Check(); };
        timer.Start();
        Check();
    }

    // Ближайшие 21:00: сегодня, если ещё не наступило, иначе — завтра.
    static DateTime NextShutdownTime(DateTime now)
    {
        DateTime today = now.Date.AddHours(Settings.ShutdownHour).AddMinutes(Settings.ShutdownMinute);
        return now < today ? today : today.AddDays(1);
    }

    void Check()
    {
        DateTime now = DateTime.Now;

        if (now >= target)
        {
            bool onTime = now - target <= TimeSpan.FromMinutes(Settings.LateToleranceMinutes);
            if (onTime)
                ShutDown();

            // Если выключение отменили (shutdown /a) или время пропущено
            // из-за сна — следующая попытка будет завтра.
            target = NextShutdownTime(now);
            HideForm();
            return;
        }

        TimeSpan left = target - now;
        if (left <= TimeSpan.FromMinutes(Settings.WarningMinutes))
        {
            if (form == null)
                form = new CountdownForm(Settings.WarningMinutes >= 60);
            form.SetTimeLeft(left);
        }
        else
        {
            HideForm();
        }
    }

    void HideForm()
    {
        if (form != null)
        {
            form.Close();
            form.Dispose();
            form = null;
        }
    }

    static void ShutDown()
    {
        var psi = new ProcessStartInfo("shutdown.exe", "/s /f /t 0");
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        try { Process.Start(psi); } catch { }
    }
}

// Маленькое окно с таймером в правом нижнем углу.
// Всегда поверх остальных окон, не забирает фокус, пропускает клики
// сквозь себя и не появляется на панели задач и в Alt+Tab.
class CountdownForm : Form
{
    const int WS_EX_LAYERED = 0x00080000;
    const int WS_EX_TRANSPARENT = 0x00000020;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_TOPMOST = 0x00000008;
    const int WS_EX_NOACTIVATE = 0x08000000;

    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOACTIVATE = 0x0010;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    const string Title = "До выключения";

    static readonly Color NormalBack = Color.FromArgb(32, 32, 32);
    static readonly Color UrgentBack = Color.FromArgb(170, 30, 30);

    readonly Font titleFont = new Font("Segoe UI", 10f);
    readonly Font timeFont = new Font("Segoe UI", 24f, FontStyle.Bold);
    readonly int padding;
    readonly int titleHeight;
    readonly bool showHours;
    string timeText = "";

    public CountdownForm(bool showHours)
    {
        this.showHours = showHours;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = NormalBack;
        ForeColor = Color.White;
        Opacity = 0.9;
        DoubleBuffered = true;

        // Размер окна считаем по реальному размеру текста,
        // чтобы всё выглядело нормально при любом масштабе экрана.
        Size titleSize = TextRenderer.MeasureText(Title, titleFont);
        Size timeSize = TextRenderer.MeasureText(showHours ? "0:00:00" : "00:00", timeFont);
        padding = titleSize.Height / 2;
        titleHeight = titleSize.Height;
        ClientSize = new Size(
            Math.Max(titleSize.Width, timeSize.Width) + padding * 4,
            titleSize.Height + timeSize.Height + padding * 2);

        Location = CornerPosition();
        Show();
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    // WorkingArea — экран без панели задач, так что окно будет прямо над часами.
    Point CornerPosition()
    {
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        return new Point(area.Right - Width - padding, area.Bottom - Height - padding);
    }

    public void SetTimeLeft(TimeSpan left)
    {
        int total = (int)Math.Ceiling(left.TotalSeconds);
        int hours = total / 3600;
        int minutes = total / 60 % 60;
        int seconds = total % 60;

        timeText = showHours && hours > 0
            ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds)
            : string.Format("{0:00}:{1:00}", hours * 60 + minutes, seconds);

        // Последние 5 минут — красный фон.
        BackColor = total <= 5 * 60 ? UrgentBack : NormalBack;

        // Окно могли перекрыть или сменилось разрешение — возвращаем его
        // наверх и в угол. Свойство TopMost тут не подходит: оно забирает фокус.
        Point pos = CornerPosition();
        SetWindowPos(Handle, HWND_TOPMOST, pos.X, pos.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

        TextRenderer.DrawText(e.Graphics, Title, titleFont,
            new Rectangle(0, padding, ClientSize.Width, titleHeight),
            Color.FromArgb(200, 200, 200), flags);

        TextRenderer.DrawText(e.Graphics, timeText, timeFont,
            new Rectangle(0, padding + titleHeight, ClientSize.Width, ClientSize.Height - titleHeight - padding * 2),
            ForeColor, flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            timeFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
