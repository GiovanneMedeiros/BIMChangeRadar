namespace BIMChangeRadar.Core.Models;

/// <summary>Diferença estimada de área/volume. Só existe quando há dados suficientes.</summary>
public record QuantityEstimate(
    double? AreaBefore,
    double? AreaAfter,
    double? VolumeBefore,
    double? VolumeAfter,
    string Message)
{
    public double? AreaDelta => AreaBefore.HasValue && AreaAfter.HasValue ? AreaAfter - AreaBefore : null;
    public double? VolumeDelta => VolumeBefore.HasValue && VolumeAfter.HasValue ? VolumeAfter - VolumeBefore : null;
}
