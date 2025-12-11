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

        public WebcamPresenceDetector(ILogger<WebcamPresenceDetector> logger, IConfiguration config)
        {
            _logger = logger;

            // Bundled Haar cascade
            _cascadePath = "haarcascade_frontalface_default.xml";

            var section = config.GetSection("Presence:Webcam");

            _requiredHits = section.GetValue("RequiredHits", 1);
            _minFaceRatio = section.GetValue("MinFaceRatio", 0.08);
            _centerCrop = section.GetValue("CenterCrop", false);
            _cameraBusyCountsAsPresent = section.GetValue("CameraBusyCountsAsPresent", true);
            _maxGrabFailures = section.GetValue("MaxGrabFailures", 5);

            _logger.LogInformation(
                "WebcamPresenceDetector initialised. RequiredHits={RequiredHits}, MinFaceRatio={MinFaceRatio}, CenterCrop={CenterCrop}, CameraBusyCountsAsPresent={CameraBusyCountsAsPresent}, MaxGrabFailures={MaxGrabFailures}",
                _requiredHits, _minFaceRatio, _centerCrop, _cameraBusyCountsAsPresent, _maxGrabFailures);
        }

        /// <summary>
        /// Main presence detection entry point.
        /// This is what PresenceWorker.cs calls.
        /// </summary>
        public bool IsUserPresent(TimeSpan window)
        {
            _logger.LogInformation("Starting presence detection for window {Seconds} seconds.", window.TotalSeconds);

            var deadline = DateTime.UtcNow + window;
            int hits = 0;
            int frames = 0;
            int grabFailures = 0;

            // Try open webcam
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

                // optionally crop centre of frame
                Mat roiMat = gray;
                Rect roiRect = new Rect(0, 0, gray.Width, gray.Height);

                if (_centerCrop)
                {
                    int w = gray.Width;
                    int h = gray.Height;
                    int cw = (int)(w * 0.6);
                    int ch = (int)(h * 0.6);
                    int x = (w - cw) / 2;
                    int y = (h - ch) / 2;

                    roiRect = new Rect(x, y, cw, ch);
                    roiMat = new Mat(gray, roiRect);
                }

                // Minimum acceptable face size
                double minFaceArea = gray.Width * gray.Height * _minFaceRatio;

                var faces = cascade.DetectMultiScale(
                    roiMat,
                    scaleFactor: 1.1,
                    minNeighbors: 4,
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
    }
}
