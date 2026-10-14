import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatUtcDateTime } from '../../../lib/format';
import type { Agent, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  records: TicketEscalation[];
  agents: Agent[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

export function EscalationHistory({
  records,
  agents,
  isLoading,
  error,
  onRetry,
}: EscalationHistoryProps) {
  const agentNames = new Map(agents.map((agent) => [agent.id, agent.fullName]));
  const orderedRecords = [...records].sort(
    (left, right) =>
      new Date(right.escalatedAtUtc).getTime() - new Date(left.escalatedAtUtc).getTime() ||
      right.id - left.id,
  );
  const agentLabel = (id: number | null) =>
    id === null ? 'Unassigned' : (agentNames.get(id) ?? `Agent #${id}`);

  return (
    <section className="card" aria-labelledby="escalation-history-heading">
      <h2 id="escalation-history-heading">Escalation history</h2>

      {isLoading && records.length === 0 && <Spinner label="Loading escalation history" />}
      {error && <ErrorMessage message={error} onRetry={onRetry} />}
      {!isLoading && !error && records.length === 0 && (
        <EmptyState
          title="No escalations yet"
          description="Escalations for this ticket will appear here."
        />
      )}

      {orderedRecords.length > 0 && (
        <ol className="history-list">
          {orderedRecords.map((record) => (
            <li className="history-list__item" key={record.id}>
              <header className="history-list__header">
                <strong>
                  {record.fromPriority} → {record.toPriority}
                </strong>
                <time dateTime={record.escalatedAtUtc}>
                  {formatUtcDateTime(record.escalatedAtUtc)}
                </time>
              </header>

              <dl className="history-list__changes">
                <div>
                  <dt>Assigned agent</dt>
                  <dd>
                    {agentLabel(record.fromAgentId)} → {agentLabel(record.toAgentId)}
                  </dd>
                </div>
                <div>
                  <dt>SLA due</dt>
                  <dd>
                    {formatUtcDateTime(record.fromDueAtUtc)} →{' '}
                    {formatUtcDateTime(record.toDueAtUtc)}
                  </dd>
                </div>
              </dl>

              <p className="history-list__reason">{record.reason}</p>
              <p className="history-list__actor">Escalated by {record.escalatedBy}</p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
