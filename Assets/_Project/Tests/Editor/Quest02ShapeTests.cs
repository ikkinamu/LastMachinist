using LastMachinist.Inventory;
using LastMachinist.Machining.Core;
using NUnit.Framework;

namespace LastMachinist.Tests
{
    public sealed class Quest02ShapeTests
    {
        [Test]
        public void GeneralShapesValidateWithoutPretendingToBeRoundProfiles()
        {
            var plate = new WorkpieceGeometry { Type = GeometryType.Plate, General = new GeneralWorkpieceGeometry { Shape = GeometryType.Plate, Width = 100, Length = 80, Thickness = 5 } };
            plate.Validate();
            var tube = new WorkpieceGeometry { Type = GeometryType.Tube, General = new GeneralWorkpieceGeometry { Shape = GeometryType.Tube, OuterDiameter = 40, Thickness = 3, Width = 40, Length = 100 } };
            tube.Validate();
            Assert.That(tube.General.InnerDiameter, Is.EqualTo(34));
            Assert.That(plate.RoundProfile, Is.Null);
        }

        [Test]
        public void UnsupportedShapesRemainAvailableForInventoryAndExplainLatheLimit()
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<WorkpieceDefinition>();
            definition.WorkpieceDefinitionId = "plate_test"; definition.ShapeType = WorkpieceShape.Plate;
            definition.NominalDimensions = new NominalWorkpieceDimensions { Width = 100, Length = 80, Thickness = 5 };
            Assert.That(InventoryMachiningSession.SupportsTurning(definition), Is.False);
            Assert.That(InventoryMachiningSession.TurningCompatibilityReason(definition), Does.Contain("환봉"));
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }
}
