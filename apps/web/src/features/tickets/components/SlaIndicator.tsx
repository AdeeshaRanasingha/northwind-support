import { Badge, type BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

const presentations: Record<SlaStatus, { label: string; tone: BadgeTone }> = {
  WithinSla: { label: 'Within SLA', tone: 'info' },
  AtRisk: { label: 'At risk', tone: 'warning' },
  Breached: { label: 'Breached', tone: 'danger' },
  Met: { label: 'Met', tone: 'success' },
  NotApplicable: { label: 'No SLA', tone: 'neutral' },
};

interface SlaIndicatorProps {
  status: SlaStatus;
}

/** A readable SLA state label shared by ticket list and detail views. */
export function SlaIndicator({ status }: SlaIndicatorProps) {
  const presentation = presentations[status];

  return <Badge tone={presentation.tone}>{presentation.label}</Badge>;
}
