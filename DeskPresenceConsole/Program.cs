using DeskPresenceService;

Console.WriteLine("Desk Presence Console Test");
Console.WriteLine("==========================\n");

Console.WriteLine("Step 1: Testing Google Calendar access (this may open a browser)...");

try
{
    var calendar = new CalendarClient();
    await calendar.EnsureHomeDayEventAsync(DateTime.Today);
    Console.WriteLine("âœ… Google Calendar auth OK. (If this is the first run, you should have logged in.)");
}
catch (Exception ex)
{
    Console.WriteLine("âŒ Error initialising CalendarClient or writing event:");
    Console.WriteLine(ex);
    Console.WriteLine("\nPress any key to quit.");
    Console.ReadKey();
    return;
}

Console.WriteLine("\nStep 2: Testing webcam presence detection.");
Console.WriteLine("Please sit in front of your webcam and look at it.");
Console.WriteLine("We will scan for about 15 seconds...\n");

try
{
    var webcam = new WebcamPresenceDetector();
    bool present = webcam.IsUserPresent(TimeSpan.FromSeconds(15));

    if (present)
        Console.WriteLine("âœ… Face detected! Presence detection appears to be working.");
    else
    {
        Console.WriteLine("âš  No face detected in the 15-second window.");
        Console.WriteLine("   Check lighting, camera selection (index 0), or camera privacy settings.");
    }
}
catch (Exception ex)
{
    Console.WriteLine("âŒ Error during webcam detection:");
    Console.WriteLine(ex);
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();
