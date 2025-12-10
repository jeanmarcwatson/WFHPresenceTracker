using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace DeskPresenceTray;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly string _logFolder;
    private readonly string _reportFolder;

    public TrayApplicationContext()
    {
        // Install paths used by service & console
        string baseDir = AppContext.BaseDirectory;
        _logFolder = Path.Combine(baseDir, "Logs");
        _reportFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "DeskPresenceReports");

        Directory.CreateDirectory(_logFolder);
        Directory.CreateDirectory(_reportFolder);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("View Logs", null, (_, _) => OpenFolder(_logFolder));
        contextMenu.Items.Add("View Reports", null, (_, _) => OpenFolder(_reportFolder));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => Exit());

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = contextMenu,
            Visible = true,
            Text = "Desk Presence Tracker"
        };
    }

    private void OpenFolder(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
