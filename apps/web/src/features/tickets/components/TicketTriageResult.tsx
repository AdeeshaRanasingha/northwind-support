import { formatUtcDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';
import { SlaIndicator } from './SlaIndicator';

interface TicketTriageResultProps {
  ticket: TicketDetail;
}

/** Displays the automatic triage decisions returned by ticket creation. */
export function TicketTriageResult({ ticket }: TicketTriageResultProps) {
  return (
    <section aria-labelledby="triage-result-heading">
      <h3 id="triage-result-heading">Automatic triage result</h3>

      <dl className="summary">
        <div>
          <dt>Applied priority</dt>
          <dd>
            <PriorityBadge priority={ticket.priority} />
          </dd>
        </div>
        <div>
          <dt>SLA due</dt>
          <dd>{formatUtcDateTime(ticket.dueAtUtc)}</dd>
        </div>
        <div>
          <dt>Assigned agent</dt>
          <dd>{ticket.assignedAgent?.fullName ?? 'Unassigned'}</dd>
        </div>
        <div>
          <dt>SLA status</dt>
          <dd>
            <SlaIndicator status={ticket.slaStatus} />
          </dd>
        </div>
      </dl>

      <h4>Triage explanations</h4>
      <dl className="triage-explanations">
        <div>
          <dt>Priority</dt>
          <dd>{ticket.triage?.priorityReason ?? 'No priority explanation was provided.'}</dd>
        </div>
        <div>
          <dt>SLA window</dt>
          <dd>{ticket.triage?.slaReason ?? 'No SLA explanation was provided.'}</dd>
        </div>
        <div>
          <dt>Assignment</dt>
          <dd>{ticket.triage?.assignmentReason ?? 'No assignment explanation was provided.'}</dd>
        </div>
      </dl>
    </section>
  );
}
