using System;
using System.Collections.Generic;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Measurement
{
    public enum MeasurementPointType { Diameter, Length }

    [Serializable]
    public sealed class MeasurementPoint
    {
        public string Id;
        public MeasurementPointType Type;
        public float StartZ;
        public float EndZ;
        public string DisplayHint;

        public MeasurementPoint(string id, MeasurementPointType type, float startZ, float endZ, string displayHint = null)
        {
            if (string.IsNullOrWhiteSpace(id) || startZ < 0 || endZ <= startZ) throw new ArgumentException("Invalid measurement point.");
            Id = id; Type = type; StartZ = startZ; EndZ = endZ; DisplayHint = displayHint;
        }
    }

    [Serializable]
    public sealed class InstrumentReading
    {
        public string InstrumentId;
        public float Value;
        public float MainScaleValue;
        public float VernierOffset;
        public int VernierDivisions = 20;
    }

    // Temporary configurable instrument: N vernier intervals span N-1 main millimetres.
    public sealed class VernierScale
    {
        public int Divisions { get; }
        public float Resolution => 1f / Divisions;
        public float Pitch => 1f - Resolution;
        public VernierScale(int divisions = 20)
        {
            if (divisions < 2 || divisions > 50) throw new ArgumentOutOfRangeException(nameof(divisions));
            Divisions = divisions;
        }
        public InstrumentReading Read(float dimension, string instrumentId)
        {
            if (float.IsNaN(dimension) || float.IsInfinity(dimension) || dimension < 0)
                throw new ArgumentOutOfRangeException(nameof(dimension));
            float value = (float)(Math.Round((double)dimension * Divisions, MidpointRounding.AwayFromZero) / Divisions);
            float main = (float)Math.Floor(value);
            return new InstrumentReading { InstrumentId = instrumentId, Value = value,
                MainScaleValue = main, VernierOffset = value - main, VernierDivisions = Divisions };
        }
        public float TickPosition(InstrumentReading reading, int index)
        {
            if (index < 0 || index > Divisions) throw new ArgumentOutOfRangeException(nameof(index));
            return reading.Value + index * Pitch;
        }
    }

    [Serializable]
    public sealed class RecordedMeasurement
    {
        public string WorkpieceInstanceId;
        public string MeasurementPointId;
        public string InstrumentId;
        public float InstrumentReadingValue;
        public float PlayerValue;
        public long RecordedAtTicks;
    }

    public sealed class MeasurementSession
    {
        private readonly TurningProfile profile;
        private readonly string workpieceId;
        private readonly VernierScale vernier;
        private readonly Dictionary<string, RecordedMeasurement> records = new Dictionary<string, RecordedMeasurement>();
        public MeasurementPoint SelectedPoint { get; private set; }
        public InstrumentReading CurrentReading { get; private set; }
        public IReadOnlyDictionary<string, RecordedMeasurement> Records => records;
        public List<RecordedMeasurement> ExportRecords() => new List<RecordedMeasurement>(records.Values);
        public void ImportRecords(IEnumerable<RecordedMeasurement> values)
        {
            records.Clear();
            if (values == null) return;
            foreach (var value in values)
                if (value != null && !string.IsNullOrWhiteSpace(value.MeasurementPointId)) records[value.MeasurementPointId] = value;
        }
        public bool IsPrecisionMode { get; private set; }

        public MeasurementSession(TurningProfile profile, string workpieceId, int vernierDivisions = 20)
        {
            vernier = new VernierScale(vernierDivisions);
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.workpieceId = string.IsNullOrWhiteSpace(workpieceId) ? throw new ArgumentException("Workpiece ID is required.") : workpieceId;
        }

        public bool EnterPrecisionMode(bool spindleStopped)
        {
            IsPrecisionMode = spindleStopped;
            return IsPrecisionMode;
        }

        public InstrumentReading Select(MeasurementPoint point, string instrumentId, bool spindleStopped)
        {
            if (!spindleStopped) throw new InvalidOperationException("Precision measurement requires a stopped spindle.");
            if (point == null || string.IsNullOrWhiteSpace(instrumentId)) throw new ArgumentException("Measurement point and instrument are required.");
            float trueDimension = TrueDimension(point);
            CurrentReading = vernier.Read(trueDimension, instrumentId);
            SelectedPoint = point; IsPrecisionMode = true;
            return CurrentReading;
        }

        public RecordedMeasurement Record(float playerValue)
        {
            if (SelectedPoint == null || CurrentReading == null) throw new InvalidOperationException("Select a measurement point first.");
            if (float.IsNaN(playerValue) || float.IsInfinity(playerValue)) throw new ArgumentException("Invalid recorded value.");
            var record = new RecordedMeasurement { WorkpieceInstanceId = workpieceId, MeasurementPointId = SelectedPoint.Id,
                InstrumentId = CurrentReading.InstrumentId, InstrumentReadingValue = CurrentReading.Value,
                PlayerValue = playerValue, RecordedAtTicks = DateTime.UtcNow.Ticks };
            records[SelectedPoint.Id] = record;
            return record;
        }

        // A diameter point samples the centre of its named region. The instrument
        // rendering uses this same coordinate, including after partial machining.
        public float ContactPosition(MeasurementPoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            if (float.IsNaN(point.StartZ) || float.IsInfinity(point.StartZ) ||
                float.IsNaN(point.EndZ) || float.IsInfinity(point.EndZ) ||
                point.StartZ < 0 || point.EndZ <= point.StartZ || point.EndZ > profile.TotalLength ||
                !Enum.IsDefined(typeof(MeasurementPointType), point.Type))
                throw new ArgumentException("Measurement point is outside the profile or invalid.");
            return point.StartZ + (point.EndZ - point.StartZ) / 2f;
        }

        public float TrueDimension(MeasurementPoint point)
        {
            float z = ContactPosition(point);
            if (point.Type == MeasurementPointType.Length)
            {
                MaterialBounds(point, out float start, out float end);
                return end - start;
            }
            return profile.RadiusAt(z) * 2f;
        }
        public void MaterialBounds(MeasurementPoint point, out float start, out float end)
        {
            float anchor = ContactPosition(point);
            // Region coordinates locate a feature, but never define its measured length.
            // Find the live cylindrical run containing the anchor and follow its shoulders.
            if (point.Type == MeasurementPointType.Length &&
                !(point.StartZ == 0 && point.EndZ == profile.TotalLength))
            {
                int index = profile.Segments.FindIndex(s => s.StartZ <= anchor && anchor < s.EndZ);
                if (index >= 0 && profile.Segments[index].Radius > 0)
                {
                    var current = profile.Segments[index];
                    start = current.StartZ; end = current.EndZ;
                    for (int i = index - 1; i >= 0; i--)
                    {
                        var s = profile.Segments[i];
                        if (Math.Abs(s.Radius - current.Radius) > 0.0001f || Math.Abs(s.EndZ - start) > 0.0001f) break;
                        start = s.StartZ;
                    }
                    for (int i = index + 1; i < profile.Segments.Count; i++)
                    {
                        var s = profile.Segments[i];
                        if (Math.Abs(s.Radius - current.Radius) > 0.0001f || Math.Abs(s.StartZ - end) > 0.0001f) break;
                        end = s.EndZ;
                    }
                    return;
                }
            }
            start = point.EndZ; end = point.StartZ;
            foreach (var span in profile.SurfaceSpans())
            {
                if (span.StartRadius <= 0 && span.EndRadius <= 0) continue;
                float from = Math.Max(point.StartZ, span.StartZ), to = Math.Min(point.EndZ, span.EndZ);
                if (to <= from) continue;
                start = Math.Min(start, from); end = Math.Max(end, to);
            }
            if (end < start) start = end = point.StartZ;
        }
    }
}
