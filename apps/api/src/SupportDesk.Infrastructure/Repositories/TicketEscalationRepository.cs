using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class TicketEscalationRepository(SupportDbContext db) : ITicketEscalationRepository
{
    public async Task AddAsync(TicketEscalation escalation, CancellationToken ct) =>
        await db.Set<TicketEscalation>().AddAsync(escalation, ct);

    public async Task<IReadOnlyList<TicketEscalation>> GetByTicketIdAsync(
        int ticketId,
        CancellationToken ct) =>
        await db.Set<TicketEscalation>()
            .AsNoTracking()
            .Where(escalation => escalation.TicketId == ticketId)
            .OrderByDescending(escalation => escalation.EscalatedAtUtc)
            .ThenByDescending(escalation => escalation.Id)
            .ToListAsync(ct);
}
