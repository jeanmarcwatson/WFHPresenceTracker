using System;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace DeskPresenceService
{
    /// <summary>
    /// Why a sample was classified the way it was.
    /// </summary>
    public enum PresenceDetectionStatus
    {
        Away = 0,
        FaceDetected = 1,
        CameraBusyAssumedPresent = 2
    }

    public class WebcamPresenceDetector : IDisposable
    {
        private readonly ILogger<WebcamPresenceDetector> _logger;
        private readonly CascadeClassifier _faceCascade;

        private readonly int _requiredHits;
        private readonly double _minFaceRatio;
        private readonly bool _useCenterCrop;
        private readonly bool _cameraBusyCountsAsPresent;
        private readonly int _maxGrabFailures;
        private readonly double _cameraBusyMinFps;

        public WebcamPresenceDetector(
            ILogger<WebcamPresenceDetector> logger,
            IConfiguration config)
        {
            _logger = logger;

            var cascadePath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "haarcascade_frontalface_default.xml");

            if (!System.IO.File.Exists(cascadePath))
            {
                _logger.LogError("Face cascade file not found at {Path}", cascadePath);
                throw new InvalidOperationException(
                    $"Cascade file not found: {cascadePath}");
            }

            _faceCascade = new CascadeClassifier(cascadePath);

            var presenceSection = config.GetSection("Presence");

            // Tunable parameters (with safer, more forgiving defaults)
            _requiredHits = presenceSection.GetValue("FaceDetectionHits", 1);
            _minFaceRatio = presenceSection.GetValue("FaceMinSizeRatio", 0.08);
            _useCenterCrop = presenceSection.GetValue("FaceUseCenterCrop", false);

            _cameraBusyCountsAsPresent = presenceSection.GetValue("CameraBusyCountsAsPresent", true);
            _maxGrabFailures = presenceSection.GetValue("CameraBusyMaxGrabFailures", 5);

            // New: if FPS is below this, we assume "camera is being throttled / busy"
            _cameraBusyMinFps = presenceSection.GetValue("CameraBusyMinFps", 5.0);

            _logger.LogInformation(
                "WebcamPresenceDetector initialised. RequiredHits={Hits}, MinFaceRatio={Ratio}, CenterCrop={CenterCrop}, CameraBusyCountsAsPresent={BusyPresent}, MaxGrabFailures={MaxFail}, CameraBusyMinFps={MinFps}",
                _requiredHits, _minFaceRatio, _useCenterCrop, _cameraBusyCountsAsPresent, _maxGrabFailures, _cameraBusyMinFps);
        }

        /// <summary>
        /// Returns a PresenceDetectionStatus for this sample window.
        /// - FaceDetected: real face hits confirmed
        /// - CameraBusyAssumedPresent: camera appears to be in use by another app (Teams/Zoom)
        /// - Away: nothing detected
        /// </summary>
        public PresenceDetectionStatus DetectPresence(TimeSpan detectionWindow)
        {
            _logger.LogInformation(
                "Starting presence detection for window {Seconds} seconds.",
                detectionWindow.TotalSeconds);

            using var capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);

            if (!capture.IsOpened())
            {
                _logger.LogWarning("Webcam could not be opened at all.");
                return PresenceDetectionStatus.Away;
            }

            capture.FrameWidth = 640;
            capture.FrameHeight = 480;

            var sw = Stopwatch.StartNew();
            int hits = 0;
            int frames = 0;
            int grabFailures = 0;

            using var frame = new Mat();

            while (sw.Elapsed < detectionWindow)
            {
                try
                {
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        grabFailures++;

                        if (grabFailures >= _maxGrabFailures)
                        {
                            _logger.LogWarning(
                                "Webcam grab failed {Failures} times in a row. " +
                                "Camera probably in use by another application. CameraBusyCountsAsPresent={BusyPresent}.",
                                grabFailures,
                                _cameraBusyCountsAsPresent);

                            return _cameraBusyCountsAsPresent
                                ? PresenceDetectionStatus.CameraBusyAssumedPresent
                                : PresenceDetectionStatus.Away;
                        }

                        Cv2.WaitKey(50);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    grabFailures++;

                    if (grabFailures >= _maxGrabFailures)
                    {
                        _logger.LogWarning(
                            ex,
                            "Error grabbing frames from webcam {Failures} times. Assuming camera is busy. CameraBusyCountsAsPresent={BusyPresent}.",
                            grabFailures,
                            _cameraBusyCountsAsPresent);

                        return _cameraBusyCountsAsPresent
                            ? PresenceDetectionStatus.CameraBusyAssumedPresent
                            : PresenceDetectionStatus.Away;
                    }

                    Cv2.WaitKey(50);
                    continue;
                }

                // Successful read resets failure counter
                grabFailures = 0;
                frames++;

                if (DetectFace(frame))
                {
                    hits++;
                    _logger.LogDebug("Face hit {HitCount} / {Required} (frame {Frame}).",
                        hits, _requiredHits, frames);

                    if (hits >= _requiredHits)
                    {
                        _logger.LogInformation(
                            "Face confirmed with {Hits} hits over {Frames} frames.",
                            hits, frames);
                        return PresenceDetectionStatus.FaceDetected;
                    }
                }

                Cv2.WaitKey(30);
            }

            double fps = frames / Math.Max(0.001, sw.Elapsed.TotalSeconds);

            _logger.LogInformation(
                "No confirmed face. Hits={Hits}, Frames={Frames}, Window={Seconds}s, ApproxFPS={Fps}.",
                hits, frames, detectionWindow.TotalSeconds, fps);

            // NEW: low FPS + no face + flag set → assume camera busy (e.g. used by Teams/Zoom)
            if (hits == 0 &&
                frames > 0 &&
                fps < _cameraBusyMinFps &&
                _cameraBusyCountsAsPresent)
            {
                _logger.LogWarning(
                    "Low FPS ({Fps:F2} < {MinFps}) with no face detected; assuming camera is in use by another app. Treating as CameraBusyAssumedPresent.",
                    fps, _cameraBusyMinFps);

                return PresenceDetectionStatus.CameraBusyAssumedPresent;
            }

            return PresenceDetectionStatus.Away;
        }

        private bool DetectFace(Mat frame)
        {
            Mat gray = new Mat();

            try
            {
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                Mat roiMat;
                if (_useCenterCrop)
                {
                    int w = gray.Width;
                    int h = gray.Height;

                    int cropW = (int)(w * 0.6);
                    int cropH = (int)(h * 0.6);
                    int x = (w - cropW) / 2;
                    int y = (h - cropH) / 3;

                    var roi = new Rect(x, y, cropW, cropH);
                    roiMat = new Mat(gray, roi);
                }
                else
                {
                    roiMat = gray;
                }

                int minSizePx = (int)(gray.Height * _minFaceRatio);
                if (minSizePx < 30) minSizePx = 30;

                var minSize = new OpenCvSharp.Size(minSizePx, minSizePx);

                Rect[] faces = _faceCascade.DetectMultiScale(
                    roiMat,
                    scaleFactor: 1.05,
                    minNeighbors: 3,
                    flags: HaarDetectionTypes.ScaleImage,
                    minSize: minSize
                );

                bool found = faces is { Length: > 0 };

                if (found)
                {
                    _logger.LogDebug(
                        "Face candidate(s) found: {Count}, MinSize={MinSize}px.",
                        faces.Length, minSizePx);
                }

                return found;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during face detection.");
                return false;
            }
            finally
            {
                gray.Dispose();
            }
        }

        public void Dispose()
        {
            _faceCascade?.Dispose();
        }
    }
}
