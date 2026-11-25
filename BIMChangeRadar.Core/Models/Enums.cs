namespace BIMChangeRadar.Core.Models;

/// <summary>Resultado da comparação para um elemento.</summary>
public enum ChangeType { Unchanged, Added, Removed, Modified }

/// <summary>Impacto estimado, ordenado do menor para o maior (permite usar Max()).</summary>
public enum ImpactLevel { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }

/// <summary>Natureza da propriedade alterada; usada pelas regras de impacto.</summary>
public enum PropertyKind { Text, Classification, Material, Dimension, Location, Parameter }
