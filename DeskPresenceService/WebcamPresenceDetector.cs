using OpenCvSharp;

namespace DeskPresenceService;

public class WebcamPresenceDetector
{
    private readonly CascadeClassifier _faceCascade;

    public WebcamPresenceDetector()
    {
        string baseDir = AppContext.BaseDirectory;
        string cascadePath = Path.Combine(baseDir, "haarcascade_frontalface_default.xml");
        if (!File.Exists(cascadePath))
            throw new FileNotFoundException("Cascade file not found", cascadePath);

        _faceCascade = new CascadeClassifier(cascadePath);
    }

    public bool IsUserPresent(TimeSpan duration)
    {
        using var capture = new VideoCapture(0);
        if (!capture.IsOpened())
            return false;

        DateTime endTime = DateTime.UtcNow + duration;

        using var frame = new Mat();
        using var gray = new Mat();

        while (DateTime.UtcNow < endTime)
        {
            if (!capture.Read(frame) || frame.Empty())
            {
                Thread.Sleep(100);
                continue;
            }

            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            var faces = _faceCascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,
                minNeighbors: 3,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new Size(60, 60));

            if (faces.Length > 0)
                return true;

            Thread.Sleep(200);
        }

        return false;
    }
}
