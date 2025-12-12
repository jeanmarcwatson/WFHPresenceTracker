using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace DeskPresenceService
{
    public class WebcamPresenceDetector
    {
        private readonly ILogger<WebcamPresenceDetector> _logger;
        private readonly string _cascadePath;

        private readonly int _requiredHits;
        private readonly double _minFaceRatio;
        private readonly bool _centerCrop;
        private readonly bool _cameraBusyCountsAsPresent;
        private readonly int _maxGrabFailures;

        // New: crop tuning + detector strictness
        private readonly double _cropScale;     // 0..1  (bigger = less likely to crop your head out)
        private readonly double _cropYOffset;   // -0.5..+0.5 (negative = move crop up)
        private readonly int _minNeighbors;     // higher = fewer false positives, but may miss weak detections

        public WebcamPresenceDetector(ILogger<WebcamPresenceDetector> logger, IConfiguration config)
        {
            _logger = logger;

            // Bundled Haar cascade (copy next to exe)
            _cascadePath = "haarcascade_frontalface_default.xml";

            var section = config.GetSection("Presence:Webcam");

            _requiredHits = section.GetValue("RequiredHits", 3);
            _minFaceRatio = section.GetValue("MinFaceRatio", 0.012);
            _centerCrop = section.GetValue("CenterCrop", true);
            _cameraBusyCountsAsPresent = section.GetValue("CameraBusyCountsAsPresent", true);
            _maxGrabFailures = section.GetValue("MaxGrabFailures", 5);

            // Defaults chosen to work for slight recline while still limiting edge junk:
            _cropScale = Clamp(section.GetValue("CropScale", 0.85), 0.50, 1.0);
            _cropYOffset = Clamp(section.GetValue("CropYOffset", -0.10), -0.50, 0.50);
            _minNeighbors = ClampInt(section.GetValue("MinNeighbors", 6), 3, 12);

            _logger.LogInformation(
                "WebcamPresenceDetector initialised. RequiredHits={RequiredHits}, MinFaceRatio={MinFaceRatio}, CenterCrop={CenterCrop}, CropScale={CropScale}, CropYOffset={CropYOffset}, MinNeighbors={MinNeighbors}, CameraBusyCountsAsPresent={CameraBusyCountsAsPresent}, MaxGrabFailures={MaxGrabFailures}",
                _requiredHits, _minFaceRatio, _centerCrop, _cropScale, _cropYOffset, _minNeighbors, _cameraBusyCountsAsPresent, _maxGrabFailures);
        }

        public bool IsUserPresent(TimeSpan window)
        {
            _logger.LogInformation("Starting presence detection for window {Seconds} seconds.", window.TotalSeconds);

            var deadline = DateTime.UtcNow + window;
            int hits = 0;
            int frames = 0;
            int grabFailures = 0;

            using var capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                _logger.LogWarning(
                    "Could not open webcam. Returning {Present} because CameraBusyCountsAsPresent={CameraBusyCountsAsPresent}",
                    _cameraBusyCountsAsPresent ? "Present" : "Away",
                    _cameraBusyCountsAsPresent);

                return _cameraBusyCountsAsPresent;
            }

            using var cascade = new CascadeClassifier(_cascadePath);

            while (DateTime.UtcNow < deadline)
            {
                using var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty())
                {
                    grabFailures++;

                    if (grabFailures >= _maxGrabFailures)
                    {
                        _logger.LogWarning(
                            "Camera appears busy (meeting). Treating as {Present}. GrabFailures={GrabFailures}",
                            _cameraBusyCountsAsPresent ? "Present" : "Away",
                            grabFailures);

                        return _cameraBusyCountsAsPresent;
                    }

                    Thread.Sleep(100);
                    continue;
                }

                frames++;

                using var gray = new Mat();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                // Choose ROI
                Mat roiMat = gray;
                Rect roiRect = new Rect(0, 0, gray.Width, gray.Height);

                if (_centerCrop)
                {
                    roiRect = ComputeCropRect(gray.Width, gray.Height, _cropScale, _cropYOffset);
                    roiMat = new Mat(gray, roiRect);
                }

                // Minimum acceptable face size (by area)
                double minFaceArea = roiMat.Width * roiMat.Height * _minFaceRatio;

                // Detect faces
                var faces = cascade.DetectMultiScale(
                    roiMat,
                    scaleFactor: 1.1,
                    minNeighbors: _minNeighbors,
                    flags: HaarDetectionTypes.ScaleImage
                );

                bool anyBigFace = false;
                foreach (var face in faces)
                {
                    double area = face.Width * face.Height;
                    if (area >= minFaceArea)
                    {
                        anyBigFace = true;
                        break;
                    }
                }

                if (anyBigFace)
                {
                    hits++;

                    if (hits >= _requiredHits)
                    {
                        _logger.LogInformation(
                            "Face confirmed. Hits={Hits}, Frames={Frames}, Window={Seconds}s.",
                            hits, frames, window.TotalSeconds);

                        return true;
                    }
                }

                Thread.Sleep(50);
            }

            _logger.LogInformation(
                "No confirmed face. Hits={Hits}, Frames={Frames}, Window={Seconds}s.",
                hits, frames, window.TotalSeconds);

            return false;
        }

        private static Rect ComputeCropRect(int w, int h, double scale, double yOffset)
        {
            int cw = (int)(w * scale);
            int ch = (int)(h * scale);

            int x = (w - cw) / 2;

            // yOffset is fraction of remaining margin: -0.5..+0.5
            int marginY = h - ch;
            int y = (marginY / 2) + (int)(marginY * yOffset);

            // clamp
            if (y < 0) y = 0;
            if (y + ch > h) y = h - ch;
            if (x < 0) x = 0;
            if (x + cw > w) x = w - cw;

            return new Rect(x, y, cw, ch);
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : (v > max ? max : v);

        private static int ClampInt(int v, int min, int max) =>
            v < min ? min : (v > max ? max : v);
    }
}
