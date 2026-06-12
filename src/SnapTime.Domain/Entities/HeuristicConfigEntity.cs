namespace SnapTime.Domain.Entities;

public class HeuristicConfigEntity
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public double Weight { get; set; } = 1.0;
}
