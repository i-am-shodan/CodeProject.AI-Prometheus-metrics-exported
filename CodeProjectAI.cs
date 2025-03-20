using Prometheus;
using System.Text.RegularExpressions;

namespace Log2Metric.CodeProjectAI
{
    public class CodeProjectAI
    {
        private static readonly Gauge TotalRequests = Metrics.CreateGauge("codeproject_ai_total_num_requests", "The number of requests recieved.", new GaugeConfiguration()
        {
            LabelNames = new[] { "queue" }
        });

        private static readonly Histogram Requests = Metrics.CreateHistogram("codeproject_ai_num_requests", "The number of requests recieved.", new HistogramConfiguration()
        {
            LabelNames = new[] { "queue" }
        });

        private static readonly Histogram DetectionTime = Metrics.CreateHistogram("codeproject_ai_detect_time", "How long it took to process the last message.", new HistogramConfiguration()
        {
            LabelNames = new[] { "objects" }
        });

        private static readonly Gauge Backlog = Metrics.CreateGauge("codeproject_ai_req_backlog", "Number of waiting requestd.");

        private static Dictionary<string, IDisposable> requestTimes = new();

        public static void ParseLogMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg))
            {
                return;
            }

            Backlog.Set(requestTimes.Count);

            if (msg.StartsWith("Trace Client request"))
            {
                // got message to put onto queue
                // Trace Client request 'detect' in queue 'objectdetection_queue' (#reqid 3c22f55a-b1e3-4c8d-80b8-ac518192d570)

                var match = Regex.Match(msg, "queue '(.*)' \\(#reqid (.+)\\)$");
                if (!match.Success)
                {
                    throw new InvalidDataException("Invalid log - " + msg);
                }

                TotalRequests.Inc();
                requestTimes.Add(match.Groups[2].Value, Requests.WithLabels(match.Groups[1].Value).NewTimer());
            }
            else if (msg.StartsWith("Trace Request"))
            {
                // off internal queue
                // Trace Request 'detect' dequeued from 'objectdetection_queue' (#reqid 3c22f55a-b1e3-4c8d-80b8-ac518192d570)

                //var match = Regex.Match(msg, "dequeued from '(.*)' \\(#reqid (.+)\\)$");
            }
            else if (msg.StartsWith("Infor Response rec'd"))
            {
                // work completed
                var match = Regex.Match(msg, ".+\\(#reqid (.+)\\) \\['(.*)']\\s+took (\\d+)ms");
                if (!match.Success)
                {
                    throw new InvalidDataException("Invalid log - " + msg);
                }

                requestTimes.Remove(match.Groups[1].Value, out IDisposable? timer);
                timer?.Dispose();

                //if (startTime != DateTime.MinValue)
                //{
                //    ProcessingDuration.Set((DateTime.UtcNow - startTime).TotalMilliseconds);
                //}

                var foundObjs = match.Groups[2].Value;
                var objs = foundObjs == "No objects found" || string.IsNullOrWhiteSpace(foundObjs) ? "None" : foundObjs.Replace("Found ", "");

                // Infor Response rec'd from Object Detection (Coral) command 'detect' (#reqid d7b19507-0c9e-4534-96d3-19fd6590dab4) ['No objects found']  took 13ms
                foreach (var obj in objs.Split(",", StringSplitOptions.RemoveEmptyEntries))
                {
                    DetectionTime.WithLabels(obj.Trim()).Observe(int.Parse(match.Groups[3].Value));
                }
            }
        }
    }
}