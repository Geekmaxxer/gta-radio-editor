namespace GTARadioEditor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0].Equals("--scan", StringComparison.OrdinalIgnoreCase))
        {
            RunScan(args[1]).GetAwaiter().GetResult();
            return;
        }

#if NET48
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
#else
        ApplicationConfiguration.Initialize();
#endif
        Application.Run(new MainForm());
    }

    private static async Task RunScan(string rpfPath)
    {
        var service = new Services.RpfRadioService();
        var progress = new Progress<string>(Console.WriteLine);
        var slots = await service.ScanMusicSlotsAsync(rpfPath, progress);
        Console.WriteLine($"Found {slots.Count} music slot(s):");
        foreach (var slot in slots)
        {
            Console.WriteLine($"{slot.ContainerName} [{slot.OriginalDuration:mm\\:ss}]");
        }
    }
}
