using System;
using System.Drawing;
using System.ServiceProcess;
using System.Windows.Forms;

namespace DeskPresenceTray;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

internal class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Timer _timer;
    private const string ServiceName = "DeskPresenceTracker";

    public TrayContext()
    {
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Desk Presence Tracker",
            Icon = SystemIcons.Application
        };

        var contextMenu = new ContextMenuStrip();
        var statusItem = new ToolStripMenuItem("Status: checking...") { Enabled = false };
        var startItem = new ToolStripMenuItem("Start service", null, (_, _) => StartService());
        var stopItem = new ToolStripMenuItem("Stop service", null, (_, _) => StopService());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Exit());

        contextMenu.Items.Add(statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(startItem);
        contextMenu.Items.Add(stopItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        _timer = new Timer { Interval = 5000 }; // 5 seconds
        _timer.Tick += (_, _) => UpdateStatus(statusItem, startItem, stopItem);
        _timer.Start();

        // initial update
        UpdateStatus(statusItem, startItem, stopItem);
    }

    private void UpdateStatus(ToolStripMenuItem statusItem, ToolStripMenuItem startItem, ToolStripMenuItem stopItem)
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            string status = sc.Status.ToString();

            statusItem.Text = $"Status: {status}";
            startItem.Enabled = sc.Status == ServiceControllerStatus.Stopped;
            stopItem.Enabled = sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            statusItem.Text = "Status: not installed";
            startItem.Enabled = false;
            stopItem.Enabled = false;
        }
    }

    private void StartService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start service:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Stop();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop service:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Exit()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }
}
