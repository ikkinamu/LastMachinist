using LastMachinist.Machining.Core;

namespace LastMachinist.Machining.Turning
{
    public static class TurningCoordinateConverter
    {
        public static float MachineToWorkpieceZ(float machineZ, float totalLength, Orientation orientation)
            => orientation == Orientation.Reversed ? totalLength - machineZ : machineZ;
        public static float WorkpieceToMachineZ(float localZ, float totalLength, Orientation orientation)
            => orientation == Orientation.Reversed ? totalLength - localZ : localZ;
        public static void MachineIntervalToWorkpiece(float machineA, float machineB, float totalLength, Orientation orientation,
            out float localStart, out float localEnd)
        {
            float a = MachineToWorkpieceZ(machineA, totalLength, orientation);
            float b = MachineToWorkpieceZ(machineB, totalLength, orientation);
            localStart = a < b ? a : b;
            localEnd = a < b ? b : a;
        }
    }
}
