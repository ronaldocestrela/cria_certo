namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class PlanLimit
{
    public Guid Id { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public string LimitKey { get; private set; } = default!;
    public decimal LimitValue { get; private set; }
    public string Unit { get; private set; } = default!;

    private PlanLimit() { }

    public static PlanLimit Create(string limitKey, decimal limitValue, string unit)
    {
        return new PlanLimit
        {
            Id = Guid.NewGuid(),
            LimitKey = limitKey.Trim(),
            LimitValue = limitValue,
            Unit = unit.Trim()
        };
    }

    public void SetPlanVersionId(Guid planVersionId)
    {
        PlanVersionId = planVersionId;
    }
}
