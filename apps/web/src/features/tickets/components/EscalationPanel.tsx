import { useRef, useState, type FormEvent } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { TicketDetail } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: () => Promise<void>;
}

interface ValidationErrors {
  reason?: string;
  escalatedBy?: string;
}

const blockedStatuses = new Set<TicketDetail['status']>(['Resolved', 'Closed']);

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('');
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [requestError, setRequestError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const submissionPending = useRef(false);

  const isBlocked = ticket.priority === 'Critical' || blockedStatuses.has(ticket.status);

  if (isBlocked) {
    return (
      <div className="card">
        <h2>Escalation</h2>
        <p className="field__hint">
          This ticket cannot be escalated because it is{' '}
          {ticket.priority === 'Critical' ? 'already Critical' : ticket.status}.
        </p>
      </div>
    );
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (submissionPending.current) {
      return;
    }

    const trimmedReason = reason.trim();
    const trimmedActor = escalatedBy.trim();
    const nextErrors: ValidationErrors = {};

    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      nextErrors.reason = 'Reason must contain 5 to 500 characters.';
    }

    if (trimmedActor.length === 0 || trimmedActor.length > 100) {
      nextErrors.escalatedBy = 'Escalated by is required and cannot exceed 100 characters.';
    }

    setErrors(nextErrors);
    setRequestError(undefined);

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    submissionPending.current = true;
    setIsSubmitting(true);

    try {
      await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmedReason,
        escalatedBy: trimmedActor,
      });
      await onEscalated();
      setReason('');
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 400) {
        setErrors({
          reason: firstFieldError(caught, 'Reason'),
          escalatedBy: firstFieldError(caught, 'EscalatedBy'),
        });
      }

      setRequestError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      submissionPending.current = false;
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card">
      <h2>Escalation</h2>

      <form className="form" onSubmit={(event) => void submit(event)} noValidate>
        <Field id="escalation-reason" label="Reason" error={errors.reason} hint="5–500 characters">
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              value={reason}
              maxLength={500}
              disabled={isSubmitting}
              onChange={(event) => setReason(event.target.value)}
            />
          )}
        </Field>

        <Field id="escalated-by" label="Escalated by" error={errors.escalatedBy}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              value={escalatedBy}
              maxLength={100}
              disabled={isSubmitting}
              onChange={(event) => setEscalatedBy(event.target.value)}
            />
          )}
        </Field>

        {requestError && (
          <p className="field__error" role="alert">
            {requestError}
          </p>
        )}

        <button type="submit" className="button button--primary" disabled={isSubmitting}>
          {isSubmitting ? 'Escalating...' : 'Escalate ticket'}
        </button>
      </form>
    </div>
  );
}

function firstFieldError(error: ApiError, field: string): string | undefined {
  return error.fieldErrors[field]?.[0] ?? error.fieldErrors[field.toLowerCase()]?.[0];
}
